#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Cooked.Audio;
using Cooked.Chase;
using Cooked.Cinematics;
using Cooked.Contracts;
using Cooked.Dialogue;
using Cooked.Foundation;
using Cooked.Level;
using Cooked.Session;
using Cooked.UI;
using TMPro;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
namespace Cooked.Integration.Editor
{
    public static class IntegrationSceneBuilder
    {
        public const string SourceRoot = "Assets/_Scenes/Tech_SYM/Architecture";
        public static string Root => AuthoringOutputScope.Map(SourceRoot);
        public static string EntriesPath => Root + "/Configuration/IntegrationEntrySettings.asset";
        public static SceneCatalog Catalog => new SceneCatalog(SourceRoot+"/01_Bootstrapper.unity",SourceRoot+"/02_Title.unity",
            SourceRoot+"/03_InGameCore.unity",new[]{SourceRoot+"/Prototype/1_Stage.unity",SourceRoot+"/Stages/03_2_Stage.unity",SourceRoot+"/Stages/03_3_Stage.unity"});
        public static SceneCatalog AuthoredCatalog => new SceneCatalog(AuthoringOutputScope.Map(Catalog.BootstrapPath),
            AuthoringOutputScope.Map(Catalog.TitlePath), AuthoringOutputScope.Map(Catalog.CorePath),
            Catalog.StagePaths.Select(AuthoringOutputScope.Map).ToArray());
        public static void BuildTemporaryAssets(string temporaryPath)
            => BuildTemporaryAssets(temporaryPath, Cooked.Cinematics.Editor.CinematicAssetBuilder.TemporaryOpeningCaptions,
                Cooked.Cinematics.Editor.CinematicAssetBuilder.TemporaryEndingCaptions);
        /// <summary>Main-only isolated smoke entry. Creates owned temporary outputs, never invokes legacy correction menus.</summary>
        public static void BuildTemporaryAssets(string temporaryPath, string[] openingCaptions, string[] endingCaptions)
        {
            RequireCleanEditor();
            if (openingCaptions == null || endingCaptions == null || openingCaptions.Length != 4 || endingCaptions.Length != 4 ||
                openingCaptions.Concat(endingCaptions).Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("Supply the approved captions for the eight existing artworks; no placeholder captions are authored.");
            using (AuthoringOutputScope.BeginTemporary(temporaryPath))
            using (AuthoringOutputScope.BeginScene())
            {
                foreach(var path in new[]{AuthoredCatalog.BootstrapPath,AuthoredCatalog.TitlePath,AuthoredCatalog.CorePath}.Concat(AuthoredCatalog.StagePaths))
                    if(SceneManager.GetSceneByPath(path).isLoaded) throw new InvalidOperationException("Close mapped smoke scenes before authoring: "+path);
                Cooked.UI.Editor.UiPrefabBuilder.BuildAll();
                var font=Require<TMP_FontAsset>(Cooked.UI.Editor.UiPrefabBuilder.FontPath);
                Cooked.Session.Editor.SessionActorBuilder.Build();
                FeelsGoodOnion.TechSYM.Enemies.Editor.EnemyPrefabAuthoring.BuildProfiles();
                Cooked.Level.Editor.LevelAssetBuilder.Build();
                Cooked.Dialogue.Editor.DialoguePrefabBuilder.Build(font);
                Cooked.Cinematics.Editor.CinematicAssetBuilder.BuildExistingArtwork(font,openingCaptions,endingCaptions);
                Cooked.Chase.Editor.ChaseAssetBuilder.Build();
                PrototypeStageAuthoring.Build();
                BuildScenes();
            }
        }
        [MenuItem("Cooked/Core/Build Core Owned Assets")]
        public static void BuildCoreAssets()
        {
            RequireCleanEditor();
            Cooked.Session.Editor.SessionActorBuilder.Build();
            Cooked.Chase.Editor.ChaseAssetBuilder.Build();
            FeelsGoodOnion.TechSYM.Enemies.Editor.EnemyPrefabAuthoring.BuildProfiles();
            Cooked.Level.Editor.LevelAssetBuilder.Build();
            PrototypeStageAuthoring.Build();
        }
        [MenuItem("Cooked/Core/Build Final Scenes")]
        public static void BuildScenes()
        {
            RequireCleanEditor();
            string[] required = {
                Cooked.UI.Editor.UiPrefabBuilder.GlobalPath, Cooked.UI.Editor.UiPrefabBuilder.TitlePath,
                Cooked.UI.Editor.UiPrefabBuilder.GamePath, Cooked.UI.Editor.UiPrefabBuilder.FontPath,
                Cooked.Session.Editor.SessionActorBuilder.HostPath, Cooked.Chase.Editor.ChaseAssetBuilder.PrefabPath,
                Cooked.Dialogue.Editor.DialoguePrefabBuilder.PrefabPath, Cooked.Cinematics.Editor.CinematicAssetBuilder.PrefabPath,
                Cooked.Audio.Editor.AudioPrefabBuilder.PrefabPath,
                Cooked.Level.Editor.LevelAssetBuilder.AssetRoot+"/Prefabs/CoreLevelCamera.prefab",
                Cooked.Background.Editor.KitchenContentBackdropBuilder.PrefabPath,
                SourceRoot+"/Dialogue/Data/DialogueTable.json"
            };
            foreach(string path in required) if(AssetDatabase.LoadMainAssetAtPath(path)==null)
                throw new InvalidOperationException("Required real dependency is not authored: "+path);
            Cooked.UI.Editor.ContentAssetBuilder.ValidateDialogueJson(Require<TextAsset>(SourceRoot+"/Dialogue/Data/DialogueTable.json").text);
            foreach(string path in AuthoredCatalog.StagePaths) Require<SceneAsset>(path);
            EnsureFolder(Root+"/Configuration");
            var installation=GetOrCreate<ArchitectureInstallation>(AuthoringOutputScope.Map(ArchitectureInstallation.AssetPath));
            installation.Configure(AuthoredCatalog); EditorUtility.SetDirty(installation);
            var entries=GetOrCreate<IntegrationEntrySettings>(EntriesPath);
            entries.Configure(new[]{
                new IntegrationEntrySettings.Entry("1_Stage","P_START",0),
                new IntegrationEntrySettings.Entry("03_2_Stage","S2_ENTRY",5,AbilityId.FormChange,AbilityId.RecoverShell,AbilityId.Tear),
                new IntegrationEntrySettings.Entry("03_3_Stage","S3_ENTRY",5,AbilityId.FormChange,AbilityId.RecoverShell,AbilityId.Tear)
            });
            EditorUtility.SetDirty(entries);
            Author(AuthoredCatalog.BootstrapPath,()=>BuildBootstrap(installation));
            Author(AuthoredCatalog.TitlePath,()=>Instantiate(Cooked.UI.Editor.UiPrefabBuilder.TitlePath));
            Author(AuthoredCatalog.CorePath,()=>BuildCore(entries));
            AuthoringOutputScope.SaveOwnedAssets();
            Debug.Log("[Cooked.Core] Final six scene assets linked. Actual gameplay verification is still required.");
        }
        public static void ConfigureAppOutput(GameObject output)
        {
            if (output == null || output.activeInHierarchy) throw new InvalidOperationException("Configure the inactive App output before Awake.");
            if (output.GetComponent<Camera>() == null) throw new InvalidOperationException("App output requires Camera.");
            var brain = output.GetComponent<CinemachineBrain>() ?? output.AddComponent<CinemachineBrain>();
            brain.IgnoreTimeScale = false;
            brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, .6f);
            var controller = output.GetComponent<PlayerCameraController>() ?? output.AddComponent<PlayerCameraController>();
            var data = new SerializedObject(controller); data.FindProperty("useSeparatedRig").boolValue = true;
            // Runtime T3 Bind supplies the current Actor modes/Anchor/Body and these Core cameras.
            data.ApplyModifiedPropertiesWithoutUndo();
            if (output.GetComponent<AudioListener>() == null) output.AddComponent<AudioListener>();
        }
        private static void BuildBootstrap(ArchitectureInstallation installation)
        {
            var root=new GameObject("BootstrapRoot");
            var bootstrap=root.AddComponent<Bootstrapper>();
            bootstrap.ConfigureDialogueTable(Require<TextAsset>(SourceRoot+"/Dialogue/Data/DialogueTable.json"));
            var ui=Instantiate(Cooked.UI.Editor.UiPrefabBuilder.GlobalPath).GetComponent<GlobalUiRoot>();
            ui.transform.SetParent(root.transform,false);
            var audio=Instantiate(Cooked.Audio.Editor.AudioPrefabBuilder.PrefabPath).GetComponent<AudioRuntimeHost>();
            audio.transform.SetParent(root.transform,false);
            var eventRoot=new GameObject("EventSystem");
            eventRoot.transform.SetParent(root.transform,false);
            eventRoot.AddComponent<EventSystem>();
            eventRoot.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            var cameraRoot=new GameObject("AppOutputCamera");
            cameraRoot.SetActive(false);
            cameraRoot.transform.SetParent(root.transform,false);
            var camera=cameraRoot.AddComponent<Camera>(); camera.cullingMask=~0; camera.depth=0; cameraRoot.tag="MainCamera";
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black;
            ConfigureAppOutput(cameraRoot);
            cameraRoot.SetActive(true);
            root.AddComponent<AppComposition>().Configure(bootstrap,installation,ui,audio,camera);
        }
        private static void BuildCore(IntegrationEntrySettings entries)
        {
            var root=new GameObject("InGameCoreRoot");
            var host=Instantiate(Cooked.Session.Editor.SessionActorBuilder.HostPath).GetComponent<SessionActorHost>();
            host.transform.SetParent(root.transform,false);
            var ui=Instantiate(Cooked.UI.Editor.UiPrefabBuilder.GamePath).GetComponent<GameUiRoot>();
            ui.transform.SetParent(root.transform,false);
            var cameraRoot=Instantiate(Cooked.Level.Editor.LevelAssetBuilder.AssetRoot+"/Prefabs/CoreLevelCamera.prefab");
            cameraRoot.transform.SetParent(root.transform,false);
            var cameraService=cameraRoot.GetComponent<LevelCameraService>();
            var dialogue=Instantiate(Cooked.Dialogue.Editor.DialoguePrefabBuilder.PrefabPath,ui.DialogueMount).GetComponent<DialogueView>();
            // Temporary T9 API compatibility: Configure still takes DialogueDriver; pass null, never author a retired driver.
            var cinematic=Instantiate(Cooked.Cinematics.Editor.CinematicAssetBuilder.PrefabPath,ui.CinematicMount).GetComponent<CinematicHost>();
            var chase=Instantiate(Cooked.Chase.Editor.ChaseAssetBuilder.PrefabPath).GetComponent<ChaseRuntimeDriver>();
            chase.transform.SetParent(root.transform,false);
            Instantiate(Cooked.Background.Editor.KitchenContentBackdropBuilder.PrefabPath).transform.SetParent(root.transform,false);
            var binding=root.AddComponent<CoreUiBinding>();
            ui.CinematicMount.SetAsLastSibling();
            // Temporary T9 API compatibility: CoreUiBinding still accepts InstructionView; no retired view is created.
            binding.Configure(ui,null);
            root.AddComponent<CoreComposition>().Configure(host,null,dialogue,cinematic,entries,binding,cameraService,chase);
            var lightRoot=new GameObject("KitchenLight"); lightRoot.transform.SetParent(root.transform,false);
            var light=lightRoot.AddComponent<Light>(); light.type=LightType.Directional; light.intensity=1.5f;
            light.color=new Color(1,.92f,.8f); lightRoot.transform.rotation=Quaternion.Euler(48,-25,0);
            light.shadows=LightShadows.Soft;
        }
        private static GameObject Instantiate(string path,Transform parent=null)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(Require<GameObject>(path));
            if(parent!=null){go.transform.SetParent(parent,false);
                if(go.transform is RectTransform rect){rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.offsetMin=rect.offsetMax=Vector2.zero;}}
            return go;
        }
        private static void Author(string path,Action build)
        {
            Scene previous=SceneManager.GetActiveScene();
            Scene existing=SceneManager.GetSceneByPath(path);
            bool reopen=existing.IsValid() && existing.isLoaded;
            bool restoreActive=reopen && previous==existing;
            bool saved=false;
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene); build();
                foreach(var root in scene.GetRootGameObjects())
                    foreach(var transform in root.GetComponentsInChildren<Transform>(true))
                        if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject)!=0)
                            throw new InvalidOperationException("Missing script: "+transform.name);
                if(reopen) EditorSceneManager.CloseScene(existing,true);
                if(!EditorSceneManager.SaveScene(scene,path))throw new IOException("Scene save failed: "+path);
                saved=true;
            }
            finally
            {
                if(saved && reopen) { if(restoreActive) previous=scene; }
                else
                {
                    if(reopen && !existing.isLoaded)
                    { var restored=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive); if(restoreActive) previous=restored; }
                    EditorSceneManager.CloseScene(scene,true);
                }
                if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
            }
        }
        private static T Require<T>(string path) where T:UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException("Missing "+typeof(T).Name+": "+path);
        private static T GetOrCreate<T>(string path) where T:ScriptableObject
        { var asset=AssetDatabase.LoadAssetAtPath<T>(path);if(asset==null){asset=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(asset,path);}return asset; }
        private static void EnsureFolder(string path)
        { if(AssetDatabase.IsValidFolder(path))return;string parent=path.Substring(0,path.LastIndexOf('/'));EnsureFolder(parent);AssetDatabase.CreateFolder(parent,Path.GetFileName(path)); }
        private static void RequireCleanEditor()
        {
            AuthoringOutputScope.RequireEditMode();
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Edit mode required.");
        }
    }
    /// <summary>Scoped output mapping for main's isolated native first/second authoring. No runtime/global settings changes.</summary>
    public static class AuthoringOutputScope
    {
        public const string OwnedRoot = "Assets/_Scenes/Tech_SYM";
        public const string TemporaryRoot = OwnedRoot + "/_Temporary/system-v11-T8";
        private static string temporaryRoot;
        private static readonly System.Collections.Generic.HashSet<Scene> ownedScratchScenes = new System.Collections.Generic.HashSet<Scene>();
        public static bool IsTemporary => temporaryRoot != null;
        public static string Map(string canonicalPath)
        {
            if (string.IsNullOrEmpty(canonicalPath) || canonicalPath.Contains("..") || canonicalPath.Contains("\\") ||
                !(canonicalPath == OwnedRoot || canonicalPath.StartsWith(OwnedRoot + "/", StringComparison.Ordinal)))
                throw new ArgumentException("Only owned SYM canonical output paths may be mapped.", nameof(canonicalPath));
            return temporaryRoot == null ? canonicalPath : temporaryRoot + canonicalPath.Substring(OwnedRoot.Length);
        }
        public static IDisposable BeginTemporary(string path)
        {
            if (temporaryRoot != null) throw new InvalidOperationException("Nested authoring scopes are not supported.");
            if (string.IsNullOrEmpty(path) || !path.StartsWith(TemporaryRoot + "/", StringComparison.Ordinal) ||
                path.Contains("..") || path.Contains("\\") || path.EndsWith("/", StringComparison.Ordinal) ||
                path.Substring((TemporaryRoot + "/").Length).Any(c => !(char.IsLetterOrDigit(c) || c == '-' || c == '_')))
                throw new ArgumentException("Use one named folder below " + TemporaryRoot + ".", nameof(path));
            temporaryRoot = path;
            return new Scope();
        }
        private sealed class Scope : IDisposable
        {
            private bool disposed;
            public void Dispose() { if (disposed) return; disposed = true; temporaryRoot = null; }
        }
        public static void RequireEditMode()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Stable Edit mode is required for authoring.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty && !ownedScratchScenes.Contains(SceneManager.GetSceneAt(i)))
                    throw new InvalidOperationException("Save reviewed dirty scenes explicitly before authoring.");
        }
        /// <summary>Owns a disposable saved scratch under the temporary root; restores the previous active scene.</summary>
        public static IDisposable BeginScene()
        {
            RequireEditMode();
            if (ownedScratchScenes.Contains(SceneManager.GetActiveScene())) return new EmptySceneLease();
            return new SceneLease();
        }
        private sealed class EmptySceneLease : IDisposable { public void Dispose() { } }
        private sealed class SceneLease : IDisposable
        {
            private readonly Scene previous;
            private readonly System.Collections.Generic.List<string> createdFolders = new System.Collections.Generic.List<string>();
            private Scene scratch;
            private string scratchAssetPath;
            private bool disposed;
            public SceneLease()
            {
                previous=SceneManager.GetActiveScene();
                try
                {
                    string folder=temporaryRoot ?? TemporaryRoot;
                    EnsureScratchFolder(folder);
                    scratchAssetPath=AssetDatabase.GenerateUniqueAssetPath(folder+"/__AuthoringScratch_"+Guid.NewGuid().ToString("N")+".unity");
                    scratch=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
                    ownedScratchScenes.Add(scratch);
                    // Unity rejects another additive scene while a dirty untitled scene is open.
                    // Save only this owned empty scratch before prefab authoring can dirty it.
                    if(!EditorSceneManager.SaveScene(scratch,scratchAssetPath))
                        throw new IOException("Cannot save owned authoring scratch: "+scratchAssetPath);
                    SceneManager.SetActiveScene(scratch);
                }
                catch
                {
                    try { Dispose(); } catch(Exception cleanupError) { Debug.LogException(cleanupError); }
                    throw; // preserve the acquisition/save failure; report cleanup failure separately
                }
            }
            private void EnsureScratchFolder(string path)
            {
                if(AssetDatabase.IsValidFolder(path)) return;
                int slash=path.LastIndexOf('/');
                if(slash<1) throw new ArgumentException("Invalid scratch folder: "+path);
                EnsureScratchFolder(path.Substring(0,slash));
                if(string.IsNullOrEmpty(AssetDatabase.CreateFolder(path.Substring(0,slash),path.Substring(slash+1))))
                    throw new IOException("Cannot create owned scratch folder: "+path);
                createdFolders.Add(path);
            }
            public void Dispose()
            {
                if(disposed) return;disposed=true;
                try { if(previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous); }
                finally
                {
                    ownedScratchScenes.Remove(scratch);
                    try
                    {
                        if(scratch.IsValid() && scratch.isLoaded && !EditorSceneManager.CloseScene(scratch,true))
                            throw new IOException("Cannot close owned authoring scratch: "+scratchAssetPath);
                    }
                    finally
                    {
                        // The unique asset was reserved by this lease; never delete a user scene.
                        if(!string.IsNullOrEmpty(scratchAssetPath) && AssetDatabase.AssetPathExists(scratchAssetPath) && !AssetDatabase.DeleteAsset(scratchAssetPath))
                            throw new IOException("Cannot delete owned authoring scratch: "+scratchAssetPath);
                        for(int i=createdFolders.Count-1;i>=0;i--)
                        {
                            string folder=createdFolders[i];
                            string absoluteFolder=Path.Combine(Path.GetDirectoryName(Application.dataPath),folder);
                            // Include unimported/hidden files in the emptiness check; preserve any unrelated contents.
                            if(AssetDatabase.IsValidFolder(folder) && Directory.Exists(absoluteFolder) && !Directory.EnumerateFileSystemEntries(absoluteFolder).Any())
                                if(!AssetDatabase.DeleteAsset(folder)) throw new IOException("Cannot delete empty owned scratch folder: "+folder);
                        }
                    }
                }
            }
        }
        public static void SaveOwnedAssets()
        {
            if (!IsTemporary) { AssetDatabase.SaveAssets(); return; }
            // Never flush unrelated dirty real assets as a side effect of the isolated smoke.
            foreach (var path in AssetDatabase.GetAllAssetPaths())
                if (path.StartsWith(temporaryRoot + "/", StringComparison.Ordinal))
                {
                    var asset = AssetDatabase.LoadMainAssetAtPath(path);
                    if (asset != null) AssetDatabase.SaveAssetIfDirty(asset);
                }
        }
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            if (slash < 1) throw new ArgumentException("Invalid asset folder: " + path);
            EnsureFolder(path.Substring(0, slash));
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(path.Substring(0,slash),path.Substring(slash+1))))
                throw new IOException("Cannot create owned output folder: " + path);
        }
    }
}
#endif
