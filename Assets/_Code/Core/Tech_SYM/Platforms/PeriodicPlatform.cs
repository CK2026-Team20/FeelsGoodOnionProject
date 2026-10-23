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
        [SerializeField, InspectorLabel("지지 충돌체"), Tooltip("실제 발판 몸체의 비 Trigger BoxCollider 또는 비 Convex MeshCollider를 연결합니다. 접지각과 캡슐 크기는 현재 플레이어 값을 사용합니다.")]
        private Collider supportCollider;
        [SerializeField, HideInInspector] private MeshCapsuleGeometry.Snapshot collisionGeometry;
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

        private Collider support;
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
        /// <summary>실제 교차/지지는 native 관측이다. 마지막 두 값은 실제 교차의 Y 구간이며 Box AABB가 아니다.</summary>
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

        private void Awake()
        {
            ValidateAuthoring();
            support = supportCollider;
            if (support == null || support.isTrigger || !(support is BoxCollider || support is MeshCollider))
            { Debug.LogError("주기 발판의 비 Trigger 지지 충돌체를 연결하세요.", this); enabled = false; return; }
            if (motion != null && (motion.PhysicsBody != support.attachedRigidbody || motion.Contacts == null || !motion.Contacts.ContainsSurface(support)))
            { Debug.LogError("주기 발판의 이동과 지지 충돌체는 같은 물리 몸체여야 합니다.", this); enabled = false; return; }
            colliderDefault = support.enabled;
            if (support is MeshCollider mesh) collisionGeometry = RequireGeometry(mesh, collisionGeometry);
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
            Bounds localBounds = support is BoxCollider box ? new Bounds(box.center, box.size) : ((MeshCollider)support).sharedMesh.bounds;
            Matrix4x4 plannedMatrix = ColliderMatrix(support, supportPose);
            var queryBounds = new Bounds(plannedMatrix.MultiplyPoint3x4(localBounds.center), Vector3.zero);
            for (int corner = 0; corner < 8; corner++)
                queryBounds.Encapsulate(plannedMatrix.MultiplyPoint3x4(localBounds.center + Vector3.Scale(localBounds.extents,
                    new Vector3((corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1))));
            int count;
            while (true)
            {
                count = physics.OverlapBox(queryBounds.center, queryBounds.extents, candidates,
                    Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
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
                    out Vector3 bodySeparation, out Vector3 correction, collisionGeometry);
                if (overlap.Kind == OverlapKind.None || overlap.Kind == OverlapKind.SupportedTop || !examined.Add(body)) continue;
                // 실제 삼각형 교차를 확인한 뒤에만 기존 보정 소유권을 반환한다. 빈 내부/근접 후보는 취소하지 않는다.
                if (player != null && bridge.HasCorrectionOwner)
                {
                    bridge.CancelCorrection();
                    if (!bridge.IsCurrentActor(expected, generation)) continue;
                }
                if (shell != null) bridge.TrySeparateOwnedDebris(shell, bodySeparation);
                else if (bridge.IsCurrentActor(expected, generation))
                    bridge.TryResolvePlatformOverlap(this, expected, generation, correction, separationSeconds, overlap.Kind == OverlapKind.Fatal);
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
        public static OverlapResult InspectOverlap(Collider platform, Pose platformPose, Collider shape, Pose shapePose,
            bool normalTop, out Vector3 bodySeparation, out Vector3 playerCorrection,
            MeshCapsuleGeometry.Snapshot geometry = null)
        {
            if (platform == null || shape == null) throw new ArgumentNullException(nameof(shape));
            bool intersects = Physics.ComputePenetration(platform, platformPose.position, platformPose.rotation,
                shape, shapePose.position, shapePose.rotation, out Vector3 direction, out float distance);
            bodySeparation = intersects ? -direction * (distance + .001f) : Vector3.zero;
            playerCorrection = Vector3.zero;
            if (!intersects && !(platform is MeshCollider && shape is CapsuleCollider)) return new OverlapResult(OverlapKind.None, 0);
            if (shape is CapsuleCollider capsule)
            {
                WorldCapsule(capsule, shapePose, out Vector3 start, out Vector3 end, out float radius);
                var actorMovement = shape.attachedRigidbody != null ? shape.attachedRigidbody.GetComponent<CharacterMovement>() : null;
                if (actorMovement == null || !actorMovement.isActiveAndEnabled)
                    throw new InvalidOperationException("실제 Capsule 몸체의 활성 HMS 이동 설정이 필요합니다.");
                if (platform is MeshCollider mesh)
                {
                    geometry = RequireGeometry(mesh, geometry);
                    // 현재 Transform의 자식 offset/scale을 보존하고 물리 pose의 이동·회전만 적용한다.
                    var world = geometry.Transform(ColliderMatrix(platform, platformPose));
                    var meshResult = world.Inspect(start, end, radius, actorMovement.MaximumGroundAngle, normalTop,
                        out _, out _, out playerCorrection, actorMovement.AllowHorizontalMovement, actorMovement.AllowDepthMovement);
                    // Player는 correction을, 껍질은 아래 Box 분기의 MTD를 소비한다. Player 출력도 실제 메시 분리와 일치시킨다.
                    bodySeparation = playerCorrection;
                    return meshResult;
                }
                if (!(platform is BoxCollider box)) throw new ArgumentException("주기 발판은 Box 또는 비 Convex Mesh만 지원합니다.", nameof(platform));
                Vector3 center = platformPose.position + platformPose.rotation * Vector3.Scale(box.center, platform.transform.lossyScale);
                var half = Vector3.Scale(box.size * .5f, Abs(platform.transform.lossyScale));
                var result = InspectCapsulePlacement(center, platformPose.rotation, half, start, end, radius,
                    actorMovement.MaximumGroundAngle, normalTop, actorMovement.AllowHorizontalMovement, actorMovement.AllowDepthMovement,
                    out playerCorrection);
                if (result.Kind == OverlapKind.None)
                    throw new InvalidOperationException("PhysX 교차와 실제 Box–Capsule 계산이 일치하지 않습니다.");
                return result;
            }
            if (shape is BoxCollider) // 껍질은 최소 MTD 분리만 소비하며 Player 사망 비율을 사용하지 않는다.
                return new OverlapResult(normalTop ? OverlapKind.SupportedTop : OverlapKind.Shallow, 0);
            throw new ArgumentException("주기 판정은 실제 Player Capsule 또는 껍질 Box만 수락합니다.", nameof(shape));
        }
        private static MeshCapsuleGeometry.Snapshot RequireGeometry(MeshCollider mesh, MeshCapsuleGeometry.Snapshot geometry)
        {
            if (mesh.convex || mesh.sharedMesh == null)
                throw new InvalidOperationException("주기 발판은 실제 삼각형을 사용하는 비 Convex MeshCollider가 필요합니다.");
            if (geometry != null && geometry.Matches(mesh.sharedMesh)) return geometry;
            if (!mesh.sharedMesh.isReadable)
                throw new InvalidOperationException("주기 발판의 읽기 불가 메시 CPU 사본이 없습니다. Editor에서 BakeCollisionGeometry를 호출하고 프리팹을 저장하세요. 원본 import 변경은 필요하지 않습니다.");
            return MeshCapsuleGeometry.Snapshot.Capture(mesh.sharedMesh);
        }
        private static Matrix4x4 ColliderMatrix(Collider collider, Pose pose) =>
            Matrix4x4.TRS(pose.position, pose.rotation, Vector3.one) *
            Matrix4x4.TRS(collider.transform.position, collider.transform.rotation, Vector3.one).inverse * collider.transform.localToWorldMatrix;
#if UNITY_EDITOR
        /// <summary>프리팹 이관 담당자가 연결 완료 후 호출하고 저장한다. 원본 메시/import는 변경하지 않는다.</summary>
        public void BakeCollisionGeometry()
        {
            RefreshCollisionGeometry();
        }
        /// <summary>Editor 메인 스레드에서만 호출한다. 빈 참조를 채우지 않고 변경된 파생 데이터만 교체한다.</summary>
        public bool RefreshCollisionGeometry()
        {
            var fresh = supportCollider is MeshCollider mesh && mesh.sharedMesh != null
                ? MeshCapsuleGeometry.Snapshot.Capture(mesh.sharedMesh) : null;
            if (fresh == null && collisionGeometry == null) return false;
            if (fresh != null && collisionGeometry != null && collisionGeometry.ContentEquals(fresh)) return false;
            collisionGeometry = fresh;
            return true;
        }
#endif
        /// <summary>보정 전 실제 교차를 판정하고, 얕을 때만 현재 HMS 접지각/이동축으로 윗면 또는 지지 가능한 가장자리를 구한다.</summary>
        public static OverlapResult InspectCapsulePlacement(Vector3 boxCenter, Quaternion boxRotation, Vector3 boxHalfExtents,
            Vector3 capsuleStart, Vector3 capsuleEnd, float capsuleRadius, float maximumGroundAngle, bool normalTop,
            bool horizontalAllowed, bool depthAllowed, out Vector3 correction)
        {
            var result = InspectCapsuleBox(boxCenter, boxRotation, boxHalfExtents, capsuleStart, capsuleEnd,
                capsuleRadius, normalTop, out _, out _, out float exit);
            correction = Vector3.zero;
            if (result.Kind == OverlapKind.Fatal) correction = new Vector3(0, exit + .001f, 0);
            else if (result.Kind == OverlapKind.Shallow)
                correction = TopSupportCorrection(boxCenter, boxRotation, boxHalfExtents, capsuleStart, capsuleEnd,
                    capsuleRadius, maximumGroundAngle, horizontalAllowed, depthAllowed, exit);
            return result;
        }
        private static Vector3 TopSupportCorrection(Vector3 boxCenter, Quaternion rotation, Vector3 boxHalf,
            Vector3 capsuleStart, Vector3 capsuleEnd, float radius, float maximumGroundAngle, bool horizontalAllowed, bool depthAllowed,
            float upwardExit)
        {
            if (!float.IsFinite(maximumGroundAngle) || maximumGroundAngle < 0 || maximumGroundAngle > 85)
                throw new ArgumentOutOfRangeException(nameof(maximumGroundAngle), "현재 HMS의 유효한 접지각이 필요합니다.");
            double norm = Math.Sqrt((double)rotation.x * rotation.x + (double)rotation.y * rotation.y +
                (double)rotation.z * rotation.z + (double)rotation.w * rotation.w);
            double qx = rotation.x / norm, qy = rotation.y / norm, qz = rotation.z / norm, qw = rotation.w / norm;
            var x = new GeometryPoint(1 - 2 * (qy * qy + qz * qz), 2 * (qx * qy + qz * qw), 2 * (qx * qz - qy * qw));
            var normal = new GeometryPoint(2 * (qx * qy - qz * qw), 1 - 2 * (qx * qx + qz * qz), 2 * (qy * qz + qx * qw));
            var z = new GeometryPoint(2 * (qx * qz + qy * qw), 2 * (qy * qz - qx * qw), 1 - 2 * (qx * qx + qy * qy));
            // 이 Box의 local +Y가 작성한 윗면이다. 실제 Actor 설정 밖의 경사는 접지 성공으로 바꾸지 않는다.
            if (normal.Y <= 0 || normal.Y + 1e-7 < Math.Cos(maximumGroundAngle * Math.PI / 180))
                throw new InvalidOperationException("주기 발판의 실제 윗면이 현재 HMS 접지각 밖입니다. 지지 충돌체의 회전을 확인하세요.");
            var start = new GeometryPoint(capsuleStart); var end = new GeometryPoint(capsuleEnd);
            // 회전된 Capsule도 축 전체가 윗면의 바깥쪽에 있도록 법선에 가장 가까운 축 끝을 사용한다.
            var low = GeometryPoint.Dot(start, normal) <= GeometryPoint.Dot(end, normal) ? start : end;
            double skin = radius * .002; // 현재 몸 크기에 비례한 물리 여유; 판정 비율에는 사용하지 않는다.
            var offset = new GeometryPoint(boxCenter) + normal * (boxHalf.y + radius + skin);
            double limitX = boxHalf.x - Math.Min(skin, boxHalf.x * .01);
            double limitZ = boxHalf.z - Math.Min(skin, boxHalf.z * .01);
            // 상면 평면의 2차원 최근접점. 사각형/상향/잠긴 XZ 축의 반평면을 유한 후보로 교차한다.
            Span<GeometryPoint> constraints = stackalloc GeometryPoint[9];
            constraints[0] = new GeometryPoint(1, 0, limitX); constraints[1] = new GeometryPoint(-1, 0, limitX);
            constraints[2] = new GeometryPoint(0, 1, limitZ); constraints[3] = new GeometryPoint(0, -1, limitZ);
            constraints[4] = new GeometryPoint(-x.Y, -z.Y, offset.Y - low.Y); int count = 5;
            if (!horizontalAllowed)
            {
                constraints[count++] = new GeometryPoint(x.X, z.X, low.X - offset.X);
                constraints[count++] = new GeometryPoint(-x.X, -z.X, offset.X - low.X);
            }
            if (!depthAllowed)
            {
                constraints[count++] = new GeometryPoint(x.Z, z.Z, low.Z - offset.Z);
                constraints[count++] = new GeometryPoint(-x.Z, -z.Z, offset.Z - low.Z);
            }
            double u = GeometryPoint.Dot(low - offset, x), v = GeometryPoint.Dot(low - offset, z);
            double tolerance = 1e-10 * Math.Max(radius, Math.Max(boxHalf.x, Math.Max(boxHalf.y, boxHalf.z)));
            double best = double.PositiveInfinity, bestU = 0, bestV = 0;
            ConsiderTopCandidate(u, v, u, v, constraints, count, tolerance, ref best, ref bestU, ref bestV);
            for (int i = 0; i < count; i++)
            {
                var a = constraints[i]; double length = a.X * a.X + a.Y * a.Y;
                if (length > 0)
                {
                    double distance = (a.X * u + a.Y * v - a.Z) / length;
                    ConsiderTopCandidate(u - a.X * distance, v - a.Y * distance, u, v,
                        constraints, count, tolerance, ref best, ref bestU, ref bestV);
                }
                for (int j = 0; j < i; j++)
                {
                    var b = constraints[j]; double determinant = a.X * b.Y - a.Y * b.X;
                    if (determinant == 0) continue;
                    ConsiderTopCandidate((a.Z * b.Y - a.Y * b.Z) / determinant, (a.X * b.Z - a.Z * b.X) / determinant,
                        u, v, constraints, count, tolerance, ref best, ref bestU, ref bestV);
                }
            }
            if (!double.IsFinite(best))
            {
                // 잠긴 축에서 중심을 상면 안쪽으로 옮길 수 없어도 가장자리 법선은 실제 지지각 안일 수 있다.
                // 이미 구한 상향 이탈점과 같은 축–OBB 최근접 계산을 사용하며 XZ 제약을 해제하지 않는다.
                var lift = new GeometryPoint(0, upwardExit, 0);
                var center = new GeometryPoint(boxCenter); var half = new GeometryPoint(boxHalf);
                double distance = SegmentBoxDistance(start + lift, end + lift, center, x, normal, z, half,
                    out var closestBox, out var closestAxis);
                var contactNormal = closestAxis - closestBox;
                if (distance > 0 && Math.Abs(GeometryPoint.Dot(closestBox - center, normal) - half.Y) <= tolerance &&
                    GeometryPoint.Dot(contactNormal, normal) > 0 &&
                    contactNormal.Y / Math.Sqrt(distance) + 1e-7 >= Math.Cos(maximumGroundAngle * Math.PI / 180))
                    return new Vector3(0, (float)(upwardExit + skin), 0);
                throw new InvalidOperationException("현재 HMS의 고정 이동축과 상향 조건으로 발판 윗면에 착지할 수 없습니다. 실제 지원면 배치를 확인하세요.");
            }
            var shift = offset + x * bestU + z * bestV - low;
            return new Vector3(horizontalAllowed ? (float)shift.X : 0, (float)Math.Max(0, shift.Y), depthAllowed ? (float)shift.Z : 0);
        }
        private static void ConsiderTopCandidate(double u, double v, double fromU, double fromV,
            Span<GeometryPoint> constraints, int count, double tolerance, ref double best, ref double bestU, ref double bestV)
        {
            if (!double.IsFinite(u) || !double.IsFinite(v)) return;
            for (int i = 0; i < count; i++)
                if (constraints[i].X * u + constraints[i].Y * v - constraints[i].Z > tolerance) return;
            double distance = (u - fromU) * (u - fromU) + (v - fromV) * (v - fromV);
            if (distance < best) { best = distance; bestU = u; bestV = v; }
        }
        private static void WorldCapsule(CapsuleCollider capsule, Pose pose, out Vector3 start, out Vector3 end, out float radius)
        {
            int direction = capsule.direction;
            Vector3 scale = capsule.transform.lossyScale;
            Vector3 sizeScale = Abs(scale);
            float radiusScale = direction == 0 ? Mathf.Max(sizeScale.y, sizeScale.z) :
                direction == 1 ? Mathf.Max(sizeScale.x, sizeScale.z) : Mathf.Max(sizeScale.x, sizeScale.y);
            radius = capsule.radius * radiusScale;
            float halfLine = Mathf.Max(0, capsule.height * sizeScale[direction] * .5f - radius);
            Vector3 axis = direction == 0 ? Vector3.right : direction == 1 ? Vector3.up : Vector3.forward;
            Vector3 center = pose.position + pose.rotation * Vector3.Scale(capsule.center, scale);
            Vector3 offset = pose.rotation * axis * halfLine;
            start = center - offset; end = center + offset;
        }
        public static Pose PhysicsPose(Collider shape)
        {
            if (shape == null) throw new ArgumentNullException(nameof(shape));
            Rigidbody body = shape.attachedRigidbody;
            if (body == null) return new Pose(shape.transform.position, shape.transform.rotation);
            Quaternion delta = body.rotation * Quaternion.Inverse(body.transform.rotation);
            return new Pose(body.position + delta * (shape.transform.position - body.transform.position), delta * shape.transform.rotation);
        }
        /// <summary>Box∩Capsule의 실제 world-Y 투영과 상향 이탈 거리. 축 선분과 반지름은 실제 scale/pose 값이다.</summary>
        public static OverlapResult InspectCapsuleBox(Vector3 boxCenter, Quaternion boxRotation, Vector3 boxHalfExtents,
            Vector3 capsuleStart, Vector3 capsuleEnd, float capsuleRadius, bool normalTop,
            out double intersectionBottom, out double intersectionTop, out float upwardExit)
        {
            var center = new GeometryPoint(boxCenter); var half = new GeometryPoint(boxHalfExtents);
            var start = new GeometryPoint(capsuleStart); var end = new GeometryPoint(capsuleEnd);
            double qx = boxRotation.x, qy = boxRotation.y, qz = boxRotation.z, qw = boxRotation.w;
            double norm = Math.Sqrt(qx * qx + qy * qy + qz * qz + qw * qw);
            if (!center.Finite || !half.Finite || !start.Finite || !end.Finite ||
                half.X <= 0 || half.Y <= 0 || half.Z <= 0 || !float.IsFinite(capsuleRadius) || capsuleRadius <= 0 ||
                !double.IsFinite(norm) || norm <= 0) throw new ArgumentOutOfRangeException(nameof(boxHalfExtents));
            qx /= norm; qy /= norm; qz /= norm; qw /= norm;
            var x = new GeometryPoint(1 - 2 * (qy * qy + qz * qz), 2 * (qx * qy + qz * qw), 2 * (qx * qz - qy * qw));
            var y = new GeometryPoint(2 * (qx * qy - qz * qw), 1 - 2 * (qx * qx + qz * qz), 2 * (qy * qz + qx * qw));
            var z = new GeometryPoint(2 * (qx * qz + qy * qw), 2 * (qy * qz - qx * qw), 1 - 2 * (qx * qx + qy * qy));
            intersectionBottom = intersectionTop = 0; upwardExit = 0;
            double radiusSquared = (double)capsuleRadius * capsuleRadius;
            // double 계산의 거리 제곱 오차만 허용한다. 몸 높이 비율의 .25 경계는 그대로 소비한다.
            double contactLimit = radiusSquared + 1e-12 * Math.Max(1, radiusSquared);
            if (SegmentBoxDistance(start, end, center, x, y, z, half, out var seed, out _) > contactLimit)
                return new OverlapResult(OverlapKind.None, 0);
            Span<GeometryPoint> corners = stackalloc GeometryPoint[8];
            Span<GeometryPoint> section = stackalloc GeometryPoint[12];
            for (int i = 0; i < 8; i++) corners[i] = center + x * ((i & 1) == 0 ? -half.X : half.X) +
                y * ((i & 2) == 0 ? -half.Y : half.Y) + z * ((i & 4) == 0 ? -half.Z : half.Z);
            double boxBottom = corners[0].Y, boxTop = boxBottom;
            for (int i = 1; i < 8; i++) { boxBottom = Math.Min(boxBottom, corners[i].Y); boxTop = Math.Max(boxTop, corners[i].Y); }
            double bodyBottom = Math.Min(start.Y, end.Y) - capsuleRadius;
            double bodyTop = Math.Max(start.Y, end.Y) + capsuleRadius;
            double low = Math.Max(boxBottom, bodyBottom), high = Math.Min(boxTop, bodyTop);
            if (high <= low) return new OverlapResult(OverlapKind.None, 0);
            double seedY = Clamp(seed.Y, low, high);
            // 수평 Box 단면은 볼록 다각형이다. Capsule 축 선분까지의 거리가 r 이하인 Y도 한 구간이다.
            // 48회씩 유한 이분 탐색하며 외측 bracket을 반환해 정확한 .25를 아래로 반올림하지 않는다.
            intersectionBottom = low;
            if (SectionDistance(low, corners, section, start, end) > contactLimit)
            {
                double outside = low, inside = seedY;
                for (int i = 0; i < 48; i++)
                {
                    double mid = (outside + inside) * .5;
                    if (SectionDistance(mid, corners, section, start, end) <= contactLimit) inside = mid; else outside = mid;
                }
                intersectionBottom = outside;
            }
            intersectionTop = high;
            if (SectionDistance(high, corners, section, start, end) > contactLimit)
            {
                double inside = seedY, outside = high;
                for (int i = 0; i < 48; i++)
                {
                    double mid = (inside + outside) * .5;
                    if (SectionDistance(mid, corners, section, start, end) <= contactLimit) inside = mid; else outside = mid;
                }
                intersectionTop = outside;
            }
            // AABB는 이탈 탐색의 유효 상한일 뿐, 최종 윗면/거리/침투 구간으로 사용하지 않는다.
            double liftInside = 0, liftOutside = Math.Max(0, boxTop - bodyBottom) + Math.Max(1e-6, capsuleRadius * 1e-6);
            for (int i = 0; i < 48; i++)
            {
                double mid = (liftInside + liftOutside) * .5;
                var shift = new GeometryPoint(0, mid, 0);
                if (SegmentBoxDistance(start + shift, end + shift, center, x, y, z, half, out _, out _) <= contactLimit) liftInside = mid;
                else liftOutside = mid;
            }
            upwardExit = (float)liftOutside;
            return ClassifyOverlap(true, normalTop, bodyBottom, bodyTop, intersectionBottom, intersectionTop);
        }
        // 계산용 값과 고정 stack scratch만 사용한다. Native 임시 Collider와 프레임별 GC는 만들지 않는다.
        private readonly struct GeometryPoint
        {
            public readonly double X, Y, Z;
            public GeometryPoint(double x, double y, double z) { X = x; Y = y; Z = z; }
            public GeometryPoint(Vector3 value) : this(value.x, value.y, value.z) { }
            public bool Finite => double.IsFinite(X) && double.IsFinite(Y) && double.IsFinite(Z);
            public double LengthSquared => X * X + Y * Y + Z * Z;
            public double Axis(int axis) => axis == 0 ? X : axis == 1 ? Y : Z;
            public static GeometryPoint operator +(GeometryPoint a, GeometryPoint b) => new GeometryPoint(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
            public static GeometryPoint operator -(GeometryPoint a, GeometryPoint b) => new GeometryPoint(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
            public static GeometryPoint operator *(GeometryPoint a, double scale) => new GeometryPoint(a.X * scale, a.Y * scale, a.Z * scale);
            public static double Dot(GeometryPoint a, GeometryPoint b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        }
        private static double Clamp(double value, double low, double high) => Math.Max(low, Math.Min(high, value));
        private static double SegmentBoxDistance(GeometryPoint start, GeometryPoint end, GeometryPoint center,
            GeometryPoint x, GeometryPoint y, GeometryPoint z, GeometryPoint half, out GeometryPoint closestBox, out GeometryPoint closestAxis)
        {
            var local = new GeometryPoint(GeometryPoint.Dot(start - center, x), GeometryPoint.Dot(start - center, y), GeometryPoint.Dot(start - center, z));
            var delta = new GeometryPoint(GeometryPoint.Dot(end - start, x), GeometryPoint.Dot(end - start, y), GeometryPoint.Dot(end - start, z));
            Span<double> cuts = stackalloc double[8]; cuts[0] = 0; cuts[1] = 1; int count = 2;
            for (int axis = 0; axis < 3; axis++) if (delta.Axis(axis) != 0)
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    double t = (sign * half.Axis(axis) - local.Axis(axis)) / delta.Axis(axis);
                    if (t > 0 && t < 1) cuts[count++] = t;
                }
            for (int i = 1; i < count; i++) { double v = cuts[i]; int j = i - 1; while (j >= 0 && cuts[j] > v) { cuts[j + 1] = cuts[j]; j--; } cuts[j + 1] = v; }
            double best = double.PositiveInfinity; var bestLocal = default(GeometryPoint); var bestAxis = default(GeometryPoint);
            for (int i = 0; i < count - 1; i++)
            {
                double midpoint = (cuts[i] + cuts[i + 1]) * .5, a = 0, b = 0;
                for (int axis = 0; axis < 3; axis++)
                {
                    double p = local.Axis(axis), d = delta.Axis(axis), h = half.Axis(axis), sample = p + d * midpoint;
                    if (Math.Abs(sample) <= h) continue;
                    double offset = p - (sample < 0 ? -h : h); a += d * d; b += d * offset;
                }
                double t = a > 0 ? Clamp(-b / a, cuts[i], cuts[i + 1]) : cuts[i];
                var point = local + delta * t;
                var clamped = new GeometryPoint(Clamp(point.X, -half.X, half.X), Clamp(point.Y, -half.Y, half.Y), Clamp(point.Z, -half.Z, half.Z));
                double distance = (point - clamped).LengthSquared;
                if (distance < best) { best = distance; bestLocal = clamped; bestAxis = point; }
            }
            closestBox = center + x * bestLocal.X + y * bestLocal.Y + z * bestLocal.Z;
            closestAxis = center + x * bestAxis.X + y * bestAxis.Y + z * bestAxis.Z;
            return best;
        }
        private static double SectionDistance(double height, Span<GeometryPoint> corners, Span<GeometryPoint> section,
            GeometryPoint start, GeometryPoint end)
        {
            int count = 0;
            for (int i = 0; i < 8; i++) for (int bit = 1; bit <= 4; bit <<= 1) if ((i & bit) == 0)
            {
                var a = corners[i]; var b = corners[i | bit];
                if (height < Math.Min(a.Y, b.Y) || height > Math.Max(a.Y, b.Y)) continue;
                if (a.Y == b.Y) { AddSection(section, ref count, a); AddSection(section, ref count, b); }
                else AddSection(section, ref count, a + (b - a) * ((height - a.Y) / (b.Y - a.Y)));
            }
            if (count == 0) return double.PositiveInfinity;
            if (count == 1) return PointSegmentDistance(section[0], start, end);
            if (count == 2) return SegmentDistance(start, end, section[0], section[1]);
            var centroid = default(GeometryPoint);
            for (int i = 0; i < count; i++) centroid += section[i] * (1.0 / count);
            for (int i = 1; i < count; i++)
            {
                var v = section[i]; double angle = Math.Atan2(v.Z - centroid.Z, v.X - centroid.X); int j = i - 1;
                while (j >= 0 && Math.Atan2(section[j].Z - centroid.Z, section[j].X - centroid.X) > angle)
                { section[j + 1] = section[j]; j--; }
                section[j + 1] = v;
            }
            if (end.Y != start.Y)
            {
                double t = (height - start.Y) / (end.Y - start.Y);
                if (t >= 0 && t <= 1 && InsideSection(start + (end - start) * t, section, count)) return 0;
            }
            double best = Math.Min(PointSectionDistance(start, height, section, count), PointSectionDistance(end, height, section, count));
            for (int i = 0; i < count; i++) best = Math.Min(best, SegmentDistance(start, end, section[i], section[(i + 1) % count]));
            return best;
        }
        private static void AddSection(Span<GeometryPoint> section, ref int count, GeometryPoint point)
        {
            for (int i = 0; i < count; i++) if ((section[i] - point).LengthSquared <= 1e-24) return;
            section[count++] = point;
        }
        private static bool InsideSection(GeometryPoint point, Span<GeometryPoint> section, int count)
        {
            for (int i = 0; i < count; i++)
            {
                var edge = section[(i + 1) % count] - section[i]; var offset = point - section[i];
                if (edge.X * offset.Z - edge.Z * offset.X < 0) return false;
            }
            return true;
        }
        private static double PointSectionDistance(GeometryPoint point, double height, Span<GeometryPoint> section, int count)
        {
            if (InsideSection(point, section, count)) return (point.Y - height) * (point.Y - height);
            double best = double.PositiveInfinity;
            for (int i = 0; i < count; i++) best = Math.Min(best, PointSegmentDistance(point, section[i], section[(i + 1) % count]));
            return best;
        }
        private static double PointSegmentDistance(GeometryPoint point, GeometryPoint start, GeometryPoint end)
        {
            var delta = end - start; double length = delta.LengthSquared;
            double t = length > 0 ? Clamp(GeometryPoint.Dot(point - start, delta) / length, 0, 1) : 0;
            return (point - start - delta * t).LengthSquared;
        }
        private static double SegmentDistance(GeometryPoint p, GeometryPoint q, GeometryPoint a, GeometryPoint b)
        {
            var u = q - p; var v = b - a; var w = p - a;
            double uu = u.LengthSquared, vv = v.LengthSquared, uw = GeometryPoint.Dot(u, w), vw = GeometryPoint.Dot(v, w);
            if (uu == 0) return PointSegmentDistance(p, a, b);
            if (vv == 0) return PointSegmentDistance(a, p, q);
            double uv = GeometryPoint.Dot(u, v), denominator = uu * vv - uv * uv;
            double s = denominator > 0 ? Clamp((uv * vw - uw * vv) / denominator, 0, 1) : 0;
            double t = (uv * s + vw) / vv;
            if (t < 0) { t = 0; s = Clamp(-uw / uu, 0, 1); }
            else if (t > 1) { t = 1; s = Clamp((uv - uw) / uu, 0, 1); }
            return (w + u * s - v * t).LengthSquared;
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
