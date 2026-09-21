using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Platforms
{
    /// <summary>버튼 진행률에 맞춰 움직이는 연동 발판.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-400)]
    [RequireComponent(typeof(KinematicPlatformMotion))]
    public sealed class PressureLinkedPlatform : MonoBehaviour
    {
        [Header("Connection")]
        [Tooltip("연결할 압력판.")]
        [SerializeField] private PressurePlatePlatform pressurePlate;
        [Header("Movement")]
        [Tooltip("버튼을 끝까지 눌렀을 때의 월드 이동량(m).")]
        [SerializeField] private Vector3 travelOffset = new Vector3(0f, 3f, 0f);
        private KinematicPlatformMotion motion;
        private Vector3 origin;
        public PressurePlatePlatform Plate => pressurePlate;

        private void Awake()
        {
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
            if (motion == null) return;
            float progress = pressurePlate != null && pressurePlate.isActiveAndEnabled ? pressurePlate.Progress : 0f;
            motion.MoveTo(origin + travelOffset * progress);
        }

        private void OnValidate()
        {
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
