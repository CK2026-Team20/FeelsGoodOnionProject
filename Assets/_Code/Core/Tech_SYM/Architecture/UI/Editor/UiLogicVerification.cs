using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cooked.Contracts;
using DG.Tweening;
namespace Cooked.UI.Editor
{
    // Framework-free behavioral checks: same source can run in Editor and a .NET boundary harness.
    public static class UiLogicVerification
    {
        public static readonly List<string> Results = new List<string>();
        private static void Check(bool value, string name) { if (!value) throw new InvalidOperationException("FAIL: " + name); Results.Add("PASS: " + name); }
#if UNITY_EDITOR
        [UnityEditor.MenuItem("Cooked/UI/Verify logic")]
#endif
        public static void Run()
        {
            Results.Clear();
            var store = new Store(); var audio = new Audio(); var settings = new SettingsService(store, audio);
            Check(audio.Master == 1 && audio.Music == .7f && audio.Sfx == 1, "Settings applied at initialization");
            settings.SetVolumes(.5f, .3f, .8f);
            Check(store.Saves == 0 && audio.Master == .5f, "Volume applied immediately without per-frame persistence");
            bool invalid = false; try { settings.SetVolumes(float.NaN, 1, 1); } catch (ArgumentOutOfRangeException) { invalid = true; }
            Check(invalid && settings.Master == .5f, "Non-finite settings rejected without corrupting state");
            var flow = new Flow(); var control = new Control(); var logs = new List<string>();
            var options = new OptionsService(flow, control, settings, logs.Add);
            var vm = new OptionsViewModel(options, settings);
            options.Open(); options.Open();
            Check(options.IsOpen && control.Count == 0, "Title options acquires no world, input or presentation leases");
            options.Close(); Check(control.Count == 0 && store.Saves == 1, "Close releases all leases and persists once");
            flow.Emit(FlowState.Playing, false);
            var dialogueLease = control.BlockGameplay("Dialogue"); options.Open(); options.Close();
            Check(control.Count == 1 && control.State.GameplayBlocked, "Closing options preserves Dialogue input lease");
            dialogueLease.Dispose();
            flow.Emit(FlowState.Opening, true); options.Open();
            Check(options.IsOpen && control.State.PresentationPaused, "Busy Opening permits options and pauses presentation");
            flow.Emit(FlowState.Opening, true); Check(options.IsOpen, "Repeated stable Opening does not close options");
            bool synchronous = false;
            flow.Changed += s => { if (s.State == FlowState.LoadingGame) synchronous = !options.IsOpen && control.Count == 0; };
            flow.Emit(FlowState.LoadingGame, true); Check(synchronous, "Transition subscriber sees options closed synchronously");
            options.Open(); Check(!options.IsOpen, "Loading rejects option opening");
            flow.Emit(FlowState.Ending, true); vm.Open(); Check(options.IsOpen && !options.CanNavigate, "Busy Ending permits options but rejects navigation");
            vm.ReturnToTitle(); Check(flow.Requests == 0, "Ending navigation cannot bypass cinematic flow");
            flow.Emit(FlowState.Playing, false); options.Open(); vm.Retry();
            Check(flow.Requests == 1 && flow.Last == FlowCommand.RetryCheckpoint && control.Count == 0, "Retry one command and no pause leases before flow request");
            flow.Emit(FlowState.Playing, false); options.Open(); settings.SetVolumes(.2f, .3f, .4f); store.Fail = true;
            flow.Emit(FlowState.Retrying, true);
            Check(control.Count == 0 && logs.Count == 1 && !string.IsNullOrEmpty(options.Error), "Save failure logs contextual error without interrupting flow or retaining leases");
            flow.Emit(FlowState.Playing, false); options.Open(); Check(!string.IsNullOrEmpty(options.Error), "Save error survives next open");
            store.Fail = false; options.Close(); Check(options.Error == null && store.Values[SettingsService.Prefix + "Master"] == .2f, "Next close retries failed persistence successfully");
            vm.Dispose(); vm.Open(); Check(!options.IsOpen, "Disposed VM does not issue option commands");
            int notifications = 0; vm.PropertyChanged += (_, __) => notifications++; settings.SetVolumes(.4f,.4f,.4f);
            Check(notifications == 0, "Disposed VM receives no late notifications");
            var title = new TitleViewModel(flow, options); flow.Emit(FlowState.Title, false); title.NewGame(); title.NewGame();
            Check(flow.Requests == 2, "Repeated new-game input produces one accepted request while flow is busy"); title.Dispose();
#if !COOKED_HEADLESS_LOGIC
            var fade = new FadeService(.3f, DG.Tweening.UpdateType.Manual); var fadeVm = new FadeViewModel(fade);
            var revealResult = new OperationResult(); var reveal = fade.Reveal(CancellationToken.None, revealResult);
            reveal.MoveNext(); DG.Tweening.DOTween.ManualUpdate(.1f,.1f);
            Check(fade.BlocksInput && fade.Alpha < 1 && fade.Alpha > 0, "Real DOTween changes model alpha while input remains blocked");
            var duplicateResult = new OperationResult(); fade.Cover(CancellationToken.None, duplicateResult).MoveNext();
            Check(duplicateResult.Status == OperationStatus.Failed, "Concurrent fade is rejected explicitly");
            for(int i=0;i<5 && revealResult.Status==OperationStatus.Pending;i++){DG.Tweening.DOTween.ManualUpdate(.1f,.1f);reveal.MoveNext();}
            Check(revealResult.Status == OperationStatus.Succeeded && fade.Alpha == 0 && !fade.BlocksInput, "Real DOTween reveal reaches transparent endpoint");
            var coverResult = new OperationResult(); var cover = fade.Cover(CancellationToken.None, coverResult); cover.MoveNext();
            for(int i=0;i<5 && coverResult.Status==OperationStatus.Pending;i++){DG.Tweening.DOTween.ManualUpdate(.1f,.1f);cover.MoveNext();}
            Check(coverResult.Status == OperationStatus.Succeeded && fade.Alpha == 1, "Real DOTween cover reaches opaque endpoint");
            var cts = new CancellationTokenSource(); var cancelledResult = new OperationResult(); var cancelled = fade.Reveal(cts.Token, cancelledResult); cancelled.MoveNext();
            DG.Tweening.DOTween.ManualUpdate(.1f,.1f); float beforeCancel=fade.Alpha; cts.Cancel();
            DG.Tweening.DOTween.ManualUpdate(.5f,.5f); cancelled.MoveNext();
            Check(cancelledResult.Status == OperationStatus.Cancelled && fade.Alpha==beforeCancel, "Token cancellation prevents stale Tween setter and success callback"); cts.Dispose();
            var obsoleteResult = new OperationResult(); var obsolete = fade.Reveal(CancellationToken.None, obsoleteResult); obsolete.MoveNext();
            fade.SetCoveredImmediate(); DG.Tweening.DOTween.ManualUpdate(.5f,.5f); obsolete.MoveNext();
            Check(obsoleteResult.Status == OperationStatus.Cancelled && fade.Alpha == 1, "SetCoveredImmediate kills own Tween without completing old generation");
            var drainResult=new OperationResult();var drain=fade.Reveal(CancellationToken.None,drainResult);drain.MoveNext();
            fade.SetCoveredImmediate();var newerResult=new OperationResult();var newer=fade.Reveal(CancellationToken.None,newerResult);newer.MoveNext();drain.MoveNext();
            for(int i=0;i<5 && newerResult.Status==OperationStatus.Pending;i++){DG.Tweening.DOTween.ManualUpdate(.1f,.1f);newer.MoveNext();}
            Check(drainResult.Status==OperationStatus.Cancelled && newerResult.Status==OperationStatus.Succeeded,"Old coroutine cleanup cannot kill replacement generation Tween");
            float externalValue=0; var unrelated=DG.Tweening.DOTween.To(()=>externalValue,v=>externalValue=v,1,.3f).SetUpdate(DG.Tweening.UpdateType.Manual,true);
            var disposeResult = new OperationResult(); var disposed = fade.Cover(CancellationToken.None, disposeResult); disposed.MoveNext(); fade.Dispose(); disposed.MoveNext();
            DG.Tweening.DOTween.ManualUpdate(.5f,.5f);
            Check(disposeResult.Status == OperationStatus.Cancelled && externalValue==1, "Dispose cancels own Tween and preserves unrelated Tween");
            DG.Tweening.TweenExtensions.Kill(unrelated,false); fadeVm.Dispose();
#endif
            string readerPath = "Assets/_Code/Core/Tech_HMS/Features/PlayerInputReader.cs";
            string reader = File.ReadAllText(readerPath);
            Check(reader.Contains("recoverDebrisAction.AddBinding(\"<Keyboard>/q\")") && reader.Contains("formChangeAction.AddBinding(\"<Keyboard>/r\")") && reader.Contains("tearSkillAction.AddBinding(\"<Keyboard>/e\")"), "Production HMS input source asserts Q recover, R form and E tear");
            var session = new Session(); var bridge = new Bridge(); var hud = new HudViewModel(bridge, session);
            Check(hud.Abilities.Contains("형태 전환 R") && hud.Abilities.Contains("껍질 회수 Q") && hud.Abilities.Contains("상호작용 F") && hud.Abilities.Contains("눈물 E"), "HUD labels match production input mapping");
            int hudChanges = 0; hud.PropertyChanged += (_, __) => hudChanges++; bridge.Emit(new PlayerSnapshot(2,3,4,10));
            Check(hud.Health.Contains("2 / 3") && hud.Fragments.Contains("4 / 10") && hudChanges == 1, "HUD renders bridge snapshot without owning gameplay state");
            hud.Dispose(); bridge.Emit(new PlayerSnapshot(3,3,5,10)); Check(hudChanges == 1, "HUD unsubscribes from replaced/disposed actor bridge");
            var icons = new HudViewModel(bridge, session);
            Check(icons.HeartFill(0)==1 && icons.HeartFill(1)==1 && icons.HeartFill(2)==1 && icons.FragmentCount=="5/10", "Icon HUD projects three full hearts and current/max fragments");
            bridge.Emit(new PlayerSnapshot(1,3,0,5));
            Check(icons.HeartFill(0)==1 && icons.HeartFill(1)==0 && icons.HeartFill(2)==0 && icons.FragmentCount=="0/5", "Damage empties heart slots while fragment display preserves actual maximum");
            bridge.Emit(new PlayerSnapshot(3,6,0,5));
            Check(icons.HeartFill(0)==1 && icons.HeartFill(1)==.5f && icons.HeartFill(2)==0, "Three-heart display normalizes bridge maximum without changing health authority");
            Check(icons.FormUnlocked && icons.TearUnlocked && icons.RecoverUnlocked, "Ability card states project Session unlocks"); icons.Dispose();
            options.Dispose(); settings.Dispose();
            Check(control.Count == 0 && flow.Subscribers == 1, "UI disposes its own flow listener while retaining external listener");
            foreach (string result in Results) UnityEngine.Debug.Log(result);
        }
        private sealed class Store : ISettingsStore
        {
            public readonly Dictionary<string,float> Values = new Dictionary<string,float>(); public int Saves; public bool Fail;
            public float Read(string key,float fallback) => Values.TryGetValue(key,out float v) ? v : fallback;
            public void Write(string key,float value) { Values[key] = value; }
            public void Save() { if (Fail) throw new IOException("simulated storage failure"); Saves++; }
        }
        private sealed class Audio : IAudioService
        { public float Master,Music,Sfx; public void SetVolumes(float a,float b,float c) { Master=a;Music=b;Sfx=c; } public void PlayMusic(string id) {} public void StopMusic() {} public void PlaySfx(string id) {} public void Dispose() {} }
        private sealed class Flow : IGameFlowService
        {
            public FlowSnapshot Snapshot {get;private set;} = new FlowSnapshot(FlowState.Title,false,0,null);
            public event Action<FlowSnapshot> Changed; public int Requests; public FlowCommand Last;
            public int Subscribers => Changed?.GetInvocationList().Length ?? 0;
            public void Emit(FlowState state,bool busy) { Snapshot=new FlowSnapshot(state,busy,Snapshot.Revision+1,null);Changed?.Invoke(Snapshot); }
            public bool Request(FlowRequest request) { if(Snapshot.IsBusy) return false; Requests++;Last=request.Command;Emit(FlowState.LoadingGame,true);return true; }
            public void CancelCurrent() {} public void Dispose() {}
        }
        private sealed class Control : IGameplayControlService
        {
            private int input,world,presentation; public int Count => input+world+presentation;
            public ControlState State => new ControlState(input>0,world>0,presentation>0); public event Action<ControlState> Changed;
            public IDisposable BlockGameplay(string o) { input++;Changed?.Invoke(State);return new Lease(()=>{input--;Changed?.Invoke(State);}); }
            public IDisposable PauseWorld(string o) { world++;Changed?.Invoke(State);return new Lease(()=>{world--;Changed?.Invoke(State);}); }
            public IDisposable PausePresentation(string o) { presentation++;Changed?.Invoke(State);return new Lease(()=>{presentation--;Changed?.Invoke(State);}); }
            public void Dispose() {} private sealed class Lease:IDisposable { private Action release;public Lease(Action a){release=a;}public void Dispose(){var a=release;release=null;a?.Invoke();} }
        }
        private sealed class Session : IGameSessionService
        {
            public bool IsActive => true; public CheckpointSnapshot Checkpoint => new CheckpointSnapshot("stage","cp",4);
            public event Action Changed; public void Begin(CheckpointSnapshot s) {Changed?.Invoke();} public void CaptureCheckpoint(string s,string c,int f) {} public bool IsUnlocked(AbilityId id) => true;
            public void Unlock(AbilityId id) {} public void End() {} public void Dispose() {}
        }
        private sealed class Bridge : IPlayerBridgeService
        {
            public bool HasActor => true; public PlayerSnapshot Snapshot {get;private set;} = new PlayerSnapshot(3,3,0,10);
            public UnityEngine.Transform CameraTarget => null; public event Action<PlayerSnapshot> Changed;
            public void Emit(PlayerSnapshot v){Snapshot=v;Changed?.Invoke(v);} public void Attach(global::PlayerFacade actor) {} public void Detach() {} public void RestoreFragments(int f) {} public void SetDepthMovementAllowed(bool a) {} public void Dispose() {}
        }
    }
}
