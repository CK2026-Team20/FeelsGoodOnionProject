using System;
using System.Collections.Generic;
using Cooked.Authoring;
using Cooked.Audio;
using Cooked.Contracts;
using Cooked.Dialogue;
using Cooked.Integration;
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
        [SerializeField, InspectorLabel("초기 체크포인트"), Tooltip("새 진입 위치를 정할 같은 씬의 CheckpointMarker를 연결하세요. 완료 저장과는 별개입니다.")]
        private CheckpointMarker initialCheckpoint;
        [SerializeField, InspectorLabel("스테이지 음악"), Tooltip("이 씬에서 재생할 음악 용도를 선택하세요. 기존 AudioCueCatalog의 AudioClip을 사용합니다.")]
        private AudioCuePurpose stageMusicPurpose = AudioCuePurpose.Stage1Music;
        [SerializeField, InspectorLabel("체크포인트 목록"), Tooltip("같은 씬의 CheckpointMarker를 연결하세요. 개수 제한은 없으며 비우면 씬 전체에서 조회합니다.")]
        private CheckpointMarker[] checkpoints;
        [SerializeField, InspectorLabel("일반 적 목록"), Tooltip("같은 씬의 EnemyActor를 연결하세요. 비우면 비활성 루트를 포함한 씬 전체에서 조회합니다.")]
        private EnemyActor[] enemies;
        [SerializeField, InspectorLabel("추격 경로"), Tooltip("추격 무리의 고정 Transform 지점을 진행 순서로 연결하세요. 두 지점 이상이며 빈 목록은 추격 없음입니다.")]
        private Transform[] chasePathPoints = Array.Empty<Transform>();
        [SerializeField, InspectorLabel("진행 경로"), Tooltip("시작선 진행을 계산할 고정 Transform 지점을 순서로 연결하세요. 경로가 없으면 시작 지점의 오른쪽 방향을 사용합니다.")]
        private Transform[] progressPathPoints = Array.Empty<Transform>();
        [SerializeField, InspectorLabel("추격 시작 지점"), Tooltip("전신 통과를 확인할 고정 Transform을 연결하세요. 경로가 없으면 이 지점의 오른쪽이 진행 방향입니다.")]
        private Transform chaseStartPoint;
        [SerializeField, InspectorLabel("추격 퇴로"), Tooltip("추격 수락 뒤 닫을 같은 씬의 ChaseRetreatGate를 연결하세요.")]
        private ChaseRetreatGate chaseRetreatGate;
        // T11 preserves the captured old world calculations. T10 replaces ID/pair consumers, then T12 removes these fields/APIs.
        [SerializeField, HideInInspector] private string stageId;
        [SerializeField, HideInInspector] private string initialCheckpointId;
        [SerializeField, HideInInspector] private Vector3[] chaseWaypoints = Array.Empty<Vector3>();
        [SerializeField, HideInInspector] private float chaseStartProgress = 18f;
        [SerializeField, HideInInspector] private Vector3[] progressWaypoints = Array.Empty<Vector3>();
        private Vector3[] resolvedChaseWaypoints = Array.Empty<Vector3>();
        private CheckpointMarker[] sceneCheckpoints = Array.Empty<CheckpointMarker>();
        private EnemyActor[] sceneEnemies = Array.Empty<EnemyActor>();
        private float resolvedChaseStartProgress;
        private Vector3 progressOrigin, progressDirection;
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
        private Func<CheckpointMarker, PlayerFacade, bool> captureMarker;
        private Func<AbilityId, string, bool> unlockAbility;
        private Func<PlayerFacade, PlayerDebris, bool> fallRequest;
        private IDialogueTableService dialogueTable;
        private Action stopChase;
        public string StageId => string.IsNullOrEmpty(gameObject.scene.path) ? gameObject.scene.name : SceneCatalog.StageIdFromPath(gameObject.scene.path);
        public string InitialCheckpointId => InitialCheckpoint != null ? InitialCheckpoint.CheckpointId : initialCheckpointId;
        public CheckpointMarker InitialCheckpoint => initialCheckpoint != null ? initialCheckpoint : GetCheckpoint(initialCheckpointId);
        public AudioCuePurpose StageMusicPurpose => RequireStageMusicPurpose(stageMusicPurpose);
        public static AudioCuePurpose RequireStageMusicPurpose(AudioCuePurpose purpose)
        {
            if (AudioCueIds.KindForPurpose(purpose) != AudioCueKind.Music)
                throw new ArgumentException("스테이지 음악은 음악 용도만 선택하세요.", nameof(purpose));
            return purpose;
        }
        public IReadOnlyList<Transform> ChasePathPoints => Array.AsReadOnly(chasePathPoints ?? Array.Empty<Transform>());
        public IReadOnlyList<Transform> ProgressPathPoints => Array.AsReadOnly(progressPathPoints ?? Array.Empty<Transform>());
        public Transform ChaseStartPoint => chaseStartPoint;
        // Component lifetime only: retained scenes warm once, including across Title/new rounds.
        public bool IsInitialPreparationComplete { get; private set; }
        public bool IsBound => bound;
        public PlayerFacade Actor => actor;
        public IReadOnlyList<Vector3> ChaseWaypoints => Array.AsReadOnly(resolvedChaseWaypoints);
        public bool IsChaseAccepted => chaseAccepted;
        public event Action<StageService> ChaseStarted;
        public (string StartId, string EndId) CheckpointPair
        {
            get
            {
                RebuildRegistry();
                var end = Array.Find(sceneCheckpoints, point => point != InitialCheckpoint);
                return (InitialCheckpointId, end != null ? end.CheckpointId : InitialCheckpointId);
            }
        }
        public void Configure(string id, string startId, CheckpointMarker[] pair, EnemyActor[] actors)
        { stageId = id; initialCheckpointId = startId; checkpoints = pair; enemies = actors ?? Array.Empty<EnemyActor>(); RebuildRegistry(); }
        public float ProgressAt(Vector3 position)
        {
            if (!CameraZoneRequest.IsFinite(position)) throw new ArgumentException("진행 위치는 유한해야 합니다.", nameof(position));
            if (progressPath != null) return progressPath.Project(position);
            return chaseStartPoint != null ? Vector3.Dot(position - progressOrigin, progressDirection) : transform.InverseTransformPoint(position).x;
        }
        public CheckpointMarker GetCheckpoint(string id)
        {
            var points = sceneCheckpoints.Length > 0 ? sceneCheckpoints : checkpoints;
            return points == null ? null : Array.Find(points, point => point != null && point.CheckpointId == id);
        }
        public T[] FindComponents<T>() where T : Component => SceneComponentLookup.FindAll<T>(gameObject.scene);
        public bool HasAbilityUnlock(AbilityId ability)
        {
            foreach (var trigger in FindComponents<StageTrigger>())
                if (trigger.Owner == this && ((ability == AbilityId.FormChange && trigger.Kind == StageTriggerKind.UnlockForm) ||
                    (ability == AbilityId.Tear && trigger.Kind == StageTriggerKind.UnlockTear))) return true;
            return false;
        }
        public IEnumerable<string> AbilityNoticeCodes(IReadOnlyList<AbilityId> relocked)
        {
            var codes = new HashSet<string>(StringComparer.Ordinal);
            foreach (var trigger in FindComponents<StageTrigger>())
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
            foreach (var enemy in sceneEnemies) if (enemy != null) enemy.BindTarget(player);
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
        public void BindCheckpointCapture(Func<CheckpointMarker, PlayerFacade, bool> capture)
            => captureMarker = capture ?? throw new ArgumentNullException(nameof(capture));
        public void BindDialogueTable(IDialogueTableService table)
            => dialogueTable = table ?? throw new ArgumentNullException(nameof(table));
        /// <summary>T10 owns Player death / current-owned Shell return. Stage validates current Actor before dispatch.</summary>
        public void BindFallRequests(Func<PlayerFacade, PlayerDebris, bool> request)
            => fallRequest = request ?? throw new ArgumentNullException(nameof(request));
        public bool TryCloseChaseRetreatGate() => chaseAccepted && chaseRetreatGate != null && chaseRetreatGate.TryClose(actor);
        public void RebuildRegistry()
        {
            SceneComponentLookup.RequireSingle<StageService>(gameObject.scene);
            _ = StageMusicPurpose;
            if (checkpoints != null) ValidateReferences(checkpoints, "체크포인트 목록");
            if (enemies != null) ValidateReferences(enemies, "일반 적 목록");
            sceneCheckpoints = FindComponents<CheckpointMarker>(); sceneEnemies = FindComponents<EnemyActor>();
            poses.Clear();
            var resolvedProgress = ResolvePoints(progressPathPoints, progressWaypoints, "진행 경로");
            progressPath = resolvedProgress.Length > 1 ? new Cooked.Chase.ChasePath(resolvedProgress) : null;
            resolvedChaseWaypoints = ResolvePoints(chasePathPoints, chaseWaypoints, "추격 경로");
            if (resolvedChaseWaypoints.Length > 1) _ = new Cooked.Chase.ChasePath(resolvedChaseWaypoints);
            if (chaseRetreatGate != null) SceneComponentLookup.RequireSameScene(this, chaseRetreatGate, "추격 퇴로");
            if (chaseStartPoint != null)
            {
                SceneComponentLookup.RequireFixedPoint(this, chaseStartPoint, "추격 시작 지점");
                if (!CameraZoneRequest.IsFinite(chaseStartPoint.position) || !CameraZoneRequest.IsFinite(chaseStartPoint.right))
                    throw new InvalidOperationException("추격 시작 지점은 유한해야 합니다.");
                progressOrigin = chaseStartPoint.position; progressDirection = chaseStartPoint.right;
                resolvedChaseStartProgress = progressPath != null ? progressPath.Project(progressOrigin) : 0;
            }
            else resolvedChaseStartProgress = chaseStartProgress;
            if (!float.IsFinite(resolvedChaseStartProgress)) throw new InvalidOperationException("추격 시작선은 유한해야 합니다.");
            foreach (var point in sceneCheckpoints)
            {
                if (point == null || string.IsNullOrWhiteSpace(point.CheckpointId) || poses.ContainsKey(point.CheckpointId))
                    throw new InvalidOperationException("Invalid/duplicate checkpoint in " + StageId);
                poses.Add(point.CheckpointId, point.Pose);
            }
            if (InitialCheckpoint == null) throw new InvalidOperationException("Missing initial Marker: " + StageId);
            SceneComponentLookup.RequireSameScene(this, InitialCheckpoint, "초기 체크포인트");
            if (!poses.ContainsKey(InitialCheckpointId)) throw new InvalidOperationException("Initial Marker is outside this scene: " + StageId);
        }
        private void ValidateReferences<T>(T[] references, string fieldName) where T : Component
        {
            var unique = new HashSet<T>();
            foreach (var reference in references)
            {
                SceneComponentLookup.RequireSameScene(this, reference, fieldName);
                if (!unique.Add(reference)) throw new InvalidOperationException(fieldName + ": 중복 연결입니다.");
            }
        }
        private Vector3[] ResolvePoints(Transform[] references, Vector3[] legacy, string fieldName)
        {
            if (references == null || references.Length == 0) return legacy == null ? Array.Empty<Vector3>() : (Vector3[])legacy.Clone();
            if (references.Length < 2) throw new InvalidOperationException(fieldName + ": 두 지점 이상 연결하세요.");
            var points = new Vector3[references.Length];
            var unique = new HashSet<Transform>();
            for (int i = 0; i < points.Length; i++)
            {
                SceneComponentLookup.RequireFixedPoint(this, references[i], fieldName);
                if (!unique.Add(references[i]) || !CameraZoneRequest.IsFinite(references[i].position))
                    throw new InvalidOperationException(fieldName + ": 중복 또는 유효하지 않은 지점입니다.");
                points[i] = references[i].position;
            }
            return points;
        }
        public bool TryGetCheckpointPose(string id, out Pose pose)
        { if (poses.Count == 0) RebuildRegistry(); return poses.TryGetValue(id ?? string.Empty, out pose); }
        public bool CanDispatch(PlayerFacade candidate) => isActiveAndEnabled && bound && candidate != null && candidate.isActiveAndEnabled && candidate == actor && !actor.IsDead &&
            flow != null && flow.Snapshot.State == FlowState.Playing && !flow.Snapshot.IsBusy && control != null && !control.State.WorldPaused;
        public bool TryDispatch(StageTrigger trigger, PlayerFacade candidate)
        {
            if (trigger == null || trigger.Owner != this || trigger.gameObject.scene != gameObject.scene || candidate != actor) return false;
            // Keep approach history while a current contact is temporarily blocked; no start occurs here.
            if (trigger.Kind == StageTriggerKind.Chase) ObserveChaseApproach();
            if (!CanDispatch(candidate)) return false;
            switch (trigger.Kind)
            {
                case StageTriggerKind.Enter: eventBus.Publish(new StageEnteredEvent(StageId, trigger.Value)); return true;
                case StageTriggerKind.Checkpoint:
                    var marker = trigger.Checkpoint != null ? trigger.Checkpoint : GetCheckpoint(trigger.Value);
                    if (marker == null || marker.gameObject.scene != gameObject.scene || Array.IndexOf(sceneCheckpoints, marker) < 0) return false;
                    return captureMarker != null ? captureMarker(marker, candidate) : captureCheckpoint != null && captureCheckpoint(marker.CheckpointId, candidate);
                case StageTriggerKind.UnlockForm:
                case StageTriggerKind.UnlockTear:
                    if (!TryResolveDialogue(trigger, true, out var notice)) return false;
                    return unlockAbility != null && unlockAbility(trigger.Ability, notice);
                case StageTriggerKind.Dialogue:
                    return TryResolveDialogue(trigger, false, out var code) && tryStartSafeDialogue != null && tryStartSafeDialogue(code);
                case StageTriggerKind.Fall: return TryDispatchFall(trigger, candidate, null);
                case StageTriggerKind.Chase: return TryStartChaseAtBodyPosition();
                case StageTriggerKind.Escape:
                    if (finalPending || finalStarted || (resolvedChaseWaypoints.Length > 1 && !chaseAccepted) ||
                        !TryResolveDialogue(trigger, false, out var exitCode)) return false;
                    finalActor = candidate; finalCode = exitCode;
                    finalPending = true; return true; // Resolve after all physics contacts and chase capture for this frame.
                default: return false;
            }
        }
        private bool TryResolveDialogue(StageTrigger trigger, bool optional, out string code)
        {
            code = null;
            if (trigger.Dialogue != null && trigger.Dialogue.Table != null)
            {
                if (dialogueTable == null) return false;
                code = dialogueTable.RequireSelection(trigger.Dialogue);
                return true;
            }
            code = trigger.Value; // Hidden pre-T11 compatibility only; no new manual key authoring.
            return optional || !string.IsNullOrWhiteSpace(code);
        }
        public bool TryDispatchFall(StageTrigger trigger, PlayerFacade candidate, PlayerDebris shell)
        {
            if (trigger == null || trigger.Kind != StageTriggerKind.Fall || trigger.Owner != this ||
                trigger.gameObject.scene != gameObject.scene || !CanDispatch(candidate)) return false;
            if (shell != null)
            {
                var form = candidate.GetComponent<PlayerFormController>();
                if (form == null || form.OwnedDebris != shell || shell.Owner != form) return false;
                return fallRequest != null && fallRequest(candidate, shell);
            }
            return fallRequest != null ? fallRequest(candidate, null) : bridge != null && bridge.ForceDeath(Vector3.zero, skipPresentation: true) > 0;
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
                    if (ProgressAt(corner) < resolvedChaseStartProgress) return false;
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
                if (!tryStartChase(this) || revision != bindingRevision || !CanDispatch(actor)) return false;
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
            if ((outcome != DialogueOutcome.Completed && outcome != DialogueOutcome.Skipped) || finalActor == null || finalActor != actor || !CanDispatch(finalActor)) return;
            finalActor = null;
            if (!flow.Request(new FlowRequest(FlowCommand.Escape))) throw new InvalidOperationException("Final dialogue completed but Ending was rejected.");
        }
        /// <summary>Explicit initial arrangement, no saved world DTO. Call with roots inactive and Actor released.</summary>
        public void ResetForRetry()
        {
            Dispose(); RebuildRegistry();
            foreach (var motion in FindComponents<KinematicPlatformMotion>()) motion.ResetForRetry();
            foreach (var contacts in FindComponents<PlatformContacts>()) contacts.ResetForRetry();
            foreach (var mover in FindComponents<LinearShuttlePlatform>()) mover.ResetForRetry();
            foreach (var floating in FindComponents<FloatingPlatform>()) floating.ResetForRetry();
            foreach (var periodic in FindComponents<PeriodicPlatform>()) periodic.ResetForRetry();
            foreach (var pressure in FindComponents<PressurePlatePlatform>()) pressure.ResetForRetry();
            foreach (var linked in FindComponents<PressureLinkedPlatform>()) linked.ResetForRetry();
            foreach (var floor in FindComponents<DamageFloorPlatform>()) floor.ResetForRetry();
            foreach (var collapsed in FindComponents<ShatteredPlatform>()) collapsed.ResetForRetry();
            foreach (var device in FindComponents<OvenTrayObject>()) device.ResetForRetry();
            // All mover/support poses are restored before any enemy queries their terrain.
            Physics.SyncTransforms();
            foreach (var enemy in FindComponents<EnemyActor>()) enemy.ResetForRetry();
            foreach (var pickup in FindComponents<TearFragment>()) pickup.ResetForRetry();
            foreach (var trigger in FindComponents<StageTrigger>()) trigger.ResetForRetry();
            foreach (var zone in FindComponents<CameraZoneTrigger>()) zone.ResetForRetry();
            if (chaseRetreatGate != null) chaseRetreatGate.ResetForRetry();
            chaseAccepted = false; chaseStartInProgress = false;
            IsInitialPreparationComplete = true;
        }
        public void Dispose()
        {
            bound = false; bindingRevision++;
            chaseApproachObserved = chaseAccepted = chaseStartInProgress = false;
            if (dialogue != null) dialogue.Ended -= OnFinalDialogueEnded;
            foreach (var enemy in sceneEnemies) if (enemy != null) enemy.BindTarget(null);
            actor = null; bridge = null; control = null; dialogue = null; dialogueTable = null; eventBus = null; flow = null;
            tryStartSafeDialogue = null; tryStartChase = null; captureCheckpoint = null; captureMarker = null; unlockAbility = null; fallRequest = null; stopChase = null;
            ChaseStarted = null; finalPending = finalStarted = false; finalActor = null; finalCode = null; finalDialogueSession = 0;
        }
        private void OnDisable() => Dispose();
        private void OnDestroy() => Dispose();
    }
}
