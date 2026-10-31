#if UNITY_EDITOR
using System;
using Cooked.Authoring;
using Cooked.Contracts;
using Cooked.Level;
using UnityEngine;
namespace Cooked.DebugTesting
{
    [DisallowMultipleComponent]
    public sealed class DirectSceneTestSettings : MonoBehaviour
    {
        [SerializeField, InspectorLabel("플레이어 시작점"), Tooltip("플레이어 Capsule 중심을 맞출 현재 씬의 Transform입니다.")]
        private Transform startPoint;
        [SerializeField, InspectorLabel("플레이어 프리팹"), Tooltip("비활성 PlayerRoot 프리팹입니다. 비우면 프로젝트 기본 플레이어를 사용합니다.")]
        private GameObject playerPrefab;
        [SerializeField, InspectorLabel("초기 조각"), Tooltip("생성과 재생성 때 제공할 조각 수입니다."), Range(0, 5)]
        private int initialFragments;
        [SerializeField, InspectorLabel("초기 능력"), Tooltip("생성과 재생성 때 해금할 능력입니다.")]
        private AbilityMask initialAbilities;
        [SerializeField, InspectorLabel("추가 이동 지점"), Tooltip("이동 버튼에서 선택할 현재 씬의 실제 Transform 목록입니다.")]
        private Transform[] additionalPoints = Array.Empty<Transform>();
        [SerializeField, Min(1), InspectorLabel("보관할 로그 수"), Tooltip("최근 로그를 보관할 최대 개수입니다. 기본값은 200이며 오래된 항목부터 제거합니다.")]
        private int logCapacity = 200;
        public int LogCapacity => Mathf.Max(1, logCapacity);
        public Transform StartPoint => startPoint;
        public GameObject PlayerPrefab => playerPrefab;
        public int InitialFragments => initialFragments;
        public AbilityMask InitialAbilities => initialAbilities;
        public System.Collections.Generic.IReadOnlyList<Transform> AdditionalPoints => additionalPoints;
        public void SetStartPoint(Transform value) => startPoint = value;

        [SerializeField, InspectorLabel("초기 카메라 시점"), Tooltip("직접 테스트를 시작할 시점입니다. 실행 중 F1 패널에서 변경할 수 있습니다.")]
        private CameraMovementMode initialView = CameraMovementMode.Side;
        [SerializeField, InspectorLabel("구도 원본 스테이지"), Tooltip("세 구도 설정을 그대로 사용할 현재 씬의 StageService입니다. 비우면 씬의 유일한 StageService를 사용하며, 없거나 여러 개면 아래 테스트 구도를 사용합니다. 회전값을 강제로 변경하지 않습니다.")]
        private StageService cameraProfileSource;
        [SerializeField, InspectorLabel("측면 테스트 구도"), Tooltip("구도 원본 스테이지가 없을 때 사용할 본편 형식의 측면 구도입니다.")]
        private LevelCameraService.ViewSettings sideView = new LevelCameraService.ViewSettings(new Vector2(40, 50), new Vector2(.1f, .1f), Vector3.zero);
        [SerializeField, InspectorLabel("통로 테스트 구도"), Tooltip("구도 원본 스테이지가 없을 때 사용할 본편 형식의 통로 구도입니다.")]
        private LevelCameraService.ViewSettings corridorView = new LevelCameraService.ViewSettings(new Vector2(50, 55), new Vector2(.1f, .1f), new Vector3(10, 0, 0));
        [SerializeField, InspectorLabel("쿼터뷰 테스트 구도"), Tooltip("구도 원본 스테이지가 없을 때 사용할 구도입니다. 회전·반경·화면 위치를 작성한 값 그대로 적용합니다.")]
        private LevelCameraService.ViewSettings quarterView = new LevelCameraService.ViewSettings(new Vector2(50, 50), new Vector2(.15f, .1f), new Vector3(20, -30, 0));
        public CameraMovementMode InitialView => initialView;
        public StageService CameraProfileSource => cameraProfileSource;
        public LevelCameraService.ViewSettings ViewFor(CameraMovementMode mode) => mode switch
        {
            CameraMovementMode.Side => sideView,
            CameraMovementMode.Corridor => corridorView,
            CameraMovementMode.Quarter => quarterView,
            _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
    }
}
#endif
