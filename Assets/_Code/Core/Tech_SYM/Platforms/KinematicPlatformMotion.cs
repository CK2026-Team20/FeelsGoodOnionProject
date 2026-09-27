using System;
using System.Collections.Generic;
using DG.Tweening;
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
        private enum MotionPath { None, Travel, Effect, Tween }
        private MotionPath path;
        private Vector3 effectOrigin;
        private readonly Dictionary<Component, Vector3> effects = new Dictionary<Component, Vector3>();
        private bool conflictReported;
        public Vector3 EffectOrigin => effectOrigin;
        private readonly HashSet<Rigidbody> carried = new HashSet<Rigidbody>();
        private PhysicsMaterial originalMaterial;
        private PhysicsMaterial motionMaterial;
        private Tweener travelTween;
        private Vector3 tweenStart, tweenDestination;
        private float tweenDuration, tweenElapsed;
        private bool completionPending;
        private Action tweenCompleted;
        public bool IsTweening => travelTween != null;
        public float RemainingTweenSeconds => Mathf.Max(0f, tweenDuration - tweenElapsed);
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
            if (path == MotionPath.Effect || path == MotionPath.Tween) { ReportConflict(); return; }
            path = MotionPath.Travel;
            target = position;
            hasTarget = true;
        }

        /// <summary>트윈 위치 적용과 탑승 속도 전달을 같은 물리 스텝에서 처리한다.</summary>
        public bool TryTweenTo(Vector3 worldDestination, float duration, Action onCompleted)
        {
            if (!isActiveAndEnabled || body == null || IsTweening || !Finite(worldDestination)
                || !float.IsFinite(duration) || duration <= 0f) return false;
            if (path == MotionPath.Effect || path == MotionPath.Travel || HasTravelDriver())
            { ReportConflict(); return false; }
            path = MotionPath.Tween;
            tweenStart = body.position;
            tweenDestination = worldDestination;
            tweenDuration = duration;
            tweenElapsed = 0f;
            completionPending = false;
            tweenCompleted = onCompleted;
            travelTween = body.DOMove(worldDestination, duration).SetEase(Ease.OutSine)
                .SetUpdate(UpdateType.Manual).SetAutoKill(false).Pause();
            travelTween.ForceInit();
            return true;
        }

        /// <summary>자신의 트윈과 완료 통지만 취소한다. 남은 시간은 재개를 위해 보존한다.</summary>
        public void CancelTween()
        {
            travelTween?.Kill(false);
            travelTween = null;
            tweenCompleted = null;
            completionPending = false;
        }

        private bool HasTravelDriver() => GetComponent<LinearShuttlePlatform>() != null
            || GetComponent<PressurePlatePlatform>() != null || GetComponent<PressureLinkedPlatform>() != null
            || GetComponent<MovingPlatform>() != null;

        /// <summary>속도를 상속하지 않는 연출 경로에 등록한다. 일반 이동과 혼용하지 않는다.</summary>
        public bool RegisterEffect(Component owner)
        {
            if (owner == null) return false;
            if (path == MotionPath.Travel || path == MotionPath.Tween || HasTravelDriver())
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
            Debug.LogError("일반 이동, 트윈 이동, 부유·착지 연출은 같은 플랫폼에서 혼용할 수 없습니다.", this);
        }

        private static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        private void FixedUpdate()
        {
            if (body == null || contacts == null || Time.fixedDeltaTime <= 0f) return;
            // 마지막 MovePosition이 물리에 반영된 다음 스텝에만 완료를 통지한다.
            Action completed = null;
            if (completionPending)
            {
                completed = tweenCompleted;
                CancelTween();
            }
            PreviousVelocity = Velocity;
            bool carryWithoutInertia = path == MotionPath.Effect;
            Vector3 next = hasTarget ? target : body.position;
            bool tweenStep = travelTween != null;
            if (tweenStep)
            {
                tweenElapsed = Mathf.Min(tweenDuration, tweenElapsed + Time.fixedDeltaTime);
                float fraction = DOVirtual.EasedValue(0f, 1f, tweenElapsed / tweenDuration, Ease.OutSine);
                next = Vector3.LerpUnclamped(tweenStart, tweenDestination, fraction);
            }
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
            if (tweenStep)
            {
                // DOMove가 이 스텝의 유일한 위치 적용자다. 전역 ManualUpdate는 호출하지 않는다.
                travelTween.Goto(tweenElapsed, false);
                completionPending = tweenElapsed >= tweenDuration;
            }
            else if (carryWithoutInertia) body.position = next;
            else body.MovePosition(next);
            completed?.Invoke();
        }

        private void OnDisable()
        {
            CancelTween();
            hasTarget = false;
            Velocity = PreviousVelocity = Vector3.zero;
            occupants.Clear();
            carried.Clear();
        }

        private void OnDestroy()
        {
            CancelTween();
            var collider = GetComponent<BoxCollider>();
            if (collider != null && collider.sharedMaterial == motionMaterial) collider.sharedMaterial = originalMaterial;
            if (motionMaterial != null) Destroy(motionMaterial);
        }
    }
}
