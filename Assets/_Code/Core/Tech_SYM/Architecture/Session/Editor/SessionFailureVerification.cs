using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Cooked.Contracts;
using UnityEditor;
using UnityEngine;

namespace Cooked.Session.Editor
{
    /// <summary>Native regression harness; run only in a scheduled PlayMode slot without another bound host.</summary>
    public static class SessionFailureVerification
    {
        public static IEnumerator Run()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("PlayMode required.");
            var results = new List<string>();
            using (var context = new Context())
            {
                context.UsePreparedTear(.5f);
                int tear = 0, form = 0, recover = 0;
                context.Bridge.AbilitySucceeded += ability =>
                {
                    if (ability == AbilityId.Tear) tear++;
                    if (ability == AbilityId.FormChange) form++;
                    if (ability == AbilityId.RecoverShell) recover++;
                };
                context.Host.Spawn(new Pose(new Vector3(0, 100, 0), Quaternion.identity), 1);
                context.Bridge.RestoreFragments(0); context.Bridge.RestoreFragments(1);
                Check(tear + form + recover == 0, "attach and snapshot restore never emit success SFX", results);
                context.Session.Unlock(AbilityId.FormChange); context.Session.Unlock(AbilityId.Tear);
                var skill = context.Host.ActorRoot.GetComponentInChildren<PlayerSkillController>().GetEquippedSkill(1);
                Check(context.Bridge.Tear() && tear == 0 && context.Bridge.Snapshot.Fragments == 1, "prepared tear acceptance is silent and unspent", results);
                skill.Tick(.25f);
                using (context.Control.BlockGameplay("cancel-preparation"))
                    Check(!context.Bridge.Tear(), "blocked command rejected", results);
                skill.Tick(1);
                Check(tear == 0 && context.Bridge.Snapshot.Fragments == 1, "cancelled preparation emits no SFX", results);
                Check(context.Bridge.Tear(), "second tear preparation accepted", results);
                skill.Tick(.5f); skill.Tick(1);
                Check(tear == 1 && context.Bridge.Snapshot.Fragments == 0, "committed tear emits once after payment", results);
                Check(context.Bridge.ChangeForm() && form == 1, "successful shrink emits once", results);
                Check(context.Bridge.RecoverShell() && recover == 1, "successful recovery emits once", results);
                Check(!context.Bridge.RecoverShell() && recover == 1, "failed duplicate recovery is silent", results);
                var oldModel = context.Bridge.Model;
                var oldRoot = context.Host.ActorRoot;
                var oldOil = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SessionActorBuilder.OilPath));
                int tailNotifications = 0;
                Action<PlayerInteractionService> hostFailure = value => { if (value == null) throw new InvalidOperationException("injected host observer"); };
                Action<PlayerInteractionService> hostTail = value => { if (value == null) tailNotifications++; };
                Action interactionFailure = () => throw new InvalidOperationException("injected interaction observer");
                Action bridgeFailure = () => { if (!context.Bridge.HasActor) throw new InvalidOperationException("injected actor observer"); };
                Action<PlayerSnapshot> modelFailure = value => { if (!context.Bridge.HasActor) throw new InvalidOperationException("injected snapshot observer"); };
                context.Host.InteractionChanged += hostFailure; context.Host.InteractionChanged += hostTail;
                context.Host.Interaction.Changed += interactionFailure;
                context.Bridge.ActorChanged += bridgeFailure; context.Bridge.Changed += modelFailure;
                var error = ExpectAggregate(context.Host.ReleaseNow);
                Check(error.Flatten().InnerExceptions.Count == 4 && tailNotifications == 1, "four consumer failures aggregated while later consumers run", results);
                Check(context.Host.ActorRoot == null && context.Host.Interaction == null && !context.Bridge.HasActor && context.Transients.Count == 0, "all logical ownership cleared despite observers", results);
                ((PlayerModel)oldModel).ApplyDamage(1);
                Check(context.Bridge.Model == null, "old model has no bridge subscription", results);
                yield return null;
                Check(oldRoot == null && oldOil == null, "native actor and Core oil destroyed despite observers", results);
                context.Host.ReleaseNow();
                Check(tailNotifications == 1, "repeat release does not repeat teardown callbacks", results);
                context.Host.InteractionChanged -= hostFailure; context.Host.InteractionChanged -= hostTail;
                context.Bridge.ActorChanged -= bridgeFailure; context.Bridge.Changed -= modelFailure;

                var original = new InvalidOperationException("injected original spawn failure");
                Action<PlayerInteractionService> spawnFailure = value => { if (value != null) throw original; throw new ArgumentException("injected spawn rollback observer"); };
                context.Host.InteractionChanged += spawnFailure;
                error = ExpectAggregate(() => context.Host.Spawn(new Pose(new Vector3(0, 100, 0), Quaternion.identity), 0));
                Check(ReferenceEquals(error.InnerExceptions[0], original) && original.StackTrace != null, "spawn rollback preserves original exception and stack", results);
                Check(!context.Bridge.HasActor && context.Host.ActorRoot == null, "failed spawn leaves no actor", results);
                context.Host.InteractionChanged -= spawnFailure;
                yield return null;

                context.Host.Spawn(new Pose(new Vector3(0, 100, 0), Quaternion.identity), 0);
                context.Bridge.ActorChanged += bridgeFailure; context.Bridge.Changed += modelFailure;
                error = ExpectAggregate(context.Bridge.Dispose);
                Check(error.Flatten().InnerExceptions.Count == 2 && !context.Bridge.HasActor && context.Bridge.Model == null, "bridge Dispose clears subscriptions/state despite both observer failures", results);
                context.Bridge.Dispose();
                using (context.Control.BlockGameplay("post-dispose")) { }
                Check(!context.Bridge.CanAct, "disposed bridge remains inert", results);
                context.Host.InteractionChanged += hostFailure;
                ExpectAggregate(context.Host.Shutdown);
                context.Host.Shutdown();
                // Rebind proves Shutdown unbound scope even though ReleaseNow reported an error.
                var scope = AssetDatabase.LoadAssetAtPath<SessionTransientScope>(SessionActorBuilder.ScopePath);
                using (var fresh = new SessionTransientService()) { scope.Bind(fresh); scope.Unbind(fresh); }
                Check(true, "shutdown failure still unbinds scope and is idempotent", results);
            }
            yield return null;
            using (var context = new Context())
            using (context.Control.PauseWorld("coordinate-check"))
            {
                Vector3[] centers = { new Vector3(2,1.03f,0), new Vector3(20,3.33f,0), new Vector3(38,1.03f,0), new Vector3(51,1.03f,0), new Vector3(68,1.03f,0), new Vector3(82,1.03f,0) };
                foreach (var center in centers)
                {
                    context.Host.Spawn(new Pose(center, Quaternion.identity), 0);
                    Check(Vector3.Distance(context.Bridge.BodyTransform.position, center) < .0001f, "checkpoint center used with no added offset: " + center, results);
                    yield return context.Host.Despawn();
                }
            }
            string path = "output/Tech_SYM/session/unity-evidence/failure-and-audio.txt";
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllLines(path, results);
            Debug.Log("[Session Native Failure Verification] " + results.Count + " passed.");
        }
        private static AggregateException ExpectAggregate(Action action)
        {
            try { action(); } catch (AggregateException error) { return error; }
            throw new InvalidOperationException("Expected injected AggregateException was not reported.");
        }
        private static void Check(bool value, string name, List<string> results)
        {
            if (!value) throw new InvalidOperationException("FAIL: " + name);
            results.Add("PASS: " + name);
        }
        private sealed class Context : IDisposable
        {
            internal readonly GameSessionService Session = new GameSessionService();
            internal readonly GameplayControlService Control = new GameplayControlService();
            internal readonly SessionTransientService Transients = new SessionTransientService();
            internal readonly PlayerBridgeService Bridge;
            internal readonly SessionActorHost Host;
            private readonly GameObject hostObject;
            private GameObject template;
            private TearSkillDefinition definition;
            internal Context()
            {
                Bridge = new PlayerBridgeService(Control, Session, new ProbeEventBus());
                hostObject = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SessionActorBuilder.HostPath));
                Host = hostObject.GetComponent<SessionActorHost>();
                Session.Begin(new CheckpointSnapshot("03_1_Stage", "S1_START", 0));
                Host.Initialize(Bridge, Control, Transients);
            }
            internal void UsePreparedTear(float seconds)
            {
                template = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(SessionActorBuilder.ActorPath));
                definition = ScriptableObject.CreateInstance<TearSkillDefinition>();
                var data = new SerializedObject(definition);
                data.FindProperty("preparationTime").floatValue = seconds;
                data.FindProperty("cooldown").floatValue = 0; data.FindProperty("initCooldown").floatValue = 0;
                data.ApplyModifiedPropertiesWithoutUndo();
                var facade = new SerializedObject(template.GetComponentInChildren<PlayerFacade>(true));
                facade.FindProperty("tearSkillDefinition").objectReferenceValue = definition; facade.ApplyModifiedPropertiesWithoutUndo();
                var host = new SerializedObject(Host);
                host.FindProperty("actorPrefab").objectReferenceValue = template; host.ApplyModifiedPropertiesWithoutUndo();
            }
            public void Dispose()
            {
                var errors = new List<Exception>();
                SessionCleanup.Attempt(errors, Host.Shutdown);
                SessionCleanup.Attempt(errors, Bridge.Dispose);
                SessionCleanup.Attempt(errors, Transients.Dispose);
                SessionCleanup.Attempt(errors, Session.Dispose);
                SessionCleanup.Attempt(errors, Control.Dispose);
                UnityEngine.Object.Destroy(hostObject);
                if (template != null) UnityEngine.Object.Destroy(template);
                if (definition != null) UnityEngine.Object.Destroy(definition);
                SessionCleanup.ThrowIfAny(errors, "Native test context teardown failures.");
            }
        }
        private sealed class ProbeEventBus : IGameEventBus
        {
            public void Publish<T>(T message) where T : struct, IGameEvent { }
            public IDisposable Subscribe<T>(Action<T> handler) where T : struct, IGameEvent => new Noop();
            public void Dispose() { }
            private sealed class Noop : IDisposable { public void Dispose() { } }
        }
    }
}
