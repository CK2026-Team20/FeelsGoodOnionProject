using System.Collections;
using FeelsGoodOnion.TechSYM.Platforms;
using UnityEngine;
using System;
using Cooked.Authoring;

namespace FeelsGoodOnion.TechSYM.Features
{
    [DisallowMultipleComponent]
    public sealed class ShatteredPlatform : MonoBehaviour
    {
        public enum CollapsePhase { Ready = 0, Wiggling = 1, Collapsed = 2, Animating = 5 }
        [Tooltip("붕괴 표시를 담당하는 Collider 없는 Visual 자식입니다. 원래 위치·회전·크기는 재시도에만 복원합니다.")]
        [SerializeField, InspectorLabel("외형")] private Transform visual;
        [Tooltip("실제 지지 Collider를 연결합니다. 경고가 끝나면 즉시 꺼집니다.")]
        [SerializeField, InspectorLabel("지지 콜라이더")] private Collider supportCollider;
        [Tooltip("위 Visual 자신에 있는 붕괴 Animator입니다. Collapse Motion이 없으면 연결 대기를 기록하고 외형을 숨깁니다.")]
        [SerializeField, InspectorLabel("붕괴 애니메이터")] private Animator collapseAnimator;
        [Tooltip("지지 Collider와 같은 물리 객체의 접촉 전달자입니다. 루트 컨트롤러와 물리 자식을 연결합니다.")]
        [SerializeField, InspectorLabel("지지 접촉")] private PlatformContacts physicalContacts;
        [Tooltip("붕괴 전 경고 시간을 조절합니다(초, 최소 0.02). 초기값은 1초이며 종료 즉시 지지를 제거합니다.")]
        [SerializeField, InspectorLabel("경고 시간"), Min(.02f)] private float warningEndSeconds = 1f;
        [Tooltip("경고 중 Visual 회전 크기(도)입니다. 0이면 흔들리지 않습니다.")]
        [SerializeField, InspectorLabel("흔들림 각도"), Min(0f)] private float shakeAngleDegrees = 4f;
        [Tooltip("경고 중 초당 흔들림 횟수입니다. 최소 0.1입니다.")]
        [SerializeField, InspectorLabel("흔들림 횟수"), Min(0.1f)] private float shakeFrequency = 8f;
        private PlatformPresentation presentation;
        private Coroutine animation;
        private PlatformContacts contacts;
        private bool supportWasEnabled;
        private float warningElapsed;
        private int version;
        public CollapsePhase Phase { get; private set; } = CollapsePhase.Ready;
        /// <summary>Animator 없이 경고 Tween이 회전하는 실제 외형과 그 자식을 판별한다.</summary>
        public bool OwnsMovingVisual(Transform point) => visual != null && point != null &&
            (point == visual || point.IsChildOf(visual));
        private void Awake()
        {
            OnValidate();
            contacts = physicalContacts;
            if (visual == null || !visual.IsChildOf(transform) || supportCollider == null || supportCollider.isTrigger
                || contacts == null || !contacts.ContainsSurface(supportCollider)
                || (supportCollider.transform != transform && !supportCollider.transform.IsChildOf(transform))
                || visual.GetComponentsInChildren<Collider>(true).Length != 0
                || visual.GetComponentsInChildren<Rigidbody>(true).Length != 0
                || visual.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
            { Debug.LogError("붕괴 발판의 Visual과 물리 자식 참조를 확인하세요.", this); enabled = false; return; }
            supportWasEnabled = supportCollider.enabled;
            presentation = new PlatformPresentation(visual, collapseAnimator);
        }
        private void OnEnable()
        {
            if (contacts != null) { contacts.ContactReported -= TryCollapseFromContact; contacts.ContactReported += TryCollapseFromContact; }
            if (Phase == CollapsePhase.Wiggling && presentation != null)
                presentation.Shake(Mathf.Max(.02f, warningEndSeconds - warningElapsed), shakeAngleDegrees, shakeFrequency);
        }
        private void TryCollapseFromContact(Collision collision)
        {
            if (!isActiveAndEnabled || Phase != CollapsePhase.Ready || collision == null) return;
            var other = collision.collider;
            var owner = PlatformContacts.Actor(other);
            if (owner == null || other.isTrigger || owner.TryGetComponent<PlayerDebris>(out _) || !owner.TryGetComponent<PlayerFacade>(out var player)
                || !player.isActiveAndEnabled || !owner.TryGetComponent<HeavyState>(out var heavy) || !heavy.isActiveAndEnabled
                || (owner.TryGetComponent<PlayerFormController>(out var form) && form.CurrentForm != PlayerForm.Normal)) return;
            if (contacts.IsTopContact(other)) Collapses();
        }
        public void Collapses()
        {
            if (!isActiveAndEnabled || Phase != CollapsePhase.Ready || presentation == null || supportCollider == null || !supportCollider.enabled) return;
            Phase = CollapsePhase.Wiggling;
            warningElapsed = 0f;
            presentation.Shake(warningEndSeconds, shakeAngleDegrees, shakeFrequency);
        }
        private void FixedUpdate()
        {
            if (Phase != CollapsePhase.Wiggling || Time.timeScale <= 0f) return;
            warningElapsed += Time.fixedDeltaTime;
            if (!WarningFinished(warningElapsed, warningEndSeconds)) return;
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
        /// <summary>실행에서 쓰는 경고 종료 경계다. 실제 접지·클립 재생 검증과 구분한다.</summary>
        public static bool WarningFinished(float elapsedSeconds, float warningSeconds)
        {
            if (!float.IsFinite(elapsedSeconds) || elapsedSeconds < 0 || !float.IsFinite(warningSeconds) || warningSeconds < .02f)
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            return elapsedSeconds + .000001f >= warningSeconds;
        }
        private void OnValidate()
        {
            warningEndSeconds = float.IsFinite(warningEndSeconds) ? Mathf.Max(.02f, warningEndSeconds) : 1f;
            shakeAngleDegrees = float.IsFinite(shakeAngleDegrees) ? Mathf.Max(0f, shakeAngleDegrees) : 4f;
            shakeFrequency = float.IsFinite(shakeFrequency) ? Mathf.Max(0.1f, shakeFrequency) : 8f;
        }
    }
}
