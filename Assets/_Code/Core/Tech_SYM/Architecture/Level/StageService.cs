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
        private Vector3[] resolvedChaseWaypoints = Array.Empty<Vector3>();
        private CheckpointMarker[] sceneCheckpoints = Array.Empty<CheckpointMarker>();
        private EnemyActor[] sceneEnemies = Array.Empty<EnemyActor>();
        private float resolvedChaseStartProgress;
        private Vector3 progressOrigin, progressDirection;
        private Cooked.Chase.ChasePath progressPath;
        private IGameFlowService flow;
        private PlayerFacade actor;
        private PlayerBridgeService bridge;
        private IGameplayControlService control;
        private DialogueService dialogue;
        private bool bound, chaseAccepted, chaseStartInProgress, finalPending, finalStarted, endingPending;
        private bool chaseApproachObserved;
        private long bindingRevision, finalDialogueSession, finalActorGeneration;
        private EnemyContactSensor[] sceneSensors = Array.Empty<EnemyContactSensor>();
        private DamageFloorPlatform[] sceneFloors = Array.Empty<DamageFloorPlatform>();
        private PlayerFacade finalActor;
        private StageTrigger finalTrigger;
        private string finalCode;
        private DialogueSelection finalSelection;
        private TextAsset finalTable;
        private IDialogueTableService finalTableService;
        private DialogueService finalConversation;
        private GameSessionService boundRound, finalRound;
        private long finalRoundId, finalBindingRevision;
        private Func<bool> isCurrentStage;
        private Func<string, bool> tryStartSafeDialogue;
        private Func<StageTrigger, PlayerFacade, long, string, bool> tryStartFinalDialogue;
        private Func<StageService, bool> tryStartChase;
        private Func<StageService, bool> canStartChase, captureBeforeChase;
        private Func<CheckpointMarker, PlayerFacade, bool> captureMarker;
        private Func<AbilityId, string, bool> unlockAbility;
        private Func<PlayerFacade, PlayerDebris, bool> fallRequest;
        private IDialogueTableService dialogueTable;
        private Action stopChase;
        public string StageId => string.IsNullOrEmpty(gameObject.scene.path) ? gameObject.scene.name : SceneCatalog.StageIdFromPath(gameObject.scene.path);
        public CheckpointMarker InitialCheckpoint => initialCheckpoint != null ? initialCheckpoint : throw new InvalidOperationException("같은 씬의 초기 CheckpointMarker를 연결하세요.");
        public Pose InitialPose => InitialCheckpoint.Pose;
        public bool ContainsCheckpoint(CheckpointMarker marker)
        {
            if (marker == null || marker.gameObject.scene != gameObject.scene) return false;
            // LoadedStageRegistry queries before the first Reset/Bind. Prepare through this Stage's existing boundary.
            if (sceneCheckpoints.Length == 0) RebuildRegistry();
            return Array.IndexOf(sceneCheckpoints, marker) >= 0;
        }
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
        public event Action<StageService> StageEntered;
        public float ProgressAt(Vector3 position)
        {
            if (!CameraZoneRequest.IsFinite(position)) throw new ArgumentException("진행 위치는 유한해야 합니다.", nameof(position));
            if (progressPath != null) return progressPath.Project(position);
            return chaseStartPoint != null ? Vector3.Dot(position - progressOrigin, progressDirection) : transform.InverseTransformPoint(position).x;
        }
        public T[] FindComponents<T>() where T : Component => SceneComponentLookup.FindAll<T>(gameObject.scene);
        public void Initialize(IGameFlowService flowService, PlayerFacade player, Func<string, bool> safeDialogueStart)
        {
            if (flowService == null || player == null || safeDialogueStart == null) throw new ArgumentNullException("Stage requires Flow, current Actor and dialogue command.");
            SceneComponentLookup.RequireSameScene(this, this, "스테이지");
            Dispose(); RebuildRegistry(); flow = flowService; actor = player; tryStartSafeDialogue = safeDialogueStart;
            bound = true; bindingRevision++;
            ObserveChaseApproach();
        }
        public void BindRequests(PlayerBridgeService player, IGameplayControlService gameplayControl, DialogueService conversation,
            Func<AbilityId, string, bool> abilityUnlock, Action haltChase)
        {
            if (!bound) throw new InvalidOperationException("Initialize Stage before binding its request owners.");
            ClearFinalDialogue(true);
            bridge = player ?? throw new ArgumentNullException(nameof(player)); control = gameplayControl ?? throw new ArgumentNullException(nameof(gameplayControl));
            dialogue = conversation ?? throw new ArgumentNullException(nameof(conversation));
            unlockAbility = abilityUnlock ?? throw new ArgumentNullException(nameof(abilityUnlock)); stopChase = haltChase ?? throw new ArgumentNullException(nameof(haltChase));
            if (!bridge.IsCurrentActor(actor, bridge.ActorGeneration)) throw new InvalidOperationException("Stage must bind the current session Actor.");
            foreach (var sensor in sceneSensors) sensor.BindPlayer(bridge);
            foreach (var floor in sceneFloors) floor.BindPlayer(bridge);
        }
        public void BindChaseStart(Func<StageService, bool> ready, Func<StageService, bool> capture, Func<StageService, bool> start)
        {
            canStartChase = ready ?? throw new ArgumentNullException(nameof(ready));
            captureBeforeChase = capture ?? throw new ArgumentNullException(nameof(capture));
            tryStartChase = start ?? throw new ArgumentNullException(nameof(start));
        }
        public void BindCheckpointCapture(Func<CheckpointMarker, PlayerFacade, bool> capture)
            => captureMarker = capture ?? throw new ArgumentNullException(nameof(capture));
        public void BindDialogueTable(IDialogueTableService table)
            => dialogueTable = table ?? throw new ArgumentNullException(nameof(table));
        public void BindFinalDialogueStart(Func<StageTrigger, PlayerFacade, long, string, bool> start,
            GameSessionService session, Func<bool> currentStage)
        {
            ClearFinalDialogue(true);
            tryStartFinalDialogue = start ?? throw new ArgumentNullException(nameof(start));
            boundRound = session ?? throw new ArgumentNullException(nameof(session));
            isCurrentStage = currentStage ?? throw new ArgumentNullException(nameof(currentStage));
        }
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
            sceneSensors = FindComponents<EnemyContactSensor>(); sceneFloors = FindComponents<DamageFloorPlatform>();
            var resolvedProgress = ResolvePoints(progressPathPoints, "진행 경로");
            progressPath = resolvedProgress.Length > 1 ? new Cooked.Chase.ChasePath(resolvedProgress) : null;
            resolvedChaseWaypoints = ResolvePoints(chasePathPoints, "추격 경로");
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
            else
            {
                if (resolvedChaseWaypoints.Length > 1) throw new InvalidOperationException("추격 경로가 있으면 실제 추격 시작 지점을 연결하세요.");
                resolvedChaseStartProgress = 0;
            }
            if (!float.IsFinite(resolvedChaseStartProgress)) throw new InvalidOperationException("추격 시작선은 유한해야 합니다.");
            foreach (var point in sceneCheckpoints) _ = point.Pose;
            SceneComponentLookup.RequireSameScene(this, InitialCheckpoint, "초기 체크포인트");
            if (!ContainsCheckpoint(InitialCheckpoint)) throw new InvalidOperationException("Initial Marker is outside this scene: " + StageId);
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
        private Vector3[] ResolvePoints(Transform[] references, string fieldName)
        {
            if (references == null || references.Length == 0) return Array.Empty<Vector3>();
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
                case StageTriggerKind.Enter: StageEntered?.Invoke(this); return true;
                case StageTriggerKind.Checkpoint:
                    var marker = trigger.Checkpoint;
                    if (marker == null || marker.gameObject.scene != gameObject.scene || Array.IndexOf(sceneCheckpoints, marker) < 0) return false;
                    return captureMarker != null && captureMarker(marker, candidate);
                case StageTriggerKind.UnlockForm:
                case StageTriggerKind.UnlockTear:
                    if (!TryResolveDialogue(trigger, true, out var notice)) return false;
                    return unlockAbility != null && unlockAbility(trigger.Ability, notice);
                case StageTriggerKind.Dialogue:
                    return TryResolveDialogue(trigger, false, out var code) && tryStartSafeDialogue != null && tryStartSafeDialogue(code);
                case StageTriggerKind.Fall: return TryDispatchFall(trigger, candidate, null);
                case StageTriggerKind.Chase: return TryStartChaseAtBodyPosition();
                case StageTriggerKind.Escape:
                    if (finalPending || finalStarted || endingPending || (resolvedChaseWaypoints.Length > 1 && !chaseAccepted) ||
                        bridge == null || !bridge.IsCurrentActor(candidate, bridge.ActorGeneration) || tryStartFinalDialogue == null ||
                        boundRound == null || !boundRound.IsActive || isCurrentStage == null || !isCurrentStage() ||
                        !TryResolveDialogue(trigger, false, out var exitCode)) return false;
                    finalActor = candidate; finalActorGeneration = bridge.ActorGeneration; finalTrigger = trigger; finalCode = exitCode;
                    finalSelection = trigger.Dialogue; finalTable = finalSelection.Table; finalTableService = dialogueTable;
                    finalConversation = dialogue; finalRound = boundRound; finalRoundId = boundRound.RoundId; finalBindingRevision = bindingRevision;
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
            return optional;
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
            return fallRequest != null && fallRequest(candidate, null);
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
            if (chaseStartInProgress || !chaseApproachObserved || !WholeBodyPastStart(out _) || tryStartChase == null || chaseRetreatGate == null || !chaseRetreatGate.CanClose(actor) || canStartChase == null || captureBeforeChase == null) return false;
            chaseStartInProgress = true; long revision = bindingRevision;
            try
            {
                if (!CommitChaseStart(
                    () => revision == bindingRevision && CanDispatch(actor) && canStartChase(this),
                    () => captureBeforeChase(this),
                    () => revision == bindingRevision && CanDispatch(actor) && chaseRetreatGate.CanClose(actor) && tryStartChase(this))) return false;
                if (revision != bindingRevision || !CanDispatch(actor)) return false;
                chaseAccepted = true;
                if (!TryCloseChaseRetreatGate()) throw new InvalidOperationException("Accepted chase gate cannot close without moving Actor.");
                ChaseStarted?.Invoke(this); return true;
            }
            finally { chaseStartInProgress = false; }
        }
        /// <summary>Live acceptance uses this order; a refused save cannot start Chase or close the gate.</summary>
        public static bool CommitChaseStart(Func<bool> ready, Func<bool> save, Func<bool> start)
        {
            if (ready == null || save == null || start == null) throw new ArgumentNullException("Chase acceptance callbacks are required.");
            return ready() && save() && start();
        }
        private void LateUpdate()
        {
            if (endingPending)
            {
                if (!IsFinalContextCurrent(true) ||
                    (dialogue.Outcome != DialogueOutcome.Completed && dialogue.Outcome != DialogueOutcome.Skipped) ||
                    dialogue.IsActive) { ClearFinalDialogue(true); return; }
                if (control.State.WorldPaused || control.State.PresentationPaused) return;
                var endingFlow = flow;
                ClearFinalDialogue(false);
                if (!endingFlow.Request(new FlowRequest(FlowCommand.Escape))) throw new InvalidOperationException("Final dialogue completed but Ending was rejected.");
                return;
            }
            if (finalStarted)
            {
                if (!IsFinalContextCurrent(true)) ClearFinalDialogue(true);
                return;
            }
            if (!finalPending) return;
            if (!IsFinalContextCurrent(false)) { ClearFinalDialogue(true); return; }
            if (!CanDispatch(finalActor)) return;
            stopChase(); // ChaseRuntimeDriver runs earlier; capture/death has priority over arrival.
            if (!IsFinalContextCurrent(false)) { ClearFinalDialogue(true); return; }
            if (dialogue.IsActive) return;
            try
            {
                // 시작 통지 중 완료/Skip도 예상 회차에만 연결한다. 반환 뒤의 현재 ID를 채택하지 않는다.
                finalDialogueSession = DialogueService.NextSessionId(finalConversation.SessionId);
                finalPending = false; finalStarted = true;
                finalConversation.Ended += OnFinalDialogueEnded;
                if (!tryStartFinalDialogue(finalTrigger, finalActor, finalActorGeneration, finalCode) || !IsFinalContextCurrent(true))
                { ClearFinalDialogue(true); return; }
                if (finalStarted && !finalConversation.IsActive) OnFinalDialogueEnded(finalConversation.Outcome);
            }
            catch (Exception error) { SessionCleanup.RethrowAfterCleanup(error, () => ClearFinalDialogue(true)); }
        }
        /// <summary>같은 종료 접촉의 선택 자산·Code·Actor·판만 준비 및 완료 경계를 통과한다.</summary>
        private bool IsFinalContextCurrent(bool published)
        {
            if (!bound || !isActiveAndEnabled || finalBindingRevision != bindingRevision || isCurrentStage == null || !isCurrentStage() ||
                finalRound == null || !ReferenceEquals(finalRound, boundRound) || !finalRound.IsActive || finalRound.RoundId != finalRoundId ||
                finalActor == null || finalActor != actor || !finalActor.isActiveAndEnabled || finalActor.IsDead || bridge == null ||
                !bridge.IsCurrentActor(finalActor, finalActorGeneration) || finalTrigger == null || !finalTrigger.isActiveAndEnabled ||
                finalTrigger.Kind != StageTriggerKind.Escape || finalTrigger.Owner != this || finalTrigger.gameObject.scene != gameObject.scene ||
                finalSelection == null || !ReferenceEquals(finalTrigger.Dialogue, finalSelection) || finalTable == null ||
                !ReferenceEquals(finalSelection.Table, finalTable) || !string.Equals(finalSelection.Code, finalCode, StringComparison.Ordinal) ||
                finalConversation == null || !ReferenceEquals(dialogue, finalConversation) || !ReferenceEquals(dialogueTable, finalTableService) ||
                flow == null || flow.Snapshot.State != FlowState.Playing || flow.Snapshot.IsBusy ||
                (published && finalConversation.SessionId != finalDialogueSession)) return false;
            try { return string.Equals(finalTableService.RequireSelection(finalSelection), finalCode, StringComparison.Ordinal); }
            catch (FormatException) { return false; } // 현재 작성 선택이 바뀌어 유효하지 않으면 이 예약만 거절한다.
        }
        internal bool MatchesFinalDialogueRequest(GameSessionService session, DialogueService conversation, IDialogueTableService table,
            StageTrigger trigger, PlayerFacade source, long generation, string code)
            => finalStarted && IsFinalContextCurrent(false) && ReferenceEquals(session, finalRound) &&
                ReferenceEquals(conversation, finalConversation) && ReferenceEquals(table, finalTableService) && trigger == finalTrigger && source == finalActor &&
                generation == finalActorGeneration && string.Equals(code, finalCode, StringComparison.Ordinal) &&
                DialogueService.NextSessionId(conversation.SessionId) == finalDialogueSession;
        private void OnFinalDialogueEnded(DialogueOutcome outcome)
        {
            if (!finalStarted) return;
            if (!IsFinalContextCurrent(true) || (outcome != DialogueOutcome.Completed && outcome != DialogueOutcome.Skipped))
            { ClearFinalDialogue(true); return; }
            finalConversation.Ended -= OnFinalDialogueEnded;
            finalStarted = false;
            // Reserve only: ChaseRuntimeDriver.LateUpdate(200) must run before Stage.LateUpdate(500).
            endingPending = true;
        }
        private void ClearFinalDialogue(bool cancelCurrent)
        {
            var conversation = finalConversation;
            long sessionId = finalDialogueSession;
            if (conversation != null) conversation.Ended -= OnFinalDialogueEnded;
            finalPending = finalStarted = endingPending = false;
            finalActor = null; finalTrigger = null; finalCode = null; finalSelection = null; finalTable = null;
            finalTableService = null; finalConversation = null; finalRound = null;
            finalDialogueSession = finalActorGeneration = finalRoundId = finalBindingRevision = 0;
            // 다른 시작이 이미 게시한 새 회차를 취소하지 않는다. 장부는 DialogueService 소유다.
            if (cancelCurrent && conversation != null && conversation.IsActive && conversation.SessionId == sessionId) conversation.Cancel();
        }
        /// <summary>Warm authored activeSelf once before hiding Stage roots; no Actor is attached yet.</summary>
        public void PrepareInitialSources()
        {
            if (IsInitialPreparationComplete) return;
            foreach (var pickup in FindComponents<TearFragment>()) pickup.PrepareSource();
        }
        /// <summary>Restore initial arrangement with roots hidden and Actor released; no saved world DTO.</summary>
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
            var errors = new List<Exception>();
            SessionCleanup.Attempt(errors, () => ClearFinalDialogue(true));
            foreach (var sensor in sceneSensors) if (sensor != null) SessionCleanup.Attempt(errors, sensor.UnbindPlayer);
            foreach (var floor in sceneFloors) if (floor != null) SessionCleanup.Attempt(errors, floor.UnbindPlayer);
            actor = null; bridge = null; control = null; dialogue = null; dialogueTable = null; flow = null;
            boundRound = null; isCurrentStage = null;
            tryStartSafeDialogue = null; tryStartFinalDialogue = null; tryStartChase = null; canStartChase = null; captureBeforeChase = null; captureMarker = null; unlockAbility = null; fallRequest = null; stopChase = null;
            ChaseStarted = null; StageEntered = null;
            SessionCleanup.ThrowIfAny(errors, "Stage connection release failed.");
        }
        private void OnDisable() => Dispose();
        private void OnDestroy() => Dispose();
    }
}
