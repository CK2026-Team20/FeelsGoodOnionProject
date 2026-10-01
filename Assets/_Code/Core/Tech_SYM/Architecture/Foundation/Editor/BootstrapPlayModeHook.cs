using System;
using System.IO;
using Cooked.Contracts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Cooked.Foundation.Editor
{
    /// <summary>Editor-only entry routing. Existing scenes are aliases; never edits scene YAML.</summary>
    [InitializeOnLoad]
    public static class BootstrapPlayModeHook
    {
        private const string Key = "Cooked.Foundation.Entry.";
        [Serializable] private sealed class SetupRecord { public SceneRecord[] scenes; }
        [Serializable] private sealed class SceneRecord
        {
            public string path;
            public bool loaded;
            public bool active;
            public bool dirty;
        }
        static BootstrapPlayModeHook()
        {
            Bootstrapper.EditorRouteResolver = ConsumeRoute;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.projectChanged += ScheduleArm;
            EditorApplication.quitting += RestoreTemporarySettings;
            EditorApplication.update += CheckAbortedEntry;
            EditorApplication.delayCall += Arm;
        }
        private static void ScheduleArm() { EditorApplication.delayCall -= Arm; EditorApplication.delayCall += Arm; }
        private static ArchitectureInstallation Installation => AssetDatabase.LoadAssetAtPath<ArchitectureInstallation>(ArchitectureInstallation.AssetPath);
        private static void Arm()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (Installation == null)
            {
                RestoreStartScene();
                return; // Installing package/code alone must not change unrelated Play behavior.
            }
            try
            {
                SceneCatalog catalog = Installation.CreateCatalog();
                var bootstrap = AssetDatabase.LoadAssetAtPath<SceneAsset>(catalog.BootstrapPath);
                if (bootstrap == null) return; // ExitingEditMode emits the explicit installation error.
                if (!SessionState.GetBool(Key + "Armed", false))
                {
                    SessionState.SetString(Key + "PreviousStart", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
                    SessionState.SetBool(Key + "Armed", true);
                }
                // Setting this never opens Bootstrap in the user's editing scene setup.
                EditorSceneManager.playModeStartScene = bootstrap;
            }
            catch (Exception e) { Debug.LogError("Cooked architecture installation is invalid: " + e.Message); }
        }
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                if (Installation == null) return;
                try
                {
                    SceneCatalog catalog = Installation.CreateCatalog();
                    // Capture the REAL editing active scene before routing. Never read playModeStartScene as origin.
                    string source = SceneManager.GetActiveScene().path;
                    SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
                    SessionState.SetString(Key + "SourcePath", source);
                    var records = new SceneRecord[setup.Length];
                    for (int i = 0; i < setup.Length; i++)
                    {
                        var scene = SceneManager.GetSceneByPath(setup[i].path);
                        records[i] = new SceneRecord { path = setup[i].path, loaded = setup[i].isLoaded,
                            active = setup[i].isActive, dirty = scene.IsValid() && scene.isDirty };
                    }
                    SessionState.SetString(Key + "SceneSetup", JsonUtility.ToJson(new SetupRecord { scenes = records }));
                    BootRoute route = ResolveRoute(source, catalog);
                    var bootstrap = AssetDatabase.LoadAssetAtPath<SceneAsset>(catalog.BootstrapPath);
                    if (bootstrap == null) throw new InvalidOperationException("Installed Bootstrap scene missing: " + catalog.BootstrapPath);
                    ArmStartSceneForEntry(bootstrap);
                    SessionState.SetBool(Key + "RoutePending", true);
                    SessionState.SetBool(Key + "DirectGame", route.DirectGame);
                    SessionState.SetString(Key + "StageId", route.StageId ?? "");
                    SessionState.SetBool(Key + "OptionsSaved", true);
                    SessionState.SetBool(Key + "OptionsEnabled", EditorSettings.enterPlayModeOptionsEnabled);
                    SessionState.SetInt(Key + "Options", (int)EditorSettings.enterPlayModeOptions);
                    // Force a real scene reload even when the user's fast-enter configuration disables it.
                    EditorSettings.enterPlayModeOptions &= ~EnterPlayModeOptions.DisableSceneReload;
                    SessionState.SetBool(Key + "Entering", true);
                    Debug.Log($"Cooked editor entry: {source} -> Bootstrap -> {(route.DirectGame ? route.StageId : "Title")}");
                }
                catch (Exception e)
                {
                    Debug.LogError("Cooked Play entry blocked: " + e);
                    EditorApplication.isPlaying = false;
                    RestoreTemporarySettings();
                    SessionState.SetBool(Key + "RoutePending", false);
                }
            }
            else if (state == PlayModeStateChange.EnteredPlayMode)
            {
                SessionState.SetBool(Key + "Entering", false);
                // Scene reload has completed; restoring now also prevents a long-running Editor from saving a modified preference.
                RestoreOptions();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                RestoreTemporarySettings();
                VerifyRestoredSceneSetup();
                SessionState.SetBool(Key + "RoutePending", false);
                ScheduleArm();
            }
        }
        private static void ArmStartSceneForEntry(SceneAsset bootstrap)
        {
            if (!SessionState.GetBool(Key + "Armed", false))
            {
                SessionState.SetString(Key + "PreviousStart", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
                SessionState.SetBool(Key + "Armed", true);
            }
            EditorSceneManager.playModeStartScene = bootstrap;
        }
        public static BootRoute ResolveRoute(string sourcePath, SceneCatalog catalog) => BootstrapEntryRoute.Resolve(sourcePath, catalog);
        private static BootRoute ConsumeRoute(BootRoute fallback)
        {
            if (!SessionState.GetBool(Key + "RoutePending", false)) return fallback;
            SessionState.SetBool(Key + "RoutePending", false);
            return new BootRoute(SessionState.GetBool(Key + "DirectGame", false), SessionState.GetString(Key + "StageId", ""));
        }
        private static void CheckAbortedEntry()
        {
            if (!SessionState.GetBool(Key + "Entering", false) || EditorApplication.isPlayingOrWillChangePlaymode) return;
            RestoreTemporarySettings();
            SessionState.SetBool(Key + "RoutePending", false);
            ScheduleArm();
        }
        private static void RestoreOptions()
        {
            if (!SessionState.GetBool(Key + "OptionsSaved", false)) return;
            EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt(Key + "Options", 0);
            EditorSettings.enterPlayModeOptionsEnabled = SessionState.GetBool(Key + "OptionsEnabled", false);
            SessionState.SetBool(Key + "OptionsSaved", false);
        }
        private static void RestoreStartScene()
        {
            if (!SessionState.GetBool(Key + "Armed", false)) return;
            string previous = SessionState.GetString(Key + "PreviousStart", "");
            EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            SessionState.SetBool(Key + "Armed", false);
        }
        private static void RestoreTemporarySettings()
        {
            RestoreOptions();
            RestoreStartScene();
            SessionState.SetBool(Key + "Entering", false);
        }
        private static void VerifyRestoredSceneSetup()
        {
            string json = SessionState.GetString(Key + "SceneSetup", "");
            if (string.IsNullOrEmpty(json)) return;
            SessionState.EraseString(Key + "SceneSetup");
            var expected = JsonUtility.FromJson<SetupRecord>(json)?.scenes;
            var actual = EditorSceneManager.GetSceneManagerSetup();
            bool same = expected != null && expected.Length == actual.Length;
            if (same)
                for (int i = 0; i < actual.Length; i++)
                {
                    var scene = SceneManager.GetSceneByPath(actual[i].path);
                    same &= expected[i].path == actual[i].path && expected[i].loaded == actual[i].isLoaded && expected[i].active == actual[i].isActive;
                    same &= expected[i].dirty == (scene.IsValid() && scene.isDirty);
                }
            if (!same) Debug.LogError("Cooked: Editor scene setup differs after Stop. No automatic restore/save was attempted, to preserve unsaved edits.");
        }
    }
}
