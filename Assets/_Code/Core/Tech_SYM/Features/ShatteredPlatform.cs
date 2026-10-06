using System.Collections;
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
        public enum CollapsePhase { Ready, Wiggling, Collapsed, Regenerating, DestroyScheduled, Animating }

        [Header("References")]
        [Tooltip("붕괴 모습을 표시할 자식 오브젝트입니다. 자신과 하위에 Collider를 두지 말고, 실제 붕괴 Animator는 이 오브젝트에 연결하세요.")]
        [SerializeField] private Transform visual;
        [Tooltip("플레이어를 받치는 이 오브젝트의 BoxCollider입니다. Trigger를 끄세요. 경고가 끝나면 꺼져서 플레이어가 떨어집니다.")]
        [SerializeField] private Collider supportCollider;
        [Tooltip("위의 표시 자식 자신에 있는 Animator입니다. SYM 붕괴 Controller를 연결하고 Collapse 상태의 Motion에 실제 클립을 넣으세요. 클립만 비어 있으면 경고 후 물리 붕괴와 외형 숨김은 진행됩니다.")]
        [SerializeField] private Animator collapseAnimator;

        [Header("Collapse")]
        [FormerlySerializedAs("wiggleDuration")]
        [Tooltip("밟은 뒤 흔들리며 경고할 시간(초)입니다. 길수록 늦게 무너집니다. 최소 0.01초이며, 끝나면 지지 Collider를 끄고 붕괴 애니메이션을 시작합니다.")]
        [SerializeField, Min(0.01f)] private float warningEndSeconds = 1f;

        [Header("Shake")]
        [FormerlySerializedAs("wiggleAngle")]
        [Tooltip("경고 중 표시 자식이 흔들리는 회전 크기(도)입니다. 크게 하면 더 세게 흔들리고, 0이면 흔들림 없이 경고 시간만 기다립니다.")]
        [SerializeField, Min(0f)] private float shakeAngleDegrees = 4f;
        [FormerlySerializedAs("wiggleFrequency")]
        [Tooltip("경고 중 1초에 흔들리는 횟수입니다. 높을수록 빠르게 흔들립니다. 최소 0.1이며 기본값은 8입니다.")]
        [SerializeField, Min(0.1f)] private float shakeFrequency = 8f;

        [Header("Regeneration")]
        [Tooltip("켜면 붕괴 후 기다렸다가 깜빡이며 복구하고, 끄면 실제 붕괴 재생 또는 미연결 처리가 끝난 뒤 제거합니다. 밟는 순간의 값을 사용합니다. Prototype에서는 끄세요.")]
        [SerializeField] private bool canRegenerate = true;
        [Tooltip("복구를 켰을 때 붕괴 외형이 사라진 뒤 복구를 시작하기까지의 대기 시간(초)입니다. 0이면 바로 깜빡임 복구를 시작합니다.")]
        [SerializeField, Min(0f)] private float collapsedHoldSeconds = 0.5f;
        [FormerlySerializedAs("regenerationDuration")]
        [Tooltip("복구 중 표시 자식이 깜빡이는 시간(초)입니다. 최소 0.01초이며, 완료 후에 지지 Collider를 다시 켭니다. 복구를 끄면 사용하지 않습니다.")]
        [SerializeField, Min(0.01f)] private float regenerationSeconds = 3f;
        [FormerlySerializedAs("blinkInterval")]
        [Tooltip("복구 중 외형을 켜거나 끄는 간격(초)입니다. 작을수록 빠르게 깜빡입니다. 최소 0.01초이며, 복구를 끄면 사용하지 않습니다.")]
        [SerializeField, Min(0.01f)] private float blinkIntervalSeconds = 0.15f;

        /// <summary>현재 진행 단계.</summary>
        public CollapsePhase Phase { get; private set; } = CollapsePhase.Ready;
        /// <summary>재생성 가능 여부.</summary>
        public bool CanRegenerate => canRegenerate;

        private PlatformPresentation presentation;
        private Coroutine cycle;
        private int cycleVersion;
        private bool supportWasEnabled;
        private const float MinimumSupportDot = 0.5f;

        /// <summary>같은 오브젝트의 콜라이더 연결.</summary>
        private void Reset() => supportCollider = GetComponent<BoxCollider>();

        /// <summary>잘못된 설정값 보정.</summary>
        private void OnValidate()
        {
            warningEndSeconds = Valid(warningEndSeconds, 0.01f, 1f);
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
            presentation = new PlatformPresentation(visual, collapseAnimator);
            presentation.Restore();
        }

        private void OnEnable()
        {
            if (Phase != CollapsePhase.DestroyScheduled) presentation?.Restore();
        }

        private void OnCollisionEnter(Collision collision) => TryCollapseFromContact(collision);
        private void OnCollisionStay(Collision collision) => TryCollapseFromContact(collision);

        /// <summary>
        /// 현재 플랫폼을 밟은 활성 PlayerFacade와 그 객체가 소유한 활성 HeavyState를 확인한다.
        /// 두 컴포넌트의 조건과 윗면 접촉 조건이 모두 충족되면 붕괴를 시작한다.
        /// </summary>
        private void TryCollapseFromContact(Collision collision)
        {
            if (!isActiveAndEnabled || Phase != CollapsePhase.Ready || presentation == null
                || collision == null || supportCollider == null || !supportCollider.enabled) return;
            Collider other = collision.collider;
            if (other == null || !other.enabled || other.isTrigger) return;
            if (!IsActiveHeavyPlayer(other)) return;
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
        /// 복합 콜라이더는 Rigidbody 객체를 소유자로 판단한다.
        /// Facade는 플레이어 식별에만 사용하며, 무게 마커는 같은 소유 객체에서 별도로 확인한다.
        /// Facade에 무게 계약이 추가되면 직접 마커 참조를 그 계약으로 대체한다.
        /// </summary>
        private static bool IsActiveHeavyPlayer(Collider other)
        {
            if (other == null || !other.enabled || other.isTrigger || !other.gameObject.activeInHierarchy) return false;
            GameObject owner = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
            return owner.TryGetComponent<PlayerFacade>(out var player)
                && player.isActiveAndEnabled
                && owner.TryGetComponent<HeavyState>(out var heavy)
                && heavy.isActiveAndEnabled;
        }

        /// <summary>
        /// 경고·붕괴·후처리를 한 번 시작한다. 중복 요청은 무시한다.
        /// 시작 후 플레이어가 떠나도 계속 진행한다.
        /// </summary>
        public void Collapses()
        {
            if (!isActiveAndEnabled || presentation == null || !presentation.IsValid
                || supportCollider == null || !supportCollider.enabled || Phase != CollapsePhase.Ready) return;
            OnValidate();
            supportWasEnabled = supportCollider.enabled;
            Phase = CollapsePhase.Wiggling;
            int version = ++cycleVersion;
            cycle = StartCoroutine(RunCycle(version, canRegenerate));
        }

        private IEnumerator RunCycle(int version, bool regenerate)
        {
            presentation.Shake(warningEndSeconds, shakeAngleDegrees, shakeFrequency);
            while (!presentation.TweenCompleted)
            {
                if (!CycleCanContinue(version)) yield break;
                if (!presentation.TweenExists)
                {
                    AbortCycle("경고 Tween이 완료 전에 제거되었습니다.");
                    yield break;
                }
                yield return null;
            }
            // 완료 직후 옵션 정지가 들어와도 물리 해제와 다음 단계는 재개 뒤에만 진행한다.
            while (Time.timeScale <= 0f)
            {
                if (!CycleCanContinue(version)) yield break;
                yield return null;
            }
            if (!CycleCanContinue(version)) yield break;
            presentation.Restore();
            if (!CycleCanContinue(version)) yield break;
            supportCollider.enabled = false;
            Phase = CollapsePhase.Animating;
            if (!presentation.TryBeginCollapseAnimation())
                ReportAnimationFailure(false);
            else
            {
                while (true)
                {
                    if (!CycleCanContinue(version)) yield break;
                    if (Time.timeScale <= 0f) { yield return null; continue; }
                    PlatformPresentation.AnimationProgress progress = presentation.ObserveCollapseAnimation(Time.deltaTime);
                    if (progress == PlatformPresentation.AnimationProgress.Completed) break;
                    if (progress == PlatformPresentation.AnimationProgress.ClipNotLinked
                        || progress == PlatformPresentation.AnimationProgress.Failed)
                    {
                        ReportAnimationFailure(progress == PlatformPresentation.AnimationProgress.ClipNotLinked);
                        break;
                    }
                    yield return null;
                }
            }
            if (!CycleCanContinue(version)) yield break;
            presentation.EndCollapseAnimation();
            presentation.SetVisible(false);
            Phase = CollapsePhase.Collapsed;
            if (!regenerate)
            {
                cycle = null;
                Phase = CollapsePhase.DestroyScheduled;
                Destroy(gameObject);
                yield break;
            }

            float holdElapsed = 0f;
            while (holdElapsed < collapsedHoldSeconds)
            {
                if (!CycleCanContinue(version)) yield break;
                yield return null;
                holdElapsed += Time.deltaTime;
            }
            while (Time.timeScale <= 0f)
            {
                if (!CycleCanContinue(version)) yield break;
                yield return null;
            }
            if (!CycleCanContinue(version)) yield break;
            // 클립이 바꾼 자식 위치·회전·크기도 복구한다. 지지 Collider는 아직 꺼져 있다.
            presentation.Restore();
            if (!CycleCanContinue(version)) yield break;
            Phase = CollapsePhase.Regenerating;
            presentation.Blink(regenerationSeconds, blinkIntervalSeconds);
            while (!presentation.TweenCompleted)
            {
                if (!CycleCanContinue(version)) yield break;
                if (!presentation.TweenExists)
                {
                    AbortCycle("복구 Tween이 완료 전에 제거되었습니다.");
                    yield break;
                }
                yield return null;
            }
            while (Time.timeScale <= 0f)
            {
                if (!CycleCanContinue(version)) yield break;
                yield return null;
            }
            if (!CycleCanContinue(version)) yield break;
            cycle = null;
            Restore();
        }

        private void ReportAnimationFailure(bool clipNotLinked)
        {
            string message = $"붕괴 플랫폼 '{name}': {presentation.AnimationIssue} 지지 Collider 해제와 외형 숨김으로 물리 붕괴를 진행합니다.";
            if (clipNotLinked) Debug.LogWarning(message, this);
            else Debug.LogError(message, this);
        }

        /// <summary>각 대기 구간에서 자기 실행과 필수 물리·표현 참조를 확인한다.</summary>
        private bool CycleCanContinue(int version)
        {
            if (version != cycleVersion || !isActiveAndEnabled || Phase == CollapsePhase.DestroyScheduled) return false;
            if (supportCollider != null && presentation != null && presentation.IsValid) return true;
            AbortCycle("실행 중 지지 Collider 또는 표시 자식이 제거·비활성화되었습니다.");
            return false;
        }

        private void AbortCycle(string reason)
        {
            Debug.LogError($"붕괴 플랫폼 '{name}': {reason} 자기 실행을 취소하고 설정을 복원합니다.", this);
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
            ++cycleVersion;
            Coroutine owned = cycle;
            cycle = null;
            if (owned != null) StopCoroutine(owned);
            presentation?.CancelTween();
            Restore();
        }

        /// <summary>비활성화 시 자기 코루틴·트윈·Animator 요청을 취소하고 재사용 상태를 복원한다.</summary>
        private void OnDisable() => CancelCycle();
        private void OnDestroy() => CancelCycle();
    }
}
