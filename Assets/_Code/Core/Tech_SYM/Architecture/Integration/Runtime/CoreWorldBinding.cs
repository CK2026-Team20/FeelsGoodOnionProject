using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cooked.Audio;
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
        private readonly ChaseRuntimeDriver chase;
        private readonly CoreUiBinding ui;
        private readonly IGameFlowService flow;
        private readonly IGameplayControlService control;
        private readonly IDialogueTableService dialogueTable;
        private readonly IAudioService audio;
        private readonly AudioFlowBinding audioFlow;
        private readonly ISceneService scenes;
        private readonly Camera output;
        private readonly CinemachineBrain brain;
        private readonly PlayerCameraController controller;
        private readonly CinemachineCamera side, quarter;
        private StageService stage;
        private readonly List<MemoUIController> memos = new List<MemoUIController>();
        private AbilityMask observedUnlocks;
        private int fragments;
        private bool observing, subscriptionsDetached, disposed;
        public CoreWorldBinding(GameSessionRuntime runtime, LevelCameraService camera, ChaseRuntimeDriver chase,
            CoreUiBinding ui, IGameFlowService flow, IGameplayControlService control, IDialogueTableService dialogueTable,
            IAudioService audio, AudioFlowBinding audioFlow, ISceneService scenes, Camera output,
            CinemachineBrain brain, PlayerCameraController controller, CinemachineCamera side, CinemachineCamera quarter)
        {
            this.runtime = runtime; this.camera = camera; this.chase = chase; this.ui = ui; this.flow = flow;
            this.control = control; this.dialogueTable = dialogueTable; this.audio = audio; this.audioFlow = audioFlow; this.scenes = scenes;
            this.output = output; this.brain = brain; this.controller = controller; this.side = side; this.quarter = quarter;
            if (output == null || brain == null || controller == null || output.gameObject != brain.gameObject ||
                output.gameObject != controller.gameObject || side == null || quarter == null || side == quarter)
                throw new ArgumentException("World requires explicit App output and distinct Core rigs.");
            runtime.CheckpointCaptured += OnCheckpointCaptured;
            runtime.ActorReady += BindActor; runtime.ActorRemoving += UnbindActor;
            runtime.Player.AbilitySucceeded += OnAbility; runtime.Player.AbilityRejected += OnAbilityRejected;
        }
        public IEnumerator PrepareStage(string stageId, CancellationToken token, OperationResult result)
        {
            token.ThrowIfCancellationRequested();
            if (runtime.Player.HasActor) { result.Fail("Release Actor before Stage reset."); yield break; }
            var item = runtime.Stages.Require(stageId) as StageService;
            if (item == null) { result.Fail("Unexpected Stage implementation."); yield break; }
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
        private void BindActor()
        {
            if (stage != null || observing) throw new InvalidOperationException("Old world bindings remain.");
            var actor = runtime.Player.Actor;
            if (actor == null) throw new InvalidOperationException("Spawned Actor missing.");
            stage = (StageService)runtime.ActiveStage;
            try
            {
                foreach (var item in runtime.Stages.Stages) scenes.SetRootsActive(runtime.Stages.ScenePath(item.StageId), item.StageId == runtime.ActiveStageId);
                chase.Initialize(runtime.Player, control, flow);
                var active = stage;
                stage.Initialize(flow, actor, code => runtime.TryStartSafeDialogue(active.StageId, code));
                stage.BindFinalDialogueStart((trigger, source, generation, code) =>
                    runtime.TryStartFinalDialogue(active, trigger, source, generation, code));
                stage.BindRequests(runtime.Player, control, runtime.Dialogue,
                    (ability, code) => Unlock(active, ability, code), StopChase);
                stage.BindCheckpointCapture((marker, source) => runtime.TryCaptureCheckpoint(active.StageId, marker, source));
                stage.BindDialogueTable(dialogueTable);
                stage.BindFallRequests((source, shell) => runtime.Player.IsCurrentActor(source, runtime.Player.ActorGeneration) &&
                    (shell != null ? runtime.Player.ReturnOwnedDebris(shell) : runtime.Player.ForceDeath(Vector3.zero, skipPresentation: true) > 0));
                stage.BindChaseStart(s => chase.CanStart(s.StageId, s.ChaseWaypoints, s.Actor),
                    s => runtime.TryCaptureBeforeChase(s, s.Actor),
                    s => chase.TryStart(s.StageId, s.ChaseWaypoints, s.Actor));
                stage.StageEntered += OnStage;
                stage.ChaseStarted += OnChaseStarted;
                foreach (var memo in stage.FindComponents<MemoUIController>())
                { memo.Initialize(control); memos.Add(memo); }
                runtime.ActorHost.Interaction.BindOverlays(memos);
                foreach (var periodic in stage.FindComponents<PeriodicPlatform>())
                    periodic.BindMaterialization(runtime.Player);
                camera.Bind(runtime.Player, control, actor.GetComponent<PlayerMovementModeController>(), controller,
                    brain, side, quarter, runtime.Player.CameraTarget, runtime.Player.BodyTransform);
                ui.SetActorCamera(output);
                CameraZoneRequest? resolved = null;
                foreach (var zone in stage.FindComponents<CameraZoneTrigger>())
                {
                    zone.Bind(camera, actor);
                    if (zone.TryResolveAtPosition(actor.transform.position, out var request))
                    {
                        // Hierarchy order is deterministic; the last authored overlapping volume wins.
                        resolved = request;
                    }
                }
                if (!resolved.HasValue)
                {
                    // Safe unzoned entry uses authored default configuration only, not saved IDs.
                    var initial = stage.InitialCheckpoint;
                    var fallback = initial.CameraZone;
                    if (string.IsNullOrWhiteSpace(fallback.Id))
                        throw new InvalidOperationException("Actual Actor position is outside camera volumes and Stage has no authored entry mode.");
                    resolved = fallback;
                }
                camera.EnterZone(resolved.Value, true);
                audioFlow.SetStage(stage.StageMusicPurpose);
                fragments = runtime.Player.Snapshot.Fragments;
                observedUnlocks = runtime.Session.UnlockedAbilities;
                runtime.Session.Changed += OnSession; runtime.Player.Changed += OnPlayer; observing = true;
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
        { chase.Service?.Stop(ChaseStopReason.Arrival); audioFlow.SetChaseActive(false); }
        private void OnStage(StageService owner)
        { if (owner == stage && observing) audioFlow.SetStage(owner.StageMusicPurpose); }
        private void OnCheckpointCaptured()
        { if (observing) audio.PlaySfx(AudioCueIds.Checkpoint); }
        private void OnChaseStarted(StageService value) => audioFlow.SetChaseActive(true);
        private void OnAbility(AbilityId ability)
        {
            if (!observing || !runtime.Player.HasActor) return;
            if (ability == AbilityId.Tear) audio.PlaySfx(AudioCueIds.Tear);
            else if (ability == AbilityId.FormChange) audio.PlaySfx(AudioCueIds.Form);
        }
        private void OnAbilityRejected(AbilityId ability, PlayerSkillBlockReason reason)
        { if (observing && ability == AbilityId.FormChange && reason == PlayerSkillBlockReason.NotEnoughSpace) audio.PlaySfx(AudioCueIds.Click); }
        private void OnPlayer(PlayerSnapshot value)
        { if (observing && value.Fragments > fragments) audio.PlaySfx(AudioCueIds.Pickup); fragments = value.Fragments; }
        private void OnSession()
        {
            if (!observing || !runtime.Session.IsActive) return;
            var current = runtime.Session.UnlockedAbilities;
            bool added = (current & ~observedUnlocks) != AbilityMask.None;
            observedUnlocks = current;
            if (added) audio.PlaySfx(AudioCueIds.Unlock);
        }
        private void UnbindActor()
        {
            observing = false;
            runtime.Session.Changed -= OnSession; runtime.Player.Changed -= OnPlayer;
            var errors = new List<Exception>();
            SessionCleanup.Attempt(errors, () => runtime.ActorHost.Interaction?.ForceCloseOverlays());
            SessionCleanup.Attempt(errors, () => runtime.ActorHost.Interaction?.CancelOperations());
            SessionCleanup.Attempt(errors, () => runtime.ActorHost.Interaction?.BindOverlays(null));
            foreach (var memo in memos) if (memo != null) SessionCleanup.Attempt(errors, memo.Unbind);
            memos.Clear();
            if (stage != null)
            {
                SessionCleanup.Attempt(errors, () =>
                {
                    foreach (var zone in stage.FindComponents<CameraZoneTrigger>()) SessionCleanup.Attempt(errors, zone.ResetForRetry);
                });
                SessionCleanup.Attempt(errors, () =>
                {
                    foreach (var periodic in stage.FindComponents<PeriodicPlatform>()) SessionCleanup.Attempt(errors, () => periodic.BindMaterialization(null));
                });
                stage.StageEntered -= OnStage;
                stage.ChaseStarted -= OnChaseStarted;
                SessionCleanup.Attempt(errors, stage.Dispose);
            }
            stage = null;
            SessionCleanup.Attempt(errors, chase.Shutdown); SessionCleanup.Attempt(errors, camera.Unbind);
            SessionCleanup.ThrowIfAny(errors, "World release failed.");
        }
        public void Dispose()
        {
            if (disposed) return;
            var errors = new List<Exception>();
            if (!subscriptionsDetached)
            {
                runtime.CheckpointCaptured -= OnCheckpointCaptured;
                runtime.ActorReady -= BindActor; runtime.ActorRemoving -= UnbindActor;
                runtime.Player.AbilitySucceeded -= OnAbility; runtime.Player.AbilityRejected -= OnAbilityRejected;
                subscriptionsDetached = true;
            }
            SessionCleanup.Attempt(errors, UnbindActor);
            if (errors.Count == 0) disposed = true;
            SessionCleanup.ThrowIfAny(errors, "World dispose failed.");
        }
    }
}
