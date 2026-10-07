using System.Collections.Generic;
using Cooked.Session;
using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Enemies
{
    /// <summary>실제 몸통 접촉을 모으며 반복 피해 수락/무적/넉백은 플레이어의 공통 경계가 소유합니다.</summary>
    public sealed class EnemyCombat
    {
        private readonly EnemyActor enemy;
        private readonly BoxCollider body;
        private readonly PlayerFacade player;
        private readonly CapsuleCollider feet;
        private readonly HashSet<Collider> contacts = new HashSet<Collider>();
        private const float MinimumDirectionSqrMagnitude = 0.0001f;

        public EnemyCombat(EnemyActor enemy, BoxCollider body, PlayerFacade player)
        {
            this.enemy = enemy; this.body = body; this.player = player;
            // Physics shape only; gameplay state is read exclusively through PlayerFacade.
            if (player != null) feet = player.GetComponent<CapsuleCollider>();
        }
        public void Record(Collider other, bool entered)
        {
            if (other == null || player == null || other.isTrigger || other.attachedRigidbody == null ||
                other.attachedRigidbody.gameObject != player.gameObject) return;
            // Resolve the complete physics step before ending an episode: different player
            // colliders can exit/enter in either callback order while contact remains continuous.
            if (entered) contacts.Add(other); else contacts.Remove(other);
        }
        public void Clear() => contacts.Clear();
        public void Resolve()
        {
            if (!enemy.isActiveAndEnabled || player == null || !player.isActiveAndEnabled || player.IsDead)
            { Clear(); return; }
            // Disable/destroy and teleport can omit Exit. Geometric pruning preserves contact identity.
            contacts.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy ||
                !Physics.ComputePenetration(body, body.transform.position, body.transform.rotation,
                    c, c.transform.position, c.transform.rotation, out _, out _));
            if (contacts.Count == 0 || enemy.IsStunned) return;
            // 입력 차단은 전투 면역이 아니다. 피해 적용 여부는 Facade가 결정한다.
            player.Damage(1, KnockbackVelocity(), PlayerBridgeService.GetKnockbackControlLockMilliseconds(player));
        }
        private Vector3 KnockbackVelocity()
        {
            Vector3 center = feet != null ? feet.bounds.center : player.transform.position;
            Vector3 direction = center - body.bounds.center;
            if (!player.AllowDepthMovement) direction.z = 0f;
            if (direction.sqrMagnitude <= MinimumDirectionSqrMagnitude)
            {
                direction = -player.CurrentVelocity;
                if (!player.AllowDepthMovement) direction.z = 0f;
            }
            // 중심이 겹치고 정지한 경우에도 유효한 분리 방향을 제공한다.
            if (direction.sqrMagnitude <= MinimumDirectionSqrMagnitude) direction = Vector3.up;
            return direction.normalized * enemy.Definition.KnockbackSpeed;
        }
    }
}
