using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FeelsGoodOnion.TechSYM.Platforms.Editor
{
    /// <summary>사용자가 편집 중인 주기 발판만 파생 기하를 준비한다. 씬/프리팹을 자동 저장하지 않는다.</summary>
    [CustomEditor(typeof(PeriodicPlatform)), CanEditMultipleObjects]
    internal sealed class PeriodicPlatformGeometryEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            foreach (var item in targets)
            {
                var platform = (PeriodicPlatform)item;
                if (!platform.RefreshCollisionGeometry()) continue;
                if (PrefabUtility.IsPartOfPrefabInstance(platform)) PrefabUtility.RecordPrefabInstancePropertyModifications(platform);
                EditorUtility.SetDirty(platform);
            }
        }
    }

    /// <summary>빌드 사본의 캐시를 최종 메시로 준비한다. 원본 씬/프리팹/import를 저장하지 않는다.</summary>
    internal sealed class PeriodicPlatformGeometryBuildProcessor : IProcessSceneWithReport
    {
        public int callbackOrder => 0;
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var platform in root.GetComponentsInChildren<PeriodicPlatform>(true)) platform.BakeCollisionGeometry();
        }
    }
}
