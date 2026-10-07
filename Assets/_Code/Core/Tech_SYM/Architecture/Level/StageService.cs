using System;
using System.Collections.Generic;
using Cooked.Contracts;
using Cooked.Dialogue;
using Cooked.Session;
using FeelsGoodOnion.TechSYM.Enemies;
using FeelsGoodOnion.TechSYM.Features;
using FeelsGoodOnion.TechSYM.OvenTray;
using FeelsGoodOnion.TechSYM.Platforms;
using UnityEngine;
namespace Cooked.Level
{
    [DefaultExecutionOrder(500), DisallowMultipleComponent]
    public sealed class StageService : MonoBehaviour, IStageService
    {
        [Tooltip("실행 구성에서 사용할 스테이지 ID입니다. 등록 씬의 이름과 같아야 하며 체크포인트 저장값에는 포함하지 않습니다.")]
        [SerializeField] private string stageId;
        [Tooltip("새 진입의 시작 체크포인트 ID입니다. 시작·끝 두 곳 중 시작 지점을 지정합니다.")]
        [SerializeField] private string initialCheckpointId;
        [Tooltip("시작·끝 체크포인트 두 곳입니다. 시작 ID와 서로 다른 끝 ID를 지정하며 중간 해금은 별도 트리거로 만듭니다.")]
        [SerializeField] private CheckpointMarker[] checkpoints;
        [Tooltip("현재 Actor를 연결할 일반 적입니다. HP·처치·기름 없이 Idle/X/Y순찰/기절을 사용합니다.")]
        [SerializeField] private EnemyActor[] enemies;
        [Tooltip("추격 무리가 이동할 월드 경로입니다(m). 순서대로 두 점 이상 지정하며 빈 목록은 추격 없음입니다.")]
        [SerializeField] private Vector3[] chaseWaypoints = Array.Empty<Vector3>();
        [Tooltip("추격 시작이 수락된 뒤 닫힐 후퇴문입니다. 실제 플레이어 몸체와 겹치지 않아야 합니다.")]
        [SerializeField] private ChaseRetreatGate chaseRetreatGate;
        [Tooltip("플레이어 전체 몸체가 새로 통과해야 하는 시작선입니다(m). 복귀·재연결 뒤 시작선 이전 몸체를 먼저 관찰해야 하며, 선 너머에 정지한 채 생성되면 시작하지 않습니다. 진행 경로가 있으면 경로 거리, 없으면 스테이지 로컬 X 기준입니다.")]
        [SerializeField] private float chaseStartProgress = 18f;
        [Tooltip("시작선 판정의 월드 진행 경로입니다. 두 점 미만이면 스테이지 로컬 X를 사용합니다.")]
        [SerializeField] private Vector3[] progressWaypoints = Array.Empty<Vector3>();
        private Cooked.Chase.ChasePath progressPath;
        private readonly Dictionary<string, Pose> poses = new Dictionary<string, Pose>(StringComparer.Ordinal);
        private IGameEventBus eventBus;
        private IGameFlowService flow;
        private PlayerFacade actor;
        private PlayerBridgeService bridge;
        private IGameplayControlService control;
        private DialogueService dialogue;
        private bool bound, chaseAccepted, chaseStartInProgress, finalPending, finalStarted;
        private bool chaseApproachObserved;
        private long bindingRevision, finalDialogueSession;
        private PlayerFacade finalActor;
        private string finalCode;
        private Func<string, bool> tryStartSafeDialogue;
        private Func<StageService, bool> tryStartChase;
        private Func<string, PlayerFacade, bool> captureCheckpoint;
        private Func<AbilityId, string, bool> unlockAbility;
        private Action stopChase;
        public string StageId => stageId;
        public string InitialCheckpointId => initialCheckpointId;
        // Component lifetime only: retained scenes warm once, including across Title/new rounds.
        public bool IsInitialPreparationComplete { get; private set; }
        public bool IsBound => bound;
        public PlayerFacade Actor => actor;
        public IReadOnlyList<Vector3> ChaseWaypoints => Array.AsReadOnly(chaseWaypoints);
        public bool IsChaseAccepted => chaseAccepted;
        public event Action<StageService> ChaseStarted;
        public (string StartId, string EndId) CheckpointPair
        {
            get
            {
                RebuildRegistry();
                var end = checkpoints[0].CheckpointId == initialCheckpointId ? checkpoints[1] : checkpoints[0];
                return (initialCheckpointId, end.CheckpointId);
            }
        }
        public void Configure(string id, string startId, CheckpointMarker[] pair, EnemyActor[] actors)
        { stageId = id; initialCheckpointId = startId; checkpoints = pair; enemies = actors ?? Array.Empty<EnemyActor>(); RebuildRegistry(); }
        public float ProgressAt(Vector3 position) => progressPath != null ? progressPath.Project(position) : transform.InverseTransformPoint(position).x;
        public CheckpointMarker GetCheckpoint(string id) => Array.Find(checkpoints, point => point != null && point.CheckpointId == id);
        public bool HasAbilityUnlock(AbilityId ability)
        {
            foreach (var trigger in GetComponentsInChildren<StageTrigger>(true))
                if (trigger.Owner == this && ((ability == AbilityId.FormChange && trigger.Kind == StageTriggerKind.UnlockForm) ||
                    (ability == AbilityId.Tear && trigger.Kind == StageTriggerKind.UnlockTear))) return true;
            return false;
        }
        public IEnumerable<string> AbilityNoticeCodes(IReadOnlyList<AbilityId> relocked)
        {
            var codes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var trigger in GetComponentsInChildren<StageTrigger>(true))
            {
                if (trigger.Owner != this || string.IsNullOrWhiteSpace(trigger.Value)) continue;
                foreach (var ability in relocked)
                    if ((ability == AbilityId.FormChange && trigger.Kind == StageTriggerKind.UnlockForm) ||
                        (ability == AbilityId.Tear && trigger.Kind == StageTriggerKind.UnlockTear)) codes.Add(trigger.Value);
            }
            return codes;
        }
        public void Initialize(IGameEventBus bus, IGameFlowService flowService, PlayerFacade player,
            Func<string, bool> safeDialogueStart = null, IGameSessionService sessionService = null)
        {
            if (bus == null || flowService == null || player == null) throw new ArgumentNullException("Stage requires bus, flow and Actor.");
            Dispose(); RebuildRegistry(); eventBus = bus; flow = flowService; actor = player; tryStartSafeDialogue = safeDialogueStart;
            bound = true; bindingRevision++;
            ObserveChaseApproach();
            if (enemies != null) foreach (var enemy in enemies) if (enemy != null) enemy.BindTarget(player);
        }
        public void BindRequests(PlayerBridgeService player, IGameplayControlService gameplayControl, DialogueService conversation,
            Func<string, PlayerFacade, bool> checkpointCapture, Func<AbilityId, string, bool> abilityUnlock, Action haltChase)
        {
            if (!bound) throw new InvalidOperationException("Initialize Stage before binding its request owners.");
            if (dialogue != null) dialogue.Ended -= OnFinalDialogueEnded;
            bridge = player ?? throw new ArgumentNullException(nameof(player)); control = gameplayControl ?? throw new ArgumentNullException(nameof(gameplayControl));
            dialogue = conversation ?? throw new ArgumentNullException(nameof(conversation));
            captureCheckpoint = checkpointCapture ?? throw new ArgumentNullException(nameof(checkpointCapture));
            unlockAbility = abilityUnlock ?? throw new ArgumentNullException(nameof(abilityUnlock)); stopChase = haltChase ?? throw new ArgumentNullException(nameof(haltChase));
            dialogue.Ended += OnFinalDialogueEnded;
        }
        public void BindChaseStart(Func<StageService, bool> start) => tryStartChase = start ?? throw new ArgumentNullException(nameof(start));
        public bool TryCloseChaseRetreatGate() => chaseAccepted && chaseRetreatGate != null && chaseRetreatGate.TryClose(actor);
        public void RebuildRegistry()
        {
            poses.Clear(); progressPath = progressWaypoints != null && progressWaypoints.Length > 1 ? new Cooked.Chase.ChasePath(progressWaypoints) : null;
            if (string.IsNullOrWhiteSpace(stageId) || checkpoints == null || checkpoints.Length != 2 || !float.IsFinite(chaseStartProgress))
                throw new InvalidOperationException("Stage must configure exactly START/END and a finite start line: " + name);
            foreach (var point in checkpoints)
            {
                if (point == null || string.IsNullOrWhiteSpace(point.CheckpointId) || poses.ContainsKey(point.CheckpointId))
                    throw new InvalidOperationException("Invalid/duplicate checkpoint in " + stageId);
                poses.Add(point.CheckpointId, point.Pose);
            }
            if (!poses.ContainsKey(initialCheckpointId)) throw new InvalidOperationException("Missing start checkpoint: " + stageId);
        }
        public bool TryGetCheckpointPose(string id, out Pose pose)
        { if (poses.Count == 0) RebuildRegistry(); return poses.TryGetValue(id ?? string.Empty, out pose); }
        private bool CanDispatch(PlayerFacade candidate) => bound && candidate != null && candidate == actor && !actor.IsDead &&
            flow != null && flow.Snapshot.State == FlowState.Playing && !flow.Snapshot.IsBusy && control != null && !control.State.WorldPaused;
        public bool TryDispatch(StageTrigger trigger, PlayerFacade candidate)
        {
            if (trigger == null || trigger.Owner != this || candidate != actor) return false;
            // Keep approach history while a current contact is temporarily blocked; no start occurs here.
            if (trigger.Kind == StageTriggerKind.Chase) ObserveChaseApproach();
            if (!CanDispatch(candidate)) return false;
            switch (trigger.Kind)
            {
                case StageTriggerKind.Enter: eventBus.Publish(new StageEnteredEvent(stageId, trigger.Value)); return true;
                case StageTriggerKind.Checkpoint:
                    if (!poses.ContainsKey(trigger.Value)) return false;
                    return captureCheckpoint != null && captureCheckpoint(trigger.Value, candidate);
                case StageTriggerKind.UnlockForm: return unlockAbility != null && unlockAbility(AbilityId.FormChange, trigger.Value);
                case StageTriggerKind.UnlockTear: return unlockAbility != null && unlockAbility(AbilityId.Tear, trigger.Value);
                case StageTriggerKind.Dialogue: return tryStartSafeDialogue != null && tryStartSafeDialogue(trigger.Value);
                case StageTriggerKind.Fall: return bridge != null && bridge.ForceDeath(Vector3.zero, skipPresentation: true) > 0;
                case StageTriggerKind.Chase: return TryStartChaseAtBodyPosition();
                case StageTriggerKind.Escape:
                    if (finalPending || finalStarted || (chaseWaypoints.Length > 1 && !chaseAccepted)) return false;
                    finalActor = candidate; finalCode = string.IsNullOrWhiteSpace(trigger.Value) ? "POPO" : trigger.Value;
                    finalPending = true; return true; // Resolve after all physics contacts and chase capture for this frame.
                default: return false;
            }
        }
        private bool WholeBodyPastStart(out bool found)
        {
            found = false;
            foreach (var body in actor.GetComponentsInChildren<Collider>(true))
            {
                if (!body.enabled || body.isTrigger || !body.gameObject.activeInHierarchy) continue;
                found = true; Bounds bounds = body.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    if (ProgressAt(corner) < chaseStartProgress) return false;
                }
            }
            return found;
        }
        private void ObserveChaseApproach()
        {
            if (!bound || actor == null || actor.IsDead || chaseAccepted || chaseApproachObserved) return;
            if (!WholeBodyPastStart(out bool found) && found) chaseApproachObserved = true;
        }
        // Observe the approach even outside the Chase sensor. Only the sensor may request acceptance.
        private void FixedUpdate() => ObserveChaseApproach();
        private bool TryStartChaseAtBodyPosition()
        {
            if (chaseAccepted) return true;
            if (chaseStartInProgress || !chaseApproachObserved || !WholeBodyPastStart(out _) || tryStartChase == null || chaseRetreatGate == null || !chaseRetreatGate.CanClose(actor)) return false;
            chaseStartInProgress = true; long revision = bindingRevision;
            try
            {
                if (!tryStartChase(this) || !bound || revision != bindingRevision || actor == null || actor.IsDead) return false;
                chaseAccepted = true;
                if (!TryCloseChaseRetreatGate()) throw new InvalidOperationException("Accepted chase gate cannot close without moving Actor.");
                ChaseStarted?.Invoke(this); return true;
            }
            finally { chaseStartInProgress = false; }
        }
        private void LateUpdate()
        {
            if (!finalPending) return;
            if (!bound || finalActor == null || finalActor != actor || actor.IsDead) { finalPending = false; finalActor = null; return; }
            if (!CanDispatch(finalActor)) return;
            stopChase(); // ChaseRuntimeDriver runs earlier; capture/death has priority over arrival.
            if (dialogue.IsActive) return;
            long revision = bindingRevision;
            finalStarted = true;
            if (!tryStartSafeDialogue(finalCode)) { finalStarted = false; throw new InvalidOperationException("Final dialogue could not start: " + finalCode); }
            if (!bound || revision != bindingRevision || actor != finalActor || actor.IsDead) { dialogue.Cancel(); return; }
            finalDialogueSession = dialogue.SessionId; finalPending = false;
        }
        private void OnFinalDialogueEnded(DialogueOutcome outcome)
        {
            if (!finalStarted || dialogue == null || dialogue.SessionId != finalDialogueSession) return;
            finalStarted = false;
            if (outcome != DialogueOutcome.Completed || finalActor == null || finalActor != actor || !CanDispatch(finalActor)) return;
            finalActor = null;
            if (!flow.Request(new FlowRequest(FlowCommand.Escape))) throw new InvalidOperationException("Final dialogue completed but Ending was rejected.");
        }
        /// <summary>Explicit initial arrangement, no saved world DTO. Call with roots inactive and Actor released.</summary>
        public void ResetForRetry()
        {
            Dispose(); RebuildRegistry();
            foreach (var motion in GetComponentsInChildren<KinematicPlatformMotion>(true)) motion.ResetForRetry();
            foreach (var contacts in GetComponentsInChildren<PlatformContacts>(true)) contacts.ResetForRetry();
            foreach (var mover in GetComponentsInChildren<LinearShuttlePlatform>(true)) mover.ResetForRetry();
            foreach (var floating in GetComponentsInChildren<FloatingPlatform>(true)) floating.ResetForRetry();
            foreach (var periodic in GetComponentsInChildren<PeriodicPlatform>(true)) periodic.ResetForRetry();
            foreach (var pressure in GetComponentsInChildren<PressurePlatePlatform>(true)) pressure.ResetForRetry();
            foreach (var linked in GetComponentsInChildren<PressureLinkedPlatform>(true)) linked.ResetForRetry();
            foreach (var floor in GetComponentsInChildren<DamageFloorPlatform>(true)) floor.ResetForRetry();
            foreach (var collapsed in GetComponentsInChildren<ShatteredPlatform>(true)) collapsed.ResetForRetry();
            foreach (var device in GetComponentsInChildren<OvenTrayObject>(true)) device.ResetForRetry();
            // All mover/support poses are restored before any enemy queries their terrain.
            Physics.SyncTransforms();
            foreach (var enemy in GetComponentsInChildren<EnemyActor>(true)) enemy.ResetForRetry();
            foreach (var pickup in GetComponentsInChildren<TearFragment>(true)) pickup.ResetForRetry();
            foreach (var trigger in GetComponentsInChildren<StageTrigger>(true)) trigger.ResetForRetry();
            foreach (var zone in GetComponentsInChildren<CameraZoneTrigger>(true)) zone.ResetForRetry();
            if (chaseRetreatGate != null) chaseRetreatGate.ResetForRetry();
            chaseAccepted = false; chaseStartInProgress = false;
            IsInitialPreparationComplete = true;
        }
        public void Dispose()
        {
            bound = false; bindingRevision++;
            chaseApproachObserved = chaseAccepted = chaseStartInProgress = false;
            if (dialogue != null) dialogue.Ended -= OnFinalDialogueEnded;
            if (enemies != null) foreach (var enemy in enemies) if (enemy != null) enemy.BindTarget(null);
            actor = null; bridge = null; control = null; dialogue = null; eventBus = null; flow = null;
            tryStartSafeDialogue = null; tryStartChase = null; captureCheckpoint = null; unlockAbility = null; stopChase = null;
            ChaseStarted = null; finalPending = finalStarted = false; finalActor = null; finalCode = null; finalDialogueSession = 0;
        }
        private void OnDisable() => Dispose();
        private void OnDestroy() => Dispose();
    }
}
