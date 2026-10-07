using UnityEngine;
namespace Cooked.Level
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class CameraZoneTrigger : MonoBehaviour
    {
        [Tooltip("이 트리거의 카메라 구간입니다. Side(1)는 로컬 정렬점의 월드 Z에 맞추며 Quarter(3)는 위치를 유지합니다. 정렬점은 파란 기즈모 구입니다. Corridor(2)는 이전 배치 이관용이며 실행할 수 없습니다.")]
        [SerializeField] private CameraZoneData zone;
        private LevelCameraService cameraService;
        private PlayerFacade actor;
        public CameraZoneData Zone => zone;
        private CameraZoneRequest ResolvedZone => zone.Resolve(transform);
        public Vector3 AlignmentPosition => ResolvedZone.AlignmentPosition;
        public void Bind(LevelCameraService camera, PlayerFacade player) { cameraService = camera; actor = player; }
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
        // EnterZone suppresses duplicate IDs; Stay also handles an Actor spawned inside the volume.
        private void OnTriggerStay(Collider other) => Contact(other);
        private void Contact(Collider other)
        {
            if (!isActiveAndEnabled || cameraService == null || !cameraService.isActiveAndEnabled ||
                actor == null || actor.IsDead || other == null || other.attachedRigidbody == null) return;
            if (other.attachedRigidbody.GetComponentInParent<PlayerFacade>() == actor) cameraService.EnterZone(ResolvedZone);
        }
        public void ResetForRetry() { cameraService = null; actor = null; }
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
