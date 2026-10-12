using Cooked.Integration;
using System;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using Cooked.Authoring;

namespace FeelsGoodOnion.TechSYM.Platforms
{
    /// <summary>키네마틱 이동과 탑승자 속도 전달.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-300)]
    public sealed class KinematicPlatformMotion : MonoBehaviour
    {
        [Tooltip("고정 기능 루트 안에서 움직일 자식 Rigidbody를 연결합니다. 같은 몸체에 지지 BoxCollider와 PlatformContacts를 둡니다.")]
        [SerializeField, InspectorLabel("이동 몸체")] private Rigidbody physicsBody;
        private Rigidbody body;
        private Vector3 authoredLocalPosition;
        private Quaternion authoredLocalRotation;
        private Quaternion rotationStart, rotationDestination;
        private bool rotationTween, positionTween;
        private int motionRevision;
        public Rigidbody PhysicsBody => body != null ? body : physicsBody;
        public PlatformContacts Contacts => contacts != null ? contacts : PhysicsBody != null ? PhysicsBody.GetComponent<PlatformContacts>() : null;
        /// <summary>실제 이동 적용 직전의 몸체/현재/다음 월드 자세. 정지·완료 통지·Reset에는 발생하지 않는다.
        /// 소비자는 몸체를 이동하지 않고 계획된 Collider 교차를 검사한다. 취소/Reset이면 낡은 이동은 적용하지 않는다.</summary>
        public event Action<Rigidbody, Pose, Pose> BeforePhysicsMove;
        private PlatformContacts contacts;
        private readonly List<Collider> occupants = new List<Collider>();
        private Vector3 target;
        private bool hasTarget;
        private enum MotionPath { None, Travel, Effect, Tween }
        private MotionPath path;
        private Vector3 effectOrigin;
        private readonly Dictionary<Component, Vector3> effects = new Dictionary<Component, Vector3>();
        private bool conflictReported;
        private readonly HashSet<Rigidbody> carried = new HashSet<Rigidbody>();
        private PhysicsMaterial originalMaterial;
        private PhysicsMaterial motionMaterial;
        private Vector3 tweenStart, tweenDestination;
        private float tweenDuration, tweenElapsed;
        private bool completionPending;
        private Action tweenCompleted;
        public bool IsTweening => positionTween || rotationTween;
        public Vector3 Velocity { get; private set; }
        private Vector3 PreviousVelocity { get; set; }
        public Vector3 Position => PhysicsBody != null ? PhysicsBody.position : transform.position;
        public Vector3 RestPosition => body != null && body.transform.parent != null
            ? body.transform.parent.TransformPoint(authoredLocalPosition) : authoredLocalPosition;
        private Quaternion RestRotation => body != null && body.transform.parent != null
            ? body.transform.parent.rotation * authoredLocalRotation : authoredLocalRotation;

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
            if (motionMaterial != null) return;
            if (!BindPhysics(physicsBody))
            { Debug.LogError("이동 몸체 자식의 Rigidbody를 연결하고 같은 객체에 지지 BoxCollider와 PlatformContacts를 배치하세요.", this); enabled = false; return; }
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            body.constraints = RigidbodyConstraints.FreezeRotation;
            var collider = contacts.Surface;
            if (collider == null) collider = contacts.GetComponent<BoxCollider>();
            collider.isTrigger = false;
            originalMaterial = collider.sharedMaterial;
            motionMaterial = new PhysicsMaterial("Platform Motion")
            {
                staticFriction = 0f, dynamicFriction = 0f, bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum
            };
            collider.sharedMaterial = motionMaterial;
        }

        private bool BindPhysics(Rigidbody physicalBody)
        {
            var physicalContacts = physicalBody != null ? physicalBody.GetComponent<PlatformContacts>() : null;
            if (physicalBody == null || physicalContacts == null
                || physicalContacts.GetComponent<Rigidbody>() != physicalBody
                || physicalContacts.Surface == null || physicalContacts.Surface.attachedRigidbody != physicalBody
                || physicalContacts.Surface.isTrigger
                || physicalBody.GetComponent<MovingPlatform>() != null || GetComponent<MovingPlatform>() != null
                || (physicalBody.transform == transform || !physicalBody.transform.IsChildOf(transform))) return false;
            int drivers = (GetComponent<LinearShuttlePlatform>() != null ? 1 : 0)
                + (GetComponent<PressurePlatePlatform>() != null ? 1 : 0)
                + (GetComponent<PressureLinkedPlatform>() != null ? 1 : 0)
                + (GetComponent<FeelsGoodOnion.TechSYM.OvenTray.OvenTrayObject>() != null ? 1 : 0);
            if (drivers > 1) return false;
            foreach (var writer in physicalBody.GetComponentsInParent<KinematicPlatformMotion>(true))
                if (writer != this && writer.PhysicsBody == physicalBody) return false;
            if (body != null && body != physicalBody && motionMaterial != null) return false;
            if (body == physicalBody && contacts == physicalContacts) return true;
            CancelMotion();
            body = physicsBody = physicalBody;
            contacts = physicalContacts;
            authoredLocalPosition = body.transform.localPosition;
            authoredLocalRotation = body.transform.localRotation;
            effectOrigin = body.position;
            return true;
        }

        public void ResetForRetry()
        {
            if (body == null) return;
            ResetForRetry(new Pose(RestPosition, RestRotation));
        }

        /// <summary>고정 시작/닫힘 지점으로 같은 물리 몸체를 즉시 복구한다. 진행 이벤트는 보내지 않는다.</summary>
        public void ResetForRetry(Pose worldPose)
        {
            if (!Finite(worldPose.position) || !ValidRotation(worldPose.rotation))
                throw new ArgumentException("복구 자세는 유한한 위치와 유효한 회전이어야 합니다.", nameof(worldPose));
            CancelMotion();
            if (body == null) return;
            // Interpolation can retain the old render Transform after a Rigidbody teleport.
            // Stage reactivation/SyncTransforms must see the same restored pose immediately.
            body.transform.SetPositionAndRotation(worldPose.position, worldPose.rotation);
            body.position = worldPose.position;
            body.rotation = worldPose.rotation;
            effectOrigin = body.position;
            contacts?.ResetForRetry();
        }

        public void CancelMotion()
        {
            CancelTween(); hasTarget = false; path = MotionPath.None;
            effects.Clear(); occupants.Clear(); carried.Clear();
            Velocity = PreviousVelocity = Vector3.zero;
            contacts?.ResetForRetry();
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
        private bool TryTweenTo(Vector3 worldDestination, float duration, Action onCompleted)
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
            positionTween = true;
            rotationTween = false;
            ++motionRevision;
            return true;
        }

        /// <summary>장치의 위치와 회전을 같은 요청/시간/물리 작성자로 진행한다.</summary>
        public bool TryTweenPose(Pose worldDestination, float duration, Action onCompleted)
        {
            if (!ValidRotation(worldDestination.rotation) || !TryTweenTo(worldDestination.position, duration, onCompleted)) return false;
            rotationStart = body.rotation;
            rotationDestination = worldDestination.rotation;
            rotationTween = true;
            return true;
        }

        /// <summary>자신의 트윈과 완료 통지만 취소한다. 남은 시간은 재개를 위해 보존한다.</summary>
        public void CancelTween()
        {
            ++motionRevision;
            positionTween = false;
            rotationTween = false;
            if (path == MotionPath.Tween) path = MotionPath.None;
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
            if (effects.ContainsKey(owner)) return true;
            if (path == MotionPath.None) { effectOrigin = RestPosition; path = MotionPath.Effect; }
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
            // 마지막 효과 제거 뒤 한 물리 스텝에서 기준 위치를 돌려 놓은 다음 경로를 닫는다.
        }

        private void ReportConflict()
        {
            if (conflictReported) return;
            conflictReported = true;
            Debug.LogError("일반 이동, 트윈 이동, 부유·착지 연출은 같은 플랫폼에서 혼용할 수 없습니다.", this);
        }

        private static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        private static bool ValidRotation(Quaternion value) => float.IsFinite(value.x) && float.IsFinite(value.y)
            && float.IsFinite(value.z) && float.IsFinite(value.w)
            && (double)value.x * value.x + (double)value.y * value.y + (double)value.z * value.z + (double)value.w * value.w > 0.000001;

        /// <summary>고정 작성 지점은 소유 기능 루트 안, 실제 몸체와 애니메이션 이동 계층 밖에 둔다.</summary>
        public bool IsFixedPoint(Transform point) => point != null && PhysicsBody != null
            && point != transform && point.IsChildOf(transform)
            && PhysicsBody.transform != transform && PhysicsBody.transform.IsChildOf(transform)
            && !point.IsChildOf(PhysicsBody.transform) && SceneComponentLookup.IsFixedPoint(this, point);

        /// <summary>실제 pose 변화 경계만 분류한다. 같은 회전의 q/-q도 정지다.</summary>
        public static bool HasPoseChange(Pose current, Pose next)
        {
            if (!Finite(current.position) || !Finite(next.position) || !ValidRotation(current.rotation) || !ValidRotation(next.rotation))
                throw new ArgumentException("이동 자세가 유효하지 않습니다.");
            var a = current.rotation; var b = next.rotation;
            bool sameRotation = (double)a.x * b.y == (double)a.y * b.x && (double)a.x * b.z == (double)a.z * b.x
                && (double)a.x * b.w == (double)a.w * b.x && (double)a.y * b.z == (double)a.z * b.y
                && (double)a.y * b.w == (double)a.w * b.y && (double)a.z * b.w == (double)a.w * b.z;
            return current.position.x != next.position.x || current.position.y != next.position.y || current.position.z != next.position.z
                || !sameRotation;
        }

        private bool NotifyBeforeMove(Pose current, Pose next)
        {
            var handlers = BeforePhysicsMove;
            int revision = motionRevision;
            bool failed = false;
            if (handlers != null)
                foreach (Action<Rigidbody, Pose, Pose> handler in handlers.GetInvocationList())
                {
                    try { handler(body, current, next); }
                    catch (Exception error)
                    {
                        failed = true;
                        Debug.LogException(new InvalidOperationException($"발판 '{name}'의 이동 전 검사에 실패했습니다.", error), this);
                    }
                    if (body == null || !isActiveAndEnabled || revision != motionRevision) return false;
                }
            if (failed) { enabled = false; return false; }
            return body != null && isActiveAndEnabled && revision == motionRevision;
        }

        private void FixedUpdate()
        {
            if (!isActiveAndEnabled || body == null || contacts == null || Time.timeScale <= 0f || Time.fixedDeltaTime <= 0f) return;
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
            bool tweenStep = positionTween;
            Quaternion nextRotation = body.rotation;
            if (rotationTween || tweenStep)
            {
                tweenElapsed = Mathf.Min(tweenDuration, tweenElapsed + Time.fixedDeltaTime);
            }
            if (rotationTween)
            {
                nextRotation = Quaternion.Slerp(rotationStart, rotationDestination,
                    DOVirtual.EasedValue(0f, 1f, tweenElapsed / tweenDuration, Ease.OutSine));
            }
            if (tweenStep)
            {
                float fraction = DOVirtual.EasedValue(0f, 1f, tweenElapsed / tweenDuration, Ease.OutSine);
                next = Vector3.LerpUnclamped(tweenStart, tweenDestination, fraction);
            }
            if (carryWithoutInertia)
            {
                next = effectOrigin;
                foreach (var entry in effects)
                    if (entry.Key != null) next += entry.Value;
            }
            Pose currentPose = new Pose(body.position, body.rotation);
            Pose nextPose = new Pose(next, nextRotation);
            if (!Finite(next) || !ValidRotation(nextRotation) || !Finite((next - body.position) / Time.fixedDeltaTime))
            { Debug.LogError("발판의 다음 자세/운반 속도가 유한한 범위를 벗어났습니다.", this); enabled = false; return; }
            if (HasPoseChange(currentPose, nextPose) && !NotifyBeforeMove(currentPose, nextPose)) return;
            Vector3 displacement = next - body.position;
            Velocity = displacement / Time.fixedDeltaTime;
            carried.Clear();
            contacts.CopyOccupants(occupants);
            foreach (Collider other in occupants)
            {
                GameObject actor = PlatformContacts.Actor(other);
                Rigidbody rigid = other.attachedRigidbody;
                if (rigid == body || (rigid != null && !carried.Add(rigid))) continue;
                Vector3 riderDisplacement = rigid != null
                    ? next + nextRotation * Quaternion.Inverse(body.rotation) * (rigid.position - body.position) - rigid.position
                    : displacement;
                // Facade에는 접지면/운반 계약이 없어 물리 컴포넌트 참조를 유지한다.
                // 해당 계약이 제공될 때 대체하며, 입력 차단은 물리 운반을 중단하지 않는다.
                if (actor != null && actor.TryGetComponent<PlatformPassenger>(out var passenger) && passenger.isActiveAndEnabled)
                {
                    var movement = actor.GetComponent<CharacterMovement>();
                    var rigidMovement = actor.GetComponent<PlatformRigidbodyMovement>();
                    bool hasMovement = (movement != null && movement.isActiveAndEnabled)
                        || (rigidMovement != null && rigidMovement.isActiveAndEnabled);
                    bool grounded = (movement != null && movement.isActiveAndEnabled && movement.IsGrounded && movement.GroundCollider == contacts.Surface)
                        || (rigidMovement != null && rigidMovement.isActiveAndEnabled && rigidMovement.IsGrounded && rigidMovement.GroundCollider == contacts.Surface);
                    if (!hasMovement || (carryWithoutInertia && grounded))
                    {
                        Rigidbody rider = other.attachedRigidbody;
                        if (rider != null && !rider.isKinematic) rider.position += riderDisplacement;
                    }
                    passenger.ReceivePlatformVelocity(contacts.Surface, other,
                        carryWithoutInertia ? Vector3.zero : riderDisplacement / Time.fixedDeltaTime,
                        carryWithoutInertia ? Vector3.zero : PreviousVelocity);
                }
                else if (rigid != null && !rigid.isKinematic)
                    rigid.position += riderDisplacement;
            }
            hasTarget = false;
            // 부유 연출은 위치만 옮겨 키네마틱 속도가 플레이어를 밀어내지 않게 한다.
            // Tween도 계산만 하고 실제 Rigidbody 작성은 이 경계 한 곳에서 수행한다.
            if (carryWithoutInertia) body.position = next;
            else body.MovePosition(next);
            if (rotationTween)
                body.MoveRotation(nextRotation);
            if (tweenStep || rotationTween) completionPending = tweenElapsed >= tweenDuration;
            if (carryWithoutInertia && effects.Count == 0) path = MotionPath.None;
            if (completed != null)
                try { completed(); }
                catch (Exception error) { Debug.LogException(new InvalidOperationException($"발판 '{name}'의 이동 완료 통지에 실패했습니다.", error), this); }
        }

        private void OnDisable() => CancelMotion();

        private void OnDestroy()
        {
            CancelTween();
            var collider = contacts != null ? contacts.Surface : null;
            if (collider != null && collider.sharedMaterial == motionMaterial) collider.sharedMaterial = originalMaterial;
            if (motionMaterial != null)
            {
                if (Application.isPlaying) Destroy(motionMaterial);
                else DestroyImmediate(motionMaterial);
            }
        }
    }
}
