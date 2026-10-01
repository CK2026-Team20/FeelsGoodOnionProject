#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Cooked.Contracts;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
namespace Cooked.UI.Editor
{
    // Isolated UI fixture. It proves production Views and InputSystem UI interaction, not the game LOOP.
    public static class UiPlayVerification
    {
        public static GlobalUiRoot Global;
        public static TitleUiRoot Title;
        public static GameUiRoot Game;
        public static OptionsService Options;
        public static SettingsService Settings;
        public static FadeService Fade;
        public static TestFlow Flow;
        public static TestControl Control;
        public static OperationResult FadeResult;
        private static GameObject events, cameraRoot;
        private static Mouse testMouse; private static Keyboard testKeyboard;
        public static readonly List<string> Results = new List<string>();
        public static void Begin()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Play Mode required.");
            Dispose(); Results.Clear();
            testMouse = InputSystem.AddDevice<Mouse>(); testKeyboard = InputSystem.AddDevice<Keyboard>();
            cameraRoot = new GameObject("UiVerificationCamera"); var camera = cameraRoot.AddComponent<Camera>(); camera.tag = "MainCamera"; camera.backgroundColor = new Color(.15f,.22f,.19f); camera.clearFlags = CameraClearFlags.SolidColor;
            events = UiPrefabBuilder.CreateEventSystem();
            Flow = new TestFlow(); Control = new TestControl(); Settings = new SettingsService(new MemoryStore(), new SilentAudio()); Options = new OptionsService(Flow,Control,Settings);
            Fade = new FadeService();
            Global = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(UiPrefabBuilder.GlobalPath)).GetComponent<GlobalUiRoot>(); Global.Bind(Options,Settings,Fade);
            Title = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(UiPrefabBuilder.TitlePath)).GetComponent<TitleUiRoot>(); Title.Bind(Flow,Options);
            Game = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(UiPrefabBuilder.GamePath)).GetComponent<GameUiRoot>(); Game.Bind(new Player(),new Session(),camera); Game.SetGameplayVisible(false);
            Reveal();
        }
        public static void Reveal() { FadeResult = new OperationResult(); Global.StartCoroutine(Fade.Reveal(System.Threading.CancellationToken.None,FadeResult)); }
        public static void Cover() { FadeResult = new OperationResult(); Global.StartCoroutine(Fade.Cover(System.Threading.CancellationToken.None,FadeResult)); }
        public static string ButtonCenter(string path)
        {
            var target = GameObject.Find(path); if(target==null)throw new InvalidOperationException(path);
            var r=target.GetComponent<RectTransform>();var p=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center));return p.x+","+p.y;
        }
        public static void MouseDown(string path)
        {
            var target = GameObject.Find(path); if(target==null)throw new InvalidOperationException(path);
            var r=target.GetComponent<RectTransform>(); var p=RectTransformUtility.WorldToScreenPoint(null,r.TransformPoint(r.rect.center));
            InputSystem.QueueStateEvent(testMouse,new MouseState{position=p}.WithButton(MouseButton.Left));
        }
        public static void ClickNative(string path)
        {
            MouseDown(path); InputSystem.Update(); UnityEngine.EventSystems.EventSystem.current.currentInputModule.Process();
            MouseUp(); InputSystem.Update(); UnityEngine.EventSystems.EventSystem.current.currentInputModule.Process();
        }
        public static void MouseUp() => InputSystem.QueueStateEvent(testMouse,new MouseState {position=testMouse.position.ReadValue()});
        public static void EscapeDown() => InputSystem.QueueStateEvent(testKeyboard,new KeyboardState(Key.Escape));
        public static void EscapeUp() => InputSystem.QueueStateEvent(testKeyboard,new KeyboardState());
        public static void Playing()
        { Title.gameObject.SetActive(false); Flow.Emit(FlowState.Playing,false); Game.SetGameplayVisible(true); Game.Prompt.SetPrompt(true,new Vector3(0,0,4),Quaternion.identity,"트레이 움직이기"); }
        public static void Opening() { Title.gameObject.SetActive(false); Game.SetGameplayVisible(false); Flow.Emit(FlowState.Opening,true); }
        public static void Check(bool condition,string name)
        { if(!condition)throw new InvalidOperationException("FAIL: "+name); Results.Add("PASS: "+name);System.IO.File.WriteAllLines("output/Tech_SYM/UI/evidence/native-ui-results.txt",Results); }
        public static void Dispose()
        {
            if(Global!=null)Global.Unbind();if(Title!=null)Title.Unbind();if(Game!=null)Game.Unbind();Options?.Dispose();Settings?.Dispose();Fade?.Dispose();
            if(Global!=null)UnityEngine.Object.Destroy(Global.gameObject);if(Title!=null)UnityEngine.Object.Destroy(Title.gameObject);if(Game!=null)UnityEngine.Object.Destroy(Game.gameObject);
            if(events!=null)UnityEngine.Object.Destroy(events);if(cameraRoot!=null)UnityEngine.Object.Destroy(cameraRoot);
            if(testMouse!=null)InputSystem.RemoveDevice(testMouse);if(testKeyboard!=null)InputSystem.RemoveDevice(testKeyboard);
            Global=null;Title=null;Game=null;Options=null;Settings=null;Fade=null;testMouse=null;testKeyboard=null;Time.timeScale=1;
        }
        public sealed class TestFlow:IGameFlowService
        {
            public int Count;public FlowCommand Last;public FlowSnapshot Snapshot{get;private set;}=new FlowSnapshot(FlowState.Title,false,0,null);public event Action<FlowSnapshot> Changed;
            public void Emit(FlowState state,bool busy){Snapshot=new FlowSnapshot(state,busy,Snapshot.Revision+1,null);Changed?.Invoke(Snapshot);}
            public bool Request(FlowRequest r){if(Snapshot.IsBusy)return false;Count++;Last=r.Command;Emit(FlowState.LoadingGame,true);return true;}public void CancelCurrent(){}public void Dispose(){}
        }
        public sealed class TestControl:IGameplayControlService
        {
            private int i,w,p;public int Count=>i+w+p;public ControlState State=>new ControlState(i>0,w>0,p>0);public event Action<ControlState> Changed;
            public IDisposable BlockGameplay(string o){i++;Changed?.Invoke(State);return new Lease(()=>{i--;Changed?.Invoke(State);});}
            public IDisposable PauseWorld(string o){w++;Time.timeScale=0;Changed?.Invoke(State);return new Lease(()=>{w--;Time.timeScale=w>0?0:1;Changed?.Invoke(State);});}
            public IDisposable PausePresentation(string o){p++;Changed?.Invoke(State);return new Lease(()=>{p--;Changed?.Invoke(State);});}public void Dispose(){}
            private sealed class Lease:IDisposable{private Action a;public Lease(Action value){a=value;}public void Dispose(){var b=a;a=null;b?.Invoke();}}
        }
        private sealed class MemoryStore:ISettingsStore{public float Read(string k,float f)=>f;public void Write(string k,float v){}public void Save(){}}
        private sealed class SilentAudio:IAudioService{public void PlayMusic(string s){}public void StopMusic(){}public void PlaySfx(string s){}public void SetVolumes(float a,float b,float c){}public void Dispose(){}}
        private sealed class Player:IPlayerBridgeService
        {public bool HasActor=>true;public PlayerSnapshot Snapshot=>new PlayerSnapshot(3,3,8,20);public Transform CameraTarget=>null;public event Action<PlayerSnapshot> Changed;public void Attach(global::PlayerFacade a){Changed?.Invoke(Snapshot);}public void Detach(){}public void RestoreFragments(int f){}public void SetDepthMovementAllowed(bool a){}public void Dispose(){}}
        private sealed class Session:IGameSessionService
        {public bool IsActive=>true;public CheckpointSnapshot Checkpoint=>new CheckpointSnapshot("03_2_Stage","OVEN_SAFE",8);public event Action Changed;public bool IsUnlocked(AbilityId a)=>true;public void Begin(CheckpointSnapshot s){Changed?.Invoke();}public void CaptureCheckpoint(string s,string c,int f){}public void Unlock(AbilityId a){}public void End(){}public void Dispose(){}}
    }
}
#endif
