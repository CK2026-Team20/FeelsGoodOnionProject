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
        public const string PrototypePath = Root + "01_Stage_Prototype.unity";
        public const string Stage2Path = Root + "Stages/03_2_Stage.unity";
        public const string Stage3Path = Root + "Stages/03_3_Stage.unity";
        // 폐기 씬 파일 보존 중 기존 동시 로드 환경의 종료만 허용한다. 해당 자산 삭제 후 이 목록도 제거한다.
        public static readonly System.Collections.Generic.IReadOnlyList<string> RetiredStagePaths =
            Array.AsReadOnly(new[] { Root + "Stages/03_1_Stage.unity", Stage2Path, Stage3Path });

        // Membership is independent of installation and of the Title route's default value.
        public static bool IsTarget(string sourcePath) =>
            sourcePath == BootstrapPath || sourcePath == TitlePath || sourcePath == CorePath ||
            sourcePath == PrototypePath;

        // 설치 자산에 등록된 실제 씬 경로를 확인합니다.
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

    }
}
