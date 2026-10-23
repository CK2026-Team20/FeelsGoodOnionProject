using System;
using System.Collections.Generic;
using Cooked.Authoring;
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

        [Serializable]
        public sealed class Section
        {
            [SerializeField, InspectorLabel("구간 이름"), Tooltip("시작과 끝 연결선에 표시할 편집용 이름입니다. 저장 키로 사용하지 않습니다.")]
            private string displayName;
            [SerializeField, InspectorLabel("시작"), Tooltip("이 구간을 시작할 같은 씬의 체크포인트를 연결하세요.")]
            private StageTrigger start;
            [SerializeField, InspectorLabel("끝"), Tooltip("저장만 수행할 같은 씬의 서로 다른 체크포인트를 연결하세요.")]
            private StageTrigger end;
            [SerializeField, InspectorLabel("시점"), Tooltip("측면은 X, 통로와 쿼터는 XZ 이동을 사용합니다.")]
            private CameraMovementMode view = CameraMovementMode.Side;
            [SerializeField, InspectorLabel("음악"), Tooltip("구간 시작과 복귀에서 사용할 실제 음원을 연결하세요.")]
            private AudioClip music;
            [SerializeField, InspectorLabel("음량"), Tooltip("사용자 음악 음량에 곱할 기본 음량입니다."), Range(0,1)]
            private float musicGain = .6f;
            [SerializeField, InspectorLabel("음악 전환 시간"), Tooltip("음악 교차 전환 시간입니다(초). 0은 즉시 변경합니다."), Min(0)]
            private float musicTransitionSeconds = .3f;
            [SerializeField, InspectorLabel("진입 보정 시간"), Tooltip("Capsule 중심을 시작 위치에 맞추는 시간입니다(초)."), Min(.01f)]
            private float correctionSeconds = .6f;
            [SerializeField, InspectorLabel("시점 전환 시간"), Tooltip("다른 시점으로 전환할 시간입니다(초)."), Min(.01f)]
            private float transitionSeconds = .6f;
            [SerializeField, InspectorLabel("차단 몸체"), Tooltip("진입 보정 후 이동할 선택 Kinematic 차단 몸체입니다. 추격 퇴로와 공유하지 마세요.")]
            private Rigidbody blockerBody;
            [SerializeField, InspectorLabel("차단 목표"), Tooltip("차단 몸체 밖의 같은 씬 고정 목표를 연결하세요.")]
            private Transform blockerTarget;
            [SerializeField, InspectorLabel("차단 이동 시간"), Tooltip("플레이어 보정 후 차단체 이동 시간입니다(초)."), Min(.01f)]
            private float blockerSeconds = .6f;
            public string DisplayName => displayName;
            public StageTrigger Start => start;
            public StageTrigger End => end;
            public CameraMovementMode View => view;
            public AudioClip Music => music;
            public float MusicGain => musicGain;
            public float MusicTransitionSeconds => musicTransitionSeconds;
            public float CorrectionSeconds => correctionSeconds;
            public float TransitionSeconds => transitionSeconds;
            public Rigidbody BlockerBody => blockerBody;
            public Vector3 BlockerGoal => blockerTarget.position;
            public float BlockerSeconds => blockerSeconds;
            private Pose blockerInitial;
            private bool hasBlockerInitial;
            internal void Validate(StageService owner)
            {
                if (start == null || end == null || start == end || music == null)
                    throw new InvalidOperationException("구간 '" + displayName + "'의 서로 다른 시작·끝과 음악을 연결하세요.");
                foreach (var point in new[]{start,end})
                {
                    if (point.Owner != owner || point.Kind != StageTriggerKind.Checkpoint)
                        throw new InvalidOperationException("구간 '" + displayName + "'의 체크포인트 소속/종류가 잘못되었습니다.");
                    SceneComponentLookup.RequireSameScene(owner, point, "구간 체크포인트");
                    _ = point.CheckpointPose;
                }
                CameraProfileValidation.Validate(view, start.transform.position);
                if (!float.IsFinite(musicGain) || musicGain < 0 || musicGain > 1 ||
                    !float.IsFinite(musicTransitionSeconds) || musicTransitionSeconds < 0)
                    throw new InvalidOperationException("구간 음악 음량/전환 시간을 확인하세요: " + displayName);
                foreach (float seconds in new[]{correctionSeconds,transitionSeconds,blockerSeconds})
                    if (!float.IsFinite(seconds) || seconds < .01f) throw new InvalidOperationException("구간 이동 시간은 0.01초 이상의 유한 값이어야 합니다.");
                if ((blockerBody == null) != (blockerTarget == null)) throw new InvalidOperationException("차단 몸체와 목표를 함께 연결하세요.");
                if (blockerBody == null) return;
                SceneComponentLookup.RequireSameScene(owner, blockerBody, "차단 몸체");
                SceneComponentLookup.RequireFixedPoint(owner, blockerTarget, "차단 목표");
                if (!blockerBody.isKinematic || blockerTarget.IsChildOf(blockerBody.transform) ||
                    start.transform.IsChildOf(blockerBody.transform) || end.transform.IsChildOf(blockerBody.transform) ||
                    !blockerBody.TryGetComponent<BoxCollider>(out var box) || box.isTrigger || !box.enabled ||
                    blockerBody.GetComponentsInChildren<Collider>().Length != 1)
                    throw new InvalidOperationException("고정 체크포인트·목표와 단일 BoxCollider Kinematic 차단체가 필요합니다.");
                if (!hasBlockerInitial) { blockerInitial = new Pose(blockerBody.position, blockerBody.rotation); hasBlockerInitial = true; }
            }
            internal void ResetBlocker()
            {
                if (hasBlockerInitial && blockerBody != null)
                { blockerBody.position = blockerInitial.position; blockerBody.rotation = blockerInitial.rotation; }
            }
            internal bool IsBlockerPathSafe(PlayerFacade candidate, Vector3 playerGoal)
            {
                if (blockerBody == null) return true;
                var box = blockerBody.GetComponent<BoxCollider>();
                var capsule = candidate.GetComponent<CapsuleCollider>();
                if (box == null || capsule == null || !capsule.enabled || !blockerBody.gameObject.activeInHierarchy) return false;
                Vector3 delta = BlockerGoal - blockerBody.position;
                Vector3 center = box.transform.TransformPoint(box.center);
                Vector3 scale = box.transform.lossyScale;
                Vector3 half = Vector3.Scale(box.size*.5f, new Vector3(Mathf.Abs(scale.x),Mathf.Abs(scale.y),Mathf.Abs(scale.z)));
                bool Obstacle(Collider other) => other != null && other.attachedRigidbody != blockerBody &&
                    other.attachedRigidbody != candidate.GetComponent<Rigidbody>() && !other.isTrigger &&
                    !Physics.GetIgnoreLayerCollision(box.gameObject.layer,other.gameObject.layer) && !Physics.GetIgnoreCollision(box,other);
                foreach (var other in Physics.OverlapBox(center+delta, half, box.transform.rotation, ~0, QueryTriggerInteraction.Ignore))
                    if (Obstacle(other) && Physics.ComputePenetration(box,blockerBody.position+delta,blockerBody.rotation,
                        other,other.transform.position,other.transform.rotation,out _,out float depth) && depth>.002f) return false;
                if (delta.sqrMagnitude>.000004f)
                    foreach (var hit in Physics.BoxCastAll(center,Vector3.Max(half-Vector3.one*.002f,Vector3.one*.0001f),delta.normalized,
                        box.transform.rotation,delta.magnitude,~0,QueryTriggerInteraction.Ignore))
                        if (Obstacle(hit.collider)) return false;
                var swept=box.bounds; var endBounds=box.bounds; endBounds.center+=delta; swept.Encapsulate(endBounds);
                PlayerBridgeService.CapsuleGeometry(capsule,out var a,out var b,out float radius);
                Vector3 offset=playerGoal-candidate.GetComponent<Rigidbody>().position;
                var projected=new Bounds(a+offset,Vector3.one*radius*2);projected.Encapsulate(new Bounds(b+offset,Vector3.one*radius*2));
                return !swept.Intersects(projected);
            }
        }
        [SerializeField, InspectorLabel("구간"), Tooltip("각 시작·끝 체크포인트와 시점·음악을 작성하세요. 같은 체크포인트를 중복 배정하지 마세요.")]
        private Section[] sections = Array.Empty<Section>();
        [SerializeField, InspectorLabel("첫 구간 시작"), Tooltip("첫 구간의 시작 체크포인트를 명시적으로 연결하세요. 목록 순번은 저장 키가 아닙니다.")]
        private StageTrigger firstSectionStart;
        [SerializeField, InspectorLabel("측면 구도"), Tooltip("모든 측면 구간이 공유하는 구도입니다.")]
        private LevelCameraService.ViewSettings sideView = new LevelCameraService.ViewSettings(new Vector2(40,50), new Vector2(.1f,.1f), Vector3.zero);
        [SerializeField, InspectorLabel("통로 구도"), Tooltip("모든 통로 구간이 공유하는 구도입니다.")]
        private LevelCameraService.ViewSettings corridorView = new LevelCameraService.ViewSettings(new Vector2(50,55), new Vector2(.1f,.1f), new Vector3(10,0,0));
        [SerializeField, InspectorLabel("쿼터 구도"), Tooltip("모든 쿼터 구간이 공유하는 구도입니다.")]
        private LevelCameraService.ViewSettings quarterView = new LevelCameraService.ViewSettings(new Vector2(50,50), new Vector2(.15f,.1f), new Vector3(20,-30,0));
        public LevelCameraService.ViewSettings ViewFor(CameraMovementMode view) =>
            view == CameraMovementMode.Side ? sideView : view == CameraMovementMode.Corridor ? corridorView : quarterView;
        public IReadOnlyList<Section> Sections => Array.AsReadOnly(sections);
        private readonly Dictionary<StageTrigger,string> rejectedContacts = new Dictionary<StageTrigger,string>();
        private readonly HashSet<StageTrigger> activated = new HashSet<StageTrigger>();
        private CheckpointSnapshot? checkpoint;
        private StageTrigger lastCheckpoint;
        private Section currentSection, savedSection;
        private long entryRevision;
        private Action<Section, bool> enterSection;
        private Action<AudioClip,float,float> sectionMusic;
        public Section CurrentSection => currentSection ?? FirstSection;
        public Section RestoreSection => savedSection ?? FirstSection;
        public Section FirstSection
        {
            get
            {
                var result = Array.Find(sections, s => s != null && s.Start == firstSectionStart && firstSectionStart != null);
                return result ?? throw new InvalidOperationException("StageService의 구간과 첫 구간 시작을 작성하세요.");
            }
        }
        public bool HasCheckpoint => checkpoint.HasValue;
        public CheckpointSnapshot Checkpoint => checkpoint ?? throw new InvalidOperationException("저장된 체크포인트가 없습니다.");
        public int ActivatedCheckpointCount => activated.Count;
        public bool IsActivated(StageTrigger point) => activated.Contains(point);
        public event Action<StageTrigger> CheckpointCaptured;
        public void ValidateSections()
        {
            if (sections == null || sections.Length == 0) throw new InvalidOperationException("StageService에 하나 이상의 구간을 작성하세요.");
            var unique = new HashSet<StageTrigger>();
            foreach (var section in sections)
            {
                if (section == null) throw new InvalidOperationException("빈 구간 항목입니다.");
                section.Validate(this);
                if (!unique.Add(section.Start) || !unique.Add(section.End)) throw new InvalidOperationException("체크포인트가 여러 구간에 중복 배정되었습니다.");
            }
            _ = FirstSection;
        }
        public CheckpointSnapshot ResolveRestore(CheckpointSnapshot initial)
        {
            currentSection = RestoreSection;
            return checkpoint ?? new CheckpointSnapshot(FirstSection.Start.CheckpointPose.position, initial.Fragments, initial.UnlockedAbilities);
        }
        public void BindSectionPresentation(Action<Section,bool> enter, Action<AudioClip,float,float> music)
        { enterSection = enter ?? throw new ArgumentNullException(nameof(enter)); sectionMusic = music ?? throw new ArgumentNullException(nameof(music)); }
        public void RestorePresentation()
        {
            var section = CurrentSection;
            enterSection(section, true);
            sectionMusic(section.Music, section.MusicGain, section.MusicTransitionSeconds);
        }
        private bool AcceptCheckpoint(StageTrigger point, PlayerFacade candidate)
        {
            if (activated.Contains(point) || !point.HasActualContact(candidate) || bridge == null ||
                !bridge.IsCurrentActor(candidate, bridge.ActorGeneration) || boundRound == null || !boundRound.IsActive) return false;
            Section section;
            CheckpointSnapshot next;
            try
            {
                if (enterSection == null || sectionMusic == null) throw new InvalidOperationException("구간 표현/음악 연결이 준비되지 않았습니다.");
                ValidateSections();
                section = Array.Find(sections, s => s.Start == point || s.End == point);
                if (section == null) throw new InvalidOperationException("구간에 배정되지 않은 체크포인트입니다.");
                next = new CheckpointSnapshot(point.CheckpointPose.position, bridge.Snapshot.Fragments, boundRound.UnlockedAbilities);
            }
            catch (Exception error)
            {
                string reason=error.Message;
                if (!rejectedContacts.TryGetValue(point,out var previous) || previous!=reason)
                { rejectedContacts[point]=reason; Debug.LogException(new InvalidOperationException("체크포인트 '" + point.name + "' 수락 실패. 이전 저장을 유지합니다.", error), point); }
                return false;
            }
            bool start = section.Start == point;
            // No callbacks before the ledger, three-value payload and execution context are committed together.
            rejectedContacts.Remove(point);
            activated.Add(point); checkpoint = next; lastCheckpoint = point;
            if (start) currentSection = section;
            savedSection = CurrentSection;
            long revision = start ? ++entryRevision : entryRevision;
            if (start)
            {
                try { enterSection?.Invoke(section, false); }
                catch (Exception error) { Debug.LogException(new InvalidOperationException("수락한 구간의 표현 실패. 저장/소비 이력은 유지됩니다.", error), this); }
                if (revision == entryRevision && bound)
                    try { sectionMusic?.Invoke(section.Music,section.MusicGain,section.MusicTransitionSeconds); }
                    catch (Exception error) { Debug.LogException(error,this); }
            }
            var observers = CheckpointCaptured;
            if (observers != null)
                foreach (Action<StageTrigger> observer in observers.GetInvocationList())
                    try { observer(point); } catch (Exception error) { Debug.LogException(error,this); }
            return true;
        }
        public bool TryCaptureBeforeChase(PlayerFacade candidate)
        {
            if (!CanDispatch(candidate) || bridge == null || !bridge.IsCurrentActor(candidate,bridge.ActorGeneration) ||
                boundRound == null || !boundRound.IsActive) return false;
            try
            {
                var capsule=candidate.GetComponent<CapsuleCollider>();
                if (capsule == null) throw new InvalidOperationException("현재 Capsule이 없습니다.");
                var next=new CheckpointSnapshot(capsule.transform.TransformPoint(capsule.center),bridge.Snapshot.Fragments,boundRound.UnlockedAbilities);
                checkpoint=next; savedSection=CurrentSection;
                return true;
            }
            catch (Exception error) { Debug.LogException(error,this); return false; }
        }
        private void OnDrawGizmos()
        {
            if (sections == null) return;
            foreach(var section in sections)
            {
                if (section == null || section.Start == null || section.End == null) continue;
                Gizmos.color = Color.cyan; Gizmos.DrawLine(section.Start.transform.position,section.End.transform.position);
                Gizmos.DrawWireSphere(section.Start.transform.position,.2f);Gizmos.DrawWireSphere(section.End.transform.position,.2f);
#if UNITY_EDITOR
                UnityEditor.Handles.Label((section.Start.transform.position+section.End.transform.position)*.5f,section.DisplayName);
#endif
            }
        }

        public event Action Releasing;
        private StageTrigger[] sceneCheckpoints = Array.Empty<StageTrigger>();
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
        private Func<AbilityId, string, bool> unlockAbility;
        private Func<PlayerFacade, PlayerDebris, bool> fallRequest;
        private IDialogueTableService dialogueTable;
        private Action stopChase;
        public string StageId => string.IsNullOrEmpty(gameObject.scene.path) ? gameObject.scene.name : SceneCatalog.StageIdFromPath(gameObject.scene.path);
        public StageTrigger InitialCheckpoint => FirstSection.Start;
        public StageTrigger ActiveCheckpoint => lastCheckpoint != null ? lastCheckpoint : InitialCheckpoint;
        public Pose InitialPose => InitialCheckpoint.CheckpointPose;
        public bool ContainsCheckpoint(StageTrigger checkpoint)
        {
            return checkpoint != null && checkpoint.Owner == this && checkpoint.Kind == StageTriggerKind.Checkpoint &&
                checkpoint.gameObject.scene == gameObject.scene && Array.Exists(sections,s => s != null && (s.Start == checkpoint || s.End == checkpoint));
        }
        // Component lifetime only: retries retain the prepared scene; title return unloads it.
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
            sceneCheckpoints = Array.FindAll(FindComponents<StageTrigger>(), t => t.Owner == this && t.Kind == StageTriggerKind.Checkpoint);
            sceneSensors = FindComponents<EnemyContactSensor>(); sceneFloors = FindComponents<DamageFloorPlatform>();
            hasChase = Array.Exists(FindComponents<StageTrigger>(), trigger => trigger.Owner == this && trigger.Kind == StageTriggerKind.Chase);
            ValidateSections();
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
                    return AcceptCheckpoint(trigger, candidate);
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
            foreach (var section in sections) section.ResetBlocker();
            chaseAccepted = false;
            IsInitialPreparationComplete = true;
        }
        public void Dispose()
        {
            bound = false; bindingRevision++; entryRevision++;
            chaseAccepted = false;
            var errors = new List<Exception>();
            SessionCleanup.Notify(errors, Releasing);
            SessionCleanup.Attempt(errors, () => ClearFinalDialogue(true));
            foreach (var sensor in sceneSensors) if (sensor != null) SessionCleanup.Attempt(errors, sensor.UnbindPlayer);
            foreach (var floor in sceneFloors) if (floor != null) SessionCleanup.Attempt(errors, floor.UnbindPlayer);
            actor = null; bridge = null; control = null; dialogue = null; dialogueTable = null; flow = null;
            boundRound = null; isCurrentStage = null;
            tryStartSafeDialogue = null; tryStartFinalDialogue = null; enterSection = null; sectionMusic = null; unlockAbility = null; fallRequest = null; stopChase = null;
            ChaseStarted = null;
            SessionCleanup.ThrowIfAny(errors, "Stage connection release failed.");
        }
        private void OnDisable() => Dispose();
        private void OnDestroy() => Dispose();
    }
}
