#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Cooked.Contracts;
using DG.Tweening;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Cooked.Chase.Editor
{
    /// <summary>Real DOTween/Transform checks. Explicitly invoked only in the assigned Editor slot.</summary>
    public static class ChaseTweenVerification
    {
        [MenuItem("Cooked/Architecture/Verify Chase Tweens")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Use Edit Mode for isolated Chase tween verification.");
            var results = new List<string>();
            string output = Path.GetFullPath("output/Tech_SYM/Chase/Verification/unity-tween-results.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            Scene preview = EditorSceneManager.NewPreviewScene();
            GameObject root = null, other = null;
            ChaseService service = null;
            ChaseSwarmPresentation view = null;
            Tween unrelated = null;
            try
            {
                root = new GameObject("ChaseTweenVerification");
                SceneManager.MoveGameObjectToScene(root, preview);
                var visual = new GameObject("Visual");
                visual.transform.SetParent(root.transform, false);
                view = root.AddComponent<ChaseSwarmPresentation>();
                var serialized = new SerializedObject(view);
                serialized.FindProperty("visual").objectReferenceValue = visual;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var control = new Control();
                var flow = new Flow();
                service = new ChaseService(new Bridge(), control, flow, new EventBus(), new ChaseSettings());
                view.Bind(service, control);
                var route = new[] { new Vector3(74, 0, 0), new Vector3(108, 0, 0) };
                Assert(service.Start(ChaseSettings.StageId, route, new Actor()), "Start accepted", results);
                view.Tick(.1f, .1f);
                float midway = visual.transform.localScale.x;
                Assert(midway > .85f && midway < 1f && view.Phase == ChaseVisualPhase.Appearing,
                    "Real DOScale reaches intermediate scale", results);
                float midwayY = visual.transform.localPosition.y;
                control.Set(true, true);
                view.Tick(10, 10);
                Assert(Math.Abs(visual.transform.localScale.x - midway) < .0001f &&
                    Math.Abs(visual.transform.localPosition.y - midwayY) < .0001f,
                    "Options freezes entrance and bob", results);
                control.Set(false, false);
                view.Tick(.05f, .05f);
                Assert(view.Phase == ChaseVisualPhase.Appearing, "Resume does not restart or prematurely finish delay", results);
                view.Tick(.051f, .051f);
                Assert(view.Phase == ChaseVisualPhase.Looping && Math.Abs(visual.transform.localScale.x - 1) < .0001f,
                    "Entrance finishes using remaining duration", results);
                float beforeBob = visual.transform.localPosition.y;
                view.Tick(.11f, .11f);
                Assert(Math.Abs(visual.transform.localPosition.y - beforeBob) > .0001f &&
                    Math.Abs(root.transform.position.x - 76) < .0001f,
                    "DOLocalMoveY animates only Visual; authoritative root stays X76", results);
                var existing = DOTween.TweensByTarget(visual.transform, false);
                int countBefore = existing == null ? 0 : existing.Count;
                Assert(!service.Start(ChaseSettings.StageId, route, new Actor()) &&
                    DOTween.TweensByTarget(visual.transform, false).Count == countBefore,
                    "Duplicate Start creates no extra tweens", results);

                flow.Stop();
                control.Set(true, true);
                Vector3 exitScale = visual.transform.localScale;
                view.Tick(0, 1);
                Assert(view.Phase == ChaseVisualPhase.Exiting && visual.transform.localScale == exitScale,
                    "Exit obeys presentation pause", results);
                control.Set(true, false);
                view.Tick(0, .09f);
                Assert(view.Phase == ChaseVisualPhase.Exiting, "Exit advances with scaled delta zero", results);
                view.Tick(0, .10f);
                Assert(view.Phase == ChaseVisualPhase.Hidden && !visual.activeSelf &&
                    view.LastTransitionOutcome == ChaseVisualOutcome.Completed,
                    "Unscaled exit completes under world pause", results);

                view.Unbind(); service.Dispose();
                flow = new Flow(); control.Set(false, false);
                service = new ChaseService(new Bridge(), control, flow, new EventBus(), new ChaseSettings());
                view.Bind(service, control);
                Assert(service.Start(ChaseSettings.StageId, route, new Actor()), "New attempt starts", results);
                other = new GameObject("UnrelatedTweenOwner");
                SceneManager.MoveGameObjectToScene(other, preview);
                unrelated = other.transform.DOScale(2f, 1).SetUpdate(UpdateType.Manual).SetRecyclable(false);
                unrelated.ForceInit();
                view.Tick(.04f, .04f);
                view.Unbind();
                view.Tick(5, 5);
                Assert(view.LastTransitionOutcome == ChaseVisualOutcome.Cancelled &&
                    view.Phase == ChaseVisualPhase.Hidden && !visual.activeSelf && !DOTween.IsTweening(visual.transform),
                    "Cancel kills own handles without completion callbacks", results);
                Assert(unrelated.IsActive(), "Cancel preserves unrelated tween", results);
                for (int i = 0; i < 3; i++) { view.Bind(service, control); view.Tick(.02f, .02f); view.Unbind(); }
                Assert(!DOTween.IsTweening(visual.transform) && control.Subscribers == 0,
                    "Repeated Bind/Unbind leaves no tweens or control subscriptions", results);
                results.Add("PASS: real Unity Transform + DOTween manual handles; physics traversal/LOOP not covered.");
            }
            catch (Exception error)
            {
                results.Add("FAIL: " + error);
                throw;
            }
            finally
            {
                if (view != null) view.Unbind();
                service?.Dispose();
                if (unrelated != null && unrelated.IsActive()) unrelated.Kill(false);
                if (root != null) Object.DestroyImmediate(root);
                if (other != null) Object.DestroyImmediate(other);
                EditorSceneManager.ClosePreviewScene(preview);
                File.WriteAllLines(output, results);
            }
            Debug.Log("[Cooked.Chase] Tween verification passed: " + output);
        }

        private static void Assert(bool condition, string name, List<string> results)
        { if (!condition) throw new InvalidOperationException(name); results.Add("PASS: " + name); }
        private sealed class Actor : IChaseActor
        { public bool IsAlive => true; public Vector3 Position => new Vector3(84, 0, 0); }
        private sealed class Bridge : IPlayerBridgeService
        {
            public bool HasActor => true;
            public PlayerSnapshot Snapshot => new PlayerSnapshot(3, 3, 0, 1);
            public Transform CameraTarget => null;
            public event Action<PlayerSnapshot> Changed { add { } remove { } }
            public void Attach(PlayerFacade actor) { } public void Detach() { }
            public void RestoreFragments(int value) { } public void SetDepthMovementAllowed(bool value) { }
            public void Dispose() { }
        }
        private sealed class Control : IGameplayControlService
        {
            private Action<ControlState> changed;
            public int Subscribers { get; private set; }
            public ControlState State { get; private set; }
            public event Action<ControlState> Changed { add { changed += value; Subscribers++; } remove { changed -= value; Subscribers--; } }
            public void Set(bool world, bool presentation) { State = new ControlState(world, world, presentation); changed?.Invoke(State); }
            public IDisposable BlockGameplay(string owner) => new Lease();
            public IDisposable PauseWorld(string owner) => new Lease();
            public IDisposable PausePresentation(string owner) => new Lease();
            public void Dispose() { }
        }
        private sealed class Flow : IGameFlowService
        {
            public FlowSnapshot Snapshot { get; private set; } = new FlowSnapshot(FlowState.Playing, false, 1, null);
            public event Action<FlowSnapshot> Changed;
            public void Stop() { Snapshot = new FlowSnapshot(FlowState.ReturningToTitle, true, 2, null); Changed?.Invoke(Snapshot); }
            public bool Request(FlowRequest request) => false;
            public void CancelCurrent() { } public void Dispose() { }
        }
        private sealed class EventBus : IGameEventBus
        {
            public IDisposable Subscribe<T>(Action<T> handler) where T : struct, IGameEvent => new Lease();
            public void Publish<T>(T value) where T : struct, IGameEvent { }
            public void Dispose() { }
        }
        private sealed class Lease : IDisposable { public void Dispose() { } }
    }
}
#endif
