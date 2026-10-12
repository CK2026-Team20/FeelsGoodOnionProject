using System;
using System.Collections.Generic;
using Cooked.Authoring;
using Cooked.Session;
using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Enemies
{
    /// <summary>실제 Hurt Trigger의 접촉 집합·반복 피해·현재 Actor 연결 수명을 소유합니다.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
    public sealed class EnemyContactSensor : MonoBehaviour
    {
        [SerializeField, InspectorLabel("소유 일반 적"), Tooltip("이 몸체 Trigger를 소유한 루트의 EnemyActor를 연결하세요.")]
        private EnemyActor owner;
        private readonly HashSet<Collider> contacts = new HashSet<Collider>();
        private BoxCollider shape;
        private PlayerBridgeService bridge;
        private PlayerFacade actor;
        private CapsuleCollider actorBody;
        private long actorGeneration;
        private double lastDamagePhysicsStep = double.NegativeInfinity;
        private bool resolving;
        public EnemyActor Owner => owner;
        public bool PhysicsPaused => bridge != null && bridge.PhysicsPaused;
        private void Awake() => shape = GetComponent<BoxCollider>();
        private void OnTriggerEnter(Collider other) => RecordContact(other, true);
        private void OnTriggerStay(Collider other) => RecordContact(other, true);
        private void OnTriggerExit(Collider other) => RecordContact(other, false);
        private void FixedUpdate() => ResolveContacts(Time.fixedTimeAsDouble);
        private void OnDisable() => UnbindPlayer();

        public void BindPlayer(PlayerBridgeService player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (shape == null) shape = GetComponent<BoxCollider>();
            if (owner == null || shape != owner.HurtCollider || !shape.isTrigger || shape.transform == owner.transform || !shape.transform.IsChildOf(owner.transform))
                throw new InvalidOperationException("소유 적의 실제 몸체 Trigger와 같은 객체에 접촉 센서를 연결하세요.");
            if (ReferenceEquals(bridge, player)) { ClearContacts(); return; }
            UnbindPlayer();
            bridge = player;
            bridge.ActorChanged += CaptureCurrentActor;
            CaptureCurrentActor();
        }
        public void UnbindPlayer()
        {
            if (bridge != null) bridge.ActorChanged -= CaptureCurrentActor;
            bridge = null; actor = null; actorBody = null; actorGeneration = 0;
            ClearContacts();
            lastDamagePhysicsStep = double.NegativeInfinity;
        }
        private void CaptureCurrentActor()
        {
            ClearContacts();
            actor = bridge.Actor; actorGeneration = bridge.ActorGeneration;
            actorBody = actor != null ? actor.GetComponent<CapsuleCollider>() : null;
            lastDamagePhysicsStep = double.NegativeInfinity;
        }
        private bool IsCurrentBody(Collider other) => other != null && other == actorBody && other.enabled &&
            other.gameObject.activeInHierarchy && other.attachedRigidbody != null && actor != null &&
            other.attachedRigidbody.gameObject == actor.gameObject && bridge != null && bridge.IsCurrentActor(actor, actorGeneration);
        public void RecordContact(Collider other, bool entered)
        {
            if (!entered) { if (other != null) contacts.Remove(other); return; }
            if (!isActiveAndEnabled || owner == null || !owner.isActiveAndEnabled || !owner.IsPrepared ||
                shape == null || shape != owner.HurtCollider || !shape.enabled || !shape.isTrigger || !IsCurrentBody(other)) return;
            contacts.Add(other);
        }
        public void ClearContacts() => contacts.Clear();
        public int ResolveContacts(double physicsStep)
        {
            if (!isActiveAndEnabled || owner == null || !owner.isActiveAndEnabled || !owner.IsPrepared || shape == null ||
                !shape.enabled || !shape.isTrigger || bridge == null || actor == null || !bridge.IsCurrentActor(actor, actorGeneration) || actor.IsDead)
            { ClearContacts(); return 0; }
            // Exit가 누락된 비활성/파괴/텔레포트도 실제 몸체 교차로 제거한다. 머리 접촉을 공격 예외로 만들지 않는다.
            contacts.RemoveWhere(c => !IsCurrentBody(c) || !Physics.ComputePenetration(shape, shape.transform.position,
                shape.transform.rotation, c, c.transform.position, c.transform.rotation, out _, out _));
            if (contacts.Count == 0 || owner.IsStunned || bridge.PhysicsPaused || resolving ||
                !double.IsFinite(physicsStep) || physicsStep == lastDamagePhysicsStep) return 0;
            resolving = true;
            lastDamagePhysicsStep = physicsStep;
            try { return bridge.Damage(shape.transform.TransformPoint(shape.center)); }
            finally { resolving = false; }
        }
    }
}
