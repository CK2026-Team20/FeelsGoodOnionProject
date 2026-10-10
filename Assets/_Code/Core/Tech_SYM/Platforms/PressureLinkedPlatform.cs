using System;
using UnityEngine;
using Cooked.Authoring;

namespace FeelsGoodOnion.TechSYM.Platforms
{
    /// <summary>버튼 시간에 맞춰 전진하고, 지정 속도로 복귀하는 발판.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-400)]
    [RequireComponent(typeof(KinematicPlatformMotion))]
    public sealed class PressureLinkedPlatform : MonoBehaviour
    {
        [Header("연결")]
        [Tooltip("이 발판을 누르는 압력판을 연결합니다. 연결은 여기 한 곳에서만 작성합니다.")]
        [SerializeField, InspectorLabel("압력판")] private PressurePlatePlatform pressurePlate;
        [Header("이동")]
        [Tooltip("복귀할 고정 시작 지점을 연결합니다. 움직이는 몸체 밖에 두며 녹색 경로의 시작입니다.")]
        [SerializeField, InspectorLabel("시작 지점")] private Transform startPoint;
        [Tooltip("눌렸을 때 도달할 고정 지점을 연결합니다. 움직이는 몸체 밖에 두며 녹색 경로의 끝입니다.")]
        [SerializeField, InspectorLabel("목표 지점")] private Transform targetPoint;
        [Tooltip("버튼을 놓았을 때의 복귀 속도(m/s). 버튼 시간과 별개입니다.")]
        [SerializeField, InspectorLabel("복귀 속도"), Min(0.01f)] private float returnSpeed = 1f;
        private KinematicPlatformMotion motion;
        public PressurePlatePlatform Plate => pressurePlate;
        public Vector3 StartPosition => startPoint != null ? startPoint.position : throw new InvalidOperationException("연결 발판의 시작 지점이 없습니다.");
        public Vector3 TargetPosition => targetPoint != null ? targetPoint.position : throw new InvalidOperationException("연결 발판의 목표 지점이 없습니다.");

        private void Awake()
        {
            OnValidate();
            motion = GetComponent<KinematicPlatformMotion>();
        }

        private void Start()
        {
            if (!ValidReferences())
            {
                Debug.LogError("연결 발판에 같은 씬의 압력판과 몸체 밖의 시작/목표 지점을 연결하세요.", this);
                enabled = false;
            }
        }

        private void FixedUpdate()
        {
            if (motion == null || !motion.isActiveAndEnabled || Time.timeScale <= 0f) return;
            if (!ValidReferences()) { Debug.LogError("연결 발판의 압력판/고정 지점이 유효하지 않습니다.", this); enabled = false; return; }
            bool pressed = pressurePlate.isActiveAndEnabled && pressurePlate.Pressed;
            Vector3 destination = pressed ? TargetPosition : StartPosition;
            float length = Vector3.Distance(StartPosition, TargetPosition);
            float speed = MovementSpeed(pressed, length, pressurePlate.TravelSeconds, returnSpeed);
            motion.MoveTo(Vector3.MoveTowards(motion.Position, destination, speed * Time.fixedDeltaTime));
        }

        public static float MovementSpeed(bool pressed, float pathLength, float pressSeconds, float returnSpeed)
        {
            if (!float.IsFinite(pathLength) || pathLength < 0 || !float.IsFinite(pressSeconds) || pressSeconds < .02f
                || !float.IsFinite(returnSpeed) || returnSpeed < .01f) throw new ArgumentOutOfRangeException(nameof(pathLength));
            double value = pressed ? (double)pathLength / pressSeconds : returnSpeed;
            if (value > float.MaxValue) throw new ArgumentOutOfRangeException(nameof(pathLength));
            return (float)value;
        }
        private bool ValidReferences() => pressurePlate != null && pressurePlate.gameObject.scene == gameObject.scene
            && motion != null && motion.IsFixedPoint(startPoint) && motion.IsFixedPoint(targetPoint);

        public void ResetForRetry()
        {
            if (motion != null && ValidReferences()) motion.ResetForRetry(new Pose(StartPosition, startPoint.rotation));
            else motion?.ResetForRetry();
        }
        private void OnDisable() => motion?.CancelMotion();

        private void OnValidate()
        {
            if (!float.IsFinite(returnSpeed) || returnSpeed < 0.01f) returnSpeed = 0.01f;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.green;
            if (startPoint == null || targetPoint == null) return;
            Gizmos.DrawLine(StartPosition, TargetPosition);
            Gizmos.DrawWireCube(TargetPosition, Vector3.one * .15f);
        }
    }
}
