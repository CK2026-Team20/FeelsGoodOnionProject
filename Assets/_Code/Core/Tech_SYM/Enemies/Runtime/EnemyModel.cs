using System;

namespace FeelsGoodOnion.TechSYM.Enemies
{
    /// <summary>개체별 생존 상태. 공유 SO나 Inspector 표시용 복제본에는 상태를 저장하지 않는다.</summary>
    public sealed class EnemyModel
    {
        public int Health { get; private set; }
        public bool IsInvincible { get; private set; }
        public double StunMilliseconds { get; private set; }
        public bool IsStunned => StunMilliseconds > .001;
        public bool IsDead => Health == 0;
        public EnemyModel(int health, bool invincible)
        {
            if (health <= 0) throw new ArgumentOutOfRangeException(nameof(health));
            Health = health;
            IsInvincible = invincible;
        }
        public void SetInvincible(bool value) => IsInvincible = value;
        public int Damage(int amount)
        {
            if (amount <= 0 || IsDead || IsInvincible) return 0;
            int applied = Math.Min(Health, amount);
            Health -= applied;
            return applied;
        }
        public int Stun(int milliseconds)
        {
            if (milliseconds <= 0 || IsDead) return 0;
            StunMilliseconds += milliseconds;
            return milliseconds;
        }
        public void Tick(float seconds)
        {
            if (seconds > 0) StunMilliseconds = Math.Max(0, StunMilliseconds - seconds * 1000.0);
        }
        public void Kill() { Health = 0; StunMilliseconds = 0; }
    }
}
