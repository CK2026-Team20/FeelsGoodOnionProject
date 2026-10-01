using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cooked.Contracts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Cooked.Cinematics.Editor
{
    // Uses actual Play Mode and native PlayableDirector; no simulated clock replaces native playback here.
    [InitializeOnLoad]
    public static class CinematicPlayVerification
    {
        private const string PendingKey = "Cooked.Cinematics.PlayVerification.Pending";
        private const string ExitKey = "Cooked.Cinematics.PlayVerification.Exit";
        private static IEnumerator scenario;
        private static CinematicHost host;
        private static readonly List<string> passed = new List<string>();
        private static double deadline;

        static CinematicPlayVerification() { EditorApplication.playModeStateChanged += OnPlayModeChanged; }

        public static void RunBatch()
        {
            Directory.CreateDirectory("output/Tech_SYM/Cinematics");
            SessionState.SetBool(PendingKey, true);
            SessionState.SetInt(ExitKey, -1);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (!SessionState.GetBool(PendingKey, false)) return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                deadline = EditorApplication.timeSinceStartup + 60;
                scenario = Scenario();
                EditorApplication.update += Step;
            }
            if (state == PlayModeStateChange.EnteredEditMode)
            {
                int code = SessionState.GetInt(ExitKey, -1);
                if (code < 0) return;
                SessionState.EraseBool(PendingKey);
                SessionState.EraseInt(ExitKey);
                EditorApplication.Exit(code);
            }
        }

        private static void Step()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline) throw new TimeoutException("Native cinematic verification exceeded 60 seconds.");
                if (!scenario.MoveNext()) Finish(null);
            }
            catch (Exception error) { Finish(error); }
        }

        private static void Finish(Exception error)
        {
            EditorApplication.update -= Step;
            if (scenario is IDisposable disposable) disposable.Dispose();
            scenario = null;
            if (host != null) host.Shutdown();
            UnityEngine.Time.timeScale = 1;
            File.WriteAllText("output/Tech_SYM/Cinematics/native-play-verification.txt",
                (error == null ? "PASS " + passed.Count : "FAILED\n" + error) + "\n" + string.Join("\n", passed));
            SessionState.SetInt(ExitKey, error == null ? 0 : 1);
            EditorApplication.ExitPlaymode();
        }

        private static IEnumerator Scenario()
        {
            passed.Clear();
            var canvasObject = new GameObject("NativeCinematicProbe", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.GetComponent<Canvas>().sortingOrder = 20;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CinematicAssetBuilder.PrefabPath);
            if (prefab == null) throw new InvalidOperationException("Build cinematic assets before native Play verification.");
            var instance = UnityEngine.Object.Instantiate(prefab, canvasObject.transform, false);
            host = instance.GetComponent<CinematicHost>();
            var control = new NativeControlService();
            var service = (CinematicService)host.Initialize(control);
            Check(Application.isPlaying, "Actual Unity Play Mode entered");
            foreach (CinematicId id in new[] { CinematicId.Opening, CinematicId.Ending })
            {
                var prepare = new OperationResult();
                var preparing = service.Prepare(id, CancellationToken.None, prepare);
                while (preparing.MoveNext()) yield return null;
                Check(prepare.Status == OperationStatus.Succeeded, id + " native Prepare: " + prepare.Error);
                int startFrame = UnityEngine.Time.frameCount;
                double waitUntil = EditorApplication.timeSinceStartup + 0.25;
                while (EditorApplication.timeSinceStartup < waitUntil) yield return null;
                Check(service.Time == 0 && service.Presentation.Frame[0].Artwork.name == id + "_01", id + " first frame holds before Play");
                Check(UnityEngine.Time.frameCount > startFrame, "Real engine frames advanced during first-frame hold");
                var result = new OperationResult();
                UnityEngine.Time.timeScale = id == CinematicId.Opening ? 0 : 1;
                var playing = service.Play(CancellationToken.None, result);
                playing.MoveNext();
                waitUntil = EditorApplication.timeSinceStartup + 0.25;
                while (EditorApplication.timeSinceStartup < waitUntil) yield return null;
                Check(service.Time > 0 && service.Time < 2, id + " Unity advances native Director time");
                if (id == CinematicId.Opening) Check(UnityEngine.Time.timeScale == 0, "Native unscaled Timeline advances at timeScale zero");
                double pausedAt = service.Time;
                float pausedZoom = service.Presentation.Frame[0].Scale;
                using (control.PausePresentation("options"))
                {
                    waitUntil = EditorApplication.timeSinceStartup + 0.25;
                    while (EditorApplication.timeSinceStartup < waitUntil) yield return null;
                    Check(Math.Abs(service.Time - pausedAt) < 0.000001, id + " native options pause exact");
                    Check(Math.Abs(service.Presentation.Frame[0].Scale - pausedZoom) < 0.000001, id + " native options preserve Tween zoom sample");
                    service.Skip();
                    Check(service.Outcome == CinematicOutcome.None, id + " native pause blocks Skip");
                }
                while (result.Status == OperationStatus.Pending) yield return null;
                Check(service.Outcome == CinematicOutcome.Completed, id + " native 11.1 second natural completion");
                Check(!playing.MoveNext() && service.Presentation.IsVisible, id + " last image held after iterator completion");
                service.Hide();
            }
            var skipPrepare = new OperationResult();
            var preparingSkip = service.Prepare(CinematicId.Opening, CancellationToken.None, skipPrepare);
            while (preparingSkip.MoveNext()) yield return null;
            var skipped = new OperationResult();
            var skipping = service.Play(CancellationToken.None, skipped);
            skipping.MoveNext(); yield return null;
            instance.GetComponentInChildren<UnityEngine.UI.Button>().onClick.Invoke();
            Check(service.Outcome == CinematicOutcome.Skipped && skipped.Status == OperationStatus.Succeeded, "Native VM-bound button Skip succeeds");
            Check(!skipping.MoveNext() && service.Presentation.IsVisible, "Native Skip retains image until Hide");
            host.Shutdown();
            Check(control.SubscriberCount == 0, "Native Shutdown releases control listeners");
            UnityEngine.Object.Destroy(canvasObject);
        }

        private static void Check(bool condition, string detail)
        {
            if (!condition) throw new InvalidOperationException(detail);
            passed.Add(detail);
        }
        private sealed class NativeControlService : IGameplayControlService
        {
            private bool paused;
            private Action<ControlState> changed;
            public int SubscriberCount => changed?.GetInvocationList().Length ?? 0;
            public ControlState State => new ControlState(true, false, paused);
            public event Action<ControlState> Changed { add { changed += value; } remove { changed -= value; } }
            public IDisposable PausePresentation(string owner)
            {
                paused = true; changed?.Invoke(State);
                return new Lease(() => { paused = false; changed?.Invoke(State); });
            }
            public IDisposable BlockGameplay(string owner) => new Lease(null);
            public IDisposable PauseWorld(string owner) => new Lease(null);
            public void Dispose() { changed = null; }
            private sealed class Lease : IDisposable
            {
                private Action release;
                public Lease(Action release) { this.release = release; }
                public void Dispose() { var action = release; release = null; action?.Invoke(); }
            }
        }
    }
}
