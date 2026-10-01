#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Cooked.Contracts;
using Cooked.Foundation;
using Cooked.Level;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Cooked.Integration.Verification
{
    /// <summary>Opt-in sequential observation runner. All game actions use virtual device input.
    /// No teleports, service commands, puzzle setters, or game-state restoration are used here.</summary>
    public sealed class LoopVerificationHost : MonoBehaviour
    {
        [Serializable] public sealed class Observation
        {
            public string step, utc, flow, checkpoint, position, detail;
            public string actor, core; public int fragments, health;
        }
        [Serializable] public sealed class Report
        {
            public string scenario, startedUtc, endedUtc, outcome = "Running", failure;
            public bool playerBuild, fullLoopVerified;
            public List<Observation> observations = new List<Observation>();
        }
        public static LoopVerificationHost Active { get; private set; }
        public bool Finished { get; private set; }
        public Report Result { get; private set; }
        private AppComposition app;
        private VerificationInputService input;
        private string outputPath, step;
        private IEnumerator supervised;
        private OperationResult result;
        private bool savedBackground, expectedRetry;
        private Transform expectedActor;
        private Cooked.UI.FadeService observedFade; private float previousAlpha; private int fadeDirection, covers, reveals;
        private GameSessionRuntime Session => ((GameRuntimeService)app.Services.Get<IGameRuntimeService>()).CurrentSession;
        private IGameFlowService Flow => app.Services.Get<IGameFlowService>();
        private IGameplayControlService Control => app.Services.Get<IGameplayControlService>();
        private Transform Body => Session?.Player.BodyTransform;

        public static LoopVerificationHost Begin(string scenario = "full")
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Wait for EnteredPlayMode before starting verification.");
            if (Active != null) throw new InvalidOperationException("A verification run is already active.");
            var go = new GameObject("CookedInputVerification");
            DontDestroyOnLoad(go); // Verification lifetime only; not installed in any production scene.
            var host = go.AddComponent<LoopVerificationHost>();
            host.StartRun(scenario);
            return host;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartRequestedPlayerRun()
        {
            if (!Debug.isDebugBuild || Application.isEditor) return;
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-cooked-verify") >= 0) Begin();
        }
        private void StartRun(string scenario)
        {
            Active = this;
            savedBackground = Application.runInBackground; Application.runInBackground = true;
            Result = new Report { scenario = scenario, startedUtc = DateTime.UtcNow.ToString("O"), playerBuild = !Application.isEditor };
            string dir = Path.Combine(Application.dataPath, "..", "output", "Tech_SYM", "integration", "evidence", "sequential");
            Directory.CreateDirectory(dir);
            outputPath = Path.GetFullPath(Path.Combine(dir, DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + scenario + ".json"));
            Write(); result = new OperationResult();
            supervised = CoroutineOperationRunner.Run(Execute(scenario), result);
            StartCoroutine(Supervise());
        }
        private IEnumerator Supervise()
        {
            yield return supervised;
            supervised = null;
            Finish(result.Status == OperationStatus.Succeeded ? "Passed" : "Failed", result.Error);
            if (!Application.isEditor && result.Status == OperationStatus.Succeeded)
                using (input = new VerificationInputService(message => { })) { yield return Click("Quit"); }
        }
        private IEnumerator Execute(string scenario)
        {
            yield return Wait("bootstrap-ready", () =>
            {
                app = FindAnyObjectByType<AppComposition>();
                return app != null && app.Services != null && app.Services.TryGet<IGameFlowService>(out _);
            }, 30);
            observedFade = (Cooked.UI.FadeService)app.Services.Get<IFadeService>(); previousAlpha = observedFade.Alpha;
            if (scenario == "entry")
            {
                yield return Wait("entry-ready", () => !Flow.Snapshot.IsBusy &&
                    (Flow.Snapshot.State == FlowState.Title || Flow.Snapshot.State == FlowState.Playing), 35);
                Observe("Entry destination; no gameplay completion claimed"); result.Succeed(); yield break;
            }
            using (input = new VerificationInputService(message => { }))
            {
                for (int run = 1; run <= 2; run++)
                {
                    yield return Wait("game-" + run + "/title", () => Flow.Snapshot.State == FlowState.Title && !Flow.Snapshot.IsBusy, 35);
                    yield return Keys(.06f, Key.Escape); yield return Delay(.25f);
                    Check(!Control.State.WorldPaused && !Control.State.PresentationPaused, "Title options paused title presentation/world");
                    yield return Keys(.06f, Key.Escape); yield return Delay(.25f);
                    int coversBefore = covers, revealsBefore = reveals;
                    yield return Click("NewGame");
                    yield return Wait("opening", () => Flow.Snapshot.State == FlowState.Opening, 30);
                    if (run == 2)
                    {
                        yield return Click("SkipButton");
                    }
                    yield return Wait("playing", Playing, 25);
                    expectedActor = Body;
                    yield return StageOne();
                    yield return StageTwo();
                    yield return StageThree();
                    yield return Wait("ending", () => Flow.Snapshot.State == FlowState.Ending, 20);
                    if (run == 2) yield return Click("SkipButton");
                    yield return Wait("title-return", () => Flow.Snapshot.State == FlowState.Title && !Flow.Snapshot.IsBusy, 30);
                    Check(Session == null, "Session remained after title return");
                    yield return null;
                    Check(covers - coversBefore == 5 && reveals - revealsBefore == 5, "Expected five cover/reveal transitions per game (load/opening-end/retry/ending-start/title); observed="+(covers-coversBefore)+"/"+(reveals-revealsBefore));
                    Check(FindObjectsByType<StageService>(FindObjectsSortMode.None).Length == 0 && FindObjectsByType<PlayerFacade>(FindObjectsSortMode.None).Length == 0 && UnityEngine.SceneManagement.SceneManager.sceneCount == 2, "Gameplay objects/scenes remained at Title");
                    Observe("Completed game " + run + " through input, world triggers and cinematic result");
                }
            }
            input = null;
            Result.fullLoopVerified = true;
            result.Succeed();
        }
        private IEnumerator StageOne()
        {
            step = "S1/gap-form-shell"; Observe("begin");
            yield return Keys(.32f, Key.D); yield return Keys(.65f, Key.D, Key.Space); yield return Delay(.7f);
            yield return Keys(.78f, Key.D, Key.Space); yield return Delay(.7f);
            Check(Session.Session.IsUnlocked(AbilityId.FormChange), "Form unlock trigger not reached");
            yield return Keys(.32f, Key.D); yield return Delay(.3f); yield return Keys(.06f, Key.R); yield return Delay(.8f);
            yield return Keys(.7f, Key.D); yield return Delay(.3f);
            Check(Puzzle("03_1_Stage").IsSolved, "Real Stage1 shell did not solve pressure gate");
            yield return Keys(.45f, Key.A); yield return Delay(.3f); yield return Keys(.06f, Key.Q); yield return Delay(.3f);
            Check(Body.GetComponent<PlayerFormController>().IsDebrisRecovered, "Stage1 Q recovery failed");
            yield return Drive(new Vector3(17.65f, 0, 0), 5); yield return Delay(.25f);
            yield return Keys(.65f, Key.D, Key.Space); yield return Delay(.7f);
            Check(Session.Session.Checkpoint.CheckpointId == "S1_SKILL", "High-step checkpoint not reached");
            yield return Keys(.88f, Key.D); yield return Delay(.5f); yield return Keys(.27f, Key.D); yield return Delay(.25f);
            step = "S1/tear-expenditure-stun";
            Check(Session.Player.Snapshot.Fragments == 1 && Session.Session.IsUnlocked(AbilityId.Tear), "Missing tear resource or unlock");
            bool consumed = false, stunned = false;
            int previous = Session.Player.Snapshot.Fragments;
            void Fragments(PlayerSnapshot value) { if (value.Fragments < previous) consumed = true; previous = value.Fragments; }
            Session.Player.Changed += Fragments;
            try
            {
                yield return Keys(.06f, Key.E);
                foreach (var enemy in FindObjectsByType<FeelsGoodOnion.TechSYM.Enemies.EnemyActor>(FindObjectsSortMode.None))
                    if (enemy.gameObject.scene.name == "03_1_Stage" && enemy.IsStunned) stunned = true;
                Check(consumed && stunned && FindAnyObjectByType<TearStunGate>().IsSolved, "E must consume a fragment and stun the real enemy");
                Observe("Observed fragment decrease event and EnemyActor.IsStunned; subsequent refill is allowed");
                ScreenCapture.CaptureScreenshot(Path.ChangeExtension(outputPath, "stage1.png"));
            }
            finally { Session.Player.Changed -= Fragments; }
            yield return Drive(new Vector3(35.8f, 0, 0), 8); yield return Delay(.7f);
            yield return Drive(new Vector3(36, 0, 3), 6); yield return Delay(.3f);
            Check(Session.Dialogue.IsActive && Session.Session.Checkpoint.CheckpointId == "S2_ENTRY", "Stage2 dialogue/checkpoint entry failed");
            Observe("Stage1 completed through normal input");
        }
        private IEnumerator StageTwo()
        {
            step = "S2/dialogue";
            Vector3 before = Body.position;
            yield return Keys(.35f, Key.D, Key.Space, Key.R, Key.E);
            Check(Vector3.Distance(before, Body.position) < .01f, "Dialogue allowed player movement");
            yield return Click("Auto"); yield return Delay(.2f); yield return Click("History"); yield return Delay(.2f);
            Check(Session.Dialogue.LogOpen && !Session.Dialogue.AutoEnabled, "History did not disable auto");
            yield return Keys(.06f, Key.Escape); yield return Delay(.25f);
            Check(Session.Dialogue.OptionsPaused && Control.State.WorldPaused, "ESC options did not pause dialogue/world");
            yield return Keys(.06f, Key.Escape); yield return Delay(.25f);
            yield return Click("Close", "HistoryOverlay"); yield return Delay(.2f); yield return Click("Skip"); yield return Delay(.3f);
            Check(!Session.Dialogue.IsActive && !Control.State.GameplayBlocked, "Skip was intercepted or did not release dialogue");
            step = "S2/shell-puzzle";
            yield return Keys(.06f, Key.R); yield return Delay(.4f);
            yield return Keys(.55f, Key.W); yield return Keys(.55f, Key.W, Key.A); yield return Delay(.4f);
            yield return Keys(.06f, Key.R); yield return Delay(.8f);
            yield return Keys(.75f, Key.W, Key.A); yield return Delay(.3f);
            Check(Puzzle("03_2_Stage").IsSolved, "Stage2 pressure gate not solved");
            yield return Keys(.5f, Key.S, Key.D); yield return Delay(.3f); yield return Keys(.06f, Key.Q); yield return Delay(.3f);
            yield return Keys(.9f, Key.W, Key.A); yield return Delay(.3f);
            Check(Body.GetComponent<PlayerFormController>().IsDebrisRecovered, "Stage2 Q recovery failed");
            yield return Drive(new Vector3(37, 0, 11), 6);
            yield return Keys(.75f, Key.W, Key.A, Key.Space); yield return Delay(.7f);
            yield return Drive(new Vector3(36, 0, 15), 6);
            Check(Session.Session.Checkpoint.CheckpointId == "S2_OVEN", "Oven checkpoint not reached");
            step = "S2/oven";
            yield return Keys(.08f, Key.W, Key.A);
            yield return Wait("oven-interaction-visible", () => Session.ActorHost.Interaction.IsVisible, 3);
            yield return Keys(.06f, Key.F); yield return Delay(1.5f);
            var tray = FindAnyObjectByType<FeelsGoodOnion.TechSYM.OvenTray.OvenTrayObject>();
            Check(tray.State == FeelsGoodOnion.TechSYM.OvenTray.OvenTrayState.Open, "F failed to open tray");
            Observe("Oven open at " + tray.transform.position);
            // Geometric targets only choose keys; Drive never assigns a Transform or Rigidbody.
            yield return Drive(new Vector3(37, 0, 15), 4);
            yield return Drive(new Vector3(37, 0, 16.2f), 5);
            ScreenCapture.CaptureScreenshot(Path.ChangeExtension(outputPath, "oven.png"));
            yield return Keys(.6f, Key.W, Key.A, Key.Space); yield return Keys(.25f, Key.A);
            yield return Drive(new Vector3(36, .2f, 20.5f), 6);
            yield return Drive(new Vector3(36, .2f, 22.5f), 6);
            yield return Drive(new Vector3(36, 0, 25), 6);
            yield return Drive(new Vector3(36, 0, 27.6f), 6);
            yield return Drive(new Vector3(36, 0, 29.4f), 6);
            Observe("Oven crossed on real collision surfaces");
        }
        private IEnumerator StageThree()
        {
            step = "S3/shell-puzzle";
            yield return Wait("side3-camera", () => FindAnyObjectByType<LevelCameraService>().CurrentZoneId == "SIDE_3", 4);
            yield return Drive(new Vector3(31, 0, Body.position.z), 7);
            if (Body.GetComponent<PlayerFormController>().IsSmall) { yield return Keys(.06f, Key.R); yield return Delay(.4f); }
            yield return Keys(.06f, Key.R); yield return Delay(.8f);
            yield return Keys(.7f, Key.D); yield return Delay(.3f);
            Check(Puzzle("03_3_Stage").IsSolved, "Stage3 shell gate failed");
            yield return Keys(.45f, Key.A); yield return Delay(.3f); yield return Keys(.06f, Key.Q); yield return Delay(.3f);
            yield return Keys(1.1f, Key.D); yield return Delay(.3f);
            yield return Keys(.5f, Key.D, Key.Space); yield return Delay(.6f);
            yield return Drive(new Vector3(20, 0, Body.position.z), 8);
            Check(Session.Session.Checkpoint.CheckpointId == "S3_CHASE", "Chase checkpoint not reached");
            // First let the real chase catch the actor. No damage or retry service call.
            step = "S3/chase-failure-retry";
            ScreenCapture.CaptureScreenshot(Path.ChangeExtension(outputPath, "stage3.png"));
            int checkpointFragments = Session.Session.Checkpoint.Fragments;
            expectedRetry = true;
            var oldActor = Body.GetEntityId(); var oldCore = FindAnyObjectByType<CoreComposition>().GetEntityId();
            yield return Keys(.6f, Key.D);
            yield return Wait("chase-accepted", () => Stage("03_3_Stage").IsChaseAccepted, 4);
            yield return Wait("caught-respawn", () => Playing() && Body != null && Body.GetEntityId() != oldActor, 20);
            Check(FindAnyObjectByType<CoreComposition>().GetEntityId() == oldCore, "Retry replaced InGameCore");
            Check(Session.Session.Checkpoint.CheckpointId == "S3_CHASE" && Session.Session.IsUnlocked(AbilityId.Tear), "Retry lost checkpoint/unlocks");
            Check(Session.Player.Snapshot.Fragments == checkpointFragments, "Retry fragments differ from checkpoint");
            expectedRetry = false; expectedActor = Body;
            Observe("Actual chase catch, new Actor, same Core");
            step = "S3/chase-escape";
            if (!Body.GetComponent<PlayerFormController>().IsSmall) { yield return Keys(.06f, Key.R); yield return Delay(.35f); }
            yield return Keys(.75f, Key.D);
            Check(Stage("03_3_Stage").IsChaseAccepted, "Retry chase did not start");
            yield return Keys(.9f, Key.D, Key.Space); yield return Keys(.9f, Key.D);
            yield return Keys(.9f, Key.D, Key.Space); yield return Keys(3f, Key.D);
            yield return Wait("escape-accepted", () => Flow.Snapshot.State == FlowState.Ending, 10);
        }
        private IEnumerator Drive(Vector3 target, float timeout)
        {
            float end = Time.realtimeSinceStartup + timeout;
            while (Vector2.Distance(new Vector2(Body.position.x, Body.position.z), new Vector2(target.x, target.z)) > .28f)
            {
                Check(Playing(), "Flow left Playing while driving to " + target);
                Check(Body.position.y >= target.y + .45f, "Actor fell below route while driving to " + target);
                Check(Time.realtimeSinceStartup < end, "Movement blocked before " + target);
                Vector3 delta = target - Body.position; delta.y = 0; delta.Normalize();
                var camera = Camera.main; var right = camera.transform.right; var forward = camera.transform.forward;
                right.y = forward.y = 0; right.Normalize(); forward.Normalize();
                float x = Vector3.Dot(delta, right), z = Vector3.Dot(delta, forward);
                var keys = new List<Key>();
                if (x > .3f) keys.Add(Key.D); else if (x < -.3f) keys.Add(Key.A);
                if (z > .3f) keys.Add(Key.W); else if (z < -.3f) keys.Add(Key.S);
                yield return Keys(.08f, keys.ToArray());
            }
            Observe("Reached waypoint by input " + target);
        }
        private IEnumerator Keys(float duration, params Key[] keys)
        {
            if (Array.IndexOf(keys, Key.Space) >= 0 && !Control.State.GameplayBlocked)
                yield return Wait(step + "/grounded", () => Body.GetComponent<CharacterMovement>().IsGrounded, 3);
            yield return input.Hold(duration, keys);
            if (!expectedRetry && !ReferenceEquals(expectedActor, null) && Session != null)
                Check(Body == expectedActor, "Unexpected Actor replacement during input; current checkpoint=" + Session.Session.Checkpoint.CheckpointId);
        }
        private IEnumerator Click(string name, string parent = null)
        {
            Button found = null;
            foreach (var button in FindObjectsByType<Button>(FindObjectsSortMode.None))
                if (button.name == name && (parent == null || button.transform.parent.name == parent))
                { if (found != null) throw new InvalidOperationException("Ambiguous button " + name); found = button; }
            Check(found != null, "Button not found: " + name);
            yield return input.Click(found);
        }
        private IEnumerator Wait(string label, Func<bool> ready, float seconds)
        {
            step = label; Write();
            float end = Time.realtimeSinceStartup + seconds;
            while (!ready())
            {
                if (Time.realtimeSinceStartup >= end) throw new TimeoutException("Timed out at " + label);
                yield return null; // Readiness polling only, no presentation animation.
            }
            Observe("Ready");
        }
        private static IEnumerator Delay(float seconds)
        {
            bool done = false;
            var tween = DOVirtual.DelayedCall(seconds, () => done = true, true).SetAutoKill(false);
            try { while (!done) { if (!tween.IsActive()) throw new OperationCanceledException("Verification delay cancelled"); yield return null; } }
            finally { if (tween.IsActive()) tween.Kill(false); }
        }
        private bool Playing() => Flow.Snapshot.State == FlowState.Playing && !Flow.Snapshot.IsBusy;
        private static StageService Stage(string id)
        {
            foreach (var value in FindObjectsByType<StageService>(FindObjectsSortMode.None)) if (value.StageId == id) return value;
            throw new InvalidOperationException("Stage missing " + id);
        }
        private static ShellPressureLatch Puzzle(string id) => Stage(id).GetComponentInChildren<ShellPressureLatch>();
        private void Check(bool value, string message) { if (!value) throw new InvalidOperationException(step + ": " + message); }
        private void Observe(string detail)
        {
            var s = app?.Services != null ? Session : null;
            var core = FindAnyObjectByType<CoreComposition>();
            Result.observations.Add(new Observation { step = step, utc = DateTime.UtcNow.ToString("O"), detail = detail,
                flow = app?.Services != null ? Flow.Snapshot.State.ToString() : "Uninitialized",
                checkpoint = s?.Session.Checkpoint.CheckpointId, position = s?.Player.BodyTransform?.position.ToString("F3"),
                actor = s?.Player.BodyTransform != null ? s.Player.BodyTransform.GetEntityId().ToString() : null,
                core = core != null ? core.GetEntityId().ToString() : null, fragments = s?.Player.Snapshot.Fragments ?? 0, health = s?.Player.Snapshot.Health ?? 0 });
            Write();
        }
        private void Write() { if (outputPath != null) File.WriteAllText(outputPath, JsonUtility.ToJson(Result, true)); }
        private void Finish(string outcome, string error)
        {
            if (Finished) return;
            Finished = true; Result.outcome = outcome; Result.failure = error; Result.endedUtc = DateTime.UtcNow.ToString("O");
            try { Observe(error ?? "Finished"); } catch (Exception captureError) { Result.failure = (Result.failure ?? "") + "\nFinal observation failed: " + captureError; Result.outcome = "Failed"; } finally { if (Application.isEditor || outcome != "Passed") Application.runInBackground = savedBackground; Write(); }
        }
        private void LateUpdate()
        {
            if (observedFade == null || Finished) return;
            float alpha = observedFade.Alpha, delta = alpha - previousAlpha;
            int direction = delta > .00001f ? 1 : delta < -.00001f ? -1 : 0;
            if (direction != 0 && direction != fadeDirection)
            {
                fadeDirection = direction;
                if (direction > 0) covers++; else reveals++;
                Observe("Observed fade " + (direction > 0 ? "cover" : "reveal") + " alpha=" + alpha);
            }
            previousAlpha = alpha;
        }
        private void OnDestroy()
        {
            try { (supervised as IDisposable)?.Dispose(); }
            finally
            {
                input?.Dispose(); input = null;
                if (!Finished && Result != null) Finish("Interrupted", "Verification host destroyed at " + step);
                Application.runInBackground = savedBackground;
                if (Active == this) Active = null;
            }
        }
    }
}
#endif
