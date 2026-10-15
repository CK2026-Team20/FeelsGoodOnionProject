using System;
using System.Collections.Generic;
using Cooked.Authoring;
using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Platforms
{
    /// <summary>한 물리 몸체의 실제 지지면과 접점을 수집한다. 플레이어 접지 규칙의 표면 쪽 소유자.</summary>
    [DisallowMultipleComponent]
    public sealed class PlatformContacts : MonoBehaviour, ICharacterGroundContactFilter
    {
        [SerializeField, InspectorLabel("탑승 충돌면"), Tooltip("같은 Rigidbody에 속한 비 Trigger Collider를 연결합니다. 경사각과 아래 반구 크기는 현재 플레이어에서 읽습니다.")]
        private Collider[] surfaces = Array.Empty<Collider>();
        [SerializeField, InspectorLabel("접촉 디버그"), Tooltip("선택 시 청록색 충돌면, 노란색 아래 반구와 녹색 접점·법선을 표시합니다.")]
        private bool drawContacts;
        public readonly struct SupportContact
        {
            public readonly Collider Surface, Passenger;
            public readonly Vector3 Point, Normal;
            public readonly double PhysicsStep;
            public SupportContact(Collider surface, Collider passenger, Vector3 point, Vector3 normal)
            { Surface = surface; Passenger = passenger; Point = point; Normal = normal; PhysicsStep = Time.fixedTimeAsDouble; }
        }
        private readonly List<SupportContact> contacts = new List<SupportContact>();
        private readonly Dictionary<CapsuleCollider, (Vector3 center, Vector3 axis, float radius)> stepCapsules = new();
        private double capsuleStep;
        public IReadOnlyList<Collider> Surfaces => surfaces;
        // 이전 씬의 단일 Collider 배치를 보존한다. 명시 배열은 새 작성/이관의 계약이다.
        public Collider Surface => surfaces != null && surfaces.Length > 0 ? surfaces[0] : GetComponent<Collider>();
        public event Action<Collision> ContactReported;
        public bool ContainsSurface(Collider value)
        {
            if (value == null) return false;
            if (surfaces == null || surfaces.Length == 0) return value == Surface;
            return Array.IndexOf(surfaces, value) >= 0;
        }
        public bool ValidBody(Rigidbody body)
        {
            if (body == null || Surface == null) return false;
            if (surfaces == null || surfaces.Length == 0) return !Surface.isTrigger && Surface.attachedRigidbody == body;
            foreach (var surface in surfaces)
                if (surface == null || surface.isTrigger || surface.attachedRigidbody != body) return false;
            return true;
        }
        public bool HasActiveSurface
        {
            get
            {
                if (surfaces == null || surfaces.Length == 0) return Active(Surface);
                foreach (var surface in surfaces) if (Active(surface)) return true;
                return false;
            }
        }
        public bool TryGetBounds(out Bounds bounds)
        {
            bounds = default; bool found = false;
            if (surfaces == null || surfaces.Length == 0)
            { if (!Active(Surface)) return false; bounds = Surface.bounds; return true; }
            foreach (var surface in surfaces)
                if (Active(surface)) { if (found) bounds.Encapsulate(surface.bounds); else bounds = surface.bounds; found = true; }
            return found;
        }
        private static bool Active(Collider value) => value != null && value.enabled && !value.isTrigger && value.gameObject.activeInHierarchy;
        public void ResetForRetry() { contacts.Clear(); stepCapsules.Clear(); }
        private void FixedUpdate()
        {
            // PhysX 접점은 적분 전 자세에서 생성된다. 콜백의 적분 후 Transform과 섞지 않는다.
            stepCapsules.Clear(); capsuleStep = Time.fixedTimeAsDouble;
            foreach (var contact in contacts)
                if (contact.Passenger is CapsuleCollider capsule && Active(capsule))
                {
                    LowerHemisphere(capsule, out var center, out var axis, out float radius);
                    stepCapsules[capsule] = (center, axis, radius);
                }
        }
        public bool IsTopContact(Collider other)
        {
            Prune();
            foreach (var contact in contacts) if (contact.Passenger == other) return true;
            return false;
        }
        /// <summary>HMS는 이 계약만 소비하며 SYM 구현을 참조하지 않는다. 법선은 지지면에서 플레이어 쪽 월드 방향.</summary>
        public bool AllowsGroundContact(Collider surface, CapsuleCollider capsule, Vector3 point, Vector3 normal, float maximumAngle)
        {
            if (!isActiveAndEnabled || !ContainsSurface(surface) || !Active(surface) || !Active(capsule) ||
                !float.IsFinite(maximumAngle) || Vector3.Dot(normal.normalized, Vector3.up) + .000001f < Mathf.Cos(maximumAngle * Mathf.Deg2Rad)) return false;
            // HMS가 다음 FixedUpdate에서 소비할 때도 수집 시 승인한 바로 그 접점만 재사용한다.
            foreach (var contact in contacts)
                if (contact.Surface == surface && contact.Passenger == capsule && !Expired(contact) &&
                    (contact.Point - point).sqrMagnitude < .00000001f && Vector3.Dot(contact.Normal, normal) > .999f) return true;
            if (IsLowerHemisphereContact(capsule, point, normal, maximumAngle)) return true;
            return capsuleStep == Time.fixedTimeAsDouble && stepCapsules.TryGetValue(capsule, out var pose) &&
                InsideHemisphere(point, pose.center, pose.axis, pose.radius, capsule.contactOffset);
        }
        public static void LowerHemisphere(CapsuleCollider capsule, out Vector3 center, out Vector3 axis, out float radius)
        {
            var t = capsule.transform;
            Vector3 scale = t.lossyScale;
            scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            int direction = capsule.direction;
            Vector3 localAxis = direction == 0 ? Vector3.right : direction == 2 ? Vector3.forward : Vector3.up;
            axis = t.TransformDirection(localAxis).normalized;
            if (Vector3.Dot(axis, Vector3.up) < 0f) axis = -axis;
            float axialScale = direction == 0 ? scale.x : direction == 2 ? scale.z : scale.y;
            float radialScale = direction == 0 ? Mathf.Max(scale.y, scale.z) : direction == 2 ? Mathf.Max(scale.x, scale.y) : Mathf.Max(scale.x, scale.z);
            radius = capsule.radius * radialScale;
            center = t.TransformPoint(capsule.center) - axis * Mathf.Max(0f, capsule.height * axialScale * .5f - radius);
        }
        public static bool IsLowerHemisphereContact(CapsuleCollider capsule, Vector3 point, Vector3 normal, float maximumAngle)
        {
            if (!Active(capsule) || !float.IsFinite(maximumAngle)) return false;
            if (Vector3.Dot(normal.normalized, Vector3.up) + .000001f < Mathf.Cos(maximumAngle * Mathf.Deg2Rad)) return false;
            LowerHemisphere(capsule, out var center, out var axis, out float radius);
            return InsideHemisphere(point, center, axis, radius, capsule.contactOffset);
        }
        private static bool InsideHemisphere(Vector3 point, Vector3 center, Vector3 axis, float radius, float contactOffset)
        {
            float tolerance = Mathf.Max(.001f, contactOffset);
            Vector3 offset = point - center;
            return Vector3.Dot(offset, axis) <= tolerance && offset.sqrMagnitude <= (radius + tolerance) * (radius + tolerance);
        }
        private bool Accept(SupportContact contact)
        {
            if (!Active(contact.Surface) || !Active(contact.Passenger) || !ContainsSurface(contact.Surface)) return false;
            var actor = Actor(contact.Passenger);
            if (actor != null && actor.TryGetComponent<CharacterMovement>(out var player))
                return player.isActiveAndEnabled && contact.Passenger is CapsuleCollider capsule &&
                    AllowsGroundContact(contact.Surface, capsule, contact.Point, contact.Normal, player.MaximumGroundAngle);
            // 일반 물체는 기존 월드 윗면 운반을 유지한다. 껍질의 최종 접지는 HMS가 판정한다.
            return Vector3.Dot(contact.Normal, Vector3.up) > .5f;
        }
        private void OnCollisionEnter(Collision collision) { Collect(collision); Report(collision); }
        private void OnCollisionStay(Collision collision) { Collect(collision); Report(collision); }
        private void RemovePair(Collider surface, Collider passenger)
            => contacts.RemoveAll(c => c.Surface == surface && c.Passenger == passenger);
        private void OnCollisionExit(Collision collision)
        {
            // Exit has no reliable local shape identity. Never erase another compound face's record.
            // Nonrenewed pairs expire in Prune on the next physics step.
            Prune();
        }
        private void Collect(Collision collision)
        {
            if (!isActiveAndEnabled || collision == null) return;
            for (int i = 0; i < collision.contactCount; i++)
            {
                var p = collision.GetContact(i);
                RemovePair(p.thisCollider, p.otherCollider);
            }
            for (int i = 0; i < collision.contactCount; i++)
            {
                var p = collision.GetContact(i);
                var contact = new SupportContact(p.thisCollider, p.otherCollider, p.point, -p.normal);
                if (Accept(contact)) contacts.Add(contact);
            }
        }
        private void Report(Collision collision)
        {
            var handlers = ContactReported;
            if (!isActiveAndEnabled || handlers == null || collision == null) return;
            foreach (Action<Collision> handler in handlers.GetInvocationList())
            {
                if (!isActiveAndEnabled) return;
                try { handler(collision); }
                catch (Exception error) { Debug.LogException(new InvalidOperationException($"발판 '{name}'의 접촉 통지에 실패했습니다.", error), this); }
            }
        }
        private void Prune()
        {
            if (!isActiveAndEnabled) { contacts.Clear(); return; }
            contacts.RemoveAll(c => !Active(c.Surface) || !Active(c.Passenger) || !ContainsSurface(c.Surface) || !Nearby(c) || Expired(c) || Physics.GetIgnoreCollision(c.Surface, c.Passenger) || Physics.GetIgnoreLayerCollision(c.Surface.gameObject.layer, c.Passenger.gameObject.layer));
        }
        private static bool Expired(SupportContact contact)
        {
            var body = contact.Passenger.attachedRigidbody;
            if (body != null && body.IsSleeping()) return false;
            return Time.fixedTimeAsDouble - contact.PhysicsStep > Time.fixedDeltaTime * 1.5;
        }
        private static bool Nearby(SupportContact contact)
        { Bounds bounds = contact.Surface.bounds; bounds.Expand(.3f); return bounds.Intersects(contact.Passenger.bounds); }
        public void CopySupportContacts(List<SupportContact> result)
        { if (result == null) return; Prune(); result.Clear(); result.AddRange(contacts); }
        public void CopyOccupants(List<Collider> result)
        {
            if (result == null) return; Prune(); result.Clear();
            foreach (var contact in contacts) if (!result.Contains(contact.Passenger)) result.Add(contact.Passenger);
        }
        public static GameObject Actor(Collider collider) => collider == null ? null :
            collider.attachedRigidbody != null ? collider.attachedRigidbody.gameObject : collider.gameObject;
        private void OnDisable() => ResetForRetry();
        private void OnDrawGizmosSelected()
        {
            if (!drawContacts) return;
            PlatformColliderGizmos.Draw(this, Matrix4x4.identity);
            foreach (var contact in contacts)
            {
                Gizmos.color = Color.green; Gizmos.DrawSphere(contact.Point, .015f); Gizmos.DrawRay(contact.Point, contact.Normal * .25f);
                if (contact.Passenger is CapsuleCollider capsule)
                { LowerHemisphere(capsule, out var center, out var axis, out float radius); PlatformColliderGizmos.DrawHemisphere(center, axis, radius); }
            }
        }
    }
}
