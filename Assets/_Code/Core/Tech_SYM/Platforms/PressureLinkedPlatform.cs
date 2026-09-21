using UnityEngine;
using UnityEngine.Serialization;

namespace FeelsGoodOnion.TechSYM.Platforms
{
    /// <summary>버튼 시간에 맞춰 전진하고, 지정 속도로 복귀하는 발판.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-400)]
    [RequireComponent(typeof(KinematicPlatformMotion))]
    public sealed class PressureLinkedPlatform : MonoBehaviour
    {
        [Header("Connection")]
        [Tooltip("연결할 압력판.")]
        [SerializeField] private PressurePlatePlatform pressurePlate;
        [Header("Movement")]
        [Tooltip("시작점에서 끝점까지의 월드 이동량(m). 방향과 거리를 지정합니다.")]
        [SerializeField] private Vector3 travelOffset = new Vector3(0f, 3f, 0f);
        [Tooltip("버튼을 놓았을 때의 복귀 속도(m/s). 버튼 시간과 별개입니다.")]
        [FormerlySerializedAs("moveSpeed")]
        [SerializeField, Min(0.01f)] private float returnSpeed = 1f;
        private KinematicPlatformMotion motion;
        private Vector3 origin;
        public PressurePlatePlatform Plate => pressurePlate;

        private void Awake()
        {
            OnValidate();
            motion = GetComponent<KinematicPlatformMotion>();
            origin = transform.position;
        }

        private void Start()
        {
            if (pressurePlate == null)
            {
                Debug.LogError("연동 발판에 압력판을 연결하세요.", this);
                enabled = false;
            }
        }

        private void FixedUpdate()
        {
            if (motion == null || !motion.isActiveAndEnabled) return;
            bool pressed = pressurePlate != null && pressurePlate.isActiveAndEnabled && pressurePlate.IsPressed;
            Vector3 destination = pressed ? origin + travelOffset : origin;
            float speed = pressed ? travelOffset.magnitude / pressurePlate.TravelSeconds : returnSpeed;
            motion.MoveTo(Vector3.MoveTowards(motion.Position, destination, speed * Time.fixedDeltaTime));
        }

        private void OnValidate()
        {
            if (!float.IsFinite(returnSpeed) || returnSpeed < 0.01f) returnSpeed = 0.01f;
            if (!float.IsFinite(travelOffset.x) || !float.IsFinite(travelOffset.y) || !float.IsFinite(travelOffset.z))
                travelOffset = Vector3.zero;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            Vector3 start = Application.isPlaying ? origin : transform.position;
            Gizmos.DrawLine(start, start + travelOffset);
            Gizmos.DrawWireCube(start + travelOffset, transform.lossyScale);
        }
    }
}
