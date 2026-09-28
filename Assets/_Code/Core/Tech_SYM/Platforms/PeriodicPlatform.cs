using DG.Tweening;
using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Platforms
{
    /// <summary>생성, 숨김, 경고를 반복하는 고정 발판.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
    public sealed class PeriodicPlatform : MonoBehaviour
    {
        public enum CyclePhase { Visible, Hidden, Warning }
        [Header("Cycle")]
        [Tooltip("밟을 수 있는 시간(초).")]
        [SerializeField, Min(0.02f)] private float lifetime = 5f;
        [Tooltip("경고 전에 완전히 숨기는 시간(초).")]
        [SerializeField, Min(0f)] private float hiddenSeconds = 2f;
        [Tooltip("재생성 전 깜빡임 시간(초). 이때는 밟을 수 없습니다.")]
        [SerializeField, Min(0.02f)] private float warningSeconds = 3f;
        [Header("Blink")]
        [Tooltip("경고 시간 동안 깜빡이는 총 횟수.")]
        [SerializeField, Min(1)] private int blinkCount = 8;
        [Tooltip("깜빡임 가속도. 값이 클수록 후반에 빠르게 깜빡입니다.")]
        [SerializeField, Range(1.01f, 4f)] private float blinkAcceleration = 2f;

        private BoxCollider support;
        private Renderer[] renderers;
        private bool[] defaults;
        private bool colliderDefault;
        private Sequence cycle;
        public CyclePhase Phase { get; private set; }

        private void Awake()
        {
            OnValidate();
            support = GetComponent<BoxCollider>();
            colliderDefault = support.enabled;
            if (support.isTrigger)
            {
                Debug.LogError("주기 발판은 비 Trigger 콜라이더가 필요합니다.", this);
                enabled = false;
                return;
            }
            renderers = GetComponentsInChildren<Renderer>(true);
            defaults = new bool[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) defaults[i] = renderers[i] != null && renderers[i].enabled;
        }

        private void OnEnable()
        {
            if (support == null || defaults == null) return;
            OnValidate();
            ShowPlatform();
            float progress = 0f;
            int count = blinkCount;
            float acceleration = blinkAcceleration;
            cycle = DOTween.Sequence().SetTarget(this).SetUpdate(UpdateType.Fixed, false)
                .SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart).SetRecyclable(false);
            cycle.AppendInterval(lifetime);
            cycle.AppendCallback(() => { Phase = CyclePhase.Hidden; if (support != null) support.enabled = false; SetVisible(false); });
            cycle.AppendInterval(hiddenSeconds);
            cycle.AppendCallback(() => { Phase = CyclePhase.Warning; SetVisible(false); });
            cycle.Append(DOTween.To(() => progress, value =>
            {
                progress = value;
                if (Phase == CyclePhase.Warning)
                    SetVisible(Mathf.FloorToInt(Mathf.Pow(value, acceleration) * count * 2f) % 2 == 1);
            }, 1f, warningSeconds).SetEase(Ease.Linear));
            cycle.OnStepComplete(ShowPlatform);
            cycle.OnUpdate(() =>
            {
                if (support != null) return;
                Debug.LogError("주기 발판의 콜라이더가 제거되었습니다.", this);
                enabled = false;
            });
            cycle.OnKill(() => cycle = null);
            cycle.Play();
        }

        private void ShowPlatform()
        {
            Phase = CyclePhase.Visible;
            if (support != null) support.enabled = colliderDefault;
            SetVisible(true);
        }

        private void SetVisible(bool visible)
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].enabled = visible && defaults[i];
        }

        private void OnDisable()
        {
            Sequence owned = cycle;
            cycle = null;
            if (owned != null && owned.IsActive()) owned.Kill(false);
            ShowPlatform();
        }

        private void OnValidate()
        {
            lifetime = Valid(lifetime, 0.02f);
            hiddenSeconds = Valid(hiddenSeconds, 0f);
            warningSeconds = Valid(warningSeconds, 0.02f);
            blinkCount = Mathf.Clamp(blinkCount, 1, 100);
            blinkAcceleration = float.IsFinite(blinkAcceleration) ? Mathf.Clamp(blinkAcceleration, 1.01f, 4f) : 2f;
        }
        private static float Valid(float value, float minimum) => float.IsFinite(value) ? Mathf.Max(minimum, value) : minimum;
    }
}
