using System;
using DG.Tweening;
using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Enemies
{
    /// <summary>자신의 트윈 하나만 소유하고 갱신한다. Transform은 이동 담당만 변경한다.</summary>
    public sealed class EnemyFloatMotion : IDisposable
    {
        private Tween tween;
        private float phase;
        private readonly float amplitude;
        public float Offset => Mathf.Sin(phase * Mathf.PI * 2f) * amplitude;
        public EnemyFloatMotion(float amplitude, float period)
        {
            this.amplitude = amplitude;
            if (amplitude <= 0) return;
            tween = DOTween.To(() => phase, x => phase = x, 1f, period)
                .SetEase(Ease.Linear).SetLoops(-1, LoopType.Restart)
                .SetUpdate(UpdateType.Manual).SetRecyclable(false);
        }
        public void Tick(float delta) { if (tween != null) tween.ManualUpdate(delta, delta); }
        public void Dispose() { tween?.Kill(false); tween = null; phase = 0; }
    }
}
