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
        public const string Root = "Assets/_Scenes/Tech_SYM/Architecture";
        public const string EntriesPath = Root + "/Configuration/IntegrationEntrySettings.asset";
        public static SceneCatalog Catalog => new SceneCatalog(Root+"/01_Bootstrapper.unity",Root+"/02_Title.unity",
            Root+"/03_InGameCore.unity",Enumerable.Range(1,3).Select(i=>Root+"/Stages/03_"+i+"_Stage.unity"));
        [MenuItem("Cooked/Core/Build Core Owned Assets")]
        public static void BuildCoreAssets()
        {
            RequireCleanEditor();
            Cooked.Session.Editor.SessionActorBuilder.Build();
            Cooked.Chase.Editor.ChaseAssetBuilder.Build();
            Cooked.Level.Editor.LevelAssetBuilder.Build();
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
                Root+"/Dialogue/Data/DialogueTable.json"
            };
            foreach(string path in required) if(AssetDatabase.LoadMainAssetAtPath(path)==null)
                throw new InvalidOperationException("Required real dependency is not authored: "+path);
            foreach(string path in Catalog.StagePaths) Require<SceneAsset>(path);
            EnsureFolder(Root+"/Configuration");
            var installation=GetOrCreate<ArchitectureInstallation>(ArchitectureInstallation.AssetPath);
            installation.Configure(Catalog); EditorUtility.SetDirty(installation);
            var entries=GetOrCreate<IntegrationEntrySettings>(EntriesPath);
            entries.Configure(new[]{
                new IntegrationEntrySettings.Entry("03_1_Stage","S1_START",0),
                new IntegrationEntrySettings.Entry("03_2_Stage","S2_ENTRY",1,AbilityId.FormChange,AbilityId.RecoverShell,AbilityId.Tear),
                new IntegrationEntrySettings.Entry("03_3_Stage","S3_ENTRY",1,AbilityId.FormChange,AbilityId.RecoverShell,AbilityId.Tear)
            });
            EditorUtility.SetDirty(entries);
            Author(Catalog.BootstrapPath,()=>BuildBootstrap(installation));
            Author(Catalog.TitlePath,()=>Instantiate(Cooked.UI.Editor.UiPrefabBuilder.TitlePath));
            Author(Catalog.CorePath,()=>BuildCore(entries));
            AssetDatabase.SaveAssets();
            Debug.Log("[Cooked.Core] Final six scene assets linked. Actual gameplay verification is still required.");
        }
        private static void BuildBootstrap(ArchitectureInstallation installation)
        {
            var root=new GameObject("BootstrapRoot");
            var bootstrap=root.AddComponent<Bootstrapper>();
            bootstrap.ConfigureDialogueTable(Require<TextAsset>(Root+"/Dialogue/Data/DialogueTable.json"));
            var ui=Instantiate(Cooked.UI.Editor.UiPrefabBuilder.GlobalPath).GetComponent<GlobalUiRoot>();
            ui.transform.SetParent(root.transform,false);
            var audio=Instantiate(Cooked.Audio.Editor.AudioPrefabBuilder.PrefabPath).GetComponent<AudioRuntimeHost>();
            audio.transform.SetParent(root.transform,false);
            var eventRoot=new GameObject("EventSystem");
            eventRoot.transform.SetParent(root.transform,false);
            eventRoot.AddComponent<EventSystem>();
            eventRoot.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
            var cameraRoot=new GameObject("BootstrapCamera");
            cameraRoot.transform.SetParent(root.transform,false);
            var camera=cameraRoot.AddComponent<Camera>(); camera.cullingMask=0; camera.depth=-100;
            camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=Color.black;
            cameraRoot.AddComponent<AudioListener>();
            root.AddComponent<AppComposition>().Configure(bootstrap,installation,ui,audio);
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
            var camera=cameraRoot.GetComponentInChildren<Camera>(true);
            var cameraService=cameraRoot.GetComponent<LevelCameraService>();
            var dialogue=Instantiate(Cooked.Dialogue.Editor.DialoguePrefabBuilder.PrefabPath,ui.DialogueMount).GetComponent<DialogueView>();
            var driver=root.AddComponent<DialogueDriver>();
            var cinematic=Instantiate(Cooked.Cinematics.Editor.CinematicAssetBuilder.PrefabPath,ui.CinematicMount).GetComponent<CinematicHost>();
            var chase=Instantiate(Cooked.Chase.Editor.ChaseAssetBuilder.PrefabPath).GetComponent<ChaseRuntimeDriver>();
            chase.transform.SetParent(root.transform,false);
            Instantiate(Cooked.Background.Editor.KitchenContentBackdropBuilder.PrefabPath).transform.SetParent(root.transform,false);
            var binding=root.AddComponent<CoreUiBinding>();
            var screen=ui.DialogueMount.parent;
            var instruction=BuildInstruction(screen,Require<TMP_FontAsset>(Cooked.UI.Editor.UiPrefabBuilder.FontPath));
            instruction.transform.SetSiblingIndex(ui.DialogueMount.GetSiblingIndex());
            ui.CinematicMount.SetAsLastSibling();
            binding.Configure(ui,camera,instruction);
            root.AddComponent<CoreComposition>().Configure(host,driver,dialogue,cinematic,entries,binding,cameraService,camera,chase);
            var lightRoot=new GameObject("KitchenLight"); lightRoot.transform.SetParent(root.transform,false);
            var light=lightRoot.AddComponent<Light>(); light.type=LightType.Directional; light.intensity=1.5f;
            light.color=new Color(1,.92f,.8f); lightRoot.transform.rotation=Quaternion.Euler(48,-25,0);
            light.shadows=LightShadows.Soft;
        }
        private static InstructionView BuildInstruction(Transform parent,TMP_FontAsset font)
        {
            var root=new GameObject("InstructionHud",typeof(RectTransform),typeof(CanvasGroup));
            var rect=(RectTransform)root.transform; rect.SetParent(parent,false);
            rect.anchorMin=new Vector2(.2f,.72f);rect.anchorMax=new Vector2(.8f,.82f);rect.offsetMin=rect.offsetMax=Vector2.zero;
            var labelObject=new GameObject("Label",typeof(RectTransform));
            var labelRect=(RectTransform)labelObject.transform;labelRect.SetParent(root.transform,false);
            labelRect.anchorMin=Vector2.zero;labelRect.anchorMax=Vector2.one;labelRect.offsetMin=labelRect.offsetMax=Vector2.zero;
            var text=labelObject.AddComponent<TextMeshProUGUI>();text.font=font;text.fontSize=26;text.color=Color.white;
            text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;
            var view=root.AddComponent<InstructionView>();view.Configure(root.GetComponent<CanvasGroup>(),text);
            return view;
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
            if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Edit mode required.");
            for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Dirty scene must be saved by its owner first.");
        }
    }
}
#endif