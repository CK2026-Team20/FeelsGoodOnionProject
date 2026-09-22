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
        private enum MotionPath { None, Travel, Effect }
        private MotionPath path;
        private Vector3 effectOrigin;
        private readonly Dictionary<Component, Vector3> effects = new Dictionary<Component, Vector3>();
        private bool conflictReported;
        public Vector3 EffectOrigin => effectOrigin;
        private readonly HashSet<Rigidbody> carried = new HashSet<Rigidbody>();
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
            if (path == MotionPath.Effect) { ReportConflict(); return; }
            path = MotionPath.Travel;
            target = position;
            hasTarget = true;
        }

        /// <summary>속도를 상속하지 않는 연출 경로에 등록한다. 일반 이동과 혼용하지 않는다.</summary>
        public bool RegisterEffect(Component owner)
        {
            if (owner == null) return false;
            if (path == MotionPath.Travel || GetComponent<LinearShuttlePlatform>() != null
                || GetComponent<PressurePlatePlatform>() != null || GetComponent<PressureLinkedPlatform>() != null
                || GetComponent<MovingPlatform>() != null)
            { ReportConflict(); return false; }
            if (path == MotionPath.None) { effectOrigin = Position; path = MotionPath.Effect; }
            effects[owner] = Vector3.zero;
            return true;
        }

        /// <summary>등록한 동작의 이동량만 갱신한다.</summary>
        public void SetEffectOffset(Component owner, Vector3 value)
        {
            if (owner != null && Finite(value) && effects.ContainsKey(owner)) effects[owner] = value;
        }

        /// <summary>자기 이동량만 해제한다. 부유 정지는 기준 높이를 유지한다.</summary>
        public void ReleaseEffect(Component owner, bool keepPosition = false)
        {
            if (ReferenceEquals(owner, null)) return;
            if (effects.TryGetValue(owner, out var value) && keepPosition) effectOrigin += value;
            effects.Remove(owner);
        }

        private void ReportConflict()
        {
            if (conflictReported) return;
            conflictReported = true;
            Debug.LogError("일반 이동 발판과 부유·착지 연출은 같은 객체에서 함께 사용할 수 없습니다.", this);
        }

        private static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        private void FixedUpdate()
        {
            if (body == null || contacts == null || Time.fixedDeltaTime <= 0f) return;
            PreviousVelocity = Velocity;
            bool carryWithoutInertia = path == MotionPath.Effect;
            Vector3 next = hasTarget ? target : body.position;
            if (carryWithoutInertia)
            {
                next = effectOrigin;
                foreach (var entry in effects)
                    if (entry.Key != null) next += entry.Value;
            }
            Vector3 displacement = next - body.position;
            Velocity = displacement / Time.fixedDeltaTime;
            carried.Clear();
            contacts.CopyOccupants(occupants);
            foreach (Collider other in occupants)
            {
                GameObject actor = PlatformContacts.Actor(other);
                if (actor != null && actor.TryGetComponent<PlatformPassenger>(out var passenger) && passenger.isActiveAndEnabled)
                {
                    if (carryWithoutInertia && actor.TryGetComponent<CharacterMovement>(out var movement)
                        && movement.isActiveAndEnabled && movement.IsGrounded && movement.GroundCollider == contacts.Surface)
                    {
                        Rigidbody rider = other.attachedRigidbody;
                        if (rider != null && !rider.isKinematic && carried.Add(rider)) rider.position += displacement;
                    }
                    passenger.ReceivePlatformVelocity(contacts.Surface, other,
                        carryWithoutInertia ? Vector3.zero : Velocity,
                        carryWithoutInertia ? Vector3.zero : PreviousVelocity);
                }
            }
            hasTarget = false;
            // 부유 연출은 위치만 옮겨 키네마틱 속도가 플레이어를 밀어내지 않게 한다.
            if (carryWithoutInertia) body.position = next;
            else body.MovePosition(next);
        }

        private void OnDisable()
        {
            hasTarget = false;
            Velocity = PreviousVelocity = Vector3.zero;
            occupants.Clear();
            carried.Clear();
        }

        private void OnDestroy()
        {
            var collider = GetComponent<BoxCollider>();
            if (collider != null && collider.sharedMaterial == motionMaterial) collider.sharedMaterial = originalMaterial;
            if (motionMaterial != null) Destroy(motionMaterial);
        }
    }
}
