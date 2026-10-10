using System;
using Cooked.Authoring;
using Cooked.Contracts;
using UnityEngine;
namespace Cooked.Foundation
{
    [CreateAssetMenu(menuName = "Cooked/Architecture Installation")]
    public sealed class ArchitectureInstallation : ScriptableObject
    {
        public const string AssetPath = "Assets/_Scenes/Tech_SYM/Architecture/Configuration/ArchitectureInstallation.asset";
        // 기존 직렬화 이름/GUID 보존. 사람이 입력하지 않는 Player용 생성 값이다.
        [SerializeField, HideInInspector] private string bootstrapPath;
        [SerializeField, HideInInspector] private string titlePath;
        [SerializeField, HideInInspector] private string corePath;
        [SerializeField, HideInInspector] private string[] stagePaths = Array.Empty<string>();
#if UNITY_EDITOR
        [SerializeField, InspectorLabel("부팅 씬"), Tooltip("앱을 준비하는 Bootstrap 씬을 연결하세요.")]
        private SceneReference bootstrapScene = new SceneReference();
        [SerializeField, InspectorLabel("타이틀 씬"), Tooltip("게임 시작과 종료 뒤 표시할 Title 씬을 연결하세요.")]
        private SceneReference titleScene = new SceneReference();
        [SerializeField, InspectorLabel("공통 씬"), Tooltip("플레이어와 공통 화면을 유지하는 Core 씬을 연결하세요.")]
        private SceneReference coreScene = new SceneReference();
        [SerializeField, InspectorLabel("스테이지 씬"), Tooltip("독립 진입할 Stage 씬을 연결하세요. 첫 항목이 새 게임의 프로토타입입니다.")]
        private SceneReference[] stageScenes = Array.Empty<SceneReference>();

        /// <summary>검증 성공 후에만 숨은 경로를 갱신한다. 저장은 이관/빌드 소유자가 명시적으로 수행한다.</summary>
        public SceneCatalog RefreshAuthoring()
        {
            string owner = name + " / ";
            string bootstrap = bootstrapScene?.ResolvePath(owner + "부팅 씬") ?? throw new InvalidOperationException(owner + "부팅 씬을 연결하세요.");
            string title = titleScene?.ResolvePath(owner + "타이틀 씬") ?? throw new InvalidOperationException(owner + "타이틀 씬을 연결하세요.");
            string core = coreScene?.ResolvePath(owner + "공통 씬") ?? throw new InvalidOperationException(owner + "공통 씬을 연결하세요.");
            if (stageScenes == null) throw new InvalidOperationException(owner + "스테이지 씬 목록이 없습니다.");
            var stages = new string[stageScenes.Length];
            for (int i = 0; i < stages.Length; i++) stages[i] = stageScenes[i]?.ResolvePath(owner + "스테이지 씬[" + i + "]") ?? throw new InvalidOperationException(owner + "스테이지 씬[" + i + "]을 연결하세요.");
            var catalog = new SceneCatalog(bootstrap, title, core, stages);
            StorePaths(catalog);
            return catalog;
        }

        private void OnValidate()
        {
            try { RefreshAuthoring(); }
            catch (Exception error) { Debug.LogError(name + " 작성 오류: " + error.Message, this); }
        }
#endif
        public SceneCatalog CreateCatalog()
        {
#if UNITY_EDITOR
            return RefreshAuthoring();
#else
            return new SceneCatalog(bootstrapPath, titlePath, corePath, stagePaths);
#endif
        }

        private void StorePaths(SceneCatalog catalog)
        {
            bootstrapPath = catalog.BootstrapPath; titlePath = catalog.TitlePath; corePath = catalog.CorePath;
            stagePaths = new string[catalog.StagePaths.Count];
            for (int i = 0; i < stagePaths.Length; i++) stagePaths[i] = catalog.StagePaths[i];
        }
    }
}
