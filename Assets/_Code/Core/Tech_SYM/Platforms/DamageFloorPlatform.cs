using System;
using System.Collections.Generic;
using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Platforms
{
    /// <summary>바닥을 밟은 대상에게 1 피해 적용.</summary>
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent, RequireComponent(typeof(PlatformContacts), typeof(BoxCollider))]
    public sealed class DamageFloorPlatform : MonoBehaviour
    {
        [Header("Damage")]
        [Tooltip("피해 적용 간격(초). 피해량은 1입니다.")]
        [SerializeField, Min(0.02f)] private float damageInterval = 1f;
        private const int ContactDamage = 1;
        private PlatformContacts contacts;
        private readonly List<Collider> occupants = new List<Collider>();
        private readonly HashSet<Component> damagedThisStep = new HashSet<Component>();
        private readonly Dictionary<Component, double> nextDamageTimes = new Dictionary<Component, double>();
        private readonly List<Component> removed = new List<Component>();

        /// <summary>피해 대상, 피해량, 충돌 시점 속도 전달.</summary>
        public event Action<GameObject, int, Vector3> DamageApplied;

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
                if (actor == null || !actor.TryGetComponent<IDamageable>(out var damageable)) continue;
                var owner = damageable as Component;
                if (owner == null || (owner is Behaviour behaviour && !behaviour.isActiveAndEnabled)
                    || !damagedThisStep.Add(owner) || damageable.IsInvincible()) continue;
                if (nextDamageTimes.TryGetValue(owner, out double next) && Time.fixedTimeAsDouble < next) continue;
                int applied = damageable.Damage(ContactDamage);
                if (applied > 0)
                {
                    nextDamageTimes[owner] = Time.fixedTimeAsDouble + damageInterval;
                    DamageApplied?.Invoke(actor, applied, contacts.ContactVelocity(collider));
                }
            }
            removed.Clear();
            foreach (Component owner in nextDamageTimes.Keys)
                if (owner == null || !damagedThisStep.Contains(owner)) removed.Add(owner);
            foreach (Component owner in removed) nextDamageTimes.Remove(owner);
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
        }
    }
}
