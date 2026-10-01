using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Cooked.Contracts;
using UnityEditor;
using UnityEngine;

namespace Cooked.Session.Editor
{
    /// <summary>Run in the scheduled worktree Editor; never opens a replacement scene.</summary>
    public static class SessionUnityVerification
    {
        [MenuItem("Cooked/Session/Verify Domain and Authored Prefabs")]
        public static void VerifyAuthoredAssets()
        {
            var checks = new List<string>(SessionDomainVerification.Run());
            var actor = AssetDatabase.LoadAssetAtPath<GameObject>(SessionActorBuilder.ActorPath);
            Require(actor != null && !actor.activeSelf, "inactive actor prefab exists", checks);
            SessionActorHost.ValidateInputOwnership(actor);
            var facade = actor.GetComponentInChildren<PlayerFacade>(true);
            Require(facade != null && facade.GetComponent<Rigidbody>() != null && facade.GetComponent<CapsuleCollider>() != null, "HMS physical owner colocated", checks);
            var capsule = facade.GetComponent<CapsuleCollider>();
            Require(capsule.height == 2 && capsule.radius == .5f && capsule.center == Vector3.zero, "center-origin spawn convention", checks);
            var visual = facade.transform.Find("VisualRoot");
            Require(visual != null && visual.Find("NormalVisual") != null && visual.Find("SmallVisual") != null, "HMS visual names exist", checks);
            Require(visual.GetComponentsInChildren<MonoBehaviour>(true).Length == 0 && visual.GetComponentsInChildren<Collider>(true).Length == 0, "visual has no logic or collision", checks);
            foreach (Transform child in actor.GetComponentsInChildren<Transform>(true))
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) == 0, "no missing script: " + child.name, checks);
            var oil = AssetDatabase.LoadAssetAtPath<GameObject>(SessionActorBuilder.OilPath);
            Require(oil != null && oil.GetComponent<OilSurface>() != null && oil.GetComponent<SessionTransientLifetime>() != null, "owned oil prefab compatible", checks);
            Write("authored-assets.txt", checks);
        }
        /// <summary>Caller starts this iterator in the active architecture scene, with no existing session host.</summary>
        public static IEnumerator VerifyRuntime()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Runtime verification requires Play Mode.");
            var checks = new List<string>();
            var session = new GameSessionService();
            var control = new GameplayControlService();
            var bus = new ProbeEventBus();
            var bridge = new PlayerBridgeService(control, session, bus);
            var transients = new SessionTransientService();
            var hostPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SessionActorBuilder.HostPath);
            var hostObject = UnityEngine.Object.Instantiate(hostPrefab);
            var host = hostObject.GetComponent<SessionActorHost>();
            try
            {
                session.Begin(new CheckpointSnapshot("03_1_Stage", "S1_START", 0));
                host.Initialize(bridge, control, transients);
                // Elevated body avoids incidental contacts in the current scene. World is paused only for setup.
                var setupPause = control.PauseWorld("verification-setup");
                host.Spawn(new Pose(new Vector3(0, 100, 0), Quaternion.identity), 0);
                var firstRoot = host.ActorRoot;
                var firstModel = bridge.Model;
                Require(bridge.HasActor && bridge.Snapshot.Health == bridge.Snapshot.MaxHealth, "fresh actor health", checks);
                Require(!bridge.Tear() && !bridge.ChangeForm(), "locked abilities rejected", checks);
                setupPause.Dispose();
                session.Unlock(AbilityId.FormChange); session.Unlock(AbilityId.Tear);
                bridge.RestoreFragments(1);
                Require(bridge.ChangeForm(), "HMS form request accepted", checks);
                yield return null;
                var oldDebris = host.ActorRoot.GetComponentInChildren<PlayerFormController>().OwnedDebris;
                Require(oldDebris != null, "HMS shell spawned", checks);
                var block = control.BlockGameplay("dialogue-test");
                Require(!bridge.Tear() && !bridge.ChangeForm() && !bridge.RecoverShell() && !bridge.Jump() && !host.Interaction.TryInteract(), "all gameplay actions blocked", checks);
                Require(bridge.Snapshot.Fragments == 1, "blocked tear spends nothing", checks);
                block.Dispose();
                Require(bridge.Tear(), "unblocked unlocked HMS tear succeeds", checks);
                Require(bridge.Snapshot.Fragments == 0, "tear consumes exactly one fragment", checks);
                var oilPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(SessionActorBuilder.OilPath);
                var oil = UnityEngine.Object.Instantiate(oilPrefab, new Vector3(0, 90, 0), Quaternion.identity);
                Require(transients.Count == 1, "legacy-instantiated oil explicitly registered", checks);
                session.CaptureCheckpoint("03_1_Stage", "S1_SKILL", 1);
                yield return host.Despawn();
                Require(firstRoot == null && oldDebris == null && oil == null && transients.Count == 0, "actor shell and Core transient destroyed", checks);
                host.Spawn(new Pose(new Vector3(0, 100, 0), Quaternion.identity), session.Checkpoint.Fragments);
                Require(!ReferenceEquals(bridge.Model, firstModel) && bridge.Snapshot.Fragments == 1 && session.IsUnlocked(AbilityId.Tear), "new actor restores fragments and keeps unlocks", checks);
                int changed = 0; Action<PlayerSnapshot> observe = value => changed++; bridge.Changed += observe;
                ((PlayerModel)firstModel).ApplyDamage(1);
                Require(changed == 0, "detached old model no longer notifies bridge", checks);
                bridge.Changed -= observe;
                yield return host.Despawn();
                session.End(); session.Begin(new CheckpointSnapshot("03_1_Stage", "S1_START", 0));
                host.Spawn(new Pose(new Vector3(0, 100, 0), Quaternion.identity), 0);
                Require(!session.IsUnlocked(AbilityId.FormChange) && bridge.Snapshot.Fragments == 0, "second new game clears progression", checks);
                Write("runtime.txt", checks);
            }
            finally
            {
                host.ReleaseNow(); UnityEngine.Object.Destroy(hostObject);
                bridge.Dispose(); transients.Dispose(); session.Dispose(); control.Dispose(); bus.Dispose();
            }
        }
        private static void Require(bool passed, string name, List<string> results)
        {
            if (!passed) throw new InvalidOperationException("FAIL: " + name);
            results.Add("PASS: " + name);
        }
        private static void Write(string name, List<string> checks)
        {
            string path = Path.Combine("output/Tech_SYM/session/unity-evidence", name);
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllLines(path, checks);
            Debug.Log("[Session Verification] " + checks.Count + " passed. " + path);
        }
        private sealed class ProbeEventBus : IGameEventBus
        {
            public void Publish<T>(T message) where T : struct, IGameEvent { }
            public IDisposable Subscribe<T>(Action<T> handler) where T : struct, IGameEvent => new EmptyLease();
            public void Dispose() { }
            private sealed class EmptyLease : IDisposable { public void Dispose() { } }
        }
    }
}

