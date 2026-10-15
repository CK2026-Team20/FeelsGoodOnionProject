using System;
using System.Collections.Generic;
using Cooked.Authoring;
using Cooked.Contracts;
using Cooked.Session;
using Unity.Cinemachine;
using UnityEngine;
using DG.Tweening;
namespace Cooked.Level
{
    public enum CameraMovementMode { [InspectorName("측면")] Side = 1, [InspectorName("통로")] Corridor = 2, [InspectorName("쿼터뷰")] Quarter = 3 }
    public static class CameraProfileValidation
    {
        internal static void Validate(CameraMovementMode movement, Vector3 alignmentPosition)
        {
            if (movement != CameraMovementMode.Side && movement != CameraMovementMode.Corridor && movement != CameraMovementMode.Quarter)
                throw new ArgumentOutOfRangeException(nameof(movement), "Side(1), Corridor(2), Quarter(3)만 사용할 수 있습니다.");
            if (!IsFinite(alignmentPosition)) throw new ArgumentException("Camera alignment must contain finite world XYZ values.", nameof(alignmentPosition));
        }
        internal static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }
    public enum CameraTransitionState { Idle, Accepted, CorrectingPlayer, MovingBlocker, WaitingForCamera, Completed, Cancelled, Failed }

    [DefaultExecutionOrder(1000)]
    public sealed class LevelCameraService : MonoBehaviour, IDisposable
    {
        [Serializable]
        public sealed class ViewSettings
        {
            [SerializeField, InspectorLabel("화면 위치"), Tooltip("대상의 화면 위치입니다. X는 왼쪽 0~오른쪽 100, Y는 아래 0~위 100입니다.")]
            public Vector2 screenPercent;
            [SerializeField, InspectorLabel("허용 폭과 높이"), Tooltip("추적하지 않고 허용할 화면 폭과 높이 비율입니다.")]
            public Vector2 deadZone;
            [SerializeField, InspectorLabel("추적 감쇠"), Tooltip("축별 추적 지연을 조절합니다. 0이면 즉시 추적합니다.")]
            public Vector3 damping = Vector3.one;
            [SerializeField, InspectorLabel("피사체 중심 반경"), Tooltip("시선 중심에서 카메라까지의 정착 반경입니다(m). 감쇠·허용 폭·블렌드 중에는 실제 거리가 달라집니다."), Min(.01f)]
            public float distance = 10;
            [SerializeField, InspectorLabel("피사체 중심 회전"), Tooltip("월드 기준 수직각(X)·수평각(Y)·롤(Z)입니다(도). 시선 중심 주위의 카메라 위치도 변합니다.")]
            public Vector3 angles;
            [SerializeField, InspectorLabel("시선 중심 오프셋"), Tooltip("Player CameraAnchor에 더할 월드 XYZ 오프셋입니다(m). 플레이어 회전에 따라 회전하지 않습니다.")]
            public Vector3 focusOffset;
            [SerializeField, InspectorLabel("고정 시야각"), Tooltip("원근 카메라의 수직 시야각입니다(도)."), Range(1, 179)]
            public float fieldOfView = 40;
            private CinemachineCamera appliedCamera;
            private (Vector2 screen, Vector2 zone, Vector3 damping, float distance, Vector3 angles, float fov, Vector3 offset) appliedValues;
            private float appliedAspect;
            private Quaternion appliedTargetRotation;
            private bool hasRejectedValues;
            private (Vector2 screen, Vector2 zone, Vector3 damping, float distance, Vector3 angles, float fov, Vector3 offset) Values
                => (screenPercent, deadZone, damping, distance, angles, fieldOfView, focusOffset);
            public ViewSettings(Vector2 screen, Vector2 width, Vector3 rotation)
            { screenPercent = screen; deadZone = width; angles = rotation; }
            public bool HasValidValues => !(
                    !CameraProfileValidation.IsFinite(distance) || distance < .01f || !CameraProfileValidation.IsFinite(angles) ||
                    !CameraProfileValidation.IsFinite(focusOffset) ||
                    !CameraProfileValidation.IsFinite(damping) || damping.x < 0 || damping.y < 0 || damping.z < 0 ||
                    !float.IsFinite(fieldOfView) || fieldOfView < 1 || fieldOfView > 179 ||
                    !float.IsFinite(screenPercent.x) || !float.IsFinite(screenPercent.y) ||
                    screenPercent.x < 0 || screenPercent.x > 100 || screenPercent.y < 0 || screenPercent.y > 100 ||
                    !float.IsFinite(deadZone.x) || !float.IsFinite(deadZone.y) || deadZone.x < 0 || deadZone.x > 1 || deadZone.y < 0 || deadZone.y > 1);
            public float Depth(float aspect)
            {
                if (!HasValidValues || !float.IsFinite(aspect) || aspect <= 0)
                    throw new InvalidOperationException("카메라 구도와 화면비의 유한 값·범위를 확인하세요.");
                float tangent = Mathf.Tan(fieldOfView * .5f * Mathf.Deg2Rad);
                Vector2 screen = screenPercent / 50f - Vector2.one;
                return distance / Mathf.Sqrt(1 + tangent * tangent * (screen.x * screen.x * aspect * aspect + screen.y * screen.y));
            }
            // 설치된 PositionComposer의 최소 전방 깊이(0.01m) 미만은 정확한 반경을 표현할 수 없다.
            public bool IsValidForAspect(float aspect) => HasValidValues && float.IsFinite(aspect) && aspect > 0 && Depth(aspect) >= .01f;
            public Vector3 PreviewPosition(Vector3 trackingPoint, float aspect)
            {
                float depth = Depth(aspect);
                float height = depth * Mathf.Tan(fieldOfView * .5f * Mathf.Deg2Rad);
                Vector2 screen = screenPercent / 50f - Vector2.one;
                return trackingPoint + focusOffset - Quaternion.Euler(angles) * new Vector3(screen.x * height * aspect, screen.y * height, depth);
            }
            public void Apply(CinemachineCamera camera, float aspect = 1)
            {
                if (!IsValidForAspect(aspect))
                    throw new InvalidOperationException("카메라 구도 설정의 유한 값과 범위를 확인하세요.");
                var composer = camera.GetComponent<CinemachinePositionComposer>();
                if (composer == null || !composer.enabled) throw new InvalidOperationException("활성 PositionComposer가 필요합니다.");
                camera.transform.rotation = Quaternion.Euler(angles);
                var lens = camera.Lens; lens.FieldOfView = fieldOfView; camera.Lens = lens;
                composer.CameraDistance = Depth(aspect); composer.Damping = damping;
                // Composer의 오프셋은 Follow 로컬 축이다. 작성 값은 월드 축이므로 매 프레임 회전 변화도 감지한다.
                SetTargetOffset(camera, focusOffset); composer.Lookahead.Enabled = false;
                composer.DeadZoneDepth = 0; composer.CenterOnActivate = true;
                composer.Composition = new ScreenComposerSettings
                {
                    ScreenPosition = ToScreenPosition(screenPercent),
                    DeadZone = new ScreenComposerSettings.DeadZoneSettings { Enabled = true, Size = deadZone }
                };
                appliedCamera = camera; appliedValues = Values; appliedAspect = aspect;
                hasRejectedValues = false;
            }
            private void SetTargetOffset(CinemachineCamera camera, Vector3 worldOffset)
            {
                appliedTargetRotation = camera.Follow != null ? camera.Follow.rotation : Quaternion.identity;
                camera.GetComponent<CinemachinePositionComposer>().TargetOffset = Quaternion.Inverse(appliedTargetRotation) * worldOffset;
            }
            internal void RestoreTargetOffset(CinemachineCamera camera) => SetTargetOffset(camera, appliedValues.offset);
            public bool Refresh(CinemachineCamera camera, float aspect = 1)
            {
                var values = Values;
                var targetRotation = camera.Follow != null ? camera.Follow.rotation : Quaternion.identity;
                if (camera == appliedCamera && values.Equals(appliedValues) && appliedAspect == aspect)
                {
                    // Actor가 돌아도 월드 오프셋만 보존한다. 정상 추적의 감쇠 상태를 초기화하지 않는다.
                    if (appliedTargetRotation != targetRotation)
                    {
                        RestoreTargetOffset(camera);
                    }
                    hasRejectedValues = false; return false;
                }
                if (!IsValidForAspect(aspect))
                {
                    if (!hasRejectedValues)
                        Debug.LogWarning("카메라 '" + camera.name + "'의 구도 설정 범위를 확인하세요. 마지막 유효 구도를 유지합니다.", camera);
                    hasRejectedValues = true;
                    return false;
                }
                Apply(camera, aspect);
                return true;
            }
        }
        [SerializeField, InspectorLabel("측면 카메라"), Tooltip("이 서비스가 선택할 측면 CinemachineCamera입니다. PositionComposer가 필요합니다.")]
        private CinemachineCamera sideCamera;
        [SerializeField, InspectorLabel("통로 카메라"), Tooltip("이 서비스가 선택할 통로 CinemachineCamera입니다. PositionComposer가 필요합니다.")]
        private CinemachineCamera corridorCamera;
        [SerializeField, InspectorLabel("쿼터 카메라"), Tooltip("이 서비스가 선택할 쿼터 CinemachineCamera입니다. PositionComposer가 필요합니다.")]
        private CinemachineCamera quarterCamera;
        private PlayerBridgeService player;
        private IGameplayControlService control;
        private IGameFlowService flow;
        private StageService stage;
        private PlayerMovementModeController modes;
        private CinemachineBrain brain;
        private CinemachineCamera side, corridor, quarter, requestedCamera, committedCamera;
        private Transform target, body;
        private IDisposable inputLease;
        private PlayerBridgeService.CorrectionOwnership correction;
        private Tween playerTween, blockerTween;
        private Rigidbody ownedBlockerBody;
        private StageService.Section requestedSection;

        private PlayerFacade acceptedActor;
        private long actorGeneration, requestGeneration;
        private bool initialSelection, motionsComplete, viewCommitted, playerMoveSubmitted, blockerMoveSubmitted, unbinding, cancellingTransition;
        private Vector3 playerGoal, blockerGoal;
        private CinemachineBlendDefinition previousBlend;
        private CinemachineBlenderSettings previousCustomBlends;
        private CinemachineBrain.UpdateMethods previousUpdate;
        private CinemachineBrain.BrainUpdateMethods previousBlendUpdate;
        private bool previousIgnoreTimeScale;
        private PlayerMovementMode requestedMode;
        public CameraTransitionState State { get; private set; }
        public bool IsTransitioning => State >= CameraTransitionState.Accepted && State <= CameraTransitionState.WaitingForCamera;
        public bool IsViewReady => viewCommitted && !IsTransitioning && HasBinding && modes.IsInitialized && modes.CurrentMode == requestedMode && SelectionComplete;
        public Exception TransitionFailure { get; private set; }
        /// <summary>수락 전 거절은 진행 중 작업의 State/TransitionFailure와 별개다.</summary>
        public string LastRejectionReason { get; private set; }
        private bool HasBinding => !unbinding && !cancellingTransition && player != null && player.HasActor && modes != null && modes.isActiveAndEnabled &&
            brain != null && brain.isActiveAndEnabled && target != null && body != null && side != null && corridor != null && quarter != null && stage != null && stage.IsBound;
        private bool SelectionComplete => requestedCamera != null && ReferenceEquals(brain.ActiveVirtualCamera, requestedCamera) && !brain.IsBlending;
        private bool Paused => control.State.WorldPaused || control.State.PresentationPaused || Time.timeScale <= 0;

        public void Bind(PlayerBridgeService bridge, IGameplayControlService gameplayControl,
            PlayerMovementModeController movementModes, CinemachineBrain outputBrain, StageService owner, IGameFlowService flowService)
        {
            // lease 반환의 동기 알림에서 옛 해제가 진행 중인 서비스에 새 구독을 설치하지 않는다.
            if (unbinding || cancellingTransition)
                throw new InvalidOperationException("카메라 연결 해제·취소 중에는 재바인딩할 수 없습니다. 해제 호출이 끝난 뒤 다시 연결하세요.");
            Transform cameraTarget = bridge?.CameraTarget, actorBody = bridge?.BodyTransform;
            if (bridge == null || gameplayControl == null || movementModes == null || outputBrain == null ||
                sideCamera == null || corridorCamera == null || quarterCamera == null || sideCamera == corridorCamera ||
                sideCamera == quarterCamera || corridorCamera == quarterCamera || cameraTarget == null || actorBody == null || owner == null || flowService == null)
                throw new ArgumentException("현재 Actor, Stage, Flow와 서로 다른 세 구도 카메라가 필요합니다.");
            if (!bridge.HasActor || bridge.BodyTransform != actorBody || movementModes.gameObject != bridge.Actor.gameObject || owner.Actor != bridge.Actor)
                throw new ArgumentException("카메라는 현재 Stage의 실제 Actor에 연결해야 합니다.");

            foreach (var camera in new[] { sideCamera, corridorCamera, quarterCamera })
            {
                Cooked.Integration.SceneComponentLookup.RequireSameScene(this, camera, "구도 카메라");
                if (camera.GetComponent<CinemachinePositionComposer>() == null || !camera.isActiveAndEnabled)
                    throw new ArgumentException("각 카메라의 활성 PositionComposer가 필요합니다.");
                foreach (var component in camera.GetComponents<CinemachineComponentBase>())
                    if (component.enabled && component.Stage != CinemachineCore.Stage.Body)
                        throw new ArgumentException("고정 회전 구도에는 Aim/Noise 회전 writer를 연결하지 마세요.");
            }
            Unbind();
            player = bridge; control = gameplayControl; modes = movementModes; brain = outputBrain;
            side = sideCamera; corridor = corridorCamera; quarter = quarterCamera; target = cameraTarget; body = actorBody;
            stage = owner; flow = flowService;

            previousBlend = brain.DefaultBlend; previousCustomBlends = brain.CustomBlends;
            previousIgnoreTimeScale = brain.IgnoreTimeScale; previousUpdate = brain.UpdateMethod; previousBlendUpdate = brain.BlendUpdateMethod;
            try
            {
                brain.IgnoreTimeScale = false; brain.CustomBlends = null;
                brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
                brain.BlendUpdateMethod = CinemachineBrain.BrainUpdateMethods.LateUpdate;
                foreach (var camera in Cameras)
                {
                    Settings(camera).Apply(camera, OutputAspect);
                    camera.Follow = null; camera.LookAt = null; camera.Priority = 0;
                }
                player.ActorChanged += OnActorChanged; player.Dying += OnDying;
                stage.Releasing += OnStageReleasing;
                State = CameraTransitionState.Idle;
            }
            catch { Unbind(); throw; }
        }
        private float OutputAspect => brain.OutputCamera.aspect;
        private CinemachineCamera[] Cameras => new[] { side, corridor, quarter };
        private ViewSettings Settings(CinemachineCamera camera) => stage.ViewFor(camera == side ? CameraMovementMode.Side : camera == corridor ? CameraMovementMode.Corridor : CameraMovementMode.Quarter);
        private bool RefreshView(CinemachineCamera camera)
        {
            var settings = Settings(camera);
            var previousFollow = camera.Follow;
            bool temporaryFollow = previousFollow == null && (camera == requestedCamera || camera == committedCamera);
            try
            {
                // 월드 오프셋을 Follow 로컬 값으로 바꾸기 전에 실제 추적점부터 연결한다.
                if (temporaryFollow) camera.Follow = target;
                if (!settings.Refresh(camera, OutputAspect)) return false;
                camera.PreviousStateIsValid = false;
                if (temporaryFollow) camera.UpdateCameraState(Vector3.up, -1);
                return true;
            }
            finally
            {
                if (temporaryFollow)
                {
                    camera.Follow = previousFollow;
                    // 평가한 CameraState는 유지하고, Follow와 Composer 로컬 오프셋/회전 캐시만 원상태로 맞춘다.
                    settings.RestoreTargetOffset(camera);
                }
            }
        }
        public static Vector2 ToScreenPosition(Vector2 percent)
        {
            if (!float.IsFinite(percent.x) || !float.IsFinite(percent.y) || percent.x < 0 || percent.x > 100 || percent.y < 0 || percent.y > 100)
                throw new ArgumentOutOfRangeException(nameof(percent));
            // PositionComposer.ScreenToOrtho는 Rect Y를 위쪽 원점으로 해석한다.
            return new Vector2(percent.x / 100f - .5f, .5f - percent.y / 100f);
        }
        public static PlayerMovementMode ResolveMode(CameraMovementMode view) => view switch
        {
            CameraMovementMode.Side => PlayerMovementMode.Side,
            CameraMovementMode.Corridor or CameraMovementMode.Quarter => PlayerMovementMode.Quarter,
            _ => throw new ArgumentOutOfRangeException(nameof(view))
        };
        public void EnterSection(StageService.Section section, bool restore)
        {
            if (section == null || !HasBinding || player.Actor.IsDead || !ReferenceEquals(stage.CurrentSection,section))
                throw new InvalidOperationException("현재 Stage가 선택한 구간과 살아 있는 Actor가 필요합니다.");
            var selected = section.View == CameraMovementMode.Side ? side : section.View == CameraMovementMode.Corridor ? corridor : quarter;
            bool sameView = IsTransitioning ? requestedCamera == selected : committedCamera == selected;
            // New accepted starts replace old work even when their view is identical.
            CancelTransition(false, true);
            if (!HasBinding || !ReferenceEquals(stage.CurrentSection,section)) return;
            requestedSection = section;
            long generation=++requestGeneration;
            acceptedActor=player.Actor;actorGeneration=player.ActorGeneration;
            requestedMode=ResolveMode(section.View);
            var capsule=acceptedActor.GetComponent<CapsuleCollider>();
            var rigidbody=acceptedActor.GetComponent<Rigidbody>();
            if(capsule == null || rigidbody == null) throw new InvalidOperationException("진입 보정 Capsule/Rigidbody가 필요합니다.");
            playerGoal=rigidbody.position + section.Start.CheckpointPose.position - capsule.transform.TransformPoint(capsule.center);
            blockerGoal=section.BlockerBody != null ? section.BlockerGoal : Vector3.zero;
            State=CameraTransitionState.Accepted;initialSelection=restore;motionsComplete=restore;viewCommitted=false;
            playerMoveSubmitted=blockerMoveSubmitted=false;TransitionFailure=null;LastRejectionReason=null;
            try
            {
                var lease=control.BlockGameplay("구간 진입 보정");
                if(!IsCurrent(generation)){lease.Dispose();return;}
                inputLease=lease;player.ClearInput();
                if(!restore && (!player.IsCorrectionPathSafe(playerGoal) || !HasSupport(playerGoal) || !section.IsBlockerPathSafe(acceptedActor,playerGoal)))
                    throw new InvalidOperationException("수락한 구간 '"+section.DisplayName+"' 보정 경로·지지면·차단 경로가 안전하지 않습니다.");
                requestedCamera=selected;
                if(restore || !sameView)
                {
                    foreach(var camera in Cameras){camera.Follow=camera==selected?target:null;camera.LookAt=null;camera.Priority=camera==selected?20:0;}
                    Settings(selected).Apply(selected, OutputAspect);
                    selected.PreviousStateIsValid=false;
                    selected.UpdateCameraState(Vector3.up,-1);
                    brain.DefaultBlend=new CinemachineBlendDefinition(restore?CinemachineBlendDefinition.Styles.Cut:CinemachineBlendDefinition.Styles.EaseInOut,
                        restore?0:section.TransitionSeconds);
                    if(restore)brain.ResetState();
                }
                else selected.Follow=target;
                if(!restore)
                {
                    correction=player.TryAcquireCorrection(acceptedActor,actorGeneration,()=>CancelTransition(false));
                    if(!IsCurrent(generation)||correction==null)throw new InvalidOperationException("Actor 보정 writer를 획득하지 못했습니다.");
                    State=CameraTransitionState.CorrectingPlayer;
                    playerTween=correction.Body.DOMove(playerGoal,section.CorrectionSeconds).SetEase(Ease.InOutSine).SetUpdate(UpdateType.Manual).Pause();
                }
                else State=CameraTransitionState.WaitingForCamera;
            }
            catch(Exception error){if(generation==requestGeneration)Fail(error);throw;}
        }
        private bool HasSupport(Vector3 goal)
        {
            var capsule=acceptedActor.GetComponent<CapsuleCollider>();
            PlayerBridgeService.CapsuleGeometry(capsule,out var a,out var b,out float radius);
            var delta=goal-acceptedActor.GetComponent<Rigidbody>().position;
            var bottom=(a.y<b.y?a:b)+delta;
            foreach(var hit in Physics.SphereCastAll(bottom+Vector3.up*.02f,Mathf.Max(.001f,radius-.002f),Vector3.down,.15f,~0,QueryTriggerInteraction.Ignore))
                if(hit.collider.attachedRigidbody!=acceptedActor.GetComponent<Rigidbody>() &&
                    !Physics.GetIgnoreLayerCollision(capsule.gameObject.layer,hit.collider.gameObject.layer) &&
                    !Physics.GetIgnoreCollision(capsule,hit.collider) && Vector3.Angle(hit.normal,Vector3.up)<=acceptedActor.GetComponent<CharacterMovement>().MaximumGroundAngle)
                    return true;
            return false;
        }
        private bool IsCurrent(long generation) => generation==requestGeneration && IsTransitioning && HasBinding &&
            requestedSection!=null && ReferenceEquals(stage.CurrentSection,requestedSection) &&
            player.IsCurrentActor(acceptedActor,actorGeneration) && !acceptedActor.IsDead &&
            (initialSelection || (flow.Snapshot.State==FlowState.Playing && !flow.Snapshot.IsBusy));
        private void FixedUpdate()
        {
            if (!IsTransitioning || initialSelection || player == null || Paused) return;
            if (!IsCurrent(requestGeneration)) { CancelTransition(false); return; }
            long generation = requestGeneration;
            try
            {
                if (State == CameraTransitionState.CorrectingPlayer && !playerMoveSubmitted)
                {
                    if (!player.IsCorrectionPathSafe(playerGoal)) throw new InvalidOperationException("진행 중 플레이어 보정 경로가 막혔습니다.");
                    playerMoveSubmitted = Advance(playerTween, Time.fixedDeltaTime);
                }
                else if (State == CameraTransitionState.MovingBlocker && !blockerMoveSubmitted)
                {
                    if (!requestedSection.IsBlockerPathSafe(acceptedActor, correction.Body.position)) throw new InvalidOperationException("진행 중 차단 경로가 막혔습니다.");
                    blockerMoveSubmitted = Advance(blockerTween, Time.fixedDeltaTime);
                }
            }
            catch (Exception error) { if (generation == requestGeneration) Fail(error); Debug.LogException(error, this); }
        }
        private void LateUpdate()
        {
            if (player == null) return;
            if (!HasBinding) { Unbind(); return; }
            bool initialCut = initialSelection && !control.State.PresentationPaused;
            if (IsTransitioning && !IsCurrent(requestGeneration)) { CancelTransition(false); return; }
            long generation = requestGeneration;
            try
            {
                bool viewChanged = RefreshView(side) | RefreshView(corridor) | RefreshView(quarter);
                if (Paused && !initialCut)
                {
                    if (viewChanged)
                    {
                        brain.ManualUpdate(Time.frameCount, 0);
                    }
                    return;
                }
                if (IsTransitioning && !motionsComplete)
                {
                    if (correction == null || !correction.IsCurrent) { CancelTransition(false); return; }
                    if (State == CameraTransitionState.CorrectingPlayer && playerMoveSubmitted && (correction.Body.position - playerGoal).sqrMagnitude < .000001f)
                    {
                        playerTween = null;
                        if (!IsCurrent(generation)) return;
                        if (requestedSection.BlockerBody != null)
                        {
                            if (!requestedSection.IsBlockerPathSafe(acceptedActor, correction.Body.position)) throw new InvalidOperationException("보정 후 차단 경로가 막혔습니다.");
                            State = CameraTransitionState.MovingBlocker;
                            ownedBlockerBody = requestedSection.BlockerBody;
                            blockerTween = ownedBlockerBody.DOMove(blockerGoal, requestedSection.BlockerSeconds)
                                .SetEase(Ease.InOutSine).SetUpdate(UpdateType.Manual).Pause();
                        }
                        else { motionsComplete = true; State = CameraTransitionState.WaitingForCamera; }
                    }
                    else if (State == CameraTransitionState.MovingBlocker && blockerMoveSubmitted && (requestedSection.BlockerBody.position - blockerGoal).sqrMagnitude < .000001f)
                    {
                        blockerTween = null;
                        if (!IsCurrent(generation)) return;
                        ownedBlockerBody = null; // 실제 물리 도착 후에는 보류 이동 소유권도 끝난다.
                        motionsComplete = true; State = CameraTransitionState.WaitingForCamera;
                    }
                }
                brain.ManualUpdate(Time.frameCount, initialCut ? -1 : Time.deltaTime);
                if (!IsTransitioning || !motionsComplete || !SelectionComplete || !modes.IsInitialized) return;
                correction?.Dispose(); correction = null;
                if (!IsCurrent(generation)) return;
                if (!modes.TrySetMode(requestedMode)) throw new InvalidOperationException("새 이동축 적용을 거절했습니다.");
                if (!IsCurrent(generation)) return;
                requestedCamera.Follow = target; committedCamera = requestedCamera;
                State = CameraTransitionState.Completed; viewCommitted = true; initialSelection = false;
                player.ClearInput(); ReleaseLease();
            }
            catch (Exception error) { if (generation == requestGeneration) Fail(error); Debug.LogException(error, this); }
        }
        private static bool Advance(Tween tween, float delta)
        {
            if (tween == null || !tween.IsActive()) throw new InvalidOperationException("소유 이동이 완료 전에 사라졌습니다.");
            float elapsed = Mathf.Min(tween.Duration(), tween.Elapsed() + delta);
            bool complete = elapsed >= tween.Duration();
            tween.Goto(elapsed, false);
            if (complete && tween.IsActive()) tween.Kill(false);
            return complete;
        }
        private void Fail(Exception error)
        {
            TransitionFailure = error;
            try { CancelTransition(true); }
            catch (Exception cleanup)
            {
                TransitionFailure = new AggregateException("카메라 전환 실패와 자원 복원 오류입니다.", error, cleanup);
                throw TransitionFailure;
            }
        }
        private void ReleaseLease() { var lease = inputLease; inputLease = null; lease?.Dispose(); }
        private void CancelTransition(bool failed, bool preserveView = false)
        {
            if (cancellingTransition) return;
            cancellingTransition = true;
            try
            {
                long cancelledRequest = requestGeneration++;
                var oldBlockerBody = ownedBlockerBody; ownedBlockerBody = null;
                var oldPlayerTween = playerTween; playerTween = null;
                var oldBlockerTween = blockerTween; blockerTween = null;
                var oldCorrection = correction; correction = null;
                var oldLease = inputLease; inputLease = null;
                State = failed ? CameraTransitionState.Failed : CameraTransitionState.Cancelled;
                initialSelection = false; motionsComplete = false; viewCommitted = false;
                // 먼저 무효화한다. Dispose/OnKill/제어 통지가 실패해도 나머지 자기 자원을 끝까지 반환한다.
                var errors = new List<Exception>();
                SessionCleanup.Attempt(errors, () => oldPlayerTween?.Kill(false));
                SessionCleanup.Attempt(errors, () => oldBlockerTween?.Kill(false));
                SessionCleanup.Attempt(errors, () =>
                {
                    // Kill은 이미 PhysX에 제출한 MovePosition을 지우지 않는다. 자기 수락 객체/세대만 flush한다.
                    if (oldBlockerBody == null) return;
                    Vector3 position = oldBlockerBody.position;
                    Quaternion rotation = oldBlockerBody.rotation;
                    oldBlockerBody.position = position;
                    oldBlockerBody.rotation = rotation;
                    oldBlockerBody.transform.SetPositionAndRotation(position, rotation);
                });
                SessionCleanup.Attempt(errors, () => oldCorrection?.Dispose());
                if (!preserveView && !unbinding && committedCamera != null)
                    foreach (var camera in Cameras)
                        if (camera != null) SessionCleanup.Attempt(errors, () =>
                        { camera.Priority = camera == committedCamera ? 20 : 0; camera.Follow = camera == committedCamera ? target : null; });
                SessionCleanup.Attempt(errors, () => oldLease?.Dispose());
                SessionCleanup.ThrowIfAny(errors, "카메라 전환 취소 후 일부 자원 복원에서 오류가 발생했습니다.");
            }
            finally { cancellingTransition = false; }
        }
        private void OnActorChanged() => Unbind();
        private void OnDying() => CancelTransition(false);
        private void OnStageReleasing() => Unbind();
        public void ResetForRetry() => Unbind();
        public void Unbind()
        {
            if (unbinding) return;
            unbinding = true;
            try
            {
                if (player != null) { player.ActorChanged -= OnActorChanged; player.Dying -= OnDying; }
                if (stage != null) stage.Releasing -= OnStageReleasing;
                Exception cleanupError = null;
                try { CancelTransition(false); } catch (Exception error) { cleanupError = error; }
                foreach (var camera in Cameras)
                    if (camera != null)
                    {
                        camera.Follow = null; camera.LookAt = null; camera.Priority = 0;
                    }
                if (brain != null)
                {
                    brain.DefaultBlend = previousBlend; brain.CustomBlends = previousCustomBlends;
                    brain.UpdateMethod = previousUpdate; brain.BlendUpdateMethod = previousBlendUpdate; brain.IgnoreTimeScale = previousIgnoreTimeScale;
                }
                LastRejectionReason = null;
                player = null; control = null; flow = null; stage = null; modes = null; brain = null;
                side = corridor = quarter = requestedCamera = committedCamera = null; target = body = null; requestedSection = null;
                if (cleanupError != null) throw cleanupError;
            }
            finally { unbinding = false; }
        }
        public void Dispose() => Unbind();
        private void OnDisable() => Unbind();
        private void OnDestroy() => Unbind();
    }
}
