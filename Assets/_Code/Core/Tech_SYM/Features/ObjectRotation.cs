using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

namespace FeelsGoodOnion.TechSYM.Features
{
    /// <summary>자신 또는 지정한 자식을 제자리에서 월드 Y축 양의 방향으로 등속 회전한다.</summary>
    [DisallowMultipleComponent]
    public sealed class ObjectRotation : MonoBehaviour
    {
        [FormerlySerializedAs("visual")]
        [Tooltip("Collider/Rigidbody 없는 회전 Visual 자식입니다. 비워 두면 이 컴포넌트를 붙인 표시 객체만 회전합니다. 물리 루트에는 사용하지 않습니다.")]
        [SerializeField] private Transform rotationTarget;
        [Tooltip("Y축 양의 방향 회전 속도(도/초). 다음 활성화 시 적용됩니다.")]
        [SerializeField, Min(0.01f)] private float degreesPerSecond = 90f;
        private Tween rotation;
        private Transform target;
        private Quaternion authoredRotation;
        private void Awake()
        {
            target = rotationTarget != null ? rotationTarget : transform;
            authoredRotation = target.localRotation;
        }
        public void ResetForRetry()
        {
            OnDisable();
            if (target != null) target.localRotation = authoredRotation;
            if (isActiveAndEnabled) OnEnable();
        }

        private void OnEnable()
        {
            OnValidate();
            target = rotationTarget != null ? rotationTarget : transform;
            if (target.GetComponentsInChildren<Collider>(true).Length != 0 || target.GetComponent<Rigidbody>() != null
                || (target != transform && !target.IsChildOf(transform)))
            {
                Debug.LogError("회전 대상은 Collider/Rigidbody가 없는 자기 Visual이어야 합니다.", this);
                enabled = false;
                return;
            }
            rotation = target.DORotate(target.eulerAngles + Vector3.up * 360f,
                    360f / degreesPerSecond, RotateMode.FastBeyond360)
                .SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart)
                .SetLink(gameObject, LinkBehaviour.KillOnDisable);
        }

        private void OnDisable()
        {
            rotation?.Kill();
            rotation = null;
        }

        private void OnValidate()
        {
            if (!float.IsFinite(degreesPerSecond) || degreesPerSecond < 0.01f)
                degreesPerSecond = 90f;
        }
    }
}
