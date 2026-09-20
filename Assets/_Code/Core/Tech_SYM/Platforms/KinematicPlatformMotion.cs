using System.Collections.Generic;
using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Platforms
{
    /// <summary>키네마틱 이동과 탑승자 속도 전달.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-300)]
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider), typeof(PlatformContacts))]
    public sealed class KinematicPlatformMotion : MonoBehaviour
    {
        private Rigidbody body;
        private PlatformContacts contacts;
        private readonly List<Collider> occupants = new List<Collider>();
        private Vector3 target;
        private bool hasTarget;
        private PhysicsMaterial originalMaterial;
        private PhysicsMaterial motionMaterial;
        public Vector3 Velocity { get; private set; }
        public Vector3 PreviousVelocity { get; private set; }
        public Vector3 Direction => Velocity.sqrMagnitude > 0.000001f ? Velocity.normalized : Vector3.zero;
        public float Speed => Velocity.magnitude;
        public Vector3 Position => body != null ? body.position : transform.position;

        private void Reset()
        {
            var rigidbody = GetComponent<Rigidbody>();
            if (rigidbody == null) return;
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;
            rigidbody.constraints = RigidbodyConstraints.FreezeRotation;
        }

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            contacts = GetComponent<PlatformContacts>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            var collider = GetComponent<BoxCollider>();
            collider.isTrigger = false;
            originalMaterial = collider.sharedMaterial;
            motionMaterial = new PhysicsMaterial("Platform Motion")
            {
                staticFriction = 0f, dynamicFriction = 0f, bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum
            };
            collider.sharedMaterial = motionMaterial;
        }

        /// <summary>이번 물리 스텝의 목표 위치 지정.</summary>
        public void MoveTo(Vector3 position)
        {
            if (!isActiveAndEnabled || !Finite(position)) return;
            target = position;
            hasTarget = true;
        }

        private static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        private void FixedUpdate()
        {
            if (body == null || contacts == null || Time.fixedDeltaTime <= 0f) return;
            PreviousVelocity = Velocity;
            Vector3 next = hasTarget ? target : body.position;
            Velocity = (next - body.position) / Time.fixedDeltaTime;
            contacts.CopyOccupants(occupants);
            foreach (Collider other in occupants)
            {
                GameObject actor = PlatformContacts.Actor(other);
                if (actor != null && actor.TryGetComponent<PlatformPassenger>(out var passenger) && passenger.isActiveAndEnabled)
                    passenger.ReceivePlatformVelocity(contacts.Surface, other, Velocity, PreviousVelocity);
            }
            hasTarget = false;
            body.MovePosition(next);
        }

        private void OnDisable()
        {
            hasTarget = false;
            Velocity = PreviousVelocity = Vector3.zero;
            occupants.Clear();
        }

        private void OnDestroy()
        {
            var collider = GetComponent<BoxCollider>();
            if (collider != null && collider.sharedMaterial == motionMaterial) collider.sharedMaterial = originalMaterial;
            if (motionMaterial != null) Destroy(motionMaterial);
        }
    }
}
