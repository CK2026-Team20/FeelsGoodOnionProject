using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Testing
{
    /// <summary>플랫폼 테스트용 체력과 피격 무적.</summary>
    [DisallowMultipleComponent]
    public sealed class PlatformTestHealth : MonoBehaviour, IDamageable
    {
        [Header("Health")]
        [Tooltip("테스트 시작 체력.")]
        [SerializeField, Min(1)] private int startingHealth = 20;
        [Tooltip("피격 후 무적 시간(초).")]
        [SerializeField, Min(0f)] private float invincibilitySeconds = 0.5f;
        [Tooltip("켜면 항상 무적입니다.")]
        [SerializeField] private bool invincible;
        private double invincibleUntil;
        public int Health { get; private set; }
        public int HitCount { get; private set; }
        public int LastDamage { get; private set; }

        private void Awake() { OnValidate(); Health = startingHealth; }
        public bool IsInvincible() => invincible || Time.timeAsDouble < invincibleUntil;
        public int Damage(int damage)
        {
            if (!isActiveAndEnabled || damage <= 0 || Health <= 0 || IsInvincible()) return 0;
            int applied = Mathf.Min(damage, Health);
            Health -= applied;
            LastDamage = applied;
            HitCount++;
            invincibleUntil = Time.timeAsDouble + invincibilitySeconds;
            Debug.Log($"{name} HP: {Health} (-{applied})", this);
            return applied;
        }

        private void OnValidate()
        {
            startingHealth = Mathf.Max(1, startingHealth);
            if (!float.IsFinite(invincibilitySeconds) || invincibilitySeconds < 0f) invincibilitySeconds = 0f;
        }
    }
}
