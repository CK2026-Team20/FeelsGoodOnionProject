using System.Collections.Generic;
using Cooked.Authoring;
using Cooked.Integration;
using UnityEngine;
namespace Cooked.Level
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class CameraZoneTrigger : MonoBehaviour
    {
        [SerializeField, InspectorLabel("카메라 방식"), Tooltip("이 구간의 측면 또는 쿼터뷰 방식을 선택하세요.")]
        private CameraMovementMode mode = CameraMovementMode.Side;
        [SerializeField, InspectorLabel("정렬 지점"), Tooltip("같은 씬의 고정 Transform을 연결하세요. 측면은 이 지점의 월드 Z에 정렬하며 파란 구로 표시됩니다.")]
        private Transform alignmentPoint;
        private LevelCameraService cameraService;
        private PlayerFacade actor;
        private StageService stageOwner;
        private long bindingRevision;
        private readonly HashSet<Collider> contacts = new HashSet<Collider>();
        private bool requestSubmitted;
        public CameraMovementMode Mode => mode;
        public Transform AlignmentPoint => alignmentPoint;
        internal bool IsBoundTo(LevelCameraService camera, PlayerFacade player) => isActiveAndEnabled && player != null &&
            ReferenceEquals(cameraService, camera) && ReferenceEquals(actor, player) && !player.IsDead &&
            (stageOwner == null || stageOwner.CanDispatch(player));
        public CameraZoneData Zone => new CameraZoneData(Mode, ResolveAlignment());
        public CameraZoneRequest Resolve() => new CameraZoneRequest(this, Zone);
        private CameraZoneRequest ResolvedZone => Resolve();
        public Vector3 AlignmentPosition => ResolvedZone.AlignmentPosition;
        private Vector3 ResolveAlignment()
        {
            if (alignmentPoint == null) throw new System.InvalidOperationException("같은 씬의 실제 카메라 정렬 Transform을 연결하세요.");
            SceneComponentLookup.RequireFixedPoint(this, alignmentPoint, "정렬 지점");
            return alignmentPoint.position;
        }
        public void Bind(LevelCameraService camera, PlayerFacade player)
        {
            if (ReferenceEquals(cameraService, camera) && ReferenceEquals(actor, player)) return;
            ResetForRetry();
            var owners = SceneComponentLookup.FindAll<StageService>(gameObject.scene);
            if (owners.Length > 1) throw new System.InvalidOperationException("카메라 구간의 씬에는 StageService가 하나여야 합니다.");
            stageOwner = owners.Length == 1 ? owners[0] : null;
            cameraService = camera; actor = player;
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
        public void ResetForRetry()
        {
            bindingRevision++; contacts.Clear(); requestSubmitted = false; cameraService = null; actor = null; stageOwner = null;
        }
        private void OnDisable() => ResetForRetry();
        private void OnDestroy() => ResetForRetry();
        private void OnDrawGizmos()
        {
            var previousColor = Gizmos.color;
            var previousMatrix = Gizmos.matrix;
            try
            {
                Gizmos.color = Color.blue;
                Gizmos.matrix = Matrix4x4.identity;
                // Unmigrated/unfinished authoring must not throw on every Scene view repaint.
                if (alignmentPoint == null) return;
                var position = alignmentPoint.position;
                if (!CameraZoneRequest.IsFinite(position)) return;
                Gizmos.DrawSphere(position, .2f);
                Gizmos.DrawLine(transform.position, position);
                const float extent = 2f;
                if (Mode == CameraMovementMode.Side)
                {
                    Gizmos.DrawLine(position - Vector3.forward * extent, position + Vector3.forward * extent);
                    Gizmos.DrawWireCube(position, new Vector3(extent * 2f, extent * 2f, 0f));
                }
            }
            finally
            {
                Gizmos.color = previousColor;
                Gizmos.matrix = previousMatrix;
            }
        }
    }
}
