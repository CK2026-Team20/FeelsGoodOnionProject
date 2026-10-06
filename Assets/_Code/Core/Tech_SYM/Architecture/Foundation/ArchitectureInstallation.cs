using System;
using Cooked.Contracts;
using UnityEngine;
namespace Cooked.Foundation
{
    /// <summary>Explicit installation marker, created by integration only after scene assets exist.</summary>
    [CreateAssetMenu(menuName = "Cooked/Architecture Installation")]
    public sealed class ArchitectureInstallation : ScriptableObject
    {
        public const string AssetPath = "Assets/_Scenes/Tech_SYM/Architecture/Configuration/ArchitectureInstallation.asset";
        [Tooltip("부트스트래퍼 씬의 프로젝트 상대 경로입니다. Assets/부터 .unity까지 입력하며 실제 씬 경로와 일치해야 합니다.")]
        [SerializeField] private string bootstrapPath;
        [Tooltip("게임 타이틀 씬의 프로젝트 상대 경로입니다. 시작·엔딩 후 돌아갈 실제 씬 경로를 지정하세요.")]
        [SerializeField] private string titlePath;
        [Tooltip("플레이어·공통 시스템을 유지할 Core 씬의 프로젝트 상대 경로입니다. 스테이지 씬과 구분하세요.")]
        [SerializeField] private string corePath;
        [Tooltip("등록할 스테이지 씬의 프로젝트 상대 경로 목록입니다. 첫 항목이 새 게임의 첫 스테이지이며 실제 씬 경로를 지정해야 합니다.")]
        [SerializeField] private string[] stagePaths = Array.Empty<string>();
        public SceneCatalog CreateCatalog() => new SceneCatalog(bootstrapPath, titlePath, corePath, stagePaths);
        public void Configure(SceneCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            bootstrapPath = catalog.BootstrapPath; titlePath = catalog.TitlePath; corePath = catalog.CorePath;
            stagePaths = new string[catalog.StagePaths.Count];
            for (int i = 0; i < stagePaths.Length; i++) stagePaths[i] = catalog.StagePaths[i];
        }
    }
}
