using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Platforms
{
    /// <summary>피해 적용 후 플레이어 진행 반대 방향으로 넉백.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(DamageFloorPlatform))]
    public sealed class DamagePlatformKnockback : MonoBehaviour
    {
        [Header("Knockback")]
        [Tooltip("충돌 방향의 반대로 밀어낼 속도(m/s). 낙하 방향도 포함합니다. 0이면 넉백하지 않습니다.")]
        [SerializeField, Min(0f)] private float knockbackSpeed = 3f;
        private DamageFloorPlatform damageFloor;

        private void Awake() { OnValidate(); damageFloor = GetComponent<DamageFloorPlatform>(); }
        private void OnEnable()
        {
            if (damageFloor != null) damageFloor.DamageApplied += ApplyKnockback;
        }
        private void OnDisable()
        {
            if (damageFloor != null) damageFloor.DamageApplied -= ApplyKnockback;
        }

        private void ApplyKnockback(GameObject actor, int appliedDamage, Vector3 incomingVelocity)
        {
            if (appliedDamage <= 0 || actor == null || knockbackSpeed <= 0f
                || !actor.TryGetComponent<CharacterMovement>(out var movement) || !movement.isActiveAndEnabled) return;
            Vector3 velocity = movement.Velocity;
            Vector3 direction = incomingVelocity.sqrMagnitude > 0.0001f ? incomingVelocity : velocity;
            if (direction.sqrMagnitude < 0.0001f) direction = movement.FacingDirection;
            if (direction.sqrMagnitude < 0.000001f) return;
            Vector3 targetVelocity = -direction.normalized * knockbackSpeed;
            // HMS는 바닥에서 이륙할 때 수직 속도를 0부터 적용한다.
            movement.AddImpulse(new Vector3(targetVelocity.x - velocity.x,
                targetVelocity.y, targetVelocity.z - velocity.z));
        }

        private void OnValidate()
        {
            if (!float.IsFinite(knockbackSpeed) || knockbackSpeed < 0f) knockbackSpeed = 0f;
        }
    }
}
