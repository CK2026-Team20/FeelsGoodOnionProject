using UnityEngine;
using Cooked.Session;

namespace FeelsGoodOnion.TechSYM.Platforms
{
    /// <summary>모든 방향의 실제 접촉을 공통 피해 경로로 전달한다. 무적은 플레이어가 소유한다.</summary>
    [DisallowMultipleComponent]
    public sealed class DamageFloorPlatform : MonoBehaviour
    {
        [Tooltip("피해 물리 자식의 BoxCollider와 같은 객체에 있는 접촉 전달자입니다. 윗면·옆면·아랫면을 모두 전달합니다.")]
        [SerializeField] private PlatformContacts physicalContacts;
        [Tooltip("피해원을 벗어나는 수평 및 위쪽 넉백의 기존 속력(m/s)입니다. 0은 속력을 주지 않습니다.")]
        [SerializeField, Min(0f)] private float knockbackSpeed = 3f;
        private PlatformContacts contacts;
        private void Awake() { OnValidate(); contacts = physicalContacts != null ? physicalContacts : GetComponent<PlatformContacts>(); }
        private void OnEnable() { if (contacts != null) contacts.ContactReported += DamageContact; }
        private void DamageContact(Collision collision)
        {
            if (!isActiveAndEnabled || collision == null) return;
            var owner = PlatformContacts.Actor(collision.collider);
            if (owner == null || !owner.TryGetComponent<PlayerFacade>(out var player) || !player.isActiveAndEnabled) return;
            BoxCollider surface = contacts != null ? contacts.Surface : null;
            if (surface == null) return;
            Vector3 direction = player.transform.position - surface.bounds.center;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            { direction = -player.CurrentVelocity; direction.y = 0f; }
            direction = direction.normalized + Vector3.up;
            player.Damage(1, direction.normalized * knockbackSpeed,
                PlayerBridgeService.GetKnockbackControlLockMilliseconds(player));
        }
        public void ResetForRetry() => contacts?.ResetForRetry();
        private void OnDisable() { if (contacts != null) contacts.ContactReported -= DamageContact; }
        private void OnValidate() { if (!float.IsFinite(knockbackSpeed) || knockbackSpeed < 0f) knockbackSpeed = 0f; }
    }
}
