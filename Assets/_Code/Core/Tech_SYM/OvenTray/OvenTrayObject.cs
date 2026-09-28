using UnityEngine;
using FeelsGoodOnion.TechSYM.Interaction;
using FeelsGoodOnion.TechSYM.Platforms;

namespace FeelsGoodOnion.TechSYM.OvenTray
{
    public enum OvenTrayState { Closed, Opening, Open, Closing }

    /// <summary>오븐 로컬 좌표의 두 종점 사이를 E 상호작용으로 이동한다.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-400)]
    [RequireComponent(typeof(KinematicPlatformMotion), typeof(InteractionPromptAnchor))]
    public sealed class OvenTrayObject : MonoBehaviour, IInteractable
    {
        [SerializeField] private Vector3 travelOffset = new Vector3(0f, 0f, -3f);
        [SerializeField, Min(0.02f)] private float moveDuration = 0.8f;
        private KinematicPlatformMotion motion;
        private InteractionPromptAnchor anchor;
        private Vector3 closedLocalPosition;
        private float remainingSeconds;
        public OvenTrayState State { get; private set; } = OvenTrayState.Closed;
        public bool IsMoving => State == OvenTrayState.Opening || State == OvenTrayState.Closing;
        public Vector3 ClosedLocalPosition => closedLocalPosition;
        public Vector3 OpenLocalPosition => closedLocalPosition + travelOffset;

        private void Awake()
        {
            motion = GetComponent<KinematicPlatformMotion>();
            anchor = GetComponent<InteractionPromptAnchor>();
            closedLocalPosition = transform.localPosition;
        }

        public bool Interact()
        {
            if (!isActiveAndEnabled || IsMoving || motion == null || !motion.isActiveAndEnabled) return false;
            var next = State == OvenTrayState.Closed ? OvenTrayState.Opening : OvenTrayState.Closing;
            if (!motion.TryTweenTo(Destination(next), moveDuration, CompleteMovement)) return false;
            State = next;
            remainingSeconds = moveDuration;
            anchor.enabled = false;
            return true;
        }

        private Vector3 Destination(OvenTrayState state)
        {
            Vector3 local = state == OvenTrayState.Opening ? OpenLocalPosition : closedLocalPosition;
            return transform.parent != null ? transform.parent.TransformPoint(local) : local;
        }

        private void FixedUpdate()
        {
            if (!IsMoving || motion == null) return;
            if (motion.IsTweening) remainingSeconds = motion.RemainingTweenSeconds;
            else if (motion.isActiveAndEnabled)
                motion.TryTweenTo(Destination(State), Mathf.Max(0.02f, remainingSeconds), CompleteMovement);
        }

        private void CompleteMovement()
        {
            State = State == OvenTrayState.Opening ? OvenTrayState.Open : OvenTrayState.Closed;
            anchor.enabled = isActiveAndEnabled;
        }

        private void OnEnable()
        {
            if (anchor != null) anchor.enabled = !IsMoving;
        }

        private void OnDisable()
        {
            if (motion != null && IsMoving)
            {
                if (motion.IsTweening) remainingSeconds = motion.RemainingTweenSeconds;
                motion.CancelTween();
            }
            if (anchor != null) anchor.enabled = false;
        }

        private void OnValidate()
        {
            if (!float.IsFinite(moveDuration) || moveDuration < 0.02f) moveDuration = 0.8f;
            if (!float.IsFinite(travelOffset.x) || !float.IsFinite(travelOffset.y) || !float.IsFinite(travelOffset.z))
                travelOffset = Vector3.zero;
        }

        private void OnDrawGizmos()
        {
            // 실행 중에도 최초 닫힘 위치를 사용해 도착 위치가 이동을 따라 밀리지 않게 한다.
            Vector3 start = Application.isPlaying && motion != null ? closedLocalPosition : transform.localPosition;
            Vector3 destination = start + travelOffset;
            Matrix4x4 parentMatrix = transform.parent != null ? transform.parent.localToWorldMatrix : Matrix4x4.identity;
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Color previousColor = Gizmos.color;

            Gizmos.color = Color.cyan;
            Gizmos.matrix = parentMatrix;
            Gizmos.DrawLine(start, destination);

            if (TryGetComponent<BoxCollider>(out var surface))
            {
                Gizmos.matrix = parentMatrix * Matrix4x4.TRS(destination, transform.localRotation, transform.localScale);
                Gizmos.DrawWireCube(surface.center, surface.size);
            }

            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
    }
}
