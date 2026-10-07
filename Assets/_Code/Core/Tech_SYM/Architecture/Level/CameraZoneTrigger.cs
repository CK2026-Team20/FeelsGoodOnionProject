using System.Collections.Generic;
using UnityEngine;
namespace Cooked.Level
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class CameraZoneTrigger : MonoBehaviour
    {
        [Tooltip("이 트리거의 카메라 구간입니다. Side(1)는 로컬 정렬점의 월드 Z에 맞추며 Quarter(3)는 위치를 유지합니다. 정렬점은 파란 기즈모 구입니다.")]
        [SerializeField] private CameraZoneData zone;
        private LevelCameraService cameraService;
        private PlayerFacade actor;
        private readonly HashSet<Collider> contacts = new HashSet<Collider>();
        private bool requestSubmitted;
        public CameraZoneData Zone => zone;
        private CameraZoneRequest ResolvedZone => zone.Resolve(transform);
        public Vector3 AlignmentPosition => ResolvedZone.AlignmentPosition;
        public void Bind(LevelCameraService camera, PlayerFacade player)
        {
            if (ReferenceEquals(cameraService, camera) && ReferenceEquals(actor, player)) return;
            ResetForRetry(); cameraService = camera; actor = player;
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
                other.attachedRigidbody.GetComponentInParent<PlayerFacade>() == actor;
        }
        private void Contact(Collider other)
        {
            if (!isActiveAndEnabled || cameraService == null || !cameraService.isActiveAndEnabled ||
                actor == null || actor.IsDead || !IsActorContact(other)) return;
            contacts.RemoveWhere(contact => !IsActorContact(contact));
            if (contacts.Count == 0) requestSubmitted = false;
            contacts.Add(other);
            if (requestSubmitted) return;
            // 요청 중 Reset/Bind가 재진입해도 과거 접촉 상태를 다시 기록하지 않습니다.
            requestSubmitted = true;
            cameraService.EnterZone(ResolvedZone);
        }
        public void ResetForRetry()
        {
            contacts.Clear(); requestSubmitted = false; cameraService = null; actor = null;
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
                var position = AlignmentPosition;
                if (!CameraZoneRequest.IsFinite(position)) return;
                Gizmos.DrawSphere(position, .2f);
                Gizmos.DrawLine(transform.position, position);
                const float extent = 2f;
                if (zone.Movement == CameraMovementMode.Side)
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
