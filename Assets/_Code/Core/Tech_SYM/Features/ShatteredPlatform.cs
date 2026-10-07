using System.Collections;
using FeelsGoodOnion.TechSYM.Platforms;
using UnityEngine;
using UnityEngine.Serialization;

namespace FeelsGoodOnion.TechSYM.Features
{
    [DisallowMultipleComponent]
    public sealed class ShatteredPlatform : MonoBehaviour
    {
        public enum CollapsePhase { Ready = 0, Wiggling = 1, Collapsed = 2, Animating = 5 }
        [Tooltip("붕괴 표시를 담당하는 Collider 없는 Visual 자식입니다. 원래 위치·회전·크기는 재시도에만 복원합니다.")]
        [SerializeField] private Transform visual;
        [Tooltip("PhysicsBody 자식 또는 기존 같은 객체의 지지 BoxCollider입니다. 경고 1초 끝에 즉시 꺼집니다.")]
        [SerializeField] private Collider supportCollider;
        [Tooltip("위 Visual 자신에 있는 붕괴 Animator입니다. Collapse Motion이 없으면 연결 대기를 기록하고 외형을 숨깁니다.")]
        [SerializeField] private Animator collapseAnimator;
        [Tooltip("지지 Collider와 같은 물리 객체의 접촉 전달자입니다. 루트 컨트롤러와 물리 자식을 연결합니다.")]
        [SerializeField] private PlatformContacts physicalContacts;
        [FormerlySerializedAs("wiggleDuration")]
        [Tooltip("기본 폼의 윗면 접촉 뒤 경고 시간(초)입니다. 이번 명세는 정확히 1초로 고정합니다. 종료 즉시 Collider가 꺼집니다.")]
        [SerializeField] private float warningEndSeconds = 1f;
        [FormerlySerializedAs("wiggleAngle")]
        [Tooltip("경고 중 Visual 회전 크기(도)입니다. 0이면 흔들리지 않습니다.")]
        [SerializeField, Min(0f)] private float shakeAngleDegrees = 4f;
        [FormerlySerializedAs("wiggleFrequency")]
        [Tooltip("경고 중 초당 흔들림 횟수입니다. 최소 0.1입니다.")]
        [SerializeField, Min(0.1f)] private float shakeFrequency = 8f;
        private PlatformPresentation presentation;
        private Coroutine animation;
        private PlatformContacts contacts;
        private bool supportWasEnabled;
        private float warningElapsed;
        private int version;
        public CollapsePhase Phase { get; private set; } = CollapsePhase.Ready;
        private void Awake()
        {
            OnValidate();
            if (supportCollider == null) supportCollider = GetComponent<BoxCollider>();
            contacts = physicalContacts != null ? physicalContacts : supportCollider?.GetComponent<PlatformContacts>();
            if (visual == null || !visual.IsChildOf(transform) || supportCollider == null || supportCollider.isTrigger
                || !(supportCollider is BoxCollider) || visual.GetComponentsInChildren<Collider>(true).Length != 0)
            { Debug.LogError("붕괴 발판의 Visual과 물리 자식 참조를 확인하세요.", this); enabled = false; return; }
            supportWasEnabled = supportCollider.enabled;
            presentation = new PlatformPresentation(visual, collapseAnimator);
        }
        private void OnEnable() { if (contacts != null) contacts.ContactReported += TryCollapseFromContact; }
        // Same-object callback is retained only for legacy authoring without a contact relay; T9 binds the child.
        private void OnCollisionEnter(Collision collision) { if (contacts == null) TryCollapseFromContact(collision); }
        private void OnCollisionStay(Collision collision) { if (contacts == null) TryCollapseFromContact(collision); }
        private void TryCollapseFromContact(Collision collision)
        {
            if (!isActiveAndEnabled || Phase != CollapsePhase.Ready || collision == null) return;
            var other = collision.collider;
            var owner = PlatformContacts.Actor(other);
            if (owner == null || other.isTrigger || owner.TryGetComponent<PlayerDebris>(out _) || !owner.TryGetComponent<PlayerFacade>(out var player)
                || !player.isActiveAndEnabled || !owner.TryGetComponent<HeavyState>(out var heavy) || !heavy.isActiveAndEnabled
                || (owner.TryGetComponent<PlayerFormController>(out var form) && form.CurrentForm != PlayerForm.Normal)) return;
            for (int i = 0; i < collision.contactCount; i++)
            {
                var point = collision.GetContact(i);
                if (point.thisCollider == supportCollider && Vector3.Dot(-point.normal, supportCollider.transform.up) >= 0.5f)
                { Collapses(); return; }
            }
        }
        public void Collapses()
        {
            if (!isActiveAndEnabled || Phase != CollapsePhase.Ready || presentation == null || supportCollider == null || !supportCollider.enabled) return;
            Phase = CollapsePhase.Wiggling;
            warningElapsed = 0f;
            presentation.Shake(1f, shakeAngleDegrees, shakeFrequency);
        }
        private void FixedUpdate()
        {
            if (Phase != CollapsePhase.Wiggling || Time.timeScale <= 0f) return;
            warningElapsed += Time.fixedDeltaTime;
            if (warningElapsed + 0.000001f < 1f) return;
            supportCollider.enabled = false;
            presentation.Restore();
            Phase = CollapsePhase.Animating;
            animation = StartCoroutine(PlayCollapse(++version));
        }
        private IEnumerator PlayCollapse(int request)
        {
            if (presentation.TryBeginCollapseAnimation())
            {
                while (request == version)
                {
                    if (Time.timeScale <= 0f) { yield return null; continue; }
                    var progress = presentation.ObserveCollapseAnimation(Time.deltaTime);
                    if (progress == PlatformPresentation.AnimationProgress.Completed) break;
                    if (progress == PlatformPresentation.AnimationProgress.Failed || progress == PlatformPresentation.AnimationProgress.ClipNotLinked)
                    { Debug.LogWarning(presentation.AnimationIssue, this); break; }
                    yield return null;
                }
            }
            else Debug.LogWarning(presentation.AnimationIssue, this);
            if (request != version) yield break;
            presentation.EndCollapseAnimation();
            presentation.SetVisible(false);
            Phase = CollapsePhase.Collapsed;
            animation = null;
        }
        private void CancelOwnedWork()
        {
            ++version;
            if (animation != null) StopCoroutine(animation);
            animation = null;
            presentation?.CancelTween();
            if (Phase == CollapsePhase.Animating)
            { presentation?.EndCollapseAnimation(); presentation?.SetVisible(false); Phase = CollapsePhase.Collapsed; }
        }
        public void ResetForRetry()
        {
            CancelOwnedWork();
            warningElapsed = 0f;
            presentation?.Restore();
            if (supportCollider != null) supportCollider.enabled = supportWasEnabled;
            contacts?.ResetForRetry();
            Phase = CollapsePhase.Ready;
        }
        private void OnDisable() { if (contacts != null) contacts.ContactReported -= TryCollapseFromContact; CancelOwnedWork(); }
        private void OnDestroy() => CancelOwnedWork();
        private void OnValidate()
        {
            warningEndSeconds = 1f;
            shakeAngleDegrees = float.IsFinite(shakeAngleDegrees) ? Mathf.Max(0f, shakeAngleDegrees) : 4f;
            shakeFrequency = float.IsFinite(shakeFrequency) ? Mathf.Max(0.1f, shakeFrequency) : 8f;
        }
    }
}
