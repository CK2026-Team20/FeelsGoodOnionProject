using System;
namespace Cooked.Occlusion
{
    /// <summary>렌더링과 독립적인 그룹 Fade 상태입니다.</summary>
    public sealed class OcclusionFadeState
    {
        public float Alpha { get; private set; } = 1f;
        float clearTime;
        public void Reset() { Alpha = 1f; clearTime = 0f; }
        public void Tick(bool blocked, float deltaTime, float minimum, float fadeOut, float restore, float delay)
        {
            float dt = Math.Max(0f, deltaTime);
            minimum = Math.Clamp(minimum, 0.01f, 1f);
            if (blocked) clearTime = 0f; else clearTime += dt;
            float target = blocked || clearTime < delay ? minimum : 1f;
            float duration = target < Alpha ? fadeOut : restore;
            float step = duration <= 0f ? 1f : dt * (1f - minimum) / duration;
            Alpha = target < Alpha ? Math.Max(target, Alpha - step) : Math.Min(target, Alpha + step);
        }
    }
}
