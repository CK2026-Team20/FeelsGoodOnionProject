using Cooked.Authoring;
using Cooked.Integration;
using UnityEngine;
namespace Cooked.Level
{
    public sealed class CheckpointMarker : MonoBehaviour
    {
        [SerializeField, InspectorLabel("초기 등장 지점"), Tooltip("이 체크포인트 아래에서 초기 ActorBody 중심을 놓을 Transform을 연결하세요. 접촉 저장은 플레이어의 현재 위치를 사용합니다.")]
        private Transform respawnPoint;
        [SerializeField, InspectorLabel("시작 카메라 구간"), Tooltip("초기 등장 위치의 실제 CameraZoneTrigger를 연결하세요.")]
        private CameraZoneTrigger initialCameraZone;
        public Transform RespawnPoint => respawnPoint;
        public CameraZoneTrigger InitialCameraZone => initialCameraZone;
        public CameraZoneRequest CameraZone
        {
            get
            {
                if (initialCameraZone == null) throw new System.InvalidOperationException("초기 등장 위치의 CameraZoneTrigger를 연결하세요.");
                SceneComponentLookup.RequireSameScene(this, initialCameraZone, "시작 카메라 구간");
                return initialCameraZone.Resolve();
            }
        }
        public Pose Pose
        {
            get
            {
                if (respawnPoint == null || respawnPoint == transform || !respawnPoint.IsChildOf(transform))
                    throw new System.InvalidOperationException("이 체크포인트의 자식 등장 Transform을 연결하세요.");
                SceneComponentLookup.RequireFixedPoint(this, respawnPoint, "초기 등장 지점");
                if (!CameraZoneRequest.IsFinite(respawnPoint.position))
                    throw new System.InvalidOperationException("초기 등장 지점은 유한한 월드 위치여야 합니다.");
                return new Pose(respawnPoint.position, respawnPoint.rotation);
            }
        }
    }
}
