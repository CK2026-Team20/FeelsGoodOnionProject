using System;
using System.Collections.Generic;
using Cooked.Authoring;
using Cooked.Session;
using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Platforms
{
    /// <summary>명시된 피해 Trigger의 접촉을 현재 플레이어의 공통 피해 경계에 전달합니다.</summary>
    [DisallowMultipleComponent]
    public class DamageFloorPlatform : MonoBehaviour
    {
        // 기존 자산의 GUID와 공통 타입 조회를 보존한다. 신규 배치는 Pot/Gratang/Burner 구체 타입을 사용한다.
        [SerializeField, Min(0), InspectorLabel("피해량"), Tooltip("이 플랫폼이 플레이어에게 주는 체력 피해량입니다. 0은 피해 없음이며 무적 중에는 큰 피해도 적용하지 않습니다.")]
        private int damageAmount = 1;
        public int DamageAmount => Mathf.Max(0, damageAmount);

        [SerializeField, InspectorLabel("피해 감지 영역"), Tooltip("루트 또는 자식의 피해 Trigger들을 연결하세요. 메시 Trigger는 Convex여야 하며 지지면은 제외합니다. 비워 두면 피해를 감지하지 않습니다. 선택 시 주황색은 Box 영역, 자홍색은 Convex 입력 메시 참고선입니다.")]
        private Collider[] damageTriggers = Array.Empty<Collider>();
        private readonly List<DamageTriggerRelay> subscribedRelays = new List<DamageTriggerRelay>();
        private PlayerBridgeService bridge;
        private PlayerFacade actor;
        private CapsuleCollider actorBody;
        private long actorGeneration;
        private double lastDamagePhysicsStep = double.NegativeInfinity;
        private bool resolving;

        protected virtual void OnEnable() => ResumeBinding();
        protected virtual void OnValidate() => damageAmount = Mathf.Max(0, damageAmount);

        private void ResumeBinding()
        {
            if (!isActiveAndEnabled || bridge == null) return;
            bridge.ActorChanged -= CaptureCurrentActor;
            bridge.ActorChanged += CaptureCurrentActor;
            CaptureCurrentActor();
            SubscribeTriggers();
        }

        // 일시 비활성화는 연결값을 보존하고 자신이 소유한 구독과 현재 몸체 캐시만 해제한다.
        private void SuspendBinding()
        {
            UnsubscribeTriggers();
            if (bridge != null) bridge.ActorChanged -= CaptureCurrentActor;
            actor = null; actorBody = null; actorGeneration = 0;
            lastDamagePhysicsStep = double.NegativeInfinity;
        }

        private bool IsOwnedTrigger(Collider source) => source != null && source.isTrigger &&
            (source.transform == transform || source.transform.IsChildOf(transform)) &&
            (!(source is MeshCollider mesh) || (mesh.convex && mesh.sharedMesh != null));

        private void SubscribeTriggers()
        {
            UnsubscribeTriggers();
            if (damageTriggers == null) return;
            foreach (Collider source in damageTriggers)
            {
                if (source == null) continue;
                if (!IsOwnedTrigger(source))
                {
                    Debug.LogError("피해 감지 영역은 루트 또는 자식의 Trigger여야 합니다. MeshCollider는 메시와 Convex 설정이 필요합니다.", this);
                    continue;
                }
                var relay = source.GetComponent<DamageTriggerRelay>();
                if (relay == null) relay = source.gameObject.AddComponent<DamageTriggerRelay>();
                if (subscribedRelays.Contains(relay)) continue;
                relay.ContactReported += DamageContact;
                subscribedRelays.Add(relay);
            }
        }

        private void UnsubscribeTriggers()
        {
            foreach (var relay in subscribedRelays)
                if (relay != null) relay.ContactReported -= DamageContact;
            subscribedRelays.Clear();
        }

        public void BindPlayer(PlayerBridgeService player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (!ReferenceEquals(bridge, player))
            {
                UnbindPlayer();
                bridge = player;
            }
            if (isActiveAndEnabled) ResumeBinding();
            else SuspendBinding();
        }

        public void UnbindPlayer()
        {
            SuspendBinding();
            bridge = null;
        }

        private void CaptureCurrentActor()
        {
            if (!isActiveAndEnabled || bridge == null) return;
            var currentActor = bridge.Actor;
            long currentGeneration = bridge.ActorGeneration;
            var currentBody = currentActor != null ? currentActor.GetComponent<CapsuleCollider>() : null;
            // 같은 바인딩의 재요청으로 동일 물리 스텝의 중복 방지를 해제하지 않는다.
            if (actor != currentActor || actorGeneration != currentGeneration || actorBody != currentBody)
                lastDamagePhysicsStep = double.NegativeInfinity;
            actor = currentActor; actorGeneration = currentGeneration; actorBody = currentBody;
        }

        private void DamageContact(Collider other) => TryDamageContact(other, Time.fixedTimeAsDouble);

        /// <summary>명시된 Trigger와 겹친 현재 몸체에 물리 단계당 피해 명령을 한 번만 보냅니다.</summary>
        public int TryDamageContact(Collider other, double physicsStep)
        {
            if (!isActiveAndEnabled || bridge == null || actor == null ||
                !bridge.IsCurrentActor(actor, actorGeneration) || actor.IsDead || bridge.PhysicsPaused || resolving ||
                !double.IsFinite(physicsStep) || physicsStep == lastDamagePhysicsStep || other == null || other != actorBody ||
                !other.enabled || other.isTrigger || !other.gameObject.activeInHierarchy || other.attachedRigidbody == null ||
                other.attachedRigidbody.gameObject != actor.gameObject || damageTriggers == null) return 0;
            foreach (Collider source in damageTriggers)
            {
                if (!IsOwnedTrigger(source) || !source.enabled || !source.gameObject.activeInHierarchy ||
                    Physics.GetIgnoreCollision(source, other) ||
                    Physics.GetIgnoreLayerCollision(source.gameObject.layer, other.gameObject.layer)) continue;
                // Compound 몸체의 콜백은 자기 Collider를 주지 않으므로 실제 지정 영역과의 겹침을 확인한다.
                if (!Physics.ComputePenetration(other, other.transform.position, other.transform.rotation,
                    source, source.transform.position, source.transform.rotation, out _, out _)) continue;
                resolving = true; lastDamagePhysicsStep = physicsStep;
                // Trigger 내부의 ClosestPoint는 플레이어 중심 자체가 될 수 있어 피해 영역 중심을 전달한다.
                try { return bridge.Damage(this, source.bounds.center); }
                finally { resolving = false; }
            }
            return 0;
        }

        protected virtual void FixedUpdate()
        {
            // 보정 중에도 동일한 Trigger 판정과 피해 중복 방지 경계를 사용한다.
            if (bridge != null && bridge.HasCorrectionOwner)
                TryDamageContact(actorBody, Time.fixedTimeAsDouble);
        }

        protected virtual void OnDrawGizmosSelected()
        {
            if (damageTriggers == null) return;
            foreach (var trigger in damageTriggers) PlatformColliderGizmos.Draw(trigger, Matrix4x4.identity);
        }

        public void ResetForRetry() => lastDamagePhysicsStep = double.NegativeInfinity;
        protected virtual void OnDisable() => SuspendBinding();
        protected virtual void OnDestroy() => UnbindPlayer();
    }
}
