using DG.Tweening;
using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Features
{
    /// <summary>
    /// 플랫폼의 흔들림과 깜빡임 연출.
    /// 트윈 실행과 종료는 ShatteredPlatform에서 관리한다.
    /// </summary>
    public sealed class PlatformPresentation
    {
        private readonly Transform visual;
        private readonly Quaternion restRotation;
        private readonly Renderer[] renderers;
        private readonly bool[] rendererDefaults;
        public bool IsValid => visual != null;

        public PlatformPresentation(Transform visual)
        {
            this.visual = visual;
            restRotation = visual.localRotation;
            renderers = visual.GetComponentsInChildren<Renderer>(true);
            rendererDefaults = new bool[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
                rendererDefaults[i] = renderers[i] != null && renderers[i].enabled;
        }

        /// <summary>회전 흔들림 트윈 생성.</summary>
        public Tween Shake(float seconds, float degrees, float frequency) =>
            visual.DOShakeRotation(seconds, new Vector3(degrees, 0f, degrees),
                Mathf.Clamp(Mathf.RoundToInt(seconds * frequency), 1, 1000), 90f, true).SetEase(Ease.Linear);

        /// <summary>깜빡임 트윈 생성.</summary>
        public Tween Blink(float seconds, float interval)
        {
            float elapsed = 0f;
            return DOTween.To(() => elapsed, value =>
            {
                elapsed = value;
                SetVisible(Mathf.FloorToInt(value / interval) % 2 == 0);
            }, seconds, seconds).SetEase(Ease.Linear);
        }

        public void SetVisible(bool visible)
        {
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].enabled = visible && rendererDefaults[i];
        }

        /// <summary>원래 회전과 렌더러 표시 상태 복원.</summary>
        public void Restore()
        {
            if (visual != null)
            {
                visual.localRotation = restRotation;
            }
            SetVisible(true);
        }
    }
}
