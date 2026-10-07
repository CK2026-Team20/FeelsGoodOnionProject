using System;
using UnityEngine;
using FeelsGoodOnion.TechSYM.Interaction;
using FeelsGoodOnion.TechSYM.Platforms;

namespace FeelsGoodOnion.TechSYM.OvenTray
{
    public enum OvenTrayState { Closed, Opening, Open, Closing }
    /// <summary>문·트레이·서랍 각각의 독립 두 종점과 자기 작동 수명을 소유한다.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-400)]
    [RequireComponent(typeof(KinematicPlatformMotion), typeof(InteractionPromptAnchor))]
    public sealed class OvenTrayObject : MonoBehaviour, IInteractable
    {
        public enum OperationMode { Move, Hinge }
        public enum OperationOutcome { Completed, Cancelled }
        [Tooltip("이 장치의 작동 방식입니다. 트레이·서랍은 Move, 문은 PhysicsBody 원점을 축으로 Hinge 회전을 사용합니다.")]
        [SerializeField] private OperationMode operationMode;
        [Tooltip("닫힘에서 열림까지의 부모 로컬 XYZ 이동량(m)입니다. 부모 회전·각 축 배율을 반영합니다. 0이면 제자리입니다. 청록 기즈모 경로입니다.")]
        [SerializeField] private Vector3 travelOffset = new Vector3(0f, 0f, -3f);
        [Tooltip("힌지 닫힘에서 열림까지의 물리 몸체 로컬 XYZ 회전량(도)입니다. 0이면 회전하지 않습니다. PhysicsBody 원점을 실제 경첩에 놓으세요.")]
        [SerializeField] private Vector3 hingeEulerOffset = new Vector3(0f, 90f, 0f);
        [Tooltip("한 번 열거나 닫는 시간(초, 최소 0.02)입니다. 각 장치가 독립 설정을 가지며 작동 중 추가 F는 거절합니다.")]
        [SerializeField, Min(0.02f)] private float moveDuration = 0.8f;
        private KinematicPlatformMotion motion;
        private InteractionPromptAnchor anchor;
        private Vector3 closedLocalPosition;
        private Quaternion closedLocalRotation;
        private Transform bodyParent;
        private OvenTrayState acceptedFrom;
        private bool requestPending;
        private bool anchorDefault;
        public event Action Accepted;
        public event Action<OperationOutcome> OperationEnded;
        public OvenTrayState State { get; private set; } = OvenTrayState.Closed;
        public bool IsMoving => State == OvenTrayState.Opening || State == OvenTrayState.Closing;
        public Vector3 ClosedLocalPosition => closedLocalPosition;
        public Vector3 OpenLocalPosition => closedLocalPosition + travelOffset;
        private void Awake()
        {
            OnValidate();
            motion = GetComponent<KinematicPlatformMotion>(); anchor = GetComponent<InteractionPromptAnchor>();
            var body = motion.PhysicsBody;
            if (body == null) { Debug.LogError("장치 PhysicsBody를 연결하세요.", this); enabled = false; return; }
            bodyParent = body.transform.parent;
            closedLocalPosition = body.transform.localPosition; closedLocalRotation = body.transform.localRotation;
            anchorDefault = anchor != null && anchor.enabled;
        }
        public bool Interact()
        {
            if (!isActiveAndEnabled || IsMoving || requestPending || motion == null || !motion.isActiveAndEnabled) return false;
            var next = State == OvenTrayState.Closed ? OvenTrayState.Opening : OvenTrayState.Closing;
            bool accepted = operationMode == OperationMode.Hinge
                ? motion.TryRotateTo(RotationDestination(next), moveDuration, CompleteMovement)
                : motion.TryTweenTo(Destination(next), moveDuration, CompleteMovement);
            if (!accepted) return false;
            acceptedFrom = State; State = next; requestPending = true;
            if (anchor != null) anchor.enabled = false;
            Notify(Accepted);
            return true;
        }
        private Vector3 Destination(OvenTrayState state)
        {
            Vector3 local = state == OvenTrayState.Opening ? OpenLocalPosition : closedLocalPosition;
            return bodyParent != null ? bodyParent.TransformPoint(local) : local;
        }
        private Quaternion RotationDestination(OvenTrayState state)
        {
            Quaternion local = state == OvenTrayState.Opening ? closedLocalRotation * Quaternion.Euler(hingeEulerOffset) : closedLocalRotation;
            return bodyParent != null ? bodyParent.rotation * local : local;
        }
        private void CompleteMovement()
        {
            if (!requestPending) return;
            requestPending = false; State = State == OvenTrayState.Opening ? OvenTrayState.Open : OvenTrayState.Closed;
            if (anchor != null) anchor.enabled = isActiveAndEnabled && anchorDefault;
            Notify(OperationOutcome.Completed);
        }
        // If only the physical mover is disabled, end this accepted request rather than restart it later.
        private void FixedUpdate() { if (requestPending && (motion == null || !motion.isActiveAndEnabled || !motion.IsTweening)) CancelOperation(); }
        private void OnEnable() { if (anchor != null) anchor.enabled = !IsMoving && anchorDefault; }
        public void CancelOperation()
        {
            if (!requestPending) return;
            motion?.CancelTween();
            requestPending = false; State = acceptedFrom;
            if (anchor != null) anchor.enabled = isActiveAndEnabled && anchorDefault;
            Notify(OperationOutcome.Cancelled);
        }
        public void ResetForRetry()
        {
            CancelOperation(); motion?.ResetForRetry(); State = OvenTrayState.Closed;
            if (anchor != null) anchor.enabled = isActiveAndEnabled && anchorDefault;
        }
        private void OnDisable() { CancelOperation(); if (anchor != null) anchor.enabled = false; }
        // One failing observer must not prevent the remaining lifecycle consumers.
        private void Notify(Action handlers)
        { if (handlers != null) foreach (Action h in handlers.GetInvocationList()) try { h(); } catch (Exception e) { Debug.LogException(e); } }
        private void Notify(OperationOutcome result)
        { var handlers = OperationEnded; if (handlers != null) foreach (Action<OperationOutcome> h in handlers.GetInvocationList()) try { h(result); } catch (Exception e) { Debug.LogException(e); } }
        private void OnValidate()
        {
            if (!float.IsFinite(moveDuration) || moveDuration < 0.02f) moveDuration = 0.8f;
            if (!Finite(travelOffset)) travelOffset = Vector3.zero;
            if (!Finite(hingeEulerOffset)) hingeEulerOffset = Vector3.zero;
        }
        private static bool Finite(Vector3 v) => float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z);
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            Vector3 start = Application.isPlaying && motion != null ? closedLocalPosition : transform.localPosition;
            var parent = Application.isPlaying && motion != null ? bodyParent : transform.parent;
            Gizmos.DrawLine(parent != null ? parent.TransformPoint(start) : start,
                parent != null ? parent.TransformPoint(start + travelOffset) : start + travelOffset);
        }
    }
}
