using System;
using Cooked.Contracts;
using Cooked.Session;
using DG.Tweening;
using UnityEngine;
namespace Cooked.Level
{
    public enum CameraMovementMode { Side = 1, Corridor = 2, Quarter = 3 }
    [Serializable]
    public struct CameraZoneData
    {
        [Tooltip("카메라 구간의 고유 이름입니다. 같은 이름의 구간으로 다시 진입하면 전환을 반복하지 않으므로 서로 다른 구간은 이름을 구분하세요.")]
        public string Id;
        [Tooltip("이 구간의 이동 방식입니다. Side는 월드 X로 이동하며 정렬 위치의 월드 Z에 맞춥니다. Corridor는 월드 Z로 이동하며 월드 X에 맞춥니다. Quarter는 X/Z로 이동하며 위치를 정렬하지 않습니다.")]
        public CameraMovementMode Movement;
        [Tooltip("플레이어 정렬 위치를 트리거 또는 체크포인트 자신의 로컬 XYZ 이동량으로 설정합니다. 소유 오브젝트의 이동·회전·크기를 반영한 월드 위치가 트리거의 파란 기즈모 구로 표시됩니다. Side는 그 위치의 월드 Z, Corridor는 월드 X에 플레이어를 맞추고 Y는 바꾸지 않습니다. Quarter와 초기 즉시 적용은 위치를 정렬하지 않습니다.")]
        public Vector3 AlignmentOffset;
        public CameraZoneRequest Resolve(Transform owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            return new CameraZoneRequest(Id, Movement, owner.TransformPoint(AlignmentOffset));
        }
    }
    public readonly struct CameraZoneRequest
    {
        public string Id { get; }
        public CameraMovementMode Movement { get; }
        public Vector3 AlignmentPosition { get; }
        public CameraZoneRequest(string id, CameraMovementMode movement, Vector3 alignmentPosition)
        { Id = id; Movement = movement; AlignmentPosition = alignmentPosition; }
        internal static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        internal static bool IsFinite(Vector3 value) => IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
    }
    [DefaultExecutionOrder(1000)]
    public sealed class LevelCameraService : MonoBehaviour, IDisposable
    {
        private const float MinimumBlendSeconds = .01f;
        [Tooltip("Cinemachine 블렌드와 플레이어 평면 정렬 시간입니다(초, 최소 0.01). 실제 두 동작이 끝날 때까지 입력을 잠급니다. 즉시 적용은 Cut으로 전환합니다.")]
        [SerializeField, Min(MinimumBlendSeconds)] private float blendSeconds = .6f;
        private IPlayerBridgeService player;
        private IGameplayControlService control;
        private ActorCameraRig rig;
        private IDisposable inputLease;
        private Tween alignment;
        private bool transitioning, alignmentComplete;
        private PlayerMovementMode requestedMode;
        public bool IsTransitioning => transitioning;
        public ActorCameraRig Rig => rig;
        public Exception TransitionFailure { get; private set; }
        public string CurrentZoneId { get; private set; }
        public void Bind(IPlayerBridgeService bridge) => Bind(bridge, null);
        public void Bind(IPlayerBridgeService bridge, IGameplayControlService gameplayControl)
            => Bind(bridge, gameplayControl, bridge is PlayerBridgeService actor && actor.BodyTransform != null ?
                actor.BodyTransform.root.GetComponentInChildren<ActorCameraRig>(true) : null);
        public void Bind(IPlayerBridgeService bridge, IGameplayControlService gameplayControl, ActorCameraRig actorRig)
        {
            Dispose();
            player = bridge ?? throw new ArgumentNullException(nameof(bridge));
            control = gameplayControl;
            rig = actorRig != null ? actorRig : throw new ArgumentNullException(nameof(actorRig));
            rig.ValidateConfiguration();
        }
        public static PlayerMovementMode ResolveMode(CameraZoneRequest zone) => zone.Movement switch
        {
            CameraMovementMode.Side => PlayerMovementMode.Side,
            CameraMovementMode.Corridor => PlayerMovementMode.BackFixed,
            CameraMovementMode.Quarter => PlayerMovementMode.Quarter,
            _ => throw new ArgumentOutOfRangeException(nameof(zone))
        };
        public void EnterZone(CameraZoneRequest zone, bool snap = false)
        {
            if (player == null || !player.HasActor || rig == null || zone.Id == CurrentZoneId) return;
            if (string.IsNullOrWhiteSpace(zone.Id)) throw new ArgumentException("Camera zone ID required.");
            if (!CameraZoneRequest.IsFinite(zone.AlignmentPosition))
                throw new ArgumentException($"Camera zone '{zone.Id}' alignment position must contain finite world XYZ values.", nameof(zone));
            if (!CameraZoneRequest.IsFinite(blendSeconds) || blendSeconds < MinimumBlendSeconds)
                throw new InvalidOperationException($"Camera zone '{zone.Id}' requires a finite blend duration of at least {MinimumBlendSeconds} seconds; configured {blendSeconds}.");
            if (!rig.IsReady) throw new InvalidOperationException("HMS mode must initialize before entering a camera zone.");
            var mode = ResolveMode(zone);
            CancelTransition();
            CurrentZoneId = zone.Id;
            requestedMode = mode;
            TransitionFailure = null;
            inputLease = control?.BlockGameplay("Camera transition");
            if (player is PlayerBridgeService movingActor) movingActor.ClearInput();
            transitioning = true;
            alignmentComplete = true;
            try
            {
                rig.SelectCamera(requestedMode, snap, blendSeconds);
                // Initial checkpoint mode is committed while the output is still hidden.
                // Normal transitions keep HMS axes unchanged until both Brain and alignment finish.
                if (snap)
                {
                    if (!rig.CommitMode(requestedMode)) throw new InvalidOperationException("Initial HMS mode was rejected.");
                    return;
                }
                if ((requestedMode == PlayerMovementMode.Side || requestedMode == PlayerMovementMode.BackFixed) &&
                    player is PlayerBridgeService physicalActor)
                {
                    var body = physicalActor.BodyTransform;
                    bool sidePlane = requestedMode == PlayerMovementMode.Side;
                    alignmentComplete = false;
                    alignment = DOTween.To(() => sidePlane ? body.position.z : body.position.x, value =>
                    {
                        if (body == null) return;
                        var position = body.position;
                        if (sidePlane) position.z = value; else position.x = value;
                        body.position = position;
                        if (body.TryGetComponent<Rigidbody>(out var rb)) rb.position = position;
                    }, sidePlane ? zone.AlignmentPosition.z : zone.AlignmentPosition.x, blendSeconds)
                        .SetUpdate(UpdateType.Late, false).SetRecyclable(false).SetEase(Ease.InOutSine)
                        .OnComplete(() => { alignment = null; alignmentComplete = true; });
                }
            }
            catch { CancelTransition(); CurrentZoneId = null; throw; }
        }
        private void LateUpdate()
        {
            if (player == null || !player.HasActor || rig == null)
            { CancelTransition(); CurrentZoneId = null; return; }
            if (!transitioning || !alignmentComplete || !rig.SelectionComplete ||
                Time.timeScale <= 0 || (control != null && control.State.WorldPaused)) return;
            try
            {
                if (!rig.CommitMode(requestedMode)) throw new InvalidOperationException("HMS mode commit was rejected.");
                if (player is PlayerBridgeService actor) actor.SetInputBasis(Vector3.right, Vector3.forward);
                ReleaseLease();
                transitioning = false;
            }
            catch (Exception error)
            {
                TransitionFailure = error;
                CancelTransition(); CurrentZoneId = null;
                Debug.LogException(error, this);
            }
        }
        private void ReleaseLease()
        {
            var lease = inputLease; inputLease = null; lease?.Dispose();
        }
        private void CancelTransition()
        {
            alignment?.Kill(false); alignment = null;
            if (transitioning && rig != null) rig.CancelSelection();
            transitioning = false; alignmentComplete = false;
            ReleaseLease();
        }
        private void OnDisable() { CancelTransition(); CurrentZoneId = null; }
        private void OnValidate()
        {
            if (!CameraZoneRequest.IsFinite(blendSeconds) || blendSeconds < MinimumBlendSeconds)
                blendSeconds = MinimumBlendSeconds;
        }
        public void Dispose()
        { CancelTransition(); player = null; control = null; rig = null; CurrentZoneId = null; TransitionFailure = null; }
        private void OnDestroy() => Dispose();
    }
}
