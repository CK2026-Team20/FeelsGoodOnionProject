using System;
using System.Collections.Generic;
using Cooked.Contracts;
using FeelsGoodOnion.TechSYM.Enemies;
using UnityEngine;
namespace Cooked.Level
{
    [DisallowMultipleComponent]
    public sealed class StageService : MonoBehaviour, IStageService
    {
        [SerializeField] private string stageId;
        [SerializeField] private string initialCheckpointId;
        [SerializeField] private CheckpointMarker[] checkpoints;
        [SerializeField] private EnemyActor[] enemies;
        [SerializeField] private Vector3[] chaseWaypoints = Array.Empty<Vector3>();
        [SerializeField] private ChaseRetreatGate chaseRetreatGate;
        [SerializeField] private float chaseStartProgress = 18f;
        private readonly Dictionary<string, Pose> poses = new Dictionary<string, Pose>(StringComparer.Ordinal);
        private IGameEventBus eventBus;
        private IGameFlowService flow;
        private PlayerFacade actor;
        private bool bound;
        private Func<string, bool> tryStartSafeDialogue;
        private bool rescued;
        private IGameSessionService session;
        private Func<StageService, bool> tryStartChase;
        private bool chaseAccepted;
        private bool chaseStartInProgress;
        public string StageId => stageId;
        public float ProgressAt(Vector3 worldPosition) => transform.InverseTransformPoint(worldPosition).x;
        public string InitialCheckpointId => initialCheckpointId;
        public bool IsBound => bound;
        public PlayerFacade Actor => actor;
        public IReadOnlyList<Vector3> ChaseWaypoints => Array.AsReadOnly(chaseWaypoints);
        // Successful-start notification, NOT a second command to Chase.Start. Emitted once after gate closure.
        public event Action<StageService> ChaseStarted;
        public bool IsChaseAccepted => chaseAccepted;
        public void BindChaseStart(Func<StageService, bool> start) => tryStartChase = start ?? throw new ArgumentNullException(nameof(start));
        public bool TryCloseChaseRetreatGate() => chaseAccepted && chaseRetreatGate != null && chaseRetreatGate.TryClose(actor);
        public void Initialize(IGameEventBus bus, IGameFlowService flowService, PlayerFacade player, Func<string, bool> safeDialogueStart = null, IGameSessionService sessionService = null)
        {
            if (bus == null || flowService == null || player == null) throw new ArgumentNullException("Stage binding requires bus, flow and Actor.");
            if (bound) Dispose();
            RebuildRegistry();
            eventBus = bus; flow = flowService; actor = player; tryStartSafeDialogue = safeDialogueStart; session = sessionService;
            foreach (var enemy in enemies) if (enemy != null) enemy.BindTarget(player);
            foreach (var gate in GetComponentsInChildren<TearStunGate>(true)) gate.Bind(player);
            foreach (var gate in GetComponentsInChildren<ShellPressureLatch>(true)) gate.Bind(player);
            bound = true;
        }
        public void RebuildRegistry()
        {
            poses.Clear();
            if (string.IsNullOrWhiteSpace(stageId) || checkpoints == null) throw new InvalidOperationException($"Invalid Stage registry: {name}");
            foreach (var point in checkpoints)
            {
                if (point == null || string.IsNullOrWhiteSpace(point.CheckpointId) || poses.ContainsKey(point.CheckpointId))
                    throw new InvalidOperationException($"Invalid/duplicate checkpoint in {stageId}.");
                poses.Add(point.CheckpointId, point.Pose);
            }
            if (!poses.ContainsKey(initialCheckpointId)) throw new InvalidOperationException($"Missing initial checkpoint {stageId}/{initialCheckpointId}.");
        }
        public bool TryGetCheckpointPose(string id, out Pose pose)
        {
            if (poses.Count == 0) RebuildRegistry();
            return poses.TryGetValue(id ?? string.Empty, out pose);
        }
        public bool TryDispatch(StageTrigger trigger, PlayerFacade candidate)
        {
            if (!bound || candidate != actor || actor.IsDead || flow.Snapshot.State != FlowState.Playing || flow.Snapshot.IsBusy) return false;
            switch (trigger.Kind)
            {
                case StageTriggerKind.Enter:
                    eventBus.Publish(new StageEnteredEvent(stageId, trigger.Value)); break;
                case StageTriggerKind.Checkpoint:
                    if (!poses.ContainsKey(trigger.Value)) throw new InvalidOperationException($"Unknown checkpoint {stageId}/{trigger.Value}");
                    eventBus.Publish(new CheckpointReachedEvent(stageId, trigger.Value)); break;
                case StageTriggerKind.UnlockForm:
                    eventBus.Publish(new AbilityUnlockedEvent(AbilityId.FormChange));
                    eventBus.Publish(new AbilityUnlockedEvent(AbilityId.RecoverShell)); break;
                case StageTriggerKind.UnlockTear:
                    eventBus.Publish(new AbilityUnlockedEvent(AbilityId.Tear)); break;
                case StageTriggerKind.Dialogue:
                    // One direct acceptance path: integration guards safety and delegates to IDialogueService.TryStart.
                    // Do not also publish DialogueRequestedEvent: that would issue the command twice.
                    return stageId == "03_2_Stage" && tryStartSafeDialogue != null && tryStartSafeDialogue(trigger.Value);
                case StageTriggerKind.Fall:
                    eventBus.Publish(new FlowRequestedEvent(new FlowRequest(FlowCommand.RetryCheckpoint))); break;
                case StageTriggerKind.Rescue:
                    rescued = true; break;
                case StageTriggerKind.Escape:
                    if (!rescued || (stageId == "03_3_Stage" && !chaseAccepted)) return false;
                    float resumeProgress = float.NegativeInfinity;
                    if (session != null && session.IsActive && session.Checkpoint.StageId == stageId &&
                        TryGetCheckpointPose(session.Checkpoint.CheckpointId, out var savedPose)) resumeProgress = ProgressAt(savedPose.position);
                    foreach (var puzzle in GetComponentsInChildren<ShellPressureLatch>(true))
                        if (!puzzle.IsSolved && puzzle.CrossingProgress > resumeProgress) return false;
                    eventBus.Publish(new FlowRequestedEvent(new FlowRequest(FlowCommand.Escape))); break;
                case StageTriggerKind.Chase:
                    return TryStartChaseAtBodyPosition();
            }
            return true;
        }
        private bool TryStartChaseAtBodyPosition()
        {
            if (chaseAccepted) return true;
            if (chaseStartInProgress) return false;
            // The collider can overlap the start volume before its ActorBody reaches the line.
            if (ProgressAt(actor.transform.position) < chaseStartProgress || tryStartChase == null || chaseRetreatGate == null) return false;
            if (session == null || !session.IsActive) return false;
            // CP82 is physically behind X84; jumping across the thin CP trigger must not leave an older snapshot.
            if (session.Checkpoint.StageId != stageId || session.Checkpoint.CheckpointId != "S3_CHASE")
                eventBus.Publish(new CheckpointReachedEvent(stageId, "S3_CHASE"));
            if (session.Checkpoint.StageId != stageId || session.Checkpoint.CheckpointId != "S3_CHASE") return false;
            if (!chaseRetreatGate.CanClose(actor)) return false;
            chaseStartInProgress = true;
            try
            {
                if (!tryStartChase(this)) return false;
                chaseAccepted = true;
                // This synchronous Level connection closes only on Chase's successful bool response.
                if (!TryCloseChaseRetreatGate()) throw new InvalidOperationException("Chase accepted but retreat gate overlaps Actor. Start callback must not move the Actor.");
                ChaseStarted?.Invoke(this);
                return true;
            }
            finally { chaseStartInProgress = false; }
        }
        public void Dispose()
        {
            bound = false;
            if (enemies != null) foreach (var enemy in enemies) if (enemy != null) enemy.BindTarget(null);
            foreach (var gate in GetComponentsInChildren<TearStunGate>(true)) gate.Bind(null);
            foreach (var gate in GetComponentsInChildren<ShellPressureLatch>(true)) gate.Bind(null);
            actor = null; eventBus = null; flow = null; tryStartSafeDialogue = null; session = null; tryStartChase = null; ChaseStarted = null;
        }
        private void OnDestroy() => Dispose();
    }
}




