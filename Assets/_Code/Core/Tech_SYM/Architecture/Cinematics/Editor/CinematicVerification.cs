using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cooked.Contracts;
using DG.Tweening;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

namespace Cooked.Cinematics.Editor
{
    // Editor graph/integration checks. Full game flow and player-build checks belong to integration.
    public static class CinematicVerification
    {
        private static readonly List<string> passed = new List<string>();

        private const string BatchKey = "Cooked.CinematicVerification.RunBatch";
        public static void RunBatch()
        {
            CinematicAssetBuilder.BuildAll();
            SessionState.SetBool(BatchKey, true);
            EditorApplication.isPlaying = true;
        }
        [InitializeOnLoadMethod]
        private static void ResumeBatch()
        {
            if (!SessionState.GetBool(BatchKey, false)) return;
            EditorApplication.playModeStateChanged += OnBatchState;
        }
        private static void OnBatchState(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredPlayMode) return;
            EditorApplication.playModeStateChanged -= OnBatchState;
            SessionState.SetBool(BatchKey, false);
            int exitCode = 0;
            Directory.CreateDirectory("output/Tech_SYM/Cinematics");
            try
            {
                Verify();
                File.WriteAllText("output/Tech_SYM/Cinematics/editor-verification.txt", "PASS PlayMode " + passed.Count + "\n" + string.Join("\n", passed));
            }
            catch (Exception exception)
            {
                File.WriteAllText("output/Tech_SYM/Cinematics/editor-verification.txt", "FAILED\n" + exception);
                Debug.LogException(exception); exitCode = 1;
            }
            EditorApplication.Exit(exitCode);
        }

        public static void Verify()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Run CinematicVerification in Play Mode: DOTween runtime initialization and owned Kill semantics are required.");
            passed.Clear();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CinematicAssetBuilder.PrefabPath);
            Check(prefab != null, "Prefab exists");
            Check(prefab.GetComponentsInChildren<Canvas>(true).Length == 0, "No private Canvas");
            Check(prefab.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>(true).Length == 0, "No private EventSystem");
            Check(prefab.GetComponentsInChildren<AudioListener>(true).Length == 0, "No private AudioListener");
            var catalog = AssetDatabase.LoadAssetAtPath<CinematicCatalog>(CinematicAssetBuilder.CatalogPath);
            Check(catalog != null && catalog.Opening != null && catalog.Ending != null, "Two serialized Timeline assets");
            var instance = UnityEngine.Object.Instantiate(prefab);
            var control = new ProbeControlService();
            var director = instance.GetComponentInChildren<PlayableDirector>();
            var bridge = instance.GetComponentInChildren<CinematicFrameBridge>();
            var service = new CinematicService(director, bridge, catalog, control);
            // DOTween's global count retains killed slots until a later update. Inspect owned handles.
            float externalValue = 0;
            var externalTween = DOTween.To(() => externalValue, v => externalValue = v, 1, 1).Pause().SetAutoKill(false);
            var vm = new CinematicViewModel(service, service.Presentation);
            var view = instance.GetComponentInChildren<CinematicView>();
            var button = instance.GetComponentInChildren<UnityEngine.UI.Button>();
            try
            {
                view.Bind(vm);
                foreach (CinematicId id in new[] { CinematicId.Opening, CinematicId.Ending })
                {
                    var prepared = Prepare(service, id);
                    Check(prepared.Status == OperationStatus.Succeeded, id + " prepares successfully: " + prepared.Error);
                    Check(service.Presentation.Frame[0].Artwork.name == id + "_01", id + " first frame prepared");
                    Check(service.Presentation.IsVisible && !service.Presentation.CanSkip, id + " Ready visible but cannot skip");
                    AdvanceForGraphTest(service, director, 5);
                    Check(Math.Abs(service.Time) < 0.000001, id + " Ready consumes no playback time");
                    var playing = new OperationResult();
                    IEnumerator iterator = service.Play(CancellationToken.None, playing);
                    Check(iterator.MoveNext(), id + " enters timed Play");
                    AdvanceForGraphTest(service, director, 1);
                    Check(service.Presentation.Frame[0].Scale > 1 && service.Presentation.Frame[0].Scale < 1.04f, id + " mild zoom evaluated");
                    Check(Math.Abs(service.Presentation.Frame[0].Scale - (1 + 0.04f / 3)) < 0.0001f, id + " DOTween zoom samples approved clip progress");
                    float scaleAtOne = service.Presentation.Frame[0].Scale;
                    director.time = 0.5; director.Evaluate();
                    Check(service.Presentation.Frame[0].Scale < scaleAtOne, id + " paused zoom Tween supports backward Timeline seek");
                    director.time = 1; director.Evaluate();
                    AdvanceForGraphTest(service, director, 1.85f);
                    Check(service.Presentation.Frame.RevealedCount == 2 && service.Presentation.Frame[1].Artwork.name == id + "_02", id + " second panel reveals top-right while first remains");
                    Check(service.Presentation.Frame[0].IsRevealed && !service.Presentation.Frame[2].IsRevealed, id + " no future panel is disclosed");
                    double pausedAt = service.Time;
                    using (control.PausePresentation("options"))
                    {
                        AdvanceForGraphTest(service, director, 4);
                        Check(Math.Abs(service.Time - pausedAt) < 0.000001, id + " options pause exact time");
                        service.Skip();
                        Check(service.Outcome == CinematicOutcome.None && !button.interactable, id + " options blocks Skip");
                    }
                    AdvanceForGraphTest(service, director, 0.1f);
                    Check(service.Time > pausedAt && button.interactable, id + " options resumes same playback");
                    AdvanceForGraphTest(service, director, 20);
                    Check(service.Outcome == CinematicOutcome.Completed && playing.Status == OperationStatus.Succeeded, id + " natural completion");
                    Check(service.Presentation.IsVisible && service.Presentation.Frame.RevealedCount == 4 && service.Presentation.Frame[3].Artwork.name == id + "_04", id + " holds last frame until external fade");
                    service.Skip();
                    Check(service.Outcome == CinematicOutcome.Completed && !iterator.MoveNext(), id + " duplicate completion/Skip ignored");
                    var ownedTweens = bridge.CaptureOwnedTweens();
                    Check(ownedTweens.Length == 8, id + " owns four zoom and four reveal handles");
                    service.Hide();
                    Check(!service.Presentation.IsVisible && director.playableAsset == null, id + " Hide clears graph and display");
                    Check(ownedTweens.All(t => !t.IsActive()) && bridge.CaptureOwnedTweens().Length == 0 && externalTween.IsActive(), id + " Hide releases all owned clip Tweens only (active=" + ownedTweens.Count(t => t.IsActive()) + ", registered=" + bridge.CaptureOwnedTweens().Length + ", external=" + externalTween.IsActive() + ")");
                }
                for (int cut = 0; cut < 4; cut++)
                {
                    Prepare(service, CinematicId.Opening);
                    var result = new OperationResult();
                    var iterator = service.Play(CancellationToken.None, result);
                    iterator.MoveNext();
                    AdvanceForGraphTest(service, director, cut * 2.7f + 0.1f);
                    view.Bind(vm); view.Bind(vm);
                    button.onClick.Invoke(); button.onClick.Invoke();
                    Check(service.Outcome == CinematicOutcome.Skipped && result.Status == OperationStatus.Succeeded, "Cut " + (cut + 1) + " rebound button Skip completes once");
                    Check(service.Presentation.IsVisible, "Cut " + (cut + 1) + " Skip holds frame");
                    Check(!iterator.MoveNext(), "Cut " + (cut + 1) + " Skip iterator terminates");
                    service.Hide();
                }
                Prepare(service, CinematicId.Opening);
                using (var cancellation = new CancellationTokenSource())
                {
                    var result = new OperationResult();
                    var iterator = service.Play(cancellation.Token, result);
                    iterator.MoveNext(); cancellation.Cancel(); AdvanceForGraphTest(service, director, 1);
                    Check(service.Outcome == CinematicOutcome.Cancelled && result.Status == OperationStatus.Cancelled, "External cancellation differs from Skip/completion");
                    Check(!iterator.MoveNext(), "Cancelled iterator ends");
                    service.Hide();
                }
                Prepare(service, CinematicId.Opening);
                var abandonedResult = new OperationResult();
                var abandoned = service.Play(CancellationToken.None, abandonedResult);
                abandoned.MoveNext(); ((IDisposable)abandoned).Dispose();
                Check(abandonedResult.Status == OperationStatus.Cancelled, "Disposing coroutine cancels playback");
                service.Hide();
                Prepare(service, CinematicId.Opening);
                var unboundResult = new OperationResult();
                var unbound = service.Play(CancellationToken.None, unboundResult);
                unbound.MoveNext(); view.Unbind(); button.onClick.Invoke();
                Check(service.Outcome == CinematicOutcome.None, "Unbound View cannot execute Skip");
                vm.Dispose();
                AdvanceForGraphTest(service, director, 1);
                Check(service.Outcome == CinematicOutcome.None, "Disposed VM leaves playback ownership to service");
                ((IDisposable)unbound).Dispose(); service.Hide();
                var invalid = new OperationResult();
                Drain(service.Prepare((CinematicId)99, CancellationToken.None, invalid));
                Check(invalid.Status == OperationStatus.Failed && !string.IsNullOrEmpty(invalid.Error), "Invalid catalog ID reports contextual failure");
                service.Hide();
                service.Dispose();
                Check(control.SubscriberCount == 0, "Service releases control subscription");
                Check(bridge.CaptureOwnedTweens().Length == 0 && externalTween.IsActive(), "Dispose leaves no clip Tween handles and preserves external Tween");
                service.Dispose();
                var disposedResult = new OperationResult();
                Drain(service.Prepare(CinematicId.Opening, CancellationToken.None, disposedResult));
                Check(disposedResult.Status == OperationStatus.Failed, "Disposed service rejects preparation");
            }
            finally
            {
                view.Unbind(); vm.Dispose(); service.Dispose(); control.Dispose();
                externalTween.Kill(false);
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        // Deterministic Editor-only seeking; production uses Unity native Director time.
        private static void AdvanceForGraphTest(CinematicService service, PlayableDirector director, float seconds)
        {
            if (service.Phase == CinematicPhase.Playing && service.Presentation.CanSkip)
            {
                director.time = Math.Min(director.time + seconds, director.duration - 0.000001);
                director.Evaluate();
            }
            service.ObservePlayback();
        }

        private static OperationResult Prepare(CinematicService service, CinematicId id)
        {
            var result = new OperationResult();
            Drain(service.Prepare(id, CancellationToken.None, result));
            return result;
        }
        private static void Drain(IEnumerator operation)
        {
            int limit = 20;
            while (operation.MoveNext()) if (--limit <= 0) throw new InvalidOperationException("Unexpected asynchronous preparation.");
        }
        private static void Check(bool condition, string detail)
        {
            if (!condition) throw new InvalidOperationException(detail);
            passed.Add(detail);
        }

        private sealed class ProbeControlService : IGameplayControlService
        {
            private int presentationLeases;
            private Action<ControlState> changed;
            public int SubscriberCount => changed?.GetInvocationList().Length ?? 0;
            public ControlState State => new ControlState(true, false, presentationLeases > 0);
            public event Action<ControlState> Changed { add { changed += value; } remove { changed -= value; } }
            public IDisposable BlockGameplay(string owner) => new Lease(null);
            public IDisposable PauseWorld(string owner) => new Lease(null);
            public IDisposable PausePresentation(string owner)
            {
                presentationLeases++; changed?.Invoke(State);
                return new Lease(() => { presentationLeases--; changed?.Invoke(State); });
            }
            public void Dispose() { changed = null; }
            private sealed class Lease : IDisposable
            {
                private Action release;
                public Lease(Action release) { this.release = release; }
                public void Dispose() { Action action = release; release = null; action?.Invoke(); }
            }
        }
    }
}
