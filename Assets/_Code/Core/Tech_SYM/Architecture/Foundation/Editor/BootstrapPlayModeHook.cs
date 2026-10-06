using System;
using Cooked.Contracts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Cooked.Foundation.Editor
{
    /// <summary>Editor-only routing for the six R20 active paths; never saves or rewrites scene setup.</summary>
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
            EditorSceneManager.activeSceneChangedInEditMode += OnActiveSceneChanged;
            EditorSceneManager.sceneSaved += OnSceneSaved;
            EditorApplication.delayCall += RecoverAndArm;
        }
        private static void OnActiveSceneChanged(Scene previous, Scene next) => Arm();
        private static void OnSceneSaved(Scene scene) => Arm();
        // A scheduled Stop can clear the requested Editor state while the runtime scene
        // is still playing. SceneManagerSetup is only available after both have stopped.
        private static bool InPlayTransition => Application.isPlaying || EditorApplication.isPlaying ||
            EditorApplication.isPlayingOrWillChangePlaymode;
        private static void RecoverAndArm()
        {
            if (InPlayTransition) return;
            RestoreTemporarySettings();
            VerifyRestoredSceneSetup();
            Arm();
        }
        private static void ScheduleArm() { EditorApplication.delayCall -= Arm; EditorApplication.delayCall += Arm; }
        private static ArchitectureInstallation Installation => AssetDatabase.LoadAssetAtPath<ArchitectureInstallation>(ArchitectureInstallation.AssetPath);
        private static void Arm()
        {
            if (InPlayTransition) return;
            if (!BootstrapEntryRoute.IsTarget(SceneManager.GetActiveScene().path))
            {
                RestoreTemporarySettings();
                // A restored baseline may itself be Bootstrap (or a user's custom start scene).
                // This Play policy still starts the actual active independent scene.
                ArmStartSceneForEntry(null);
                return;
            }
            try
            {
                if (Installation == null) throw new InvalidOperationException("Architecture installation is missing.");
                SceneCatalog catalog = Installation.CreateCatalog();
                if (!BootstrapEntryRoute.TryResolve(SceneManager.GetActiveScene().path, catalog, out _)) return;
                var bootstrap = AssetDatabase.LoadAssetAtPath<SceneAsset>(catalog.BootstrapPath);
                if (bootstrap == null) throw new InvalidOperationException("Installed Bootstrap scene missing: " + catalog.BootstrapPath);
                // Setting this never opens Bootstrap in the user's editing scene setup.
                ArmStartSceneForEntry(bootstrap);
            }
            catch (Exception e)
            {
                RestoreTemporarySettings();
                Debug.LogError("Cooked architecture installation is invalid: " + e.Message);
            }
        }
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingEditMode)
            {
                string source = SceneManager.GetActiveScene().path;
                if (!BootstrapEntryRoute.IsTarget(source))
                {
                    RestoreOptions();
                    ClearRoute();
                    CaptureSceneSetup();
                    ArmStartSceneForEntry(null);
                    SessionState.SetBool(Key + "Entering", true);
                    return;
                }
                try
                {
                    if (Installation == null) throw new InvalidOperationException("Architecture installation is missing.");
                    SceneCatalog catalog = Installation.CreateCatalog();
                    // Capture the REAL editing active scene before routing. Never read playModeStartScene as origin.
                    if (!BootstrapEntryRoute.TryResolve(source, catalog, out var route)) return;
                    CaptureSceneSetup();
                    SessionState.SetString(Key + "SourcePath", source);
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
                    SessionState.SetInt(Key + "OwnedOptions", (int)EditorSettings.enterPlayModeOptions);
                    SessionState.SetBool(Key + "Entering", true);
                    Debug.Log($"Cooked editor entry: {source} -> Bootstrap -> {(route.DirectGame ? route.StageId : "Title")}");
                }
                catch (Exception e)
                {
                    Debug.LogError("Cooked Play entry blocked: " + e);
                    EditorApplication.isPlaying = false;
                    RestoreTemporarySettings();
                    VerifyRestoredSceneSetup();
                }
            }
            else if (state == PlayModeStateChange.EnteredPlayMode)
            {
                SessionState.SetBool(Key + "Entering", false);
                // Scene reload has completed; restoring now also prevents a long-running Editor from saving a modified preference.
                RestoreOptions();
                RestoreStartScene();
                // Bootstrap initialization already had its opportunity to consume the one-shot route.
                ClearRoute();
            }
            else if (state == PlayModeStateChange.EnteredEditMode)
            {
                RestoreTemporarySettings();
                VerifyRestoredSceneSetup();
            }
        }
        private static void CaptureSceneSetup()
        {
            SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
            var records = new SceneRecord[setup.Length];
            for (int i = 0; i < setup.Length; i++)
            {
                var scene = SceneManager.GetSceneByPath(setup[i].path);
                records[i] = new SceneRecord { path = setup[i].path, loaded = setup[i].isLoaded,
                    active = setup[i].isActive, dirty = scene.IsValid() && scene.isDirty };
            }
            SessionState.SetString(Key + "SceneSetup", JsonUtility.ToJson(new SetupRecord { scenes = records }));
        }
        private static void ArmStartSceneForEntry(SceneAsset startScene)
        {
            string current = AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene);
            // A user/tool replacement is a new baseline, not a setting this hook owns.
            if (SessionState.GetBool(Key + "Armed", false) && current != SessionState.GetString(Key + "OwnedStart", BootstrapEntryRoute.BootstrapPath))
                SessionState.SetBool(Key + "Armed", false);
            if (!SessionState.GetBool(Key + "Armed", false))
            {
                SessionState.SetString(Key + "PreviousStart", AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
                SessionState.SetBool(Key + "Armed", true);
            }
            SessionState.SetString(Key + "OwnedStart", AssetDatabase.GetAssetPath(startScene));
            EditorSceneManager.playModeStartScene = startScene;
        }
        public static BootRoute ResolveRoute(string sourcePath, SceneCatalog catalog) => BootstrapEntryRoute.Resolve(sourcePath, catalog);
        private static BootRoute ConsumeRoute(BootRoute fallback)
        {
            if (!SessionState.GetBool(Key + "RoutePending", false)) return fallback;
            bool direct = SessionState.GetBool(Key + "DirectGame", false);
            var route = new BootRoute(direct, direct ? SessionState.GetString(Key + "StageId", "") : null);
            bool valid = BootstrapEntryRoute.IsTarget(SessionState.GetString(Key + "SourcePath", ""));
            ClearRoute();
            return valid ? route : fallback;
        }
        private static void CheckAbortedEntry()
        {
            if (InPlayTransition) return;
            if (SessionState.GetBool(Key + "Entering", false)) RestoreTemporarySettings();
            // EnteredEditMode may precede the final runtime teardown. Keep the captured
            // setup until a stable update can compare it, including after domain reload.
            VerifyRestoredSceneSetup();
        }
        private static void RestoreOptions()
        {
            if (!SessionState.GetBool(Key + "OptionsSaved", false)) return;
            // The fallback migrates in-flight state written by the previous hook version on recompile.
            int prior = SessionState.GetInt(Key + "Options", 0);
            int owned = SessionState.GetInt(Key + "OwnedOptions", prior & ~(int)EnterPlayModeOptions.DisableSceneReload);
            if ((int)EditorSettings.enterPlayModeOptions == owned)
                EditorSettings.enterPlayModeOptions = (EnterPlayModeOptions)SessionState.GetInt(Key + "Options", 0);
            // We never change OptionsEnabled, so never overwrite another tool/user's change to it.
            SessionState.SetBool(Key + "OptionsSaved", false);
            SessionState.EraseInt(Key + "Options");
            SessionState.EraseInt(Key + "OwnedOptions");
            SessionState.EraseBool(Key + "OptionsEnabled");
        }
        private static void RestoreStartScene()
        {
            if (!SessionState.GetBool(Key + "Armed", false)) return;
            string previous = SessionState.GetString(Key + "PreviousStart", "");
            // Remove this fallback once no pre-R20 Editor session can remain loaded.
            if (AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene) == SessionState.GetString(Key + "OwnedStart", BootstrapEntryRoute.BootstrapPath))
                EditorSceneManager.playModeStartScene = string.IsNullOrEmpty(previous) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(previous);
            SessionState.SetBool(Key + "Armed", false);
            SessionState.EraseString(Key + "PreviousStart");
            SessionState.EraseString(Key + "OwnedStart");
        }
        private static void RestoreTemporarySettings()
        {
            RestoreOptions();
            RestoreStartScene();
            SessionState.SetBool(Key + "Entering", false);
            ClearRoute();
        }
        private static void ClearRoute()
        {
            SessionState.SetBool(Key + "RoutePending", false);
            SessionState.EraseBool(Key + "DirectGame");
            SessionState.EraseString(Key + "StageId");
            SessionState.EraseString(Key + "SourcePath");
        }
        private static void VerifyRestoredSceneSetup()
        {
            if (InPlayTransition || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
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
