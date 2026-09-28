using System.Collections.Generic;
using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Platforms
{
    /// <summary>현재 윗면에 닿은 콜라이더 관리.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
    public sealed class PlatformContacts : MonoBehaviour
    {
        private readonly HashSet<Collider> contacts = new HashSet<Collider>();
        private readonly Dictionary<Collider, Vector3> contactVelocities = new Dictionary<Collider, Vector3>();
        private readonly List<Collider> stale = new List<Collider>();
        private BoxCollider surface;
        public BoxCollider Surface => surface;

        private void Awake() => surface = GetComponent<BoxCollider>();
        private void OnCollisionEnter(Collision collision) => Collect(collision);
        private void OnCollisionStay(Collision collision) => Collect(collision);
        private void OnCollisionExit(Collision collision)
        {
            if (collision != null && collision.collider != null) RemoveContact(collision.collider);
        }

        /// <summary>옆면과 아랫면 충돌은 제외한다.</summary>
        private void Collect(Collision collision)
        {
            if (!isActiveAndEnabled || surface == null || !surface.enabled || surface.isTrigger || collision == null) return;
            Collider other = collision.collider;
            if (other == null || other.isTrigger) return;
            bool onTop = false;
            for (int i = 0; i < collision.contactCount; i++)
            {
                ContactPoint point = collision.GetContact(i);
                if (point.thisCollider == surface && Vector3.Dot(-point.normal, transform.up) > 0.5f)
                { onTop = true; break; }
            }
            if (onTop)
            {
                contacts.Add(other);
                contactVelocities[other] = collision.relativeVelocity;
            }
            else RemoveContact(other);
        }

        /// <summary>비활성화되거나 떨어진 대상을 제외하고 복사한다.</summary>
        public void CopyOccupants(List<Collider> result)
        {
            if (result == null) return;
            result.Clear();
            if (!isActiveAndEnabled || surface == null || !surface.enabled || surface.isTrigger)
            { contacts.Clear(); contactVelocities.Clear(); return; }
            stale.Clear();
            Bounds bounds = surface.bounds;
            foreach (Collider other in contacts)
            {
                if (other == null || !other.enabled || other.isTrigger || !other.gameObject.activeInHierarchy)
                { stale.Add(other); continue; }
                Bounds actor = other.bounds;
                if (actor.max.x < bounds.min.x || actor.min.x > bounds.max.x
                    || actor.max.z < bounds.min.z || actor.min.z > bounds.max.z
                    || actor.min.y > bounds.max.y + 0.15f || actor.max.y < bounds.max.y)
                { stale.Add(other); continue; }
                result.Add(other);
            }
            foreach (Collider other in stale) RemoveContact(other);
        }

        /// <summary>윗면 충돌 시점의 상대 속도.</summary>
        public Vector3 ContactVelocity(Collider other) => other != null && contactVelocities.TryGetValue(other, out var velocity)
            ? velocity : Vector3.zero;

        private void RemoveContact(Collider other)
        {
            contacts.Remove(other);
            contactVelocities.Remove(other);
        }

        public static GameObject Actor(Collider collider) => collider == null ? null :
            collider.attachedRigidbody != null ? collider.attachedRigidbody.gameObject : collider.gameObject;
        private void OnDisable() { contacts.Clear(); contactVelocities.Clear(); }
    }
}
