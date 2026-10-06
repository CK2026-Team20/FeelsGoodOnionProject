using System;
using System.Collections.Generic;
using Cooked.Audio;
using Cooked.Chase;
using Cooked.Contracts;
using Cooked.Level;
using UnityEngine;
namespace Cooked.Integration
{
    /// <summary>Session-owned connections. World/Actor references are released before retry destroys them.</summary>
    public sealed class CoreWorldBinding : IDisposable
    {
        private readonly GameSessionRuntime runtime;
        private readonly LevelCameraService camera;
        private readonly ChaseRuntimeDriver chase;
        private readonly CoreUiBinding ui;
        private readonly IGameFlowService flow;
        private readonly IGameplayControlService control;
        private readonly IGameEventBus bus;
        private readonly IAudioService audio;
        private readonly AudioFlowBinding audioFlow;
        private readonly IDisposable stageSubscription;
        private readonly List<StageService> stages = new List<StageService>();
        private readonly List<FeelsGoodOnion.TechSYM.Interaction.MemoUIController> memos = new List<FeelsGoodOnion.TechSYM.Interaction.MemoUIController>();
        private CheckpointSnapshot checkpoint;
        private readonly HashSet<AbilityId> unlocked = new HashSet<AbilityId>();
        private int fragments;
        private bool observing, disposed;
        public CoreWorldBinding(GameSessionRuntime runtime, LevelCameraService camera, ChaseRuntimeDriver chase,
            CoreUiBinding ui, IGameFlowService flow, IGameplayControlService control, IGameEventBus bus,
            IAudioService audio, AudioFlowBinding audioFlow)
        {
            this.runtime=runtime; this.camera=camera; this.chase=chase; this.ui=ui; this.flow=flow;
            this.control=control; this.bus=bus; this.audio=audio; this.audioFlow=audioFlow;
            stageSubscription=bus.Subscribe<StageEnteredEvent>(OnStage);
            runtime.ActorReady+=BindActor; runtime.ActorRemoving+=UnbindActor;
            runtime.Player.AbilitySucceeded+=OnAbility;
            flow.Changed+=OnFlow;
        }
        private void BindActor()
        {
            var actor=runtime.ActorHost.ActorRoot.GetComponentInChildren<PlayerFacade>(true);
            if (actor == null) throw new InvalidOperationException("Spawned Actor has no facade.");
            chase.Initialize(runtime.Player,control,flow,bus);
            var prompts=new List<CoreUiBinding.InstructionSource>();
            foreach(var item in runtime.Stages.Stages)
            {
                var stage=item as StageService;
                if(stage==null) throw new InvalidOperationException("Unexpected Stage implementation.");
                stages.Add(stage);
                stage.Initialize(bus,flow,actor,code=>runtime.TryStartSafeDialogue(stage.StageId,code),runtime.Session);
                stage.BindChaseStart(s=>chase.TryStart(s.StageId,s.ChaseWaypoints,s.Actor));
                stage.ChaseStarted+=OnChaseStarted;
                foreach(var memo in stage.GetComponentsInChildren<FeelsGoodOnion.TechSYM.Interaction.MemoUIController>(true))
                { memo.Initialize(control); memos.Add(memo); }
                foreach(var instruction in stage.GetComponentsInChildren<LevelInstruction>(true))
                    prompts.Add(new CoreUiBinding.InstructionSource(instruction.transform,instruction.Text));
            }
            ui.SetInstructions(prompts);
            runtime.ActorHost.Interaction.BindOverlays(memos);
            camera.Bind(runtime.Player, control, runtime.ActorHost.CameraRig);
            ui.SetActorCamera(runtime.ActorHost.CameraRig.OutputCamera);
            foreach(var stage in stages)
                foreach(var zone in stage.GetComponentsInChildren<CameraZoneTrigger>(true)) zone.Bind(camera,actor);
            var initialStage = (StageService)runtime.Stages.Require(runtime.Session.Checkpoint.StageId);
            var initialPoint = initialStage.GetCheckpoint(runtime.Session.Checkpoint.CheckpointId);
            var initialZone = initialPoint.CameraZone;
            if(string.IsNullOrWhiteSpace(initialZone.Id))
                initialZone = new CameraZoneRequest(initialStage.StageId + "_Entry",
                    initialStage.StageId == "03_2_Stage" ? CameraMovementMode.Quarter : CameraMovementMode.Side,
                    initialPoint.transform.position);
            camera.EnterZone(initialZone,true);
            audioFlow.SetStage(runtime.Session.Checkpoint.StageId);
            checkpoint=runtime.Session.Checkpoint; fragments=runtime.Player.Snapshot.Fragments;
            unlocked.Clear();
            foreach(AbilityId ability in Enum.GetValues(typeof(AbilityId)))
                if(runtime.Session.IsUnlocked(ability)) unlocked.Add(ability);
            runtime.Session.Changed+=OnSession;
            runtime.Player.Changed+=OnPlayer;
            observing=true;
        }
        private void OnStage(StageEnteredEvent value)
        {
            if(flow.Snapshot.State!=FlowState.Playing || flow.Snapshot.IsBusy || !runtime.Player.HasActor) return;
            runtime.Stages.Require(value.StageId);
            audioFlow.SetStage(value.StageId);
        }
        private void OnChaseStarted(StageService stage) => audioFlow.SetChaseActive(true);
        private void OnAbility(AbilityId ability)
        {
            if(!observing || !runtime.Player.HasActor) return;
            audio.PlaySfx(ability==AbilityId.Tear?AudioCueIds.Tear:ability==AbilityId.FormChange?AudioCueIds.Form:AudioCueIds.Shell);
        }
        private void OnPlayer(PlayerSnapshot value)
        {
            if(observing && value.Fragments>fragments) audio.PlaySfx(AudioCueIds.Pickup);
            fragments=value.Fragments;
        }
        private void OnSession()
        {
            if(!observing || !runtime.Session.IsActive) return;
            var next=runtime.Session.Checkpoint;
            if(next.StageId!=checkpoint.StageId || next.CheckpointId!=checkpoint.CheckpointId)
                audio.PlaySfx(AudioCueIds.Checkpoint);
            checkpoint=next;
            bool newlyUnlocked=false;
            foreach(AbilityId ability in Enum.GetValues(typeof(AbilityId)))
                if(runtime.Session.IsUnlocked(ability) && unlocked.Add(ability)) newlyUnlocked=true;
            if(newlyUnlocked) audio.PlaySfx(AudioCueIds.Unlock);
        }
        private void OnFlow(FlowSnapshot state)
        {
            if(state.State==FlowState.Ending && state.IsBusy) audio.PlaySfx(AudioCueIds.Escape);
        }
        private void UnbindActor()
        {
            observing=false;
            runtime.Session.Changed-=OnSession; runtime.Player.Changed-=OnPlayer;
            var errors=new List<Exception>();
            foreach(var memo in memos)
                if(memo != null) { try { memo.Unbind(); } catch(Exception e) { errors.Add(e); } }
            memos.Clear();
            foreach(var stage in stages)
            {
                if(stage==null) continue;
                foreach (var zone in stage.GetComponentsInChildren<CameraZoneTrigger>(true)) zone.Bind(null,null);
                stage.ChaseStarted-=OnChaseStarted;
                try { stage.Dispose(); } catch(Exception e) { errors.Add(e); }
            }
            stages.Clear();
            try { chase.Shutdown(); } catch(Exception e) { errors.Add(e); }
            try { camera.Dispose(); } catch(Exception e) { errors.Add(e); }
            if(errors.Count>0) throw new AggregateException("World release failed.",errors);
        }
        public void Dispose()
        {
            if(disposed) return; disposed=true;
            stageSubscription.Dispose();
            runtime.ActorReady-=BindActor; runtime.ActorRemoving-=UnbindActor;
            runtime.Player.AbilitySucceeded-=OnAbility; flow.Changed-=OnFlow;
            UnbindActor();
        }
    }
}
