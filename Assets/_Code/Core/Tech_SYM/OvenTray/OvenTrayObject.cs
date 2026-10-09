using System;
using UnityEngine;
using FeelsGoodOnion.TechSYM.Interaction;
using FeelsGoodOnion.TechSYM.Platforms;
using Cooked.Authoring;

namespace FeelsGoodOnion.TechSYM.OvenTray
{
    public enum OvenTrayState { Closed, Opening, Open, Closing }
    /// <summary>문·트레이·서랍 각각의 독립 두 종점과 자기 작동 수명을 소유한다.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-400)]
    [RequireComponent(typeof(KinematicPlatformMotion), typeof(InteractionPromptAnchor))]
    public sealed class OvenTrayObject : MonoBehaviour, IInteractable
    {
        public enum OperationMode { [InspectorName("이동")] Move = 0, [InspectorName("힌지 회전")] Hinge = 1 }
        public enum OperationOutcome { Completed, Cancelled }
        [Tooltip("장치의 작동 방식을 선택합니다. 트레이·서랍은 이동, 문은 실제 몸체 원점을 경첩으로 회전합니다.")]
        [SerializeField, InspectorLabel("작동 방식")] private OperationMode operationMode;
        [Tooltip("닫힘 위치와 회전 지점을 연결합니다. 움직이는 몸체 밖에 두며 청록 경로의 시작입니다.")]
        [SerializeField, InspectorLabel("닫힘 자세")] private Transform closedPose;
        [Tooltip("열림 위치와 회전 지점을 연결합니다. 몸체 밖에 두며 힌지는 닫힘과 같은 경첩 위치를 사용합니다.")]
        [SerializeField, InspectorLabel("열림 자세")] private Transform openPose;
        [Tooltip("한 번 열거나 닫는 시간(초, 최소 0.02)입니다. 각 장치가 독립 설정을 가지며 작동 중 추가 F는 거절합니다.")]
        [SerializeField, InspectorLabel("작동 시간"), Min(0.02f)] private float moveDuration = 0.8f;
        private KinematicPlatformMotion motion;
        private InteractionPromptAnchor anchor;
        private OvenTrayState acceptedFrom;
        private bool requestPending;
        private bool anchorDefault;
        private bool resetting;
        public event Action Accepted;
        public event Action<OperationOutcome> OperationEnded;
        public OvenTrayState State { get; private set; } = OvenTrayState.Closed;
        public bool IsMoving => State == OvenTrayState.Opening || State == OvenTrayState.Closing;
        public Pose ClosedPose => ResolvePose(closedPose, "닫힘");
        public Pose OpenPose => ResolvePose(openPose, "열림");
        private void Awake()
        {
            OnValidate();
            motion = GetComponent<KinematicPlatformMotion>(); anchor = GetComponent<InteractionPromptAnchor>();
            var body = motion.PhysicsBody;
            anchorDefault = anchor != null && anchor.enabled;
            if (body == null || !ValidReferences())
            { Debug.LogError("장치의 고정 닫힘/열림 자세와 몸체 아래 F 안내/조작 지점을 연결하세요.", this); enabled = false; }
        }
        public bool Interact()
        {
            if (resetting || !isActiveAndEnabled || Time.timeScale <= 0f || IsMoving || requestPending || motion == null || !motion.isActiveAndEnabled || !ValidReferences()) return false;
            var next = State == OvenTrayState.Closed ? OvenTrayState.Opening : OvenTrayState.Closing;
            bool accepted = motion.TryTweenPose(next == OvenTrayState.Opening ? OpenPose : ClosedPose, moveDuration, CompleteMovement);
            if (!accepted) return false;
            acceptedFrom = State; State = next; requestPending = true;
            if (anchor != null) anchor.enabled = false;
            Notify(Accepted);
            return true;
        }
        private static Pose ResolvePose(Transform point, string field)
        {
            if (point == null) throw new InvalidOperationException($"장치의 {field} 자세를 연결하세요.");
            return new Pose(point.position, point.rotation);
        }
        private bool ValidReferences()
        {
            if (motion == null || !motion.IsFixedPoint(closedPose) || !motion.IsFixedPoint(openPose) || anchor == null
                || anchor.PromptPoint == null || anchor.InteractionPoint == null
                || !anchor.PromptPoint.IsChildOf(motion.PhysicsBody.transform)
                || !anchor.InteractionPoint.IsChildOf(motion.PhysicsBody.transform)) return false;
            return operationMode == OperationMode.Move || (operationMode == OperationMode.Hinge
                && (openPose.position - closedPose.position).sqrMagnitude <= .000001f);
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
            resetting = true;
            try
            {
                CancelOperation();
                if (motion != null && closedPose != null) motion.ResetForRetry(ClosedPose);
                State = OvenTrayState.Closed;
                if (anchor != null) anchor.enabled = isActiveAndEnabled && anchorDefault;
            }
            finally { resetting = false; }
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
        }
        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;
            if (closedPose == null || openPose == null) return;
            Gizmos.DrawLine(closedPose.position, openPose.position);
            Gizmos.DrawRay(closedPose.position, closedPose.forward * .3f);
            Gizmos.DrawRay(openPose.position, openPose.forward * .3f);
        }
    }
}
