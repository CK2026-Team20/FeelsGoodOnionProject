using System;
namespace Cooked.Audio
{
    /// <summary>이동 시작은 즉시, 착지 직후는 공통 간격 뒤에 재생한다. 정지 시간은 호출자가 진행하지 않는다.</summary>
    public sealed class FootstepCadence
    {
        private float remaining;
        private bool moving, landingCooldown;
        public void Land(float interval) { remaining = interval; landingCooldown = true; moving = false; }
        public void Jump() { remaining = 0; landingCooldown = moving = false; }
        public bool Tick(bool isMoving, float delta, float interval)
        {
            if (!float.IsFinite(delta) || delta < 0 || !float.IsFinite(interval) || interval <= 0) throw new ArgumentOutOfRangeException();
            if (delta == 0) return false;
            remaining = Math.Max(0, remaining - delta);
            if (!isMoving) { moving = false; return false; }
            if (!moving) { moving = true; if (!landingCooldown) remaining = 0; }
            if (remaining > .00001f) return false;
            remaining = interval; landingCooldown = false; return true;
        }
    }
}
