using System.Collections.Generic;
using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Enemies
{
    /// <summary>전 물리 단계의 접촉과 발/머리 이동을 함께 판정한다. 콜백 순서에 의존하지 않는다.</summary>
    public sealed class EnemyCombat
    {
        private readonly EnemyActor enemy;
        private readonly BoxCollider body;
        private readonly BoxCollider head;
        private readonly PlayerFacade player;
        private readonly CapsuleCollider feet;
        private readonly HashSet<Collider> contacts = new HashSet<Collider>();
        private Vector3 previousFoot;
        private Bounds previousHead;
        private bool sampled;
        private bool consumed;

        public EnemyCombat(EnemyActor enemy, BoxCollider body, BoxCollider head, PlayerFacade player)
        {
            this.enemy = enemy; this.body = body; this.head = head; this.player = player;
            // Physics shape only; gameplay state is read exclusively through PlayerFacade.
            if (player != null) feet = player.GetComponent<CapsuleCollider>();
        }
        public void Record(Collider other, bool entered, bool isHead)
        {
            if (isHead || player == null || other.isTrigger || other.attachedRigidbody == null ||
                other.attachedRigidbody.gameObject != player.gameObject) return;
            if (entered) contacts.Add(other); else contacts.Remove(other);
        }
        public void Clear() { contacts.Clear(); sampled = false; consumed = false; }
        public void Resolve()
        {
            if (player == null || !player.isActiveAndEnabled || player.IsDead)
            { Clear(); return; }
            if (feet != null && feet.enabled && feet.direction == 1)
            {
                Vector3 foot = FeetPoint(feet);
                Bounds currentHead = head.bounds;
                if (sampled && player.CurrentVelocity.y < -.01f &&
                    CrossedTop(previousFoot, foot, previousHead, currentHead))
                { enemy.Stomp(); return; }
                previousFoot = foot; previousHead = currentHead; sampled = true;
            }
            // Disable/destroy and teleport can omit Exit. Geometric pruning preserves contact identity.
            contacts.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy ||
                !Physics.ComputePenetration(body, body.transform.position, body.transform.rotation,
                    c, c.transform.position, c.transform.rotation, out _, out _));
            if (contacts.Count == 0) { consumed = false; return; }
            if (consumed || enemy.IsStunned || enemy.IsDead) return;
            IDamageable damageable = player;
            if (!damageable.IsInvincible() && damageable.Damage(enemy.Definition.ContactDamage) > 0)
                consumed = true;
        }
        public static Vector3 FeetPoint(CapsuleCollider capsule)
        {
            Vector3 scale = capsule.transform.lossyScale;
            float radius = capsule.radius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            float halfHeight = Mathf.Max(radius, capsule.height * Mathf.Abs(scale.y) * .5f);
            return capsule.transform.TransformPoint(capsule.center) - Vector3.up * halfHeight;
        }
        /// <summary>발 캡슐의 가장 낮은 실제 표면점이 좁은 머리 사각형 안을 하강 통과해야 한다.</summary>
        public static bool CrossedTop(Vector3 oldFoot, Vector3 newFoot, Bounds oldHead, Bounds newHead)
        {
            const float edgeInset = .015f;
            if (newFoot.y >= oldFoot.y - .0001f) return false;
            float before = oldFoot.y - oldHead.max.y;
            float after = newFoot.y - newHead.max.y;
            if (before < 0 || after > 0 || before - after < .0001f) return false;
            float t = before / (before - after);
            Vector3 foot = Vector3.Lerp(oldFoot, newFoot, t);
            Vector3 min = Vector3.Lerp(oldHead.min, newHead.min, t);
            Vector3 max = Vector3.Lerp(oldHead.max, newHead.max, t);
            return foot.x > min.x + edgeInset && foot.x < max.x - edgeInset &&
                foot.z > min.z + edgeInset && foot.z < max.z - edgeInset;
        }
    }
}
