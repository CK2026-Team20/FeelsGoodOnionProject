using System;
using System.IO;
using Cooked.Contracts;
namespace Cooked.Foundation
{
    /// <summary>Pure path mapping shared by the Editor hook and entry-coverage tests.</summary>
    public static class BootstrapEntryRoute
    {
        public static BootRoute Resolve(string sourcePath, SceneCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            foreach (string stagePath in catalog.StagePaths)
                if (string.Equals(sourcePath, stagePath, StringComparison.Ordinal)) return new BootRoute(true, Path.GetFileNameWithoutExtension(stagePath));
            if (sourcePath == catalog.CorePath || sourcePath == "Assets/_Scenes/Tech_SYM/03_InGame.unity" ||
                sourcePath == "Assets/_Scenes/Tech_SYM/01_BrokenablePlatformTest.unity" ||
                sourcePath == "Assets/_Scenes/Tech_SYM/02_MultiFuncPlatformTest.unity")
                return new BootRoute(true, Path.GetFileNameWithoutExtension(catalog.StagePaths[0]));
            return new BootRoute(false, null);
        }
    }
}
