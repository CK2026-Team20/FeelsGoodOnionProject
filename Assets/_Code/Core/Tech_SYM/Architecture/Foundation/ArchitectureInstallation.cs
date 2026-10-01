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
        [SerializeField] private string bootstrapPath;
        [SerializeField] private string titlePath;
        [SerializeField] private string corePath;
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
