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
        [Tooltip("스테이지의 고유 ID입니다. 진입 설정·저장 체크포인트의 Stage Id와 정확히 일치해야 합니다.")]
        [SerializeField] private string stageId;
        [Tooltip("새 진입 시 사용할 체크포인트 ID입니다. 아래 Checkpoints 목록에 같은 ID가 반드시 있어야 합니다.")]
        [SerializeField] private string initialCheckpointId;
        [Tooltip("이 스테이지의 체크포인트 목록입니다. null 또는 중복 ID가 있으면 등록에 실패합니다.")]
        [SerializeField] private CheckpointMarker[] checkpoints;
        [Tooltip("현재 플레이어를 연결할 스테이지 적 목록입니다. 추격 상태를 추가하는 설정이 아닙니다.")]
        [SerializeField] private EnemyActor[] enemies;
        [Tooltip("추격 무리가 이동할 월드 위치 경로입니다. 두 점 이상을 이동 순서대로 지정합니다. 빈 목록은 추격 경로 없음입니다.")]
        [SerializeField] private Vector3[] chaseWaypoints = Array.Empty<Vector3>();
        [Tooltip("추격 시작 후 뒤쪽 통로를 닫을 담당입니다.")]
        [SerializeField] private ChaseRetreatGate chaseRetreatGate;
        [Tooltip("추격을 시작할 최소 진행값입니다(경로 월드 거리, 진행 경로 없으면 스테이지 로컬 X). 높이면 더 나중에 추격이 시작됩니다.")]
        [SerializeField] private float chaseStartProgress = 18f;
        [Tooltip("추격 시작 조건과 복원 위치를 확인할 체크포인트 ID입니다. 이 스테이지의 등록 ID와 일치해야 합니다.")]
        [SerializeField] private string chaseCheckpointId = "S3_CHASE";
        [Tooltip("기믹·추격 시작 판단에 사용할 월드 진행 경로입니다. 두 점 이상이면 경로 거리로 계산하며 없으면 스테이지 로컬 X를 사용합니다.")]
        [SerializeField] private Vector3[] progressWaypoints = Array.Empty<Vector3>();
        private Cooked.Chase.ChasePath progressPath;
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
        public float ProgressAt(Vector3 worldPosition) => progressPath != null ? progressPath.Project(worldPosition) : transform.InverseTransformPoint(worldPosition).x;
        public CheckpointMarker GetCheckpoint(string id) => Array.Find(checkpoints, point => point != null && point.CheckpointId == id);
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
            foreach (var gate in GetComponentsInChildren<ShellPressureLatch>(true)) gate.Bind(player);
            foreach (var obsolete in GetComponentsInChildren<TearStunGate>(true)) obsolete.RetireLegacyGate();
            bound = true;
        }
        public void RebuildRegistry()
        {
            poses.Clear();
            progressPath = progressWaypoints != null && progressWaypoints.Length > 1 ? new Cooked.Chase.ChasePath(progressWaypoints) : null;
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
                    var checkpoint = GetCheckpoint(trigger.Value);
                    if (checkpoint.UnlockForm)
                    {
                        eventBus.Publish(new AbilityUnlockedEvent(AbilityId.FormChange));
                        eventBus.Publish(new AbilityUnlockedEvent(AbilityId.RecoverShell));
                    }
                    if (checkpoint.UnlockTear) eventBus.Publish(new AbilityUnlockedEvent(AbilityId.Tear));
                    eventBus.Publish(new CheckpointReachedEvent(stageId, trigger.Value)); break;
                case StageTriggerKind.UnlockForm:
                    eventBus.Publish(new AbilityUnlockedEvent(AbilityId.FormChange));
                    eventBus.Publish(new AbilityUnlockedEvent(AbilityId.RecoverShell)); break;
                case StageTriggerKind.UnlockTear:
                    eventBus.Publish(new AbilityUnlockedEvent(AbilityId.Tear)); break;
                case StageTriggerKind.Dialogue:
                    // One direct acceptance path: integration guards safety and delegates to IDialogueService.TryStart.
                    // Do not also publish DialogueRequestedEvent: that would issue the command twice.
                    return tryStartSafeDialogue != null && tryStartSafeDialogue(trigger.Value);
                case StageTriggerKind.Fall:
                    eventBus.Publish(new FlowRequestedEvent(new FlowRequest(FlowCommand.RetryCheckpoint))); break;
                case StageTriggerKind.Rescue:
                    rescued = true; break;
                case StageTriggerKind.Escape:
                    if (!rescued || (chaseWaypoints.Length > 1 && !chaseAccepted)) return false;
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
            if (session.Checkpoint.StageId != stageId || session.Checkpoint.CheckpointId != chaseCheckpointId)
                eventBus.Publish(new CheckpointReachedEvent(stageId, chaseCheckpointId));
            if (session.Checkpoint.StageId != stageId || session.Checkpoint.CheckpointId != chaseCheckpointId) return false;
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
            foreach (var gate in GetComponentsInChildren<ShellPressureLatch>(true)) gate.Bind(null);
            actor = null; eventBus = null; flow = null; tryStartSafeDialogue = null; session = null; tryStartChase = null; ChaseStarted = null;
        }
        private void OnDestroy() => Dispose();
    }
}




