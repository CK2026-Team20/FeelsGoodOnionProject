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
    /// <summary>실물 구간에서 계산한 월드 정렬 값. 작성용 컴포넌트나 저장 자산이 아니다.</summary>
    public readonly struct CameraZoneData
    {
        public CameraMovementMode Movement { get; }
        public Vector3 AlignmentPosition { get; }
        public CameraZoneData(CameraMovementMode movement, Vector3 worldPosition)
        {
            CameraZoneRequest.Validate(movement, worldPosition);
            Movement = movement; AlignmentPosition = worldPosition;
        }
    }
    public readonly struct CameraZoneRequest
    {
        public CameraMovementMode Movement { get; }
        public Vector3 AlignmentPosition { get; }
        public CameraZoneTrigger Source { get; }
        public CameraZoneRequest(CameraMovementMode movement, Vector3 alignmentPosition)
        { Validate(movement, alignmentPosition); Movement = movement; AlignmentPosition = alignmentPosition; Source = null; }
        public CameraZoneRequest(CameraZoneTrigger source, CameraZoneData data)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            Validate(data.Movement, data.AlignmentPosition);
            Source = source; Movement = data.Movement; AlignmentPosition = data.AlignmentPosition;
        }
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
        private sealed class ViewSettings
        {
            [SerializeField, InspectorLabel("화면 위치"), Tooltip("대상의 화면 위치입니다. X는 왼쪽 0~오른쪽 100, Y는 아래 0~위 100입니다.")]
            public Vector2 screenPercent;
            [SerializeField, InspectorLabel("허용 폭과 높이"), Tooltip("추적하지 않고 허용할 화면 폭과 높이 비율입니다.")]
            public Vector2 deadZone;
            [SerializeField, InspectorLabel("추적 감쇠"), Tooltip("축별 추적 지연을 조절합니다. 0이면 즉시 추적합니다.")]
            public Vector3 damping = Vector3.one;
            [SerializeField, InspectorLabel("카메라 거리"), Tooltip("대상에서 카메라 전방축을 따라 유지할 거리입니다(m)."), Min(.01f)]
            public float distance = 10;
            [SerializeField, InspectorLabel("고정 각도"), Tooltip("월드 기준 카메라 회전입니다(도). 플레이어가 뒤돌아도 변하지 않습니다.")]
            public Vector3 angles;
            [SerializeField, InspectorLabel("고정 시야각"), Tooltip("원근 카메라의 수직 시야각입니다(도)."), Range(1, 179)]
            public float fieldOfView = 40;
            public ViewSettings(Vector2 screen, Vector2 width, Vector3 rotation)
            { screenPercent = screen; deadZone = width; angles = rotation; }
            public void Apply(CinemachineCamera camera)
            {
                if (!CameraZoneRequest.IsFinite(distance) || distance <= 0 || !CameraZoneRequest.IsFinite(angles) ||
                    !CameraZoneRequest.IsFinite(damping) || damping.x < 0 || damping.y < 0 || damping.z < 0 ||
                    !float.IsFinite(fieldOfView) || fieldOfView < 1 || fieldOfView > 179 ||
                    !float.IsFinite(screenPercent.x) || !float.IsFinite(screenPercent.y) ||
                    screenPercent.x < 0 || screenPercent.x > 100 || screenPercent.y < 0 || screenPercent.y > 100 ||
                    !float.IsFinite(deadZone.x) || !float.IsFinite(deadZone.y) || deadZone.x < 0 || deadZone.x > 1 || deadZone.y < 0 || deadZone.y > 1)
                    throw new InvalidOperationException("카메라 구도 설정의 유한 값과 범위를 확인하세요.");
                var composer = camera.GetComponent<CinemachinePositionComposer>();
                if (composer == null || !composer.enabled) throw new InvalidOperationException("활성 PositionComposer가 필요합니다.");
                camera.transform.rotation = Quaternion.Euler(angles);
                var lens = camera.Lens; lens.FieldOfView = fieldOfView; camera.Lens = lens;
                composer.CameraDistance = distance; composer.Damping = damping;
                composer.TargetOffset = Vector3.zero; composer.Lookahead.Enabled = false;
                composer.Composition = new ScreenComposerSettings
                {
                    ScreenPosition = ToScreenPosition(screenPercent),
                    DeadZone = new ScreenComposerSettings.DeadZoneSettings { Enabled = true, Size = deadZone }
                };
            }
        }
        [SerializeField, InspectorLabel("측면 구도"), Tooltip("측면 추적의 화면 위치, 거리와 고정 각도를 조절합니다.")]
        private ViewSettings sideView = new ViewSettings(new Vector2(40, 50), new Vector2(.1f, .1f), Vector3.zero);
        [SerializeField, InspectorLabel("통로 구도"), Tooltip("통로 추적의 화면 위치, 거리와 고정 각도를 조절합니다.")]
        private ViewSettings corridorView = new ViewSettings(new Vector2(50, 55), new Vector2(.1f, .1f), new Vector3(10, 0, 0));
        [SerializeField, InspectorLabel("쿼터 구도"), Tooltip("쿼터 추적의 화면 위치, 거리와 고정 각도를 조절합니다.")]
        private ViewSettings quarterView = new ViewSettings(new Vector2(50, 50), new Vector2(.15f, .1f), new Vector3(20, -30, 0));
        [SerializeField, InspectorLabel("측면 카메라"), Tooltip("이 서비스가 선택할 측면 CinemachineCamera입니다. PositionComposer와 Confiner3D가 필요합니다.")]
        private CinemachineCamera sideCamera;
        [SerializeField, InspectorLabel("통로 카메라"), Tooltip("이 서비스가 선택할 별도 통로 CinemachineCamera입니다. PositionComposer와 Confiner3D가 필요합니다.")]
        private CinemachineCamera corridorCamera;
        [SerializeField, InspectorLabel("쿼터 카메라"), Tooltip("이 서비스가 선택할 쿼터 CinemachineCamera입니다. PositionComposer와 Confiner3D가 필요합니다.")]
        private CinemachineCamera quarterCamera;
        private PlayerBridgeService player;
        private IGameplayControlService control;
        private IGameFlowService flow;
        private StageService stage;
        private PlayerMovementModeController modes;
        private CinemachineBrain brain;
        private CinemachineCamera side, corridor, quarter, requestedCamera, committedCamera;
        private Transform target, body;
        private BoxCollider bounds;
        private IDisposable inputLease;
        private PlayerBridgeService.CorrectionOwnership correction;
        private Tween playerTween, blockerTween;
        private PlayerFacade acceptedActor;
        private long actorGeneration, requestGeneration, zoneRevision;
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
        public CameraZoneTrigger LastRejectedZone { get; private set; }
        private readonly HashSet<(CameraZoneTrigger source, long binding, string reason)> reportedRejections = new();
        public CameraZoneTrigger CurrentZone { get; private set; }
        private bool HasBinding => !unbinding && !cancellingTransition && player != null && player.HasActor && modes != null && modes.isActiveAndEnabled &&
            brain != null && brain.isActiveAndEnabled && target != null && body != null && side != null && corridor != null && quarter != null && stage != null && stage.IsBound;
        private bool SelectionComplete => requestedCamera != null && ReferenceEquals(brain.ActiveVirtualCamera, requestedCamera) && !brain.IsBlending;
        private bool Paused => control.State.WorldPaused || control.State.PresentationPaused || Time.timeScale <= 0;

        public void Bind(PlayerBridgeService bridge, IGameplayControlService gameplayControl,
            PlayerMovementModeController movementModes, CinemachineBrain outputBrain, StageService owner, IGameFlowService flowService)
        {
            Transform cameraTarget = bridge?.CameraTarget, actorBody = bridge?.BodyTransform;
            if (bridge == null || gameplayControl == null || movementModes == null || outputBrain == null ||
                sideCamera == null || corridorCamera == null || quarterCamera == null || sideCamera == corridorCamera ||
                sideCamera == quarterCamera || corridorCamera == quarterCamera || cameraTarget == null || actorBody == null || owner == null || flowService == null)
                throw new ArgumentException("현재 Actor, Stage, Flow와 서로 다른 세 구도 카메라가 필요합니다.");
            if (!bridge.HasActor || bridge.BodyTransform != actorBody || movementModes.gameObject != bridge.Actor.gameObject || owner.Actor != bridge.Actor)
                throw new ArgumentException("카메라는 현재 Stage의 실제 Actor에 연결해야 합니다.");
            var boundary = owner.CameraBounds;
            foreach (var camera in new[] { sideCamera, corridorCamera, quarterCamera })
            {
                Cooked.Integration.SceneComponentLookup.RequireSameScene(this, camera, "구도 카메라");
                if (camera.GetComponent<CinemachinePositionComposer>() == null || camera.GetComponent<CinemachineConfiner3D>() == null || !camera.isActiveAndEnabled)
                    throw new ArgumentException("각 카메라의 활성 PositionComposer와 Confiner3D 연결이 필요합니다.");
                foreach (var component in camera.GetComponents<CinemachineComponentBase>())
                    if (component.enabled && component.Stage != CinemachineCore.Stage.Body)
                        throw new ArgumentException("고정 회전 구도에는 Aim/Noise 회전 writer를 연결하지 마세요.");
            }
            Unbind();
            player = bridge; control = gameplayControl; modes = movementModes; brain = outputBrain;
            side = sideCamera; corridor = corridorCamera; quarter = quarterCamera; target = cameraTarget; body = actorBody;
            stage = owner; flow = flowService; bounds = boundary;
            previousBlend = brain.DefaultBlend; previousCustomBlends = brain.CustomBlends;
            previousIgnoreTimeScale = brain.IgnoreTimeScale; previousUpdate = brain.UpdateMethod; previousBlendUpdate = brain.BlendUpdateMethod;
            try
            {
                brain.IgnoreTimeScale = false; brain.CustomBlends = null;
                brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
                brain.BlendUpdateMethod = CinemachineBrain.BrainUpdateMethods.LateUpdate;
                foreach (var camera in Cameras)
                {
                    Settings(camera).Apply(camera);
                    camera.Follow = null; camera.LookAt = null; camera.Priority = 0;
                    var confiner = camera.GetComponent<CinemachineConfiner3D>();
                    confiner.BoundingVolume = bounds; confiner.SlowingDistance = 0; confiner.enabled = true;
                }
                player.ActorChanged += OnActorChanged; player.Dying += OnDying;
                stage.Releasing += OnStageReleasing;
                State = CameraTransitionState.Idle;
            }
            catch { Unbind(); throw; }
        }
        private CinemachineCamera[] Cameras => new[] { side, corridor, quarter };
        private ViewSettings Settings(CinemachineCamera camera) => camera == side ? sideView : camera == corridor ? corridorView : quarterView;
        public static Vector2 ToScreenPosition(Vector2 percent)
        {
            if (!float.IsFinite(percent.x) || !float.IsFinite(percent.y) || percent.x < 0 || percent.x > 100 || percent.y < 0 || percent.y > 100)
                throw new ArgumentOutOfRangeException(nameof(percent));
            return percent / 100f - Vector2.one * .5f;
        }
        public static PlayerMovementMode ResolveMode(CameraZoneRequest zone) => zone.Movement switch
        {
            CameraMovementMode.Side => PlayerMovementMode.Side,
            CameraMovementMode.Corridor or CameraMovementMode.Quarter => PlayerMovementMode.Quarter,
            _ => throw new ArgumentOutOfRangeException(nameof(zone))
        };
        public bool TryEnterZone(CameraZoneRequest zone, PlayerFacade candidate)
        {
            if (IsTransitioning || !HasBinding || candidate != player.Actor || candidate == null || candidate.IsDead || Paused ||
                zone.Source == null || !zone.Source.IsBoundTo(this, candidate) || !zone.Source.HasActualContact(candidate) ||
                flow.Snapshot.State != FlowState.Playing || flow.Snapshot.IsBusy) return false;
            if (CurrentZone == zone.Source && State == CameraTransitionState.Completed) return true;
            EnterZone(zone);
            return CurrentZone == zone.Source && (IsTransitioning || State == CameraTransitionState.Completed);
        }
        public void EnterZone(CameraZoneRequest zone, bool snap = false)
        {
            CameraZoneRequest.Validate(zone.Movement, zone.AlignmentPosition);
            if (IsTransitioning || !HasBinding || player.Actor.IsDead) return;
            if (zone.Source == null) throw new ArgumentException("구도 요청은 실제 작성 구간이 필요합니다.");
            if (!snap && (Paused || !stage.CanDispatch(player.Actor) || !zone.Source.HasActualContact(player.Actor))) return;
            var source = zone.Source;
            source.ValidateSetup();
            Vector3 goal = source.PlayerGoal(player.Actor.GetComponent<Rigidbody>().position);
            bool move = !snap && !source.BlockerCompleted && (source.HasBlocker || zone.Movement == CameraMovementMode.Side);
            string unsafeReason = move ? UnsafePathReason(source, player.Actor, goal) : null;
            if (unsafeReason != null) { RejectZone(source, unsafeReason); return; }
            long generation = ++requestGeneration;
            LastRejectionReason = null; LastRejectedZone = null;
            CurrentZone = source; zoneRevision = source.BindingRevision; requestedMode = ResolveMode(zone); TransitionFailure = null;
            acceptedActor = player.Actor; actorGeneration = player.ActorGeneration;
            playerGoal = goal; blockerGoal = source.HasBlocker ? source.BlockerGoal : Vector3.zero;
            playerMoveSubmitted = blockerMoveSubmitted = false;
            State = CameraTransitionState.Accepted; initialSelection = snap; motionsComplete = !move; viewCommitted = false;
            try
            {
                var lease = control.BlockGameplay("카메라·통로 전환");
                if (!IsCurrent(generation)) { lease.Dispose(); return; }
                inputLease = lease; player.ClearInput();
                if (!snap && !source.HasActualContact(acceptedActor)) { CancelTransition(false); return; }
                unsafeReason = move ? UnsafePathReason(source, acceptedActor, goal) : null;
                if (unsafeReason != null)
                {
                    // 이미 수락한 작업은 실행 실패다. 시작 전 거절과 구분한다.
                    Fail(new InvalidOperationException(unsafeReason));
                    LogRejectionOnce(source, "수락 후 안전 재검사 실패: " + unsafeReason);
                    return;
                }
                requestedCamera = zone.Movement == CameraMovementMode.Side ? side : zone.Movement == CameraMovementMode.Corridor ? corridor : quarter;
                foreach (var camera in Cameras) { camera.Follow = null; camera.LookAt = null; camera.Priority = camera == requestedCamera ? 20 : 0; }
                Settings(requestedCamera).Apply(requestedCamera);
                // 수락 시 목표 구도를 한 번 평가한 뒤 Follow를 끊는다. 이전/새 추적 모두 완료 전에는 움직이지 않는다.
                requestedCamera.Follow = target;
                requestedCamera.PreviousStateIsValid = false;
                requestedCamera.UpdateCameraState(Vector3.up, -1);
                requestedCamera.Follow = null;
                brain.DefaultBlend = new CinemachineBlendDefinition(snap ? CinemachineBlendDefinition.Styles.Cut : CinemachineBlendDefinition.Styles.EaseInOut,
                    snap ? 0 : source.TransitionSeconds);
                if (snap) brain.ResetState();
                if (move)
                {
                    correction = player.TryAcquireCorrection(acceptedActor, actorGeneration, () => CancelTransition(false));
                    if (!IsCurrent(generation) || correction == null) throw new InvalidOperationException("Actor 보정 writer를 획득하지 못했습니다.");
                    State = CameraTransitionState.CorrectingPlayer;
                    playerTween = correction.Body.DOMove(goal, source.PlayerMoveSeconds).SetEase(source.PlayerEase).SetUpdate(UpdateType.Manual).Pause();
                }
                else State = CameraTransitionState.WaitingForCamera;
            }
            catch (Exception error) { if (generation == requestGeneration) Fail(error); throw; }
        }
        private string UnsafePathReason(CameraZoneTrigger source, PlayerFacade candidate, Vector3 goal)
        {
            if (!player.IsCorrectionPathSafe(goal))
                return "플레이어 보정 목표 또는 이동 경로가 안전하지 않습니다. 실제 Capsule, 안전 앵커 높이, 바닥·통로·닫힌 차단체를 확인하세요.";
            if (!source.IsBlockerPathSafe(candidate, goal))
                return "차단체 목표 또는 이동 경로가 안전하지 않습니다. 차단 목표·장애물·플레이어 안전 범위를 확인하세요.";
            return null;
        }
        private void RejectZone(CameraZoneTrigger source, string reason)
        {
            LastRejectedZone = source; LastRejectionReason = reason;
            LogRejectionOnce(source, reason);
        }
        private void LogRejectionOnce(CameraZoneTrigger source, string reason)
        {
            // Stay 및 겹친 여러 구간을 번갈아 검사해도 각 바인딩/사유당 한 번만 기록한다.
            if (reportedRejections.Add((source, source.BindingRevision, reason)))
                Debug.LogWarning($"카메라 구간 '{source.name}': {reason}", source);
        }
        private bool IsCurrent(long generation) => generation == requestGeneration && IsTransitioning && HasBinding &&
            player.IsCurrentActor(acceptedActor, actorGeneration) && !acceptedActor.IsDead &&
            CurrentZone != null && CurrentZone.BindingRevision == zoneRevision && CurrentZone.IsBindingCurrent(this, acceptedActor) &&
            (initialSelection || !CurrentZone.HasBlocker || ((CurrentZone.BlockerGoal - blockerGoal).sqrMagnitude < .000001f &&
                CurrentZone.SafeAnchor != null && (CurrentZone.SafeAnchor.position - playerGoal).sqrMagnitude < .000001f)) &&
            (initialSelection || (flow.Snapshot.State == FlowState.Playing && !flow.Snapshot.IsBusy));
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
                    if (!CurrentZone.IsBlockerPathSafe(acceptedActor, correction.Body.position)) throw new InvalidOperationException("진행 중 차단 경로가 막혔습니다.");
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
            if (Paused && !initialCut) return;
            long generation = requestGeneration;
            try
            {
                if (bounds == null || !bounds.enabled || !bounds.gameObject.activeInHierarchy)
                    throw new InvalidOperationException("활성 카메라 경계가 해제되었습니다.");
                if (IsTransitioning && !motionsComplete)
                {
                    if (correction == null || !correction.IsCurrent) { CancelTransition(false); return; }
                    if (State == CameraTransitionState.CorrectingPlayer && playerMoveSubmitted && (correction.Body.position - playerGoal).sqrMagnitude < .000001f)
                    {
                        playerTween = null;
                        if (!IsCurrent(generation)) return;
                        if (CurrentZone.HasBlocker && !CurrentZone.BlockerCompleted)
                        {
                            if (!CurrentZone.IsBlockerPathSafe(acceptedActor, correction.Body.position)) throw new InvalidOperationException("보정 후 차단 경로가 막혔습니다.");
                            State = CameraTransitionState.MovingBlocker;
                            blockerTween = CurrentZone.BlockerBody.DOMove(blockerGoal, CurrentZone.BlockerMoveSeconds)
                                .SetEase(CurrentZone.BlockerEase).SetUpdate(UpdateType.Manual).Pause();
                        }
                        else { motionsComplete = true; State = CameraTransitionState.WaitingForCamera; }
                    }
                    else if (State == CameraTransitionState.MovingBlocker && blockerMoveSubmitted && (CurrentZone.BlockerBody.position - blockerGoal).sqrMagnitude < .000001f)
                    {
                        blockerTween = null;
                        if (!IsCurrent(generation)) return;
                        CurrentZone.MarkBlockerCompleted(); motionsComplete = true; State = CameraTransitionState.WaitingForCamera;
                    }
                }
                brain.ManualUpdate(Time.frameCount, initialCut ? -1 : Time.deltaTime);
                // Confiner는 각 vcam의 Body 단계에 적용된다. 회전/비선형 blend 이후 최종 출력도 같은 실제 Box 안으로 제한한다.
                brain.transform.position = bounds.ClosestPoint(brain.transform.position);
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
        private void CancelTransition(bool failed)
        {
            if (cancellingTransition) return;
            cancellingTransition = true;
            try
            {
                ++requestGeneration;
                var oldPlayerTween = playerTween; playerTween = null;
                var oldBlockerTween = blockerTween; blockerTween = null;
                var oldCorrection = correction; correction = null;
                var oldLease = inputLease; inputLease = null;
                var oldZone = CurrentZone;
                State = failed ? CameraTransitionState.Failed : CameraTransitionState.Cancelled;
                initialSelection = false; motionsComplete = false; viewCommitted = false;
                // 먼저 무효화한다. Dispose/OnKill/제어 통지가 실패해도 나머지 자기 자원을 끝까지 반환한다.
                var errors = new List<Exception>();
                SessionCleanup.Attempt(errors, () => oldPlayerTween?.Kill(false));
                SessionCleanup.Attempt(errors, () => oldBlockerTween?.Kill(false));
                SessionCleanup.Attempt(errors, () => oldCorrection?.Dispose());
                SessionCleanup.Attempt(errors, () => oldZone?.RequestCancelled());
                if (!unbinding && committedCamera != null)
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
        internal void CancelFrom(CameraZoneTrigger source) { if (CurrentZone == source) CancelTransition(false); }
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
                        var confiner = camera.GetComponent<CinemachineConfiner3D>();
                        if (confiner != null && confiner.BoundingVolume == bounds) confiner.BoundingVolume = null;
                    }
                if (brain != null)
                {
                    brain.DefaultBlend = previousBlend; brain.CustomBlends = previousCustomBlends;
                    brain.UpdateMethod = previousUpdate; brain.BlendUpdateMethod = previousBlendUpdate; brain.IgnoreTimeScale = previousIgnoreTimeScale;
                }
                reportedRejections.Clear(); LastRejectionReason = null; LastRejectedZone = null;
                player = null; control = null; flow = null; stage = null; modes = null; brain = null;
                side = corridor = quarter = requestedCamera = committedCamera = null; target = body = null; bounds = null; CurrentZone = null;
                if (cleanupError != null) throw cleanupError;
            }
            finally { unbinding = false; }
        }
        public void Dispose() => Unbind();
        private void OnDisable() => Unbind();
        private void OnDestroy() => Unbind();
    }
}
