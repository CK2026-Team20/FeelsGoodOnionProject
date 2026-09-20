using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;

namespace FeelsGoodOnion.TechSYM.Features
{
    /// <summary>
    /// 무거운 플레이어가 밟으면 붕괴하는 플랫폼.
    /// 설정에 따라 재생성하거나 파괴한다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ShatteredPlatform : MonoBehaviour, IBrokenable
    {
        /// <summary>플랫폼 붕괴 진행 단계.</summary>
        public enum CollapsePhase { Ready, Wiggling, Collapsed, Regenerating, DestroyScheduled }

        [Header("References")]
        [Tooltip("콜라이더가 없는 렌더 오브젝트")]
        [SerializeField] private Transform visual;
        [Tooltip("오브젝트 충돌 검증 콜라이더")]
        [SerializeField] private Collider supportCollider;

        [Header("Collapse")]
        [FormerlySerializedAs("wiggleDuration")]
        [Tooltip("플랫폼 붕괴 경고 트윈 시간")]
        [SerializeField, Min(0.01f)] private float warningEndSeconds = 1f;
        [Tooltip("플랫폼 붕괴 트윈 지정 시간")]
        [SerializeField, Min(0.01f)] private float collapseEndSeconds = 1.25f;

        [Header("Shake")]
        [FormerlySerializedAs("wiggleAngle")]
        [Tooltip("회전강도. 0이면 흔들림 없음.")]
        [SerializeField, Min(0f)] private float shakeAngleDegrees = 4f;
        [FormerlySerializedAs("wiggleFrequency")]
        [Tooltip("초당 흔들림 횟수.")]
        [SerializeField, Min(0.1f)] private float shakeFrequency = 8f;

        [Header("Regeneration")]
        [Tooltip("재생성 가능 여부. 동적으로 작동하지 않습니다.")]
        [FormerlySerializedAs("canRegenerate")]
        [SerializeField] private bool canRegenerate = true;
        [Tooltip("붕괴 유지 시간.")]
        [SerializeField, Min(0f)] private float collapsedHoldSeconds = 0.5f;
        [FormerlySerializedAs("regenerationDuration")]
        [Tooltip("재생성 까지 걸리는 시간.")]
        [SerializeField, Min(0.01f)] private float regenerationSeconds = 3f;
        [FormerlySerializedAs("blinkInterval")]
        [Tooltip("Blink Tween 간격")]
        [SerializeField, Min(0.01f)] private float blinkIntervalSeconds = 0.15f;

        /// <summary>현재 진행 단계.</summary>
        public CollapsePhase Phase { get; private set; } = CollapsePhase.Ready;
        /// <summary>붕괴 시작부터 발판이 사라질 때까지의 시간(초).</summary>
        public float CollapseEndSeconds => Mathf.Max(warningEndSeconds, collapseEndSeconds);
        /// <summary>재생성 가능 여부.</summary>
        public bool CanRegenerate => canRegenerate;
        /// <summary>붕괴 시작부터 재생성 완료 또는 파괴 예약까지의 시간(초).</summary>
        public float CycleDurationSeconds => CollapseEndSeconds + (canRegenerate ? collapsedHoldSeconds + regenerationSeconds : 0f);

        private PlatformPresentation presentation;
        private Sequence cycle;
        private bool supportWasEnabled;
        private const float MinimumSupportDot = 0.5f;

        /// <summary>같은 오브젝트의 콜라이더 연결.</summary>
        private void Reset() => supportCollider = GetComponent<BoxCollider>();

        /// <summary>잘못된 설정값 보정.</summary>
        private void OnValidate()
        {
            warningEndSeconds = Valid(warningEndSeconds, 0.01f, 1f);
            collapseEndSeconds = Valid(collapseEndSeconds, warningEndSeconds, warningEndSeconds);
            shakeAngleDegrees = Valid(shakeAngleDegrees, 0f, 4f);
            shakeFrequency = Valid(shakeFrequency, 0.1f, 8f);
            collapsedHoldSeconds = Valid(collapsedHoldSeconds, 0f, 0.5f);
            regenerationSeconds = Valid(regenerationSeconds, 0.01f, 3f);
            blinkIntervalSeconds = Valid(blinkIntervalSeconds, 0.01f, 0.15f);
        }

        private static float Valid(float value, float minimum, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? Mathf.Max(minimum, fallback) : Mathf.Max(minimum, value);

        /// <summary>렌더 오브젝트와 콜라이더 확인 및 초기화.</summary>
        private void Awake()
        {
            OnValidate();
            if (supportCollider == null) supportCollider = GetComponent<BoxCollider>();
            if (visual == null || visual == transform || !visual.IsChildOf(transform)
                || !visual.gameObject.activeInHierarchy || supportCollider == null
                || supportCollider.gameObject != gameObject || supportCollider.isTrigger
                || !(supportCollider is BoxCollider)
                || visual.GetComponentsInChildren<Collider>(true).Length != 0)
            {
                Debug.LogError("붕괴 플랫폼: 활성 자식 표시 객체(Collider 없음)와 같은 객체의 비 Trigger BoxCollider가 필요합니다.", this);
                enabled = false;
                return;
            }
            presentation = new PlatformPresentation(visual);
        }

        private void OnCollisionEnter(Collision collision) => TryCollapseFromContact(collision);
        private void OnCollisionStay(Collision collision) => TryCollapseFromContact(collision);

        /// <summary>
        /// 현재 플랫폼을 밟은 플레이어의 HeavyState를 확인한다.
        /// 컴포넌트가 있으면 붕괴를 시작한다.
        /// </summary>
        private void TryCollapseFromContact(Collision collision)
        {
            if (!isActiveAndEnabled || Phase != CollapsePhase.Ready || presentation == null
                || collision == null || supportCollider == null || !supportCollider.enabled) return;
            Collider other = collision.collider;
            if (other == null || !other.enabled || other.isTrigger) return;
            GameObject actor = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
            if (actor == null || !actor.TryGetComponent<HeavyState>(out var heavy) || heavy == null) return;
            for (int i = 0; i < collision.contactCount; i++)
            {
                ContactPoint contact = collision.GetContact(i);
                // 접촉 방향을 반대로 계산해 윗면 충돌을 확인한다.
                if (contact.thisCollider == supportCollider
                    && Vector3.Dot(-contact.normal, transform.up) >= MinimumSupportDot)
                {
                    Collapses();
                    return;
                }
            }
        }

        /// <summary>
        /// 붕괴 트윈 시작. 중복 요청은 무시한다.
        /// 시작 후 플레이어가 떠나도 계속 진행한다.
        /// </summary>
        public void Collapses()
        {
            if (!isActiveAndEnabled || presentation == null || !presentation.IsValid
                || supportCollider == null || !supportCollider.enabled || Phase != CollapsePhase.Ready) return;
            OnValidate();
            supportWasEnabled = supportCollider.enabled;
            Phase = CollapsePhase.Wiggling;
            cycle = DOTween.Sequence();
            cycle.SetTarget(this).SetLink(gameObject, LinkBehaviour.KillOnDisable)
                .SetUpdate(UpdateType.Fixed, false).SetEase(Ease.Linear).SetAutoKill(true).SetRecyclable(false);
            cycle.Append(presentation.Shake(warningEndSeconds, shakeAngleDegrees, shakeFrequency));
            cycle.AppendInterval(CollapseEndSeconds - warningEndSeconds);
            cycle.AppendCallback(CompleteCollapse);
            // 재생성 여부는 트윈 시작 시 결정한다.
            if (canRegenerate)
            {
                cycle.AppendInterval(collapsedHoldSeconds);
                cycle.AppendCallback(BeginRegeneration);
                cycle.Append(presentation.Blink(regenerationSeconds, blinkIntervalSeconds));
                cycle.OnComplete(Restore);
            }
            else
            {
                cycle.OnComplete(DestroyPlatform);
            }
            cycle.OnUpdate(ValidateRunningReferences);
            cycle.OnKill(() => { cycle = null; Restore(); });
            cycle.Play();
        }

        /// <summary>붕괴 완료. 콜라이더를 끄고 플랫폼을 숨긴다.</summary>
        private void CompleteCollapse()
        {
            if (supportCollider != null) supportCollider.enabled = false;
            presentation?.SetVisible(false);
            Phase = CollapsePhase.Collapsed;
        }

        /// <summary>재생성 시작. 콜라이더는 꺼진 상태로 유지한다.</summary>
        private void BeginRegeneration()
        {
            Phase = CollapsePhase.Regenerating;
            presentation?.SetVisible(true);
        }

        /// <summary>1회성 플랫폼 파괴 예약.</summary>
        private void DestroyPlatform()
        {
            Phase = CollapsePhase.DestroyScheduled;
            Destroy(gameObject);
        }

        /// <summary>필수 오브젝트가 사라지면 트윈을 취소한다.</summary>
        private void ValidateRunningReferences()
        {
            if (supportCollider != null && presentation != null && presentation.IsValid) return;
            Debug.LogError("붕괴 플랫폼 실행 중 필수 참조가 제거되어 연출을 취소합니다.", this);
            CancelCycle();
            enabled = false;
        }

        /// <summary>플랫폼 표시와 콜라이더 복원. 파괴 예정이면 제외한다.</summary>
        private void Restore()
        {
            if (Phase == CollapsePhase.DestroyScheduled) return;
            presentation?.Restore();
            if (Phase != CollapsePhase.Ready && supportCollider != null) supportCollider.enabled = supportWasEnabled;
            Phase = CollapsePhase.Ready;
        }

        private void CancelCycle()
        {
            Sequence owned = cycle;
            cycle = null;
            if (owned != null && owned.IsActive()) owned.Kill(false);
            Restore();
        }

        /// <summary>비활성화 시 트윈 취소.</summary>
        private void OnDisable() => CancelCycle();
        private void OnDestroy() => CancelCycle();
    }
}
