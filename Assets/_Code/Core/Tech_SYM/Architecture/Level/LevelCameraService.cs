using System;
using Cooked.Contracts;
using Cooked.Session;
using Unity.Cinemachine;
using UnityEngine;
namespace Cooked.Level
{
    public enum CameraMovementMode { Side = 1, Quarter = 3 }
    [Serializable]
    public struct CameraZoneData
    {
        [Tooltip("카메라 구간의 고유 이름입니다. 같은 이름의 구간으로 다시 진입하면 전환을 반복하지 않으므로 서로 다른 구간은 이름을 구분하세요.")]
        public string Id;
        [Tooltip("이 구간의 이동 방식입니다. Side(1)는 월드 X 이동과 Z 정렬, Quarter(3)는 X/Z 이동이며 위치 정렬이 없습니다.")]
        public CameraMovementMode Movement;
        [Tooltip("플레이어 정렬점까지 트리거 자신의 로컬 XYZ 이동량입니다(월드 단위). 이동·회전·크기를 반영한 점을 파란 기즈모 구로 표시합니다. 모두 0이면 트리거 중심입니다. Side는 이 점의 월드 Z에만 정렬하며 Quarter와 초기 위치 복원은 위치를 바꾸지 않습니다.")]
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
        private PlayerMovementModeController modes;
        private PlayerCameraController controller;
        private CinemachineBrain brain;
        private CinemachineCamera side, quarter, requestedCamera;
        private Transform target, body;
        private IDisposable inputLease;
        private bool transitioning, alignmentComplete, viewCommitted, ownsBinding, initialSelection;
        private bool previousIgnoreTimeScale;
        private CinemachineBlendDefinition previousBlend, ownedBlend;
        private bool ownsBlend;
        private bool initialBrainResetPending;
        private float alignmentStartZ, alignmentEndZ, alignmentElapsed;
        private long requestGeneration;
        private PlayerMovementMode requestedMode;
        public bool IsTransitioning => transitioning;
        public bool IsViewReady => viewCommitted && !transitioning && HasBinding && modes.IsInitialized &&
            modes.isActiveAndEnabled && modes.CurrentMode == requestedMode && SelectionComplete;
        public Exception TransitionFailure { get; private set; }
        public string CurrentZoneId { get; private set; }

        /// <summary>Borrow App output, Core cameras and the current Actor; never discover an Actor rig.</summary>
        public void Bind(IPlayerBridgeService bridge, IGameplayControlService gameplayControl,
            PlayerMovementModeController movementModes, PlayerCameraController hmsController,
            CinemachineBrain outputBrain, CinemachineCamera sideCamera, CinemachineCamera quarterCamera,
            Transform cameraTarget, Transform actorBody)
        {
            if (bridge == null) throw new ArgumentNullException(nameof(bridge));
            if (gameplayControl == null) throw new ArgumentNullException(nameof(gameplayControl));
            if (movementModes == null || hmsController == null || outputBrain == null || sideCamera == null ||
                quarterCamera == null || sideCamera == quarterCamera || cameraTarget == null || actorBody == null)
                throw new ArgumentException("Camera binding requires distinct Side/Quarter cameras and explicit Actor references.");
            if (hmsController.gameObject != outputBrain.gameObject || hmsController.GetComponent<Camera>() == null)
                throw new ArgumentException("App HMS controller and Brain must share the output Camera.");
            if (!bridge.HasActor || !CameraZoneRequest.IsFinite(actorBody.position))
                throw new ArgumentException("Camera binding requires a live Actor at a finite position.");
            Dispose();
            if (!hmsController.Bind(movementModes, sideCamera, quarterCamera))
                throw new InvalidOperationException("HMS separated camera binding rejected. Select separated rig before Awake.");
            ownsBinding = true;
            player = bridge;
            control = gameplayControl;
            modes = movementModes; controller = hmsController; brain = outputBrain;
            side = sideCamera; quarter = quarterCamera; target = cameraTarget; body = actorBody;
            side.Follow = side.LookAt = target;
            quarter.Follow = quarter.LookAt = target;
            previousBlend = brain.DefaultBlend;
            previousIgnoreTimeScale = brain.IgnoreTimeScale;
            brain.IgnoreTimeScale = false;
        }
        public static PlayerMovementMode ResolveMode(CameraZoneRequest zone) => zone.Movement switch
        {
            CameraMovementMode.Side => PlayerMovementMode.Side,
            CameraMovementMode.Quarter => PlayerMovementMode.Quarter,
            _ => throw new ArgumentOutOfRangeException(nameof(zone), "Only Side(1) and Quarter(3) are executable.")
        };
        private bool HasBinding => player != null && player.HasActor && modes != null && controller != null &&
            controller.IsBound && controller.isActiveAndEnabled && brain != null && body != null && target != null &&
            side != null && quarter != null;
        private bool SelectionComplete => requestedCamera != null && requestedCamera.isActiveAndEnabled &&
            brain != null && brain.isActiveAndEnabled && ReferenceEquals(brain.ActiveVirtualCamera, requestedCamera) && !brain.IsBlending;
        private bool Paused => Time.timeScale <= 0 || (control != null &&
            (control.State.WorldPaused || control.State.PresentationPaused));

        /// <param name="snap">Initial spatial selection: Cut, preserving restored Position; readiness still waits for Brain and HMS Start.</param>
        public void EnterZone(CameraZoneRequest zone, bool snap = false)
        {
            // Validate before replacing any running request, including duplicate IDs.
            if (string.IsNullOrWhiteSpace(zone.Id)) throw new ArgumentException("Camera zone ID required.");
            if (!CameraZoneRequest.IsFinite(zone.AlignmentPosition))
                throw new ArgumentException($"Camera zone '{zone.Id}' alignment position must contain finite world XYZ values.", nameof(zone));
            if (!CameraZoneRequest.IsFinite(blendSeconds) || blendSeconds < MinimumBlendSeconds)
                throw new InvalidOperationException($"Camera zone '{zone.Id}' requires a finite blend duration of at least {MinimumBlendSeconds} seconds; configured {blendSeconds}.");
            var mode = ResolveMode(zone);
            if (!HasBinding || zone.Id == CurrentZoneId) return;
            long generation = requestGeneration + 1;
            CancelTransition();
            if (generation != requestGeneration || !HasBinding) return;
            CurrentZoneId = zone.Id;
            requestedMode = mode;
            TransitionFailure = null;
            transitioning = true;
            viewCommitted = false;
            alignmentComplete = true;
            initialSelection = snap;
            initialBrainResetPending = snap;
            try
            {
                var lease = control.BlockGameplay("Camera transition");
                // Control observers may unbind or replace the request before Acquire returns its lease.
                if (generation != requestGeneration || !HasBinding) { lease.Dispose(); return; }
                inputLease = lease;
                if (player is PlayerBridgeService movingActor) movingActor.ClearInput();
                ownedBlend = new CinemachineBlendDefinition(snap ? CinemachineBlendDefinition.Styles.Cut :
                    CinemachineBlendDefinition.Styles.EaseInOut, snap ? 0 : blendSeconds);
                brain.DefaultBlend = ownedBlend; ownsBlend = true;
                requestedCamera = requestedMode == PlayerMovementMode.Side ? side : quarter;
                if (!controller.Preview(requestedMode)) throw new InvalidOperationException("HMS camera preview rejected.");
                if (snap) requestedCamera.PreviousStateIsValid = false;
                if (!snap && requestedMode == PlayerMovementMode.Side)
                {
                    alignmentComplete = false;
                    alignmentStartZ = body.position.z; alignmentEndZ = zone.AlignmentPosition.z; alignmentElapsed = 0;
                }
            }
            catch (Exception error)
            {
                if (generation == requestGeneration)
                { TransitionFailure = error; CurrentZoneId = null; CancelTransition(); }
                throw;
            }
        }
        private void LateUpdate()
        {
            if (!HasBinding) { Unbind(); return; }
            // GameFlow keeps its world-pause lease until this initial Cut is ready. This path
            // advances no time or position; Options presentation pause still defers physical commit.
            bool initialSetup = initialSelection && control.State.WorldPaused && !control.State.PresentationPaused;
            if (!transitioning || (Paused && !initialSetup) || !modes.IsInitialized || !modes.isActiveAndEnabled) return;
            long generation = requestGeneration;
            try
            {
                if (initialBrainResetPending)
                {
                    initialBrainResetPending = false;
                    // DefaultBlend=Cut does not interrupt an already running/custom blend.
                    // Reset the borrowed Brain's root selection once after Preview, then let
                    // its normal update select the requested camera before checking readiness.
                    brain.ResetState();
                    return;
                }
                // This owner advances only Side Z. No external tween survives a pause, replacement or unbind.
                if (!alignmentComplete)
                {
                    alignmentElapsed = Mathf.Min(blendSeconds, alignmentElapsed + Time.deltaTime);
                    float t = alignmentElapsed / blendSeconds;
                    var position = body.position;
                    position.z = Mathf.Lerp(alignmentStartZ, alignmentEndZ, .5f - .5f * Mathf.Cos(Mathf.PI * t));
                    body.position = position;
                    if (body.TryGetComponent<Rigidbody>(out var rb)) rb.position = position;
                    alignmentComplete = alignmentElapsed >= blendSeconds;
                }
                if (!alignmentComplete || !SelectionComplete) return;
                bool committed = controller.Commit(requestedMode);
                // ModeChanged listeners can release the Actor or request a newer zone during Commit.
                if (generation != requestGeneration || !HasBinding) return;
                if (!committed) throw new InvalidOperationException("HMS mode commit was rejected.");
                transitioning = false;
                viewCommitted = true;
                ReleaseLease();
            }
            catch (Exception error)
            {
                // Old observers may accept a new request before throwing; only cancel our own generation.
                if (generation == requestGeneration)
                { TransitionFailure = error; CurrentZoneId = null; CancelTransition(); }
                Debug.LogException(error, this);
            }
        }
        private void ReleaseLease()
        {
            var lease = inputLease; inputLease = null; lease?.Dispose();
        }
        private void CancelTransition()
        {
            requestGeneration++;
            if (transitioning && controller != null && controller.IsBound && modes != null)
                controller.Preview(modes.CurrentMode);
            transitioning = false; alignmentComplete = false;
            initialSelection = false;
            initialBrainResetPending = false;
            viewCommitted = false; requestedCamera = null;
            ReleaseLease();
        }
        /// <summary>T9 Stage/Host must call before Actor removal or retry; rebind explicitly after spawning.</summary>
        public void ResetForRetry() => Unbind();
        public void Unbind()
        {
            requestGeneration++;
            transitioning = false; alignmentComplete = false; viewCommitted = false; requestedCamera = null;
            initialSelection = false;
            initialBrainResetPending = false;
            var lease = inputLease; inputLease = null;
            try
            {
                if (ownsBinding && controller != null) controller.Unbind();
                ClearTarget(side); ClearTarget(quarter);
                if (brain != null)
                {
                    if (ownsBlend && brain.DefaultBlend.Equals(ownedBlend)) brain.DefaultBlend = previousBlend;
                    if (!brain.IgnoreTimeScale) brain.IgnoreTimeScale = previousIgnoreTimeScale;
                }
            }
            finally
            {
                ownsBinding = false; ownsBlend = false;
                player = null; control = null; modes = null; controller = null; brain = null;
                side = null; quarter = null; target = null; body = null; CurrentZoneId = null;
                // Finish detach before notifying control observers: failures or rebinds cannot retain old targets.
                lease?.Dispose();
            }
        }
        private void ClearTarget(CinemachineCamera camera)
        {
            if (camera == null || target == null) return;
            if (camera.Follow == target) camera.Follow = null;
            if (camera.LookAt == target) camera.LookAt = null;
        }
        private void OnDisable() => Unbind();
        private void OnValidate()
        {
            if (!CameraZoneRequest.IsFinite(blendSeconds) || blendSeconds < MinimumBlendSeconds)
                blendSeconds = MinimumBlendSeconds;
        }
        public void Dispose()
        { Unbind(); TransitionFailure = null; }
        private void OnDestroy() => Dispose();
    }
}
