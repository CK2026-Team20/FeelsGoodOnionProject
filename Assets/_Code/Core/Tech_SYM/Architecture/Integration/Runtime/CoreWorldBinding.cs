using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cooked.Audio;
using Cooked.UI;
using Cooked.Chase;
using Cooked.Contracts;
using Cooked.Level;
using Cooked.Session;
using FeelsGoodOnion.TechSYM.Interaction;
using FeelsGoodOnion.TechSYM.Platforms;
using Unity.Cinemachine;
using UnityEngine;
namespace Cooked.Integration
{
    /// <summary>One round's borrowed connections; releases Actor before resetting resident Stage owners.</summary>
    public sealed class CoreWorldBinding : IDisposable
    {
        private readonly GameSessionRuntime runtime;
        private readonly LevelCameraService camera;
        private readonly CoreUiBinding ui;
        private readonly IGameFlowService flow;
        private readonly IGameplayControlService control;
        private readonly IDialogueTableService dialogueTable;
        private readonly AudioFlowBinding audioFlow;
        private readonly AudioRuntimeHost audioHost;
        private readonly ISceneService scenes;
        private readonly Camera output;
        private readonly CinemachineBrain brain;
        private StageService stage;
        private readonly List<TutorialPrompt> tutorials = new List<TutorialPrompt>();
        private readonly List<MemoUIController> memos = new List<MemoUIController>();
        private readonly List<StageTrigger> chaseTriggers = new List<StageTrigger>();
        private readonly List<ChaseRuntimeDriver> chases = new List<ChaseRuntimeDriver>();
        private readonly List<PeriodicPlatform> periodicPlatforms = new List<PeriodicPlatform>();
        private bool observing, subscriptionsDetached, disposed;
        public CoreWorldBinding(GameSessionRuntime runtime, LevelCameraService camera,
            CoreUiBinding ui, IGameFlowService flow, IGameplayControlService control, IDialogueTableService dialogueTable,
            AudioFlowBinding audioFlow, ISceneService scenes, Camera output,
            CinemachineBrain brain, AudioRuntimeHost audioHost)
        {
            this.runtime = runtime; this.camera = camera; this.ui = ui; this.flow = flow;
            this.control = control; this.dialogueTable = dialogueTable; this.audioFlow = audioFlow; this.scenes = scenes;
            this.output = output; this.brain = brain; this.audioHost = audioHost;
            if (output == null || brain == null || output.gameObject != brain.gameObject)
                throw new ArgumentException("World requires explicit App output and distinct Core rigs.");
            runtime.ActorReady += BindActor; runtime.ActorRemoving += UnbindActor;

        }
        public IEnumerator PrepareStage(string stageId, CancellationToken token, OperationResult result)
        {
            token.ThrowIfCancellationRequested();
            if (runtime.Player.HasActor) { result.Fail("Release Actor before Stage reset."); yield break; }
            var item = runtime.Stages.Require(stageId);
            var path = runtime.Stages.ScenePath(stageId);
            try
            {
                // Warm authored owners only once in this persistent Stage's component lifetime.
                // Reactivating an old retry world first would validate enemies against moved supports.
                if (!item.IsInitialPreparationComplete)
                {
                    scenes.SetRootsActive(path, true);
                    item.PrepareInitialSources();
                    yield return null;
                }
                token.ThrowIfCancellationRequested();
                scenes.SetRootsActive(path, false);
                item.ResetForRetry();
                result.Succeed();
            }
            finally { scenes.SetRootsActive(path, false); }
        }
        public void ActivateForSpawn(string stageId)
        {
            if (disposed) throw new ObjectDisposedException(nameof(CoreWorldBinding));
            if (runtime.Player.HasActor) throw new InvalidOperationException("스폰 월드 활성화는 Actor 생성 전에 수행해야 합니다.");
            runtime.Stages.Require(stageId);
            foreach (var item in runtime.Stages.Stages)
                scenes.SetRootsActive(runtime.Stages.ScenePath(item.StageId), item.StageId == stageId);
            // PrepareStage deliberately leaves roots disabled. Queries must see the reset physical world
            // before checkpoint registration and before Host's first and post-Awake placement checks.
            Physics.SyncTransforms();
        }
        private void BindActor()
        {
            if (stage != null || observing) throw new InvalidOperationException("Old world bindings remain.");
            var actor = runtime.Player.Actor;
            if (actor == null) throw new InvalidOperationException("Spawned Actor missing.");
            stage = runtime.ActiveStage;
            try
            {
                // The selected physical world was enabled before spawning; bind its gameplay observers now.
                var active = stage;
                stage.Initialize(flow, actor, code => runtime.TryStartSafeDialogue(active.StageId, code));
                stage.BindFinalDialogueStart((trigger, source, generation, code) =>
                    runtime.TryStartFinalDialogue(active, trigger, source, generation, code),
                    runtime.Session, () => runtime.IsCurrentStage(active));
                stage.BindRequests(runtime.Player, control, runtime.Dialogue,
                    (ability, code) => Unlock(active, ability, code), StopChase);

                stage.BindSectionPresentation((section, restore) => camera.EnterSection(section, restore));
                audioFlow.SetStage(stage.StageId);
                stage.BindDialogueTable(dialogueTable);
                stage.BindFallRequests((source, shell) => runtime.Player.IsCurrentActor(source, runtime.Player.ActorGeneration) &&
                    (shell != null ? runtime.Player.ReturnOwnedDebris(shell) : runtime.Player.ForceDeath(Vector3.zero, skipPresentation: true) > 0));
                foreach (var trigger in stage.FindComponents<StageTrigger>())
                    if (trigger.Kind == StageTriggerKind.Chase && trigger.Owner == stage)
                    {
                        chaseTriggers.Add(trigger);
                        var chase = trigger.ChaseDriver;
                        trigger.BindChase(chase, source => runtime.TryCaptureBeforeChase(active, source));
                        if (!chases.Contains(chase))
                        {
                            chases.Add(chase); // 초기화 실패도 연결 당시 참조로 끝까지 회수한다.
                            chase.Initialize(runtime.Player, control, flow);
                        }
                    }
                stage.ChaseStarted += OnChaseStarted;
                foreach (var memo in stage.FindComponents<MemoUIController>())
                { memo.Initialize(control); memos.Add(memo); }
                runtime.ActorHost.Interaction.BindOverlays(memos);
                periodicPlatforms.AddRange(stage.FindComponents<PeriodicPlatform>());
                foreach (var periodic in periodicPlatforms)
                    periodic.BindMaterialization(runtime.Player);
                camera.Bind(runtime.Player, control, actor.GetComponent<PlayerMovementModeController>(), brain, stage, flow);
                ui.SetActorCamera(output);
                stage.RestorePresentation();
                foreach (var prompt in stage.FindComponents<TutorialPrompt>())
                {
                    tutorials.Add(prompt);
                    prompt.Bind(runtime.Tutorials, actor, flow, control, output);
                }
                audioHost.BindGameplay(runtime.Player, stage.gameObject.scene, control, flow); observing = true;
            }
            catch (Exception error) { SessionCleanup.RethrowAfterCleanup(error, UnbindActor); }
        }
        private bool Unlock(StageService owner, AbilityId ability, string code)
        {
            if (owner != stage || owner.Actor != runtime.Player.Actor || owner.Actor == null || owner.Actor.IsDead ||
                flow.Snapshot.State != FlowState.Playing || flow.Snapshot.IsBusy) return false;
            runtime.Session.Unlock(ability);
            // A busy notice does not undo an already accepted ability; contact remains eligible to present it.
            return string.IsNullOrWhiteSpace(code) || runtime.Dialogue.IsCompleted(code) || runtime.TryStartSafeDialogue(owner.StageId, code);
        }
        private void StopChase()
        {
            foreach (var chase in chases) if (chase != null) chase.Stop();
            audioFlow.SetChaseActive(false);
        }
        private void OnChaseStarted(StageService value) => audioFlow.SetChaseActive(true);
        private void UnbindActor()
        {
            observing = false;
            audioHost.UnbindGameplay();
            var errors = new List<Exception>();
            SessionCleanup.Attempt(errors, () => runtime.ActorHost.Interaction?.ForceCloseOverlays());
            SessionCleanup.Attempt(errors, () => runtime.ActorHost.Interaction?.CancelOperations());
            SessionCleanup.Attempt(errors, () => runtime.ActorHost.Interaction?.BindOverlays(null));
            foreach (var memo in memos) if (memo != null) SessionCleanup.Attempt(errors, memo.Unbind);
            memos.Clear();
            foreach (var prompt in tutorials) if (prompt != null) SessionCleanup.Attempt(errors, prompt.Unbind);
            tutorials.Clear();
            // Release only the objects borrowed at bind time. Unity may already be
            // unloading this Stage during app shutdown, when scene lookup is invalid.
            foreach (var trigger in chaseTriggers) if (trigger != null) SessionCleanup.Attempt(errors, trigger.UnbindChase);
            chaseTriggers.Clear();
            foreach (var periodic in periodicPlatforms) if (periodic != null) SessionCleanup.Attempt(errors, () => periodic.BindMaterialization(null));
            periodicPlatforms.Clear();
            if (stage != null)
            {
                stage.ChaseStarted -= OnChaseStarted;

                SessionCleanup.Attempt(errors, stage.Dispose);
            }
            stage = null;
            foreach (var chase in chases) if (chase != null) SessionCleanup.Attempt(errors, chase.Shutdown);
            chases.Clear();
            SessionCleanup.Attempt(errors, camera.Unbind);
            SessionCleanup.ThrowIfAny(errors, "World release failed.");
        }
        public void Dispose()
        {
            if (disposed) return;
            var errors = new List<Exception>();
            if (!subscriptionsDetached)
            {
                runtime.ActorReady -= BindActor; runtime.ActorRemoving -= UnbindActor;

                subscriptionsDetached = true;
            }
            SessionCleanup.Attempt(errors, UnbindActor);
            if (errors.Count == 0) disposed = true;
            SessionCleanup.ThrowIfAny(errors, "World dispose failed.");
        }
    }
}
