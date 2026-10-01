using DG.Tweening;
using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Platforms
{
    /// <summary>시작 높이와 목표 Y 사이를 반복 이동한다. 일반 오브젝트에도 사용할 수 있다.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-350)]
    public sealed class FloatingMotion : MonoBehaviour
    {
        [Header("Float")]
        [Tooltip("도착할 월드 Y 좌표. 시작 높이는 활성화 시점의 위치입니다.")]
        [SerializeField] private float targetY = 1.5f;
        [Tooltip("목표 높이까지 편도 이동 시간(초).")]
        [SerializeField, Min(0.02f)] private float travelSeconds = 1.5f;
        [Tooltip("부유 이동의 속도 곡선. 부드러운 움직임은 InOutSine을 사용합니다.")]
        [SerializeField] private Ease ease = Ease.InOutSine;
        [Tooltip("반복 방식. Yoyo는 시작 높이로 되돌아옵니다. Restart는 시작점으로 즉시 돌아갑니다.")]
        [SerializeField] private LoopType loopType = LoopType.Yoyo;

        private KinematicPlatformMotion motion;
        private Rigidbody body;
        private Vector3 origin;
        private float currentY;
        private Tween floatingTween;

        private void Reset() => targetY = transform.position.y + 0.24f;
        private void Awake()
        {
            motion = GetComponent<KinematicPlatformMotion>();
            body = GetComponent<Rigidbody>();
        }

        private void OnEnable()
        {
            OnValidate();
            if (motion != null)
            {
                if (!motion.RegisterEffect(this)) { enabled = false; return; }
                origin = motion.EffectOrigin;
            }
            else
            {
                if ((body != null && !body.isKinematic) || GetComponent<MovingPlatform>() != null)
                {
                    Debug.LogError("단독 부유는 다른 이동 기능이 없는 키네마틱 Rigidbody 또는 Transform에 사용하세요.", this);
                    enabled = false; return;
                }
                origin = body != null ? body.position : transform.position;
            }
            currentY = origin.y;
            floatingTween = DOTween.To(() => currentY, value => currentY = value, targetY, travelSeconds)
                .SetEase(ease).SetLoops(-1, loopType).SetUpdate(UpdateType.Manual)
                .SetTarget(this).SetRecyclable(false).OnKill(() => floatingTween = null);
            floatingTween.Play();
        }

        private void FixedUpdate()
        {
            if ((motion != null && !motion.isActiveAndEnabled) || floatingTween == null || !floatingTween.IsActive()) return;
            floatingTween.ManualUpdate(Time.fixedDeltaTime, Time.fixedDeltaTime);
            if (motion != null) motion.SetEffectOffset(this, Vector3.up * (currentY - origin.y));
            else
            {
                Vector3 next = new Vector3(origin.x, currentY, origin.z);
                if (body != null) { if (body.isKinematic) body.MovePosition(next); }
                else transform.position = next;
            }
        }

        private void OnDisable()
        {
            Tween owned = floatingTween;
            floatingTween = null;
            if (owned != null && owned.IsActive()) owned.Kill(false);
            if (motion != null) motion.ReleaseEffect(this, true);
        }

        private void OnValidate()
        {
            if (!float.IsFinite(targetY)) targetY = transform.position.y;
            if (!float.IsFinite(travelSeconds) || travelSeconds < 0.02f) travelSeconds = 0.02f;
            // Incremental은 지정한 높이 범위를 벗어나므로 사용하지 않는다.
            if (loopType != LoopType.Yoyo && loopType != LoopType.Restart) loopType = LoopType.Yoyo;
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0.15f);
            Vector3 start = Application.isPlaying ? origin : transform.position;
            Gizmos.DrawLine(start, new Vector3(start.x, targetY, start.z));
        }
    }
}