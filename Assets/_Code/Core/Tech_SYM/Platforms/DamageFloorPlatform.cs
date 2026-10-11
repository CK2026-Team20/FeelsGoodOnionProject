using System;
using Cooked.Authoring;
using Cooked.Session;
using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Platforms
{
    /// <summary>실제 표면의 모든 방향 접촉을 현재 플레이어의 공통 피해 경계에 전달합니다.</summary>
    [DisallowMultipleComponent]
    public sealed class DamageFloorPlatform : MonoBehaviour
    {
        [SerializeField, InspectorLabel("물리 접촉 수집기"), Tooltip("피해 표면 BoxCollider와 같은 객체의 PlatformContacts를 연결하세요. 윗면·옆면·아랫면 접촉을 받습니다.")]
        private PlatformContacts physicalContacts;
        private PlatformContacts subscribedContacts;
        private PlayerBridgeService bridge;
        private PlayerFacade actor;
        private CapsuleCollider actorBody;
        private long actorGeneration;
        private double lastDamagePhysicsStep = double.NegativeInfinity;
        private bool resolving;
        private void OnEnable() => SubscribeContacts();
        private void SubscribeContacts()
        {
            if (subscribedContacts == physicalContacts) return;
            if (subscribedContacts != null) subscribedContacts.ContactReported -= DamageContact;
            subscribedContacts = physicalContacts;
            if (subscribedContacts != null) subscribedContacts.ContactReported += DamageContact;
        }
        public void BindPlayer(PlayerBridgeService player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (physicalContacts == null || physicalContacts.Surface == null || physicalContacts.Surface.isTrigger ||
                (physicalContacts.transform != transform && !physicalContacts.transform.IsChildOf(transform)))
                throw new InvalidOperationException("피해원 루트 또는 물리 자식의 실제 비 Trigger 표면과 접촉 수집기를 연결하세요.");
            if (ReferenceEquals(bridge, player)) return;
            UnbindPlayer();
            bridge = player; bridge.ActorChanged += CaptureCurrentActor;
            CaptureCurrentActor();
            if (isActiveAndEnabled) SubscribeContacts();
        }
        public void UnbindPlayer()
        {
            if (bridge != null) bridge.ActorChanged -= CaptureCurrentActor;
            bridge = null; actor = null; actorBody = null; actorGeneration = 0;
            lastDamagePhysicsStep = double.NegativeInfinity;
        }
        private void CaptureCurrentActor()
        {
            actor = bridge.Actor; actorGeneration = bridge.ActorGeneration;
            actorBody = actor != null ? actor.GetComponent<CapsuleCollider>() : null;
            lastDamagePhysicsStep = double.NegativeInfinity;
        }
        private void DamageContact(Collision collision)
        {
            if (collision != null) TryDamageContact(collision.collider, Time.fixedTimeAsDouble);
        }
        /// <summary>물리 콜백 한 단계에서 피해원 하나당 현재 몸체에 한 명령만 보냅니다.</summary>
        public int TryDamageContact(Collider other, double physicsStep)
        {
            if (!isActiveAndEnabled || physicalContacts == null || !physicalContacts.isActiveAndEnabled || bridge == null || actor == null ||
                !bridge.IsCurrentActor(actor, actorGeneration) || actor.IsDead || bridge.PhysicsPaused || resolving ||
                !double.IsFinite(physicsStep) || physicsStep == lastDamagePhysicsStep || other == null || other != actorBody ||
                !other.enabled || !other.gameObject.activeInHierarchy || other.attachedRigidbody == null ||
                other.attachedRigidbody.gameObject != actor.gameObject) return 0;
            BoxCollider surface = physicalContacts.Surface;
            if (surface == null || !surface.enabled || surface.isTrigger) return 0;
            resolving = true; lastDamagePhysicsStep = physicsStep;
            try { return bridge.Damage(surface.ClosestPoint(other.bounds.center)); }
            finally { resolving = false; }
        }
        private void FixedUpdate()
        {
            // Kinematic 보정 중 static/kinematic 표면은 Collision callback을 주지 않을 수 있다.
            if (bridge == null || !bridge.HasCorrectionOwner || bridge.PhysicsPaused || actorBody == null ||
                physicalContacts == null || physicalContacts.Surface == null) return;
            var surface = physicalContacts.Surface;
            if (!surface.enabled || !surface.gameObject.activeInHierarchy ||
                Physics.GetIgnoreCollision(surface, actorBody) ||
                Physics.GetIgnoreLayerCollision(surface.gameObject.layer, actorBody.gameObject.layer)) return;
            PlayerBridgeService.CapsuleGeometry(actorBody, out var a, out var b, out float radius);
            foreach (var hit in Physics.CapsuleCastAll(a + Vector3.up * .01f, b + Vector3.up * .01f, radius, Vector3.down, .02f, ~0, QueryTriggerInteraction.Ignore))
                if (hit.collider == surface)
                { TryDamageContact(actorBody, Time.fixedTimeAsDouble); return; }
            if (Physics.ComputePenetration(surface, surface.transform.position, surface.transform.rotation,
                actorBody, actorBody.transform.position, actorBody.transform.rotation, out _, out _))
                TryDamageContact(actorBody, Time.fixedTimeAsDouble);
        }
        public void ResetForRetry() => physicalContacts?.ResetForRetry();
        private void OnDisable()
        {
            if (subscribedContacts != null) subscribedContacts.ContactReported -= DamageContact;
            subscribedContacts = null;
            UnbindPlayer();
        }
    }
}
