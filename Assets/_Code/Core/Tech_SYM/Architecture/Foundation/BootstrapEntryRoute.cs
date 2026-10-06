using System;
using System.IO;
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

        public static bool TryResolve(string sourcePath, SceneCatalog catalog, out BootRoute route)
        {
            route = default;
            if (!IsTarget(sourcePath)) return false;
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            if (catalog.BootstrapPath != BootstrapPath || catalog.TitlePath != TitlePath ||
                catalog.CorePath != CorePath || catalog.StagePaths.Count != 3 ||
                catalog.StagePaths[0] != PrototypePath || catalog.StagePaths[1] != Stage2Path ||
                catalog.StagePaths[2] != Stage3Path)
                throw new ArgumentException("R20 requires the six designated scenes, with Prototype/1_Stage first. Update the installation before Play.", nameof(catalog));

            if (sourcePath == BootstrapPath || sourcePath == TitlePath)
                route = new BootRoute(false, null);
            else
                route = new BootRoute(true, Path.GetFileNameWithoutExtension(
                    sourcePath == CorePath ? PrototypePath : sourcePath));
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
