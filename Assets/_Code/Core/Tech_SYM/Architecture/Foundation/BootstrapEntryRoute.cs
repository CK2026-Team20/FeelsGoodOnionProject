using System;
using System.Linq;
using Cooked.Contracts;
namespace Cooked.Foundation
{
    /// <summary>Pure path mapping shared by the Editor hook and entry-coverage tests.</summary>
    public static class BootstrapEntryRoute
    {
        public const string Root = "Assets/_Scenes/Tech_SYM/Architecture/";
        public const string BootstrapPath = Root + "01_Bootstrapper.unity";
        public const string TitlePath = Root + "02_Title.unity";
        public const string CorePath = Root + "03_InGameCore.unity";
        public const string PrototypePath = Root + "Prototype/1_Stage.unity";
        public const string Stage2Path = Root + "Stages/03_2_Stage.unity";
        public const string Stage3Path = Root + "Stages/03_3_Stage.unity";

        // Membership is independent of installation and of the Title route's default value.
        public static bool IsTarget(string sourcePath) =>
            sourcePath == BootstrapPath || sourcePath == TitlePath || sourcePath == CorePath ||
            sourcePath == PrototypePath || sourcePath == Stage2Path || sourcePath == Stage3Path;

        // SceneAsset 기반 읽기 계약. 이전 overload는 미이관 Editor hook의 호환용이다.
        public static bool IsTarget(string sourcePath, SceneCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            return sourcePath == catalog.BootstrapPath || sourcePath == catalog.TitlePath ||
                sourcePath == catalog.CorePath || catalog.StagePaths.Contains(sourcePath);
        }

        public static bool TryResolve(string sourcePath, SceneCatalog catalog, out BootRoute route)
        {
            route = default;
            if (catalog == null)
            {
                if (!IsTarget(sourcePath)) return false;
                throw new ArgumentNullException(nameof(catalog));
            }
            if (!IsTarget(sourcePath, catalog)) return false;

            if (sourcePath == catalog.BootstrapPath || sourcePath == catalog.TitlePath)
                route = new BootRoute(false, null);
            else
                route = new BootRoute(true, catalog.GetStageId(sourcePath == catalog.CorePath ? catalog.StagePaths[0] : sourcePath));
            return true;
        }

        /// <summary>Compatibility mapping only. Use TryResolve/IsTarget to decide whether to intercept Play.</summary>
        public static BootRoute Resolve(string sourcePath, SceneCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            return TryResolve(sourcePath, catalog, out var route) ? route : new BootRoute(false, null);
        }
    }
}
