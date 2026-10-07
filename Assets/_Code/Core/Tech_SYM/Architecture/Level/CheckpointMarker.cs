using UnityEngine;
namespace Cooked.Level
{
    public sealed class CheckpointMarker : MonoBehaviour
    {
        [Tooltip("이 체크포인트의 고유 ID입니다. 같은 스테이지에서 중복하지 않으며 트리거와 초기 진입 설정의 ID가 일치해야 합니다.")]
        [SerializeField] private string checkpointId;
        [Tooltip("안전한 초기 배치의 카메라 구간입니다. 복귀 모드는 실제 저장 위치를 포함하는 CameraZoneTrigger에서 먼저 조회하세요. 구간이 없는 안전 초기 위치에서만 이 배치 설정을 사용합니다. 정렬점은 표식 로컬 좌표이며 Body Offset과 별개이고 초기 적용은 위치를 바꾸지 않습니다.")]
        [SerializeField] private CameraZoneData cameraZone;
        public CameraZoneRequest CameraZone => cameraZone.Resolve(transform);
        [Tooltip("체크포인트 표식에서 플레이어 물리 본체 중심까지의 월드 좌표 이동량입니다(월드 단위). XYZ를 표식의 월드 위치에 그대로 더하며 표식 회전으로 돌리지 않습니다. Y를 높이면 더 높은 위치에 복원합니다. 모두 0이면 표식 위치에 복원하므로 바닥과 겹치지 않게 설정하세요.")]
        [SerializeField] private Vector3 bodyOffset = new Vector3(0f, 1.03f, 0f);
        public string CheckpointId => checkpointId;
        // Marker/beacon stay at the surface. Spawn uses centre=(0,0,0), height=2 normal capsule.
        public Pose Pose => new Pose(transform.position + bodyOffset, transform.rotation);
    }
}
