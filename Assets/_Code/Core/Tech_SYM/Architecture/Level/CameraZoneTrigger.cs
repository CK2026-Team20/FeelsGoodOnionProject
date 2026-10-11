using System.Collections.Generic;
using Cooked.Authoring;
using Cooked.Integration;
using UnityEngine;
using DG.Tweening;
using Cooked.Session;
using System;
namespace Cooked.Level
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class CameraZoneTrigger : MonoBehaviour
    {
        [SerializeField, InspectorLabel("카메라 방식"), Tooltip("이 구간의 측면·통로·쿼터 구도를 선택하세요.")]
        private CameraMovementMode mode = CameraMovementMode.Side;
        [SerializeField, InspectorLabel("정렬 지점"), Tooltip("일반 측면의 Z 정렬 앵커입니다. 현재 X·Y는 보존하며 이 지점의 Y는 사용하지 않습니다. 청록색으로 표시됩니다.")]
        private Transform alignmentPoint;
        [SerializeField, InspectorLabel("플레이어 안전 지점"), Tooltip("통로 전환의 실제 ActorBody 중심 목표입니다. Capsule이 바닥에 침투하지 않게 높이를 배치하고 이동 몸체 밖에 두세요. 청록색 안전 범위로 표시됩니다.")]
        private Transform safeAnchor;
        [SerializeField, InspectorLabel("차단 몸체"), Tooltip("이 전환 Root 아래의 전용 Kinematic Rigidbody를 연결하세요. 추격 문과 공유하지 않습니다.")]
        private Rigidbody blockerBody;
        [SerializeField, InspectorLabel("차단 목표"), Tooltip("움직이는 몸체 밖, 전환 Root 아래의 고정 목표를 연결하세요. 주황색 경로와 범위로 표시됩니다.")]
        private Transform blockerTarget;
        [SerializeField, InspectorLabel("카메라 전환 시간"), Tooltip("구도 전환 시간입니다(초)."), Min(.01f)]
        private float transitionSeconds = .6f;
        [SerializeField, InspectorLabel("플레이어 이동 시간"), Tooltip("플레이어 안전 보정 시간입니다(초)."), Min(.01f)]
        private float playerMoveSeconds = .6f;
        [SerializeField, InspectorLabel("차단 이동 시간"), Tooltip("안전 보정 뒤 차단체가 이동할 시간입니다(초)."), Min(.01f)]
        private float blockerMoveSeconds = .6f;
        [SerializeField, InspectorLabel("플레이어 이동 곡선"), Tooltip("안전 지점까지 이동 속도의 변화를 선택하세요.")]
        private Ease playerEase = Ease.InOutSine;
        [SerializeField, InspectorLabel("차단 이동 곡선"), Tooltip("차단 목표까지 이동 속도의 변화를 선택하세요.")]
        private Ease blockerEase = Ease.InOutSine;
        private Pose initialBlockerPose;
        private bool initialBlockerCaptured;
        private Vector3 playerStart;
        public bool BlockerCompleted { get; private set; }
        public bool HasBlocker => blockerBody != null;
        public Rigidbody BlockerBody => blockerBody;
        public Vector3 BlockerGoal => blockerTarget.position;
        public float TransitionSeconds => transitionSeconds;
        public float PlayerMoveSeconds => playerMoveSeconds;
        public float BlockerMoveSeconds => blockerMoveSeconds;
        public Ease PlayerEase => playerEase;
        public Ease BlockerEase => blockerEase;
        private LevelCameraService cameraService;
        private PlayerFacade actor;
        private StageService stageOwner;
        private long bindingRevision;
        private readonly HashSet<Collider> contacts = new HashSet<Collider>();
        private bool requestSubmitted;
        internal long BindingRevision => bindingRevision;
        public CameraMovementMode Mode => mode;
        public Transform AlignmentPoint => alignmentPoint;
        public Transform SafeAnchor => safeAnchor;
        internal bool IsBindingCurrent(LevelCameraService camera, PlayerFacade player) => isActiveAndEnabled &&
            cameraService == camera && actor == player && stageOwner != null && stageOwner.IsBound && stageOwner.Actor == player;
        internal bool IsBoundTo(LevelCameraService camera, PlayerFacade player) => isActiveAndEnabled && player != null &&
            ReferenceEquals(cameraService, camera) && ReferenceEquals(actor, player) && !player.IsDead &&
            (stageOwner == null || stageOwner.CanDispatch(player));
        public CameraZoneData Zone => new CameraZoneData(Mode, ResolveAlignment());
        public CameraZoneRequest Resolve() => new CameraZoneRequest(this, Zone);
        private CameraZoneRequest ResolvedZone => Resolve();
        public Vector3 AlignmentPosition => ResolvedZone.AlignmentPosition;
        private Vector3 ResolveAlignment()
        {
            if (alignmentPoint == null || alignmentPoint == transform || !alignmentPoint.IsChildOf(transform.parent != null ? transform.parent : transform))
                throw new System.InvalidOperationException("전환 Root 내부의 고정 정렬 Transform을 연결하세요.");
            SceneComponentLookup.RequireFixedPoint(this, alignmentPoint, "정렬 지점");
            return alignmentPoint.position;
        }
        public void Bind(LevelCameraService camera, PlayerFacade player)
        {
            if (ReferenceEquals(cameraService, camera) && ReferenceEquals(actor, player)) return;
            Unbind();
            ValidateSetup();
            var owners = SceneComponentLookup.FindAll<StageService>(gameObject.scene);
            if (owners.Length > 1) throw new System.InvalidOperationException("카메라 구간의 씬에는 StageService가 하나여야 합니다.");
            stageOwner = owners.Length == 1 ? owners[0] : null;
            cameraService = camera; actor = player;
            playerStart = player != null ? player.transform.position : Vector3.zero;
        }
        /// <summary>Query authored volume at actual restored Position, never from a saved checkpoint ID.</summary>
        public bool TryResolveAtPosition(Vector3 position, out CameraZoneRequest request)
        {
            request = default;
            if (!isActiveAndEnabled || !CameraZoneRequest.IsFinite(position) ||
                !TryGetComponent<BoxCollider>(out var volume) || !volume.enabled) return false;
            var local = transform.InverseTransformPoint(position) - volume.center;
            if (!CameraZoneRequest.IsFinite(local) || Mathf.Abs(local.x) > volume.size.x * .5f ||
                Mathf.Abs(local.y) > volume.size.y * .5f || Mathf.Abs(local.z) > volume.size.z * .5f) return false;
            request = ResolvedZone;
            return true;
        }
        public Vector3 PlayerGoal(Vector3 current) => ResolvePlayerGoal(Mode, HasBlocker, current, HasBlocker ? safeAnchor.position : ResolveAlignment());
        public static Vector3 ResolvePlayerGoal(CameraMovementMode mode, bool passage, Vector3 current, Vector3 anchor)
        {
            CameraZoneRequest.Validate(mode, anchor);
            if (!CameraZoneRequest.IsFinite(current)) throw new ArgumentOutOfRangeException(nameof(current));
            return passage ? anchor : mode == CameraMovementMode.Side ? new Vector3(current.x, current.y, anchor.z) : current;
        }
        public void ValidateSetup()
        {
            _ = Zone;
            foreach (float duration in new[] { transitionSeconds, playerMoveSeconds, blockerMoveSeconds })
                if (!float.IsFinite(duration) || duration < .01f) throw new InvalidOperationException("전환 시간은 0.01초 이상의 유한 값이어야 합니다.");
            // Overshoot/elastic/back/custom curves invalidate the swept safety path.
            if (!SafeEase(playerEase) || !SafeEase(blockerEase)) throw new InvalidOperationException("이동 곡선은 Linear/Sine/Quad/Cubic만 사용하세요.");
            var volume = GetComponent<BoxCollider>();
            SceneComponentLookup.RequireFixedPoint(this, transform, "전환 Volume");
            if (!volume.isTrigger) throw new InvalidOperationException("카메라 Volume은 Trigger여야 합니다.");
            if ((blockerBody == null) != (blockerTarget == null)) throw new InvalidOperationException("차단 몸체와 목표를 함께 연결하세요.");
            if (!HasBlocker) return;
            Transform root = transform.parent;
            if (root == null || !blockerBody.transform.IsChildOf(root) || blockerBody.transform == root ||
                safeAnchor == null || !safeAnchor.IsChildOf(root) || !blockerTarget.IsChildOf(root) ||
                safeAnchor.IsChildOf(blockerBody.transform) || blockerTarget.IsChildOf(blockerBody.transform) ||
                transform.IsChildOf(blockerBody.transform) || !blockerBody.isKinematic || !blockerBody.gameObject.activeInHierarchy)
                throw new InvalidOperationException("고정 Volume·두 앵커와 Kinematic BlockerBody를 전환 Root 아래에 나란히 배치하세요.");
            SceneComponentLookup.RequireFixedPoint(this, safeAnchor, "플레이어 안전 지점");
            SceneComponentLookup.RequireFixedPoint(this, blockerTarget, "차단 목표");
            var box = blockerBody.GetComponent<BoxCollider>();
            if (box == null || !box.enabled || box.isTrigger || box.attachedRigidbody != blockerBody ||
                blockerBody.GetComponentsInChildren<Collider>().Length != 1 || !CameraZoneRequest.IsFinite(BlockerGoal))
                throw new InvalidOperationException("전용 차단 몸체에는 활성 비 Trigger BoxCollider 하나와 유한 목표가 필요합니다.");
            if (!initialBlockerCaptured)
            { initialBlockerPose = new Pose(blockerBody.position, blockerBody.rotation); initialBlockerCaptured = true; }
        }
        private static bool SafeEase(Ease ease) => ease == Ease.Linear || ease == Ease.InSine || ease == Ease.OutSine || ease == Ease.InOutSine ||
            ease == Ease.InQuad || ease == Ease.OutQuad || ease == Ease.InOutQuad || ease == Ease.InCubic || ease == Ease.OutCubic || ease == Ease.InOutCubic;
        internal bool HasActualContact(PlayerFacade candidate)
        {
            if (candidate == null || candidate != actor) return false;
            var volume = GetComponent<BoxCollider>();
            var capsule = candidate.GetComponent<CapsuleCollider>();
            return volume.enabled && volume.gameObject.activeInHierarchy && capsule != null && capsule.enabled &&
                Physics.ComputePenetration(volume, volume.transform.position, volume.transform.rotation,
                    capsule, capsule.transform.position, capsule.transform.rotation, out _, out _);
        }
        internal bool IsBlockerPathSafe(PlayerFacade candidate, Vector3 playerGoal)
        {
            if (!HasBlocker || BlockerCompleted) return true;
            ValidateSetup();
            var box = blockerBody.GetComponent<BoxCollider>();
            var capsule = candidate.GetComponent<CapsuleCollider>();
            if (capsule == null || !capsule.enabled) return false;
            Vector3 delta = BlockerGoal - blockerBody.position;
            Vector3 center = blockerBody.position + blockerBody.rotation * Vector3.Scale(box.center, box.transform.lossyScale);
            var scale = box.transform.lossyScale;
            Vector3 half = Vector3.Scale(box.size * .5f, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            Vector3 trimmed = Vector3.Max(half - Vector3.one * .002f, Vector3.one * .0001f);
            bool Obstacle(Collider other) => other != null && other.attachedRigidbody != blockerBody &&
                other.attachedRigidbody != candidate.GetComponent<Rigidbody>() && !other.isTrigger &&
                !Physics.GetIgnoreLayerCollision(box.gameObject.layer, other.gameObject.layer) && !Physics.GetIgnoreCollision(box, other);
            foreach (var other in Physics.OverlapBox(center + delta, half, box.transform.rotation, ~0, QueryTriggerInteraction.Ignore))
                if (Obstacle(other) && Physics.ComputePenetration(box, blockerBody.position + delta, blockerBody.rotation,
                    other, other.transform.position, other.transform.rotation, out _, out float depth) && depth > .002f) return false;
            if (delta.sqrMagnitude > .000004f)
                foreach (var hit in Physics.BoxCastAll(center, trimmed, delta.normalized, box.transform.rotation, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                    if (Obstacle(hit.collider)) return false;
            // Conservative swept box against the projected actual capsule: reject crossing the safe player location as well as its endpoint.
            var swept = box.bounds; var end = box.bounds; end.center += delta; swept.Encapsulate(end);
            PlayerBridgeService.CapsuleGeometry(capsule, out var a, out var b, out float radius);
            Vector3 offset = playerGoal - candidate.GetComponent<Rigidbody>().position;
            var projected = new Bounds(a + offset, Vector3.one * radius * 2); projected.Encapsulate(new Bounds(b + offset, Vector3.one * radius * 2));
            return !swept.Intersects(projected);
        }
        internal void MarkBlockerCompleted() => BlockerCompleted = true;
        internal void RequestCancelled() => requestSubmitted = false;
        private void OnTriggerEnter(Collider other) => Contact(other);
        // Stay는 Bind 전 접촉과 구간 안 복귀를 보완합니다. 겹친 구간에서도 각 체류당 한 번만 요청합니다.
        private void OnTriggerStay(Collider other) => Contact(other);
        private void OnTriggerExit(Collider other)
        {
            contacts.Remove(other);
            contacts.RemoveWhere(contact => !IsActorContact(contact));
            if (contacts.Count == 0) requestSubmitted = false;
        }
        private bool IsActorContact(Collider other)
        {
            return actor != null && other != null && other.enabled && other.gameObject.activeInHierarchy && other.attachedRigidbody != null &&
                other.attachedRigidbody.GetComponent<PlayerDebris>() == null &&
                other.attachedRigidbody.GetComponent<PlayerFacade>() == actor;
        }
        private void Contact(Collider other)
        {
            if (!isActiveAndEnabled || cameraService == null || !cameraService.isActiveAndEnabled ||
                actor == null || actor.IsDead || !IsActorContact(other) || (stageOwner != null && !stageOwner.CanDispatch(actor))) return;
            contacts.RemoveWhere(contact => !IsActorContact(contact));
            if (contacts.Count == 0) requestSubmitted = false;
            contacts.Add(other);
            if (requestSubmitted) return;
            playerStart = actor.transform.position;
            // 요청 중 Reset/Bind가 재진입해도 과거 접촉 상태를 다시 기록하지 않습니다.
            var owner = cameraService;
            var current = actor;
            long revision = bindingRevision;
            requestSubmitted = true;
            try
            {
                bool accepted = owner.TryEnterZone(ResolvedZone, current);
                if (!accepted && revision == bindingRevision) requestSubmitted = false;
            }
            catch
            {
                if (revision == bindingRevision) requestSubmitted = false;
                throw;
            }
        }
        public void Unbind()
        {
            cameraService?.CancelFrom(this);
            bindingRevision++; contacts.Clear(); requestSubmitted = false; cameraService = null; actor = null; stageOwner = null;
        }
        public void ResetForRetry()
        {
            Unbind();
            if (initialBlockerCaptured && blockerBody != null)
            { blockerBody.position = initialBlockerPose.position; blockerBody.rotation = initialBlockerPose.rotation; }
            BlockerCompleted = false;
        }
        private void OnDisable() => Unbind();
        private void OnDestroy() => Unbind();
        private void OnDrawGizmos()
        {
            var color = Gizmos.color; var matrix = Gizmos.matrix;
            try
            {
                Gizmos.matrix = Matrix4x4.identity;
                if (alignmentPoint == null) return;
                Gizmos.color = Color.cyan;
                Vector3 current = actor != null ? actor.transform.position : alignmentPoint.position;
                Vector3 goal = HasBlocker && safeAnchor != null ? safeAnchor.position : new Vector3(current.x, current.y, alignmentPoint.position.z);
                Gizmos.DrawWireSphere(current, .12f); Gizmos.DrawWireSphere(playerStart, .1f);
                Gizmos.DrawLine(current, goal); Gizmos.DrawWireSphere(goal, .2f);
                if (actor != null && actor.TryGetComponent<CapsuleCollider>(out var capsule))
                {
                    PlayerBridgeService.CapsuleGeometry(capsule, out var a, out var b, out float radius);
                    var offset = goal - current;
                    Gizmos.DrawWireSphere(a + offset, radius); Gizmos.DrawWireSphere(b + offset, radius);
                    Gizmos.DrawLine(a + offset, b + offset);
                }
                if (!HasBlocker || blockerTarget == null) return;
                Gizmos.color = new Color(1, .5f, 0);
                Gizmos.DrawLine(blockerBody.position, blockerTarget.position);
                if (blockerBody.TryGetComponent<BoxCollider>(out var box))
                {
                    foreach (var position in new[] { blockerBody.position, initialBlockerCaptured ? initialBlockerPose.position : blockerBody.position, blockerTarget.position })
                    {
                        Gizmos.matrix = Matrix4x4.TRS(position, blockerBody.rotation, blockerBody.transform.lossyScale);
                        Gizmos.DrawWireCube(box.center, box.size);
                    }
                }
            }
            finally { Gizmos.color = color; Gizmos.matrix = matrix; }
        }
    }
}
