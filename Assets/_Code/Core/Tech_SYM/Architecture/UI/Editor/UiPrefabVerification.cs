#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Cooked.Contracts;
using TMPro;
using UnityEditor;
using UnityEngine;
namespace Cooked.UI.Editor
{
    public static class UiPrefabVerification
    {
        [MenuItem("Cooked/UI/Build and verify prefabs")]
        public static void Run()
        {
            var results = new List<string>();
            Action<bool,string> check = (valid,name) => { if (!valid) throw new InvalidOperationException(name); results.Add("PASS: " + name); };
            UiPrefabBuilder.BuildAll();
            UiLogicVerification.Run(); results.AddRange(UiLogicVerification.Results);
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UiPrefabBuilder.FontPath);
            check(font != null && font.HasCharacters("어니 포포 눈물 조각 체크포인트 옵션"), "Saved font contains Korean UI glyphs");
            var roots = new List<GameObject>();
            var scratch = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            try
            {
                foreach (string path in new[] { UiPrefabBuilder.GlobalPath, UiPrefabBuilder.TitlePath, UiPrefabBuilder.GamePath })
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path); check(prefab != null, "Saved prefab " + path);
                    check(prefab.GetComponent<RectTransform>() == null && prefab.GetComponent<Canvas>() == null, "Empty Transform root " + prefab.name);
                    var root = UnityEngine.Object.Instantiate(prefab); roots.Add(root); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scratch);
                    foreach (var t in root.GetComponentsInChildren<Transform>(true)) check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0, "No missing script " + root.name + "/" + t.name);
                    check(root.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true).Length == 0, "UI prefab does not duplicate EventSystem");
                    foreach (var text in root.GetComponentsInChildren<TMP_Text>(true)) check(text.font == font && !text.raycastTarget, "TMP font and passive raycast " + text.name);
                }
                var global = roots[0]; var title = roots[1]; var game = roots[2];
                var gameRoot = game.GetComponent<GameUiRoot>();
                check(game.GetComponentsInChildren<Canvas>(true).Length == 2, "Core has one screen Canvas and one world Canvas only");
                check(gameRoot.CinematicMount.parent == gameRoot.DialogueMount.parent && gameRoot.CinematicMount.GetSiblingIndex() > gameRoot.DialogueMount.GetSiblingIndex(), "CinematicMount is later sibling of DialogueMount");
                check(global.transform.Find("OptionsCanvas").GetComponent<Canvas>().sortingOrder == 200 && global.transform.Find("FadeCanvas").GetComponent<Canvas>().sortingOrder == 300, "Global ordering Options200 Fade300");
                var flow = new TestFlow(); var settings = new SettingsService(new TestStore(), new TestAudio()); var control = new TestControl(); var options = new OptionsService(flow,control,settings);
                var model = new TitleViewModel(flow,options); var view = title.GetComponent<TitleView>();
                var button = title.transform.Find("TitleCanvas/NewGame").GetComponent<UnityEngine.UI.Button>(); int external=0; button.onClick.AddListener(()=>external++);
                for(int i=0;i<10;i++) view.Bind(model);
                button.onClick.Invoke(); check(flow.Count==1 && external==1,"Ten rebinds produce one View command and preserve external listener");
                view.enabled=false; view.enabled=true; button.onClick.Invoke(); check(flow.Count==2 && external==2,"Re-enable reconnects once");
                view.Unbind();button.onClick.Invoke();check(flow.Count==2 && external==3,"Unbind removes own listener only");
                view.Bind(model);model.Dispose();button.onClick.Invoke();check(flow.Count==2,"Disposed VM still bound rejects click");
                options.Dispose(); settings.Dispose();
            }
            finally
            {
                // Always close our preview scene, even if a component throws during cleanup.
                try { foreach (var root in roots) UnityEngine.Object.DestroyImmediate(root); }
                finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scratch); }
            }
            Directory.CreateDirectory("output/Tech_SYM/UI"); File.WriteAllLines("output/Tech_SYM/UI/editor-prefab-results.txt",results);
            Debug.Log("UI prefab verification passed " + results.Count + " checks. Visual play/build verification remains required.");
        }
        private sealed class TestFlow:IGameFlowService
        {
            public int Count; public FlowSnapshot Snapshot=>new FlowSnapshot(FlowState.Title,false,0,null); public event Action<FlowSnapshot> Changed;
            public bool Request(FlowRequest r){Count++;return true;}public void CancelCurrent(){Changed?.Invoke(Snapshot);}public void Dispose(){}
        }
        private sealed class TestStore:ISettingsStore{public float Read(string k,float f)=>f;public void Write(string k,float v){}public void Save(){}}
        private sealed class TestAudio:IAudioService{public void PlayMusic(string s){}public void StopMusic(){}public void PlaySfx(string s){}public void SetVolumes(float a,float b,float c){}public void Dispose(){}}
        private sealed class TestControl:IGameplayControlService
        {public ControlState State=>new ControlState(false,false,false);public event Action<ControlState> Changed;public IDisposable BlockGameplay(string o)=>new Lease();public IDisposable PauseWorld(string o)=>new Lease();public IDisposable PausePresentation(string o)=>new Lease();public void Dispose(){Changed?.Invoke(State);}private sealed class Lease:IDisposable{public void Dispose(){}}}
    }
}
#endif
