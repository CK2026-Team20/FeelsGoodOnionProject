using Cooked.Foundation.Editor;
using Cooked.Integration;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Cooked.DebugTesting.Editor
{
    public static class DirectSceneTestMenu
    {
        private const string DirectMenu = "Cooked/Testing/현재 씬 직접 테스트";
        [MenuItem(DirectMenu)]
        private static void SelectDirect()
        {
            DirectSceneTestEntry.IsSelected = true;
            BootstrapPlayModeHook.RefreshEntrySelection();
            Menu.SetChecked(DirectMenu, true);
        }
        [MenuItem("Cooked/Testing/본편 실행")]
        private static void SelectNormal()
        {
            DirectSceneTestEntry.IsSelected = false;
            BootstrapPlayModeHook.RefreshEntrySelection();
            Menu.SetChecked(DirectMenu, false);
        }
        [MenuItem(DirectMenu, true)]
        private static bool ValidateDirect()
        {
            Menu.SetChecked(DirectMenu, DirectSceneTestEntry.IsSelected);
            return !EditorApplication.isPlayingOrWillChangePlaymode;
        }
        [MenuItem("Cooked/Testing/본편 실행", true)]
        private static bool ValidateNormal() => !EditorApplication.isPlayingOrWillChangePlaymode;
        [MenuItem("Cooked/Testing/현재 씬 테스트 설정 추가")]
        private static void CreateSettings()
        {
            var scene = SceneManager.GetActiveScene();
            var existing = SceneComponentLookup.FindAll<DirectSceneTestSettings>(scene);
            if (existing.Length > 0) { Selection.activeObject = existing[0]; return; }
            var selected = Selection.activeTransform;
            var root = new GameObject("Direct Scene Test Settings") { tag = "EditorOnly" };
            SceneManager.MoveGameObjectToScene(root, scene);
            Undo.RegisterCreatedObjectUndo(root, "테스트 설정 추가");
            var settings = Undo.AddComponent<DirectSceneTestSettings>(root);
            settings.SetStartPoint(selected != null && selected.gameObject.scene == scene ? selected : root.transform);
            Selection.activeObject = root;
            SelectDirect();
            // User action is undoable and intentionally never calls SaveScene/SaveAssets.
        }
        [MenuItem("Cooked/Testing/현재 씬 테스트 설정 추가", true)]
        private static bool ValidateCreate() => !EditorApplication.isPlayingOrWillChangePlaymode;
    }
    /// <summary>Checks build copies only. Never rewrites authoring scenes or saves them.</summary>
    public sealed class DirectSceneTestBuildGuard : IProcessSceneWithReport
    {
        public int callbackOrder => 0;
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null) return;
            foreach (var settings in SceneComponentLookup.FindAll<DirectSceneTestSettings>(scene))
            {
                // A manually added component may share a gameplay/start-point object.
                // Strip only this editor configuration, never that user's GameObject.
                Object.DestroyImmediate(settings);
            }
        }
    }
}
