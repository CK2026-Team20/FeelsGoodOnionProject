using System;
using System.Collections.Generic;
using Cooked.Authoring;
using Cooked.Session;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FeelsGoodOnion.TechSYM.Platforms
{
    /// <summary>주기와 실제 Collider 침범을 소유한다. Actor 운동과 사망 종결은 Bridge/HMS가 소유한다.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-350)]
    public sealed class PeriodicPlatform : MonoBehaviour
    {
        // 기존 직렬화 번호를 유지한다. Warning은 비충돌 형성 단계다.
        public enum CyclePhase { Visible, Hidden, Warning }
        public enum OverlapKind { None, SupportedTop, Shallow, Fatal }
        public readonly struct OverlapResult
        {
            public OverlapResult(OverlapKind kind, double ratio) { Kind = kind; Ratio = ratio; }
            public OverlapKind Kind { get; }
            public double Ratio { get; }
        }
        [SerializeField, InspectorLabel("지지 충돌체"), Tooltip("실제 발판 몸체의 비 Trigger BoxCollider를 연결합니다.")]
        private BoxCollider supportCollider;
        [SerializeField, InspectorLabel("발판 이동"), Tooltip("이 발판 몸체를 움직이는 KinematicPlatformMotion을 연결합니다. 고정 발판은 비워 둡니다.")]
        private KinematicPlatformMotion motion;
        [SerializeField, InspectorLabel("표시 시간"), Tooltip("밟을 수 있는 시간을 조절합니다(초). 0이면 이 단계를 건너뜁니다.")]
        private float lifetime = 5f;
        [SerializeField, InspectorLabel("소멸 시간"), Tooltip("외형과 충돌이 없는 시간을 조절합니다(초). 0이면 이 단계를 건너뜁니다.")]
        private float hiddenSeconds = 2f;
        [SerializeField, InspectorLabel("형성 시간"), Tooltip("충돌 없이 나타나는 시간을 조절합니다(초). 0이면 이 단계를 건너뜁니다.")]
        private float warningSeconds = 3f;
        [SerializeField, InspectorLabel("시작 위상"), Tooltip("첫 주기의 시작 위치를 조절합니다(초). 0은 첫 유효 단계의 시작입니다.")]
        private float initialPhaseSeconds;
        [SerializeField, InspectorLabel("깜빡임 횟수"), Tooltip("형성 중 깜빡이는 횟수를 조절합니다.")]
        private int blinkCount = 8;
        [SerializeField, InspectorLabel("깜빡임 가속"), Tooltip("형성 후반의 깜빡임 속도 증가를 조절합니다. 1.01~4를 사용합니다.")]
        private float blinkAcceleration = 2f;
        [SerializeField, InspectorLabel("몸체 분리 시간"), Tooltip("깊이 끼인 플레이어를 위로 이동시키는 시간을 조절합니다(초). 피격 클립 길이와 독립입니다.")]
        private float separationSeconds = .35f;

        private BoxCollider support;
        private Renderer[] renderers;
        private bool[] defaults;
        private bool colliderDefault;
        private double elapsed;
        private Collider[] candidates = new Collider[32];
        private readonly HashSet<Rigidbody> examined = new HashSet<Rigidbody>();
        private PlayerBridgeService bridge;
        private KinematicPlatformMotion subscribedMotion;
        public CyclePhase Phase { get; private set; }
        public double CycleSeconds => ValidateCycle(lifetime, hiddenSeconds, warningSeconds);

        public static double ValidateCycle(double visible, double hidden, double forming)
        {
            if (!double.IsFinite(visible) || visible < 0 || !double.IsFinite(hidden) || hidden < 0 ||
                !double.IsFinite(forming) || forming < 0)
                throw new ArgumentOutOfRangeException(nameof(visible), "각 단계는 유한한 0 이상의 시간이어야 합니다.");
            double total = visible + hidden + forming;
            if (!double.IsFinite(total) || total <= 0) throw new ArgumentOutOfRangeException(nameof(visible), "전체 주기는 0보다 커야 합니다.");
            return total;
        }
        public static CyclePhase PhaseAt(double seconds, double visible, double hidden, double forming)
        {
            double total = ValidateCycle(visible, hidden, forming);
            if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            double phase = seconds % total;
            return phase < visible ? CyclePhase.Visible : phase < visible + hidden ? CyclePhase.Hidden : CyclePhase.Warning;
        }
        /// <summary>실제 교차/지지는 native 관측이다. 비율은 MTD가 아닌 현재 몸체의 수직 겹침/높이다.</summary>
        public static OverlapResult ClassifyOverlap(bool intersects, bool normalTop, double bodyBottom, double bodyTop,
            double platformBottom, double platformTop)
        {
            if (!double.IsFinite(bodyBottom) || !double.IsFinite(bodyTop) || bodyTop <= bodyBottom ||
                !double.IsFinite(platformBottom) || !double.IsFinite(platformTop) || platformTop <= platformBottom)
                throw new ArgumentOutOfRangeException(nameof(bodyTop), "몸체와 발판의 유한한 수직 구간이 필요합니다.");
            if (!intersects) return new OverlapResult(OverlapKind.None, 0);
            double overlap = Math.Max(0, Math.Min(bodyTop, platformTop) - Math.Max(bodyBottom, platformBottom));
            double ratio = overlap / (bodyTop - bodyBottom);
            if (normalTop) return new OverlapResult(OverlapKind.SupportedTop, ratio);
            return new OverlapResult(ratio < .25 ? OverlapKind.Shallow : OverlapKind.Fatal, ratio);
        }
        public void BindMaterialization(PlayerBridgeService player)
        {
            if (bridge != player) bridge?.CancelSeparation(this);
            bridge = player;
            RefreshMotionSubscription();
        }
        // T10 연결 이관 후 T12에서 제거할 구형 서명. 외형 이동은 더 이상 분리 수단이 아니다.
        public void BindMaterialization(PlayerBridgeService player, Func<Transform, Transform> visualResolver)
            => BindMaterialization(player);
        private void Awake()
        {
            ValidateAuthoring();
            support = supportCollider;
            if (support == null || support.isTrigger)
            { Debug.LogError("주기 발판의 비 Trigger 지지 충돌체를 연결하세요.", this); enabled = false; return; }
            if (motion != null && (motion.PhysicsBody != support.attachedRigidbody || motion.Contacts == null || motion.Contacts.Surface != support))
            { Debug.LogError("주기 발판의 이동과 지지 충돌체는 같은 물리 몸체여야 합니다.", this); enabled = false; return; }
            colliderDefault = support.enabled;
            renderers = GetComponentsInChildren<Renderer>(true);
            defaults = new bool[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) defaults[i] = renderers[i].enabled;
            elapsed = initialPhaseSeconds % CycleSeconds;
        }
        private void OnEnable()
        {
            RefreshMotionSubscription();
            if (support != null) ApplyPhase(true);
        }
        private void RefreshMotionSubscription()
        {
            var wanted = isActiveAndEnabled ? motion : null;
            if (subscribedMotion == wanted) return;
            if (subscribedMotion != null) subscribedMotion.BeforePhysicsMove -= BeforePhysicsMove;
            subscribedMotion = wanted;
            if (subscribedMotion != null) subscribedMotion.BeforePhysicsMove += BeforePhysicsMove;
        }
        private void FixedUpdate()
        {
            if (support == null || Time.timeScale <= 0f || (bridge != null && bridge.PhysicsPaused)) return;
            elapsed = (elapsed + Time.fixedDeltaTime) % CycleSeconds;
            ApplyPhase(false);
        }
        private void ApplyPhase(bool enabling)
        {
            CyclePhase previous = Phase;
            Phase = PhaseAt(elapsed, lifetime, hiddenSeconds, warningSeconds);
            support.enabled = Phase == CyclePhase.Visible && colliderDefault;
            if (support.enabled && (enabling || previous != CyclePhase.Visible)) ResolveCurrentIntersections();
            bool visible = Phase == CyclePhase.Visible || (Phase == CyclePhase.Warning &&
                Mathf.FloorToInt(Mathf.Pow((float)((elapsed - lifetime - hiddenSeconds) / warningSeconds), blinkAcceleration) * blinkCount * 2f) % 2 == 1);
            for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].enabled = visible && defaults[i];
        }
        public void ResolveCurrentIntersections()
        {
            if (support == null || !support.enabled || bridge == null || bridge.PhysicsPaused) return;
            ResolveIntersections(PhysicsPose(support), false);
        }
        private void BeforePhysicsMove(Rigidbody movingBody, Pose current, Pose next)
        {
            if (support == null || !support.enabled || bridge == null || bridge.PhysicsPaused ||
                movingBody != support.attachedRigidbody) return;
            Pose supportPose = PhysicsPose(support);
            Quaternion delta = next.rotation * Quaternion.Inverse(current.rotation);
            var planned = new Pose(next.position + delta * (supportPose.position - current.position), delta * supportPose.rotation);
            ResolveIntersections(planned, true);
        }
        private void ResolveIntersections(Pose supportPose, bool moving)
        {
            examined.Clear();
            PhysicsScene physics = support.gameObject.scene.GetPhysicsScene();
            if (!physics.IsValid()) throw new InvalidOperationException("발판 소속 물리 씬이 유효하지 않습니다.");
            Vector3 scale = Abs(support.transform.lossyScale);
            Vector3 center = supportPose.position + supportPose.rotation * Vector3.Scale(support.center, support.transform.lossyScale);
            int count;
            while (true)
            {
                count = physics.OverlapBox(center, Vector3.Scale(support.size * .5f, scale), candidates,
                    supportPose.rotation, ~0, QueryTriggerInteraction.Ignore);
                if (count < candidates.Length) break;
                if (candidates.Length >= 4096) throw new InvalidOperationException("주기 발판 교차 후보가 4096개 이상입니다. 누락 판정 대신 배치를 확인하세요.");
                Array.Resize(ref candidates, candidates.Length * 2);
            }
            PlayerFacade expected = bridge.Actor;
            long generation = bridge.ActorGeneration;
            for (int i = 0; i < count; i++)
            {
                Collider other = candidates[i];
                Rigidbody body = other != null ? other.attachedRigidbody : null;
                if (body == null || other == support || !other.enabled || !other.gameObject.activeInHierarchy ||
                    Physics.GetIgnoreCollision(support, other) || Physics.GetIgnoreLayerCollision(support.gameObject.layer, other.gameObject.layer)) continue;
                PlayerFacade player = body.GetComponent<PlayerFacade>();
                PlayerDebris shell = body.GetComponent<PlayerDebris>();
                if (player == null && shell == null) continue;
                if (player != null && (other != body.GetComponent<CapsuleCollider>() || !bridge.IsCurrentActor(player, generation))) continue;
                if (shell != null && (other != body.GetComponent<BoxCollider>() || !bridge.OwnsDebris(shell))) continue;
                Pose bodyShapePose = PhysicsPose(other);
                // Move 통지는 운반 이전이다. 실제 접지/윗면 접촉을 가진 탑승자는 이번 운반 의도를 존중한다.
                bool normalTop = moving && IsSupportedPassenger(other);
                OverlapResult overlap = InspectOverlap(support, supportPose, other, bodyShapePose, normalTop,
                    out Vector3 bodySeparation, out float lift);
                if (overlap.Kind == OverlapKind.None || overlap.Kind == OverlapKind.SupportedTop || !examined.Add(body)) continue;
                if (shell != null) bridge.TrySeparateOwnedDebris(shell, bodySeparation);
                else if (bridge.IsCurrentActor(expected, generation))
                    bridge.TryResolvePlatformOverlap(this, expected, generation, lift, separationSeconds, overlap.Kind == OverlapKind.Fatal);
            }
            Array.Clear(candidates, 0, count);
        }
        private bool IsSupportedPassenger(Collider other)
        {
            if (motion == null || motion.Contacts == null || !motion.Contacts.IsTopContact(other)) return false;
            var body = other.attachedRigidbody;
            var playerMovement = body != null ? body.GetComponent<CharacterMovement>() : null;
            if (playerMovement != null)
                return playerMovement.IsGrounded && playerMovement.GroundCollider == support &&
                    playerMovement.Velocity.y - motion.Velocity.y <= .1f;
            var shellMovement = body != null ? body.GetComponent<PlatformRigidbodyMovement>() : null;
            return shellMovement != null && shellMovement.IsGrounded && shellMovement.GroundCollider == support;
        }
        /// <summary>실제 엔진 교차 검사. 다른 씬 pose에도 동작하며 전역 물리 씬을 전진시키지 않는다.</summary>
        public static OverlapResult InspectOverlap(BoxCollider platform, Pose platformPose, Collider shape, Pose shapePose,
            bool normalTop, out Vector3 bodySeparation, out float liftMetres)
        {
            if (platform == null || shape == null) throw new ArgumentNullException(nameof(shape));
            Bounds platformBounds = ShapeBounds(platform, platformPose);
            Bounds bodyBounds = ShapeBounds(shape, shapePose);
            bool intersects = Physics.ComputePenetration(platform, platformPose.position, platformPose.rotation,
                shape, shapePose.position, shapePose.rotation, out Vector3 direction, out float distance);
            bodySeparation = intersects ? -direction * (distance + .001f) : Vector3.zero;
            liftMetres = intersects ? Mathf.Max(0f, platformBounds.max.y - bodyBounds.min.y) + .005f : 0f;
            bool topTolerance = intersects && bodyBounds.min.y >= platformBounds.max.y - .001f && bodySeparation.y > 0;
            return ClassifyOverlap(intersects, normalTop || topTolerance, bodyBounds.min.y, bodyBounds.max.y,
                platformBounds.min.y, platformBounds.max.y);
        }
        public static Pose PhysicsPose(Collider shape)
        {
            if (shape == null) throw new ArgumentNullException(nameof(shape));
            Rigidbody body = shape.attachedRigidbody;
            if (body == null) return new Pose(shape.transform.position, shape.transform.rotation);
            Quaternion delta = body.rotation * Quaternion.Inverse(body.transform.rotation);
            return new Pose(body.position + delta * (shape.transform.position - body.transform.position), delta * shape.transform.rotation);
        }
        public static Bounds ShapeBounds(Collider shape, Pose pose)
        {
            Vector3 scale = shape.transform.lossyScale;
            if (shape is BoxCollider box)
            {
                Vector3 half = Vector3.Scale(box.size * .5f, Abs(scale));
                Vector3 extent = Abs(pose.rotation * new Vector3(half.x, 0, 0)) +
                    Abs(pose.rotation * new Vector3(0, half.y, 0)) + Abs(pose.rotation * new Vector3(0, 0, half.z));
                return new Bounds(pose.position + pose.rotation * Vector3.Scale(box.center, scale), extent * 2f);
            }
            if (shape is CapsuleCollider capsule)
            {
                int direction = capsule.direction;
                Vector3 sizeScale = Abs(scale);
                float radiusScale = direction == 0 ? Mathf.Max(sizeScale.y, sizeScale.z) :
                    direction == 1 ? Mathf.Max(sizeScale.x, sizeScale.z) : Mathf.Max(sizeScale.x, sizeScale.y);
                float radius = capsule.radius * radiusScale;
                float halfLine = Mathf.Max(0, capsule.height * sizeScale[direction] * .5f - radius);
                Vector3 axis = direction == 0 ? Vector3.right : direction == 1 ? Vector3.up : Vector3.forward;
                Vector3 extent = Abs(pose.rotation * axis) * halfLine + Vector3.one * radius;
                return new Bounds(pose.position + pose.rotation * Vector3.Scale(capsule.center, scale), extent * 2f);
            }
            throw new ArgumentException("주기 판정은 실제 Player Capsule 또는 껍질 Box만 수락합니다.", nameof(shape));
        }
        private static Vector3 Abs(Vector3 value) => new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
        public void ResetForRetry()
        {
            bridge?.CancelSeparation(this);
            examined.Clear();
            elapsed = initialPhaseSeconds % CycleSeconds;
            if (support != null) ApplyPhase(true);
        }
        private void OnDisable()
        {
            RefreshMotionSubscription();
            bridge?.CancelSeparation(this);
            bridge = null;
            examined.Clear();
            if (support != null) support.enabled = false;
        }
        private void ValidateAuthoring()
        {
            _ = CycleSeconds;
            if (!float.IsFinite(initialPhaseSeconds) || initialPhaseSeconds < 0 || blinkCount < 1 || blinkCount > 100 ||
                !float.IsFinite(blinkAcceleration) || blinkAcceleration < 1.01f || blinkAcceleration > 4f ||
                !float.IsFinite(separationSeconds) || separationSeconds <= 0)
                throw new InvalidOperationException("주기 발판의 시작 위상·깜빡임·양수 분리 시간을 확인하세요.");
        }
        private void OnValidate()
        {
            try { ValidateAuthoring(); }
            catch (Exception error) { Debug.LogError(error.Message, this); }
        }
    }
}
