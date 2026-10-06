using UnityEngine;
namespace Cooked.Level
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class CameraZoneTrigger : MonoBehaviour
    {
        [Tooltip("이 트리거에 들어왔을 때 적용할 카메라 구간입니다. 정렬 위치는 이 트리거 자신의 로컬 좌표이며 파란 기즈모 구로 표시됩니다. Side는 월드 Z, Corridor는 월드 X에 플레이어를 맞추며 Quarter는 위치를 정렬하지 않습니다.")]
        [SerializeField] private CameraZoneData zone;
        private LevelCameraService cameraService;
        private PlayerFacade actor;
        public CameraZoneData Zone => zone;
        private CameraZoneRequest ResolvedZone => zone.Resolve(transform);
        public Vector3 AlignmentPosition => ResolvedZone.AlignmentPosition;
        public void Bind(LevelCameraService camera, PlayerFacade player) { cameraService = camera; actor = player; }
        private void OnTriggerEnter(Collider other) => Contact(other);
        private void Contact(Collider other)
        {
            if (cameraService == null || actor == null || actor.IsDead || other.attachedRigidbody == null) return;
            if (other.attachedRigidbody.GetComponent<PlayerFacade>() == actor) cameraService.EnterZone(ResolvedZone);
        }
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
                else if (zone.Movement == CameraMovementMode.Corridor)
                {
                    Gizmos.DrawLine(position - Vector3.right * extent, position + Vector3.right * extent);
                    Gizmos.DrawWireCube(position, new Vector3(0f, extent * 2f, extent * 2f));
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
