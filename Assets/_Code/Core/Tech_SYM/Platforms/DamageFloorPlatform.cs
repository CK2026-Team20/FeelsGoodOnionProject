using System.Collections.Generic;
using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Platforms
{
    /// <summary>바닥을 밟은 플레이어에게 Facade의 공통 계약을 통해 피해와 넉백을 요청한다.</summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent, RequireComponent(typeof(PlatformContacts), typeof(BoxCollider))]
    public sealed class DamageFloorPlatform : MonoBehaviour
    {
        [Header("Damage")]
        [Tooltip("피해 적용 간격(초). 피해량은 1입니다.")]
        [SerializeField, Min(0.02f)] private float damageInterval = 1f;
        [Header("Knockback")]
        [Tooltip("피해가 적용된 플레이어를 진행 반대 방향으로 밀어낼 월드 속력(m/s). 0이면 넉백하지 않습니다.")]
        [SerializeField, Min(0f)] private float knockbackSpeed = 3f;
        [Tooltip("넉백 시 이동·점프 제어 제한 요청 시간(ms). 0이면 새 제한을 요청하지 않습니다.")]
        [SerializeField, Min(0f)] private float controlLockMilliseconds;
        private const int ContactDamage = 1;
        private const float MinimumDirectionSqrMagnitude = 0.0001f;
        private PlatformContacts contacts;
        private readonly List<Collider> occupants = new List<Collider>();
        private readonly HashSet<PlayerFacade> damagedThisStep = new HashSet<PlayerFacade>();
        private readonly Dictionary<PlayerFacade, double> nextDamageTimes = new Dictionary<PlayerFacade, double>();
        private readonly List<PlayerFacade> removed = new List<PlayerFacade>();

        private void Awake()
        {
            OnValidate();
            contacts = GetComponent<PlatformContacts>();
        }

        private void FixedUpdate()
        {
            if (contacts == null) return;
            contacts.CopyOccupants(occupants);
            damagedThisStep.Clear();
            foreach (Collider collider in occupants)
            {
                GameObject actor = PlatformContacts.Actor(collider);
                if (actor == null || !actor.TryGetComponent<PlayerFacade>(out var player)
                    || !player.isActiveAndEnabled || !damagedThisStep.Add(player)) continue;
                IDamageable damageable = player;
                if (damageable.IsInvincible()) continue;
                if (nextDamageTimes.TryGetValue(player, out double next) && Time.fixedTimeAsDouble < next) continue;
                Vector3 incomingVelocity = contacts.ContactVelocity(collider);
                Vector3 velocity = player.CurrentVelocity;
                int applied = damageable.Damage(ContactDamage);
                if (applied > 0)
                {
                    nextDamageTimes[player] = Time.fixedTimeAsDouble + damageInterval;
                    RequestKnockback(player, incomingVelocity, velocity);
                }
            }
            removed.Clear();
            foreach (PlayerFacade owner in nextDamageTimes.Keys)
                if (owner == null || !damagedThisStep.Contains(owner)) removed.Add(owner);
            foreach (PlayerFacade owner in removed) nextDamageTimes.Remove(owner);
        }

        private void RequestKnockback(PlayerFacade player, Vector3 incomingVelocity, Vector3 velocity)
        {
            if (knockbackSpeed <= 0f) return;
            Vector3 direction = incomingVelocity.sqrMagnitude > MinimumDirectionSqrMagnitude
                ? -incomingVelocity : -velocity;
            // 정지 접촉은 플랫폼 윗방향으로 밀어낸다. 이동 구현의 방향 상태에는 의존하지 않는다.
            if (direction.sqrMagnitude <= MinimumDirectionSqrMagnitude) direction = transform.up;
            IKnockbackable knockbackable = player;
            knockbackable.ApplyKnockback(direction.normalized * knockbackSpeed, controlLockMilliseconds);
        }

        private void OnDisable()
        {
            occupants.Clear();
            damagedThisStep.Clear();
            nextDamageTimes.Clear();
            removed.Clear();
        }

        private void OnValidate()
        {
            if (!float.IsFinite(damageInterval) || damageInterval < 0.02f) damageInterval = 0.02f;
            if (!float.IsFinite(knockbackSpeed) || knockbackSpeed < 0f) knockbackSpeed = 0f;
            if (!float.IsFinite(controlLockMilliseconds) || controlLockMilliseconds < 0f) controlLockMilliseconds = 0f;
        }
    }
}
