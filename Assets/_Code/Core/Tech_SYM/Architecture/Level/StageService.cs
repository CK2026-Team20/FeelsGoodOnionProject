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
    public sealed class StageService : MonoBehaviour, IDisposable
    {
        [SerializeField, InspectorLabel("초기 체크포인트"), Tooltip("새 진입 위치를 정할 같은 씬의 CheckpointMarker를 연결하세요. 완료 저장과는 별개입니다.")]
        private CheckpointMarker initialCheckpoint;
        [SerializeField, InspectorLabel("카메라 경계"), Tooltip("최종 카메라 위치를 제한할 같은 씬의 활성 BoxCollider입니다. 플레이어를 막지 않도록 Trigger로 작성하세요.")]
        private BoxCollider cameraBounds;
        public BoxCollider CameraBounds
        {
            get
            {
                SceneComponentLookup.RequireSameScene(this, cameraBounds, "카메라 경계");
                if (cameraBounds == null || !cameraBounds.enabled || !cameraBounds.gameObject.activeInHierarchy || !cameraBounds.isTrigger)
                    throw new InvalidOperationException("카메라 경계에는 활성 Trigger BoxCollider가 필요합니다.");
                return cameraBounds;
            }
        }
        public event Action Releasing;
        [SerializeField, InspectorLabel("스테이지 음악"), Tooltip("이 씬에서 재생할 음악 용도를 선택하세요. 기존 AudioCueCatalog의 AudioClip을 사용합니다.")]
        private AudioCuePurpose stageMusicPurpose = AudioCuePurpose.Stage1Music;
        private CheckpointMarker[] sceneCheckpoints = Array.Empty<CheckpointMarker>();
        private bool hasChase;
        private IGameFlowService flow;
        private PlayerFacade actor;
        private PlayerBridgeService bridge;
        private IGameplayControlService control;
        private DialogueService dialogue;
        private bool bound, chaseAccepted, finalPending, finalStarted, endingPending;
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
        // Component lifetime only: retained scenes warm once, including across Title/new rounds.
        public bool IsInitialPreparationComplete { get; private set; }
        public bool IsBound => bound;
        public PlayerFacade Actor => actor;
        public bool IsChaseAccepted => chaseAccepted;
        public event Action<StageService> ChaseStarted;
        public T[] FindComponents<T>() where T : Component => SceneComponentLookup.FindAll<T>(gameObject.scene);
        public void Initialize(IGameFlowService flowService, PlayerFacade player, Func<string, bool> safeDialogueStart)
        {
            if (flowService == null || player == null || safeDialogueStart == null) throw new ArgumentNullException("Stage requires Flow, current Actor and dialogue command.");
            SceneComponentLookup.RequireSameScene(this, this, "스테이지");
            Dispose(); RebuildRegistry(); flow = flowService; actor = player; tryStartSafeDialogue = safeDialogueStart;
            bound = true; bindingRevision++;
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
        internal void NotifyChaseStarted(StageTrigger source, PlayerFacade current)
        {
            if (source == null || source.Owner != this || source.Kind != StageTriggerKind.Chase || !CanDispatch(current))
                throw new InvalidOperationException("현재 스테이지의 추격 시작만 통지할 수 있습니다.");
            chaseAccepted = true;
            ChaseStarted?.Invoke(this);
        }
        public void RebuildRegistry()
        {
            SceneComponentLookup.RequireSingle<StageService>(gameObject.scene);
            _ = StageMusicPurpose;
            sceneCheckpoints = FindComponents<CheckpointMarker>();
            sceneSensors = FindComponents<EnemyContactSensor>(); sceneFloors = FindComponents<DamageFloorPlatform>();
            hasChase = Array.Exists(FindComponents<StageTrigger>(), trigger => trigger.Owner == this && trigger.Kind == StageTriggerKind.Chase);
            foreach (var point in sceneCheckpoints) _ = point.Pose;
            SceneComponentLookup.RequireSameScene(this, InitialCheckpoint, "초기 체크포인트");
            if (!ContainsCheckpoint(InitialCheckpoint)) throw new InvalidOperationException("Initial Marker is outside this scene: " + StageId);
        }
        public bool CanDispatch(PlayerFacade candidate) => isActiveAndEnabled && bound && candidate != null && candidate.isActiveAndEnabled && candidate == actor && !actor.IsDead &&
            flow != null && flow.Snapshot.State == FlowState.Playing && !flow.Snapshot.IsBusy && control != null && !control.State.WorldPaused;
        public bool TryDispatch(StageTrigger trigger, PlayerFacade candidate)
        {
            if (trigger == null || trigger.Owner != this || trigger.gameObject.scene != gameObject.scene || candidate != actor) return false;
            if (!CanDispatch(candidate)) return false;
            switch (trigger.Kind)
            {
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
                case StageTriggerKind.Escape:
                    if (finalPending || finalStarted || endingPending || (hasChase && !chaseAccepted) ||
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
            chaseAccepted = false;
            IsInitialPreparationComplete = true;
        }
        public void Dispose()
        {
            bound = false; bindingRevision++;
            chaseAccepted = false;
            var errors = new List<Exception>();
            SessionCleanup.Notify(errors, Releasing);
            SessionCleanup.Attempt(errors, () => ClearFinalDialogue(true));
            foreach (var sensor in sceneSensors) if (sensor != null) SessionCleanup.Attempt(errors, sensor.UnbindPlayer);
            foreach (var floor in sceneFloors) if (floor != null) SessionCleanup.Attempt(errors, floor.UnbindPlayer);
            actor = null; bridge = null; control = null; dialogue = null; dialogueTable = null; flow = null;
            boundRound = null; isCurrentStage = null;
            tryStartSafeDialogue = null; tryStartFinalDialogue = null; captureMarker = null; unlockAbility = null; fallRequest = null; stopChase = null;
            ChaseStarted = null;
            SessionCleanup.ThrowIfAny(errors, "Stage connection release failed.");
        }
        private void OnDisable() => Dispose();
        private void OnDestroy() => Dispose();
    }
}
