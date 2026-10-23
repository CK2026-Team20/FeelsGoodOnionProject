using System;
using System.Collections.Generic;
using Cooked.Authoring;
using Cooked.Chase;
using Cooked.Contracts;
using Cooked.Integration;
using Cooked.Session;
using UnityEngine;
namespace Cooked.Level
{
    public enum StageTriggerKind
    {
        // 0 was the redundant stage-music entry trigger. Existing kinds keep their serialized values.
        [InspectorName("체크포인트")] Checkpoint = 1,
        [InspectorName("폼 전환 해금")] UnlockForm = 2,
        [InspectorName("눈물 해금")] UnlockTear = 3,
        [InspectorName("대화")] Dialogue = 4,
        [InspectorName("데드존")] Fall = 5,
        [InspectorName("추격 시작")] Chase = 6,
        [InspectorName("포포 도착")] Escape = 7 // 8(독립 음악 트리거)은 폐기했으며 재사용하지 않습니다.
    }
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
    public sealed class StageTrigger : MonoBehaviour
    {
        [SerializeField, InspectorLabel("소속 스테이지"), Tooltip("같은 씬에서 요청을 처리할 StageService를 연결하세요.")]
        private StageService stage;
        [SerializeField, InspectorLabel("요청 종류"), Tooltip("접촉으로 요청할 기능 또는 해금할 능력을 선택하세요.")]
        private StageTriggerKind kind = StageTriggerKind.Checkpoint;
        [SerializeField, InspectorLabel("대화 선택"), Tooltip("원본 JSON TextAsset를 연결하고 그 표의 Code를 선택하세요. 해금 안내는 비워도 됩니다.")]
        private DialogueSelection dialogue = new DialogueSelection();
        [SerializeField, InspectorLabel("독립 플레이어 담당"), Tooltip("독립 씬 데드존의 SessionActorHost를 연결하세요. 현재 Player와 소유 Shell은 실행 중 주입받습니다.")]
        private SessionActorHost independentHost;
        [SerializeField, InspectorLabel("한 시도에 한 번"), Tooltip("켜면 요청을 한 번만 수락합니다. 체크포인트·포포 도착·데드존은 각 재접촉 규칙을 따릅니다.")]
        private bool once = true;
        [SerializeField, InspectorLabel("추격 퇴로"), Tooltip("추격 시작 시 닫을 같은 씬의 퇴로를 연결하세요. 플레이어와 겹치면 시작을 보류합니다.")]
        private ChaseRetreatGate chaseRetreatGate;
        [SerializeField, InspectorLabel("추격 무리"), Tooltip("이 트리거가 시작할 같은 씬의 ChaseRuntimeDriver를 연결하세요. 움직이는 무리 아래에 시작 센서를 넣지 마세요.")]
        private ChaseRuntimeDriver chaseDriver;
        private ChaseRuntimeDriver chase;
        private Func<PlayerFacade, bool> saveBeforeChase;
        private bool startingChase;
        public void BindChase(ChaseRuntimeDriver driver, Func<PlayerFacade, bool> save)
        {
            if (kind != StageTriggerKind.Chase || stage == null || chaseRetreatGate == null || chaseDriver == null)
                throw new InvalidOperationException("추격 시작 트리거에는 스테이지, 퇴로와 추격 무리를 연결하세요.");
            SceneComponentLookup.RequireSameScene(this, stage, "소속 스테이지");
            SceneComponentLookup.RequireSameScene(this, chaseRetreatGate, "추격 퇴로");
            SceneComponentLookup.RequireSameScene(this, chaseDriver, "추격 무리");
            if (driver != chaseDriver || transform.IsChildOf(driver.transform) || chaseRetreatGate.transform.IsChildOf(driver.transform))
                throw new InvalidOperationException("작성한 추격 무리를 연결하고 시작 센서와 퇴로는 움직이는 무리 밖에 배치하세요.");
            if (save == null) throw new ArgumentNullException(nameof(save));
            chase = driver; saveBeforeChase = save;
        }
        public void UnbindChase() { contactRevision++; chase = null; saveBeforeChase = null; }
        private bool TryStartChase(PlayerFacade current)
        {
            if (startingChase || !stage.CanDispatch(current) || stage.IsChaseAccepted || chase == null ||
                saveBeforeChase == null || chaseRetreatGate == null || !chaseRetreatGate.CanClose(current) || !chase.CanStart(current)) return false;
            startingChase = true;
            long revision = contactRevision;
            var driver = chase;
            var owner = stage;
            long attempt = 0;
            bool closedGate = false;
            try
            {
                if (!saveBeforeChase(current) || revision != contactRevision || !stage.CanDispatch(current) ||
                    !chaseRetreatGate.CanClose(current) || !driver.TryStart(current)) return false;
                attempt = driver.AttemptVersion;
                if (revision != contactRevision || owner != stage || driver != chase || !owner.CanDispatch(current)) return false;
                if (!chaseRetreatGate.TryClose(current))
                    throw new InvalidOperationException("추격 시작 직후 안전한 퇴로 닫기가 실패했습니다.");
                closedGate = true;
                if (revision != contactRevision || owner != stage || driver != chase || !owner.CanDispatch(current) ||
                    !driver.IsRunningAttempt(attempt, current)) return false;
                attempt = 0; // Stage accepted ownership; this request must no longer cancel it.
                owner.NotifyChaseStarted(this, current);
                return true;
            }
            finally
            {
                if (attempt != 0 && driver != null && driver.AttemptVersion == attempt)
                {
                    driver.Stop();
                    if (closedGate && revision == contactRevision) chaseRetreatGate.ResetForRetry();
                }
                startingChase = false;
            }
        }
        private readonly HashSet<Collider> occupants = new HashSet<Collider>();
        private readonly HashSet<Rigidbody> acceptedFallBodies = new HashSet<Rigidbody>();
        private bool fired;
        private bool acceptedThisOccupancy;
        private StageTriggerKind contactKind;
        private PlayerFacade contactActor;
        private PlayerFacade independentActor;
        private Func<PlayerFacade, PlayerDebris, bool> independentFall;
        private long contactRevision;
        public StageTriggerKind Kind => kind;
        public StageService Owner => stage;
        public ChaseRuntimeDriver ChaseDriver => chaseDriver;
        public Pose CheckpointPose
        {
            get
            {
                var volume = GetComponent<BoxCollider>();
                if (kind != StageTriggerKind.Checkpoint || stage == null || volume == null || !volume.isTrigger)
                    throw new InvalidOperationException("체크포인트 종류와 소속 스테이지, Trigger BoxCollider가 필요합니다.");
                SceneComponentLookup.RequireSameScene(this, stage, "소속 스테이지");
                SceneComponentLookup.RequireFixedPoint(this, transform, "체크포인트 위치");
                if (!CameraProfileValidation.IsFinite(transform.position)) throw new InvalidOperationException("체크포인트 위치가 유한하지 않습니다.");
                return new Pose(transform.position, transform.rotation);
            }
        }
        public DialogueSelection Dialogue => dialogue;
        public AbilityId Ability => kind == StageTriggerKind.UnlockTear ? AbilityId.Tear : AbilityId.FormChange;
        public SessionActorHost IndependentHost => independentHost;
        public void BindIndependentFall(PlayerFacade currentActor, Func<PlayerFacade, PlayerDebris, bool> request)
        {
            if (kind != StageTriggerKind.Fall || independentHost == null || stage != null || currentActor == null ||
                independentHost.ActorRoot == null || !currentActor.transform.IsChildOf(independentHost.ActorRoot.transform))
                throw new InvalidOperationException("독립 데드존은 지정 Host의 현재 Actor를 주입받아야 합니다.");
            SceneComponentLookup.RequireSameScene(this, independentHost, "독립 플레이어 담당");
            UnbindIndependentFall(); independentActor = currentActor;
            independentFall = request ?? throw new ArgumentNullException(nameof(request));
        }
        public void UnbindIndependentFall()
        { independentActor = null; independentFall = null; ClearContacts(); }
        public void ResetForRetry()
        {
            ClearContacts();
            if (kind == StageTriggerKind.Chase && chaseRetreatGate != null) chaseRetreatGate.ResetForRetry();
        }
        private void ClearContacts()
        {
            contactRevision++; fired = false; occupants.Clear(); acceptedFallBodies.Clear(); acceptedThisOccupancy = false;
            contactKind = kind; contactActor = null;
        }
        // 저장/최종 대화의 재접촉 판정은 서비스가, 데드존의 중복은 실제 몸체가 소유합니다.
        private bool UsesOnce => once && kind != StageTriggerKind.Checkpoint && kind != StageTriggerKind.Escape && kind != StageTriggerKind.Fall;
        private void OnTriggerEnter(Collider other) => TryHandleContact(other);
        private void OnTriggerStay(Collider other) => TryHandleContact(other);
        /// <summary>Physics sensor lives with its BoxCollider. PlayerFacade is on the attached Rigidbody; Shell is a separate owned body.</summary>
        internal bool HasActualContact(PlayerFacade candidate)
        {
            if (!isActiveAndEnabled || candidate == null || !candidate.isActiveAndEnabled || candidate.IsDead) return false;
            var volume=GetComponent<BoxCollider>();
            var capsule=candidate.GetComponent<CapsuleCollider>();
            return volume != null && volume.enabled && volume.isTrigger && volume.gameObject.activeInHierarchy &&
                capsule != null && capsule.enabled && capsule.gameObject.activeInHierarchy &&
                capsule.attachedRigidbody != null && capsule.attachedRigidbody.GetComponent<PlayerFacade>() == candidate &&
                Physics.ComputePenetration(volume,volume.transform.position,volume.transform.rotation,
                    capsule,capsule.transform.position,capsule.transform.rotation,out _,out _);
        }
        public bool TryHandleContact(Collider other)
        {
            if (!isActiveAndEnabled || other == null || !other.enabled || !other.gameObject.activeInHierarchy) return false;
            var body = other.attachedRigidbody;
            if (body == null) return false;
            var current = stage != null ? stage.Actor : independentActor;
            if (current == null || current.IsDead) return false;
            // 요청 종류 또는 현재 Actor가 바뀌면 이전 종류/Actor의 수락 기록을 승계하지 않습니다.
            if (contactKind != kind || contactActor != current)
            { ClearContacts(); contactActor = current; }
            PlayerDebris shell = null;
            if (body.TryGetComponent<PlayerDebris>(out var debris))
            {
                if (kind != StageTriggerKind.Fall) return false;
                var form = current.GetComponent<PlayerFormController>();
                if (form == null || form.OwnedDebris != debris || debris.Owner != form) return false;
                shell = debris;
            }
            else if (!body.TryGetComponent<PlayerFacade>(out var player) || player != current) return false;
            // Checkpoint progress has exactly one owner; local occupancy/reset flags do not participate.
            if (kind == StageTriggerKind.Checkpoint)
                return stage != null && shell == null && HasActualContact(current) && stage.TryDispatch(this,current);
            occupants.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
            acceptedFallBodies.RemoveWhere(acceptedBody => acceptedBody == null ||
                !System.Linq.Enumerable.Any(occupants, occupant => occupant.attachedRigidbody == acceptedBody));
            if (occupants.Count == 0) acceptedThisOccupancy = false;
            occupants.Add(other);
            if (kind == StageTriggerKind.Fall ? acceptedFallBodies.Contains(body) : (UsesOnce && fired) || acceptedThisOccupancy) return false;
            long revision = contactRevision;
            var requestedKind = kind;
            bool accepted;
            if (stage != null)
                accepted = kind == StageTriggerKind.Chase ? TryStartChase(current) :
                    kind == StageTriggerKind.Fall ? stage.TryDispatchFall(this, current, shell) : stage.TryDispatch(this, current);
            else
                accepted = kind == StageTriggerKind.Fall && independentHost != null && independentHost.ActorRoot != null &&
                    current.transform.IsChildOf(independentHost.ActorRoot.transform) && independentFall != null && independentFall(current, shell);
            if (accepted && revision == contactRevision && requestedKind == kind && contactActor == current &&
                current == (stage != null ? stage.Actor : independentActor))
            {
                if (kind == StageTriggerKind.Fall) acceptedFallBodies.Add(body);
                else { fired = UsesOnce; acceptedThisOccupancy = true; }
            }
            // Busy/unknown dialogue returns false: keep eligible for a later valid contact.
            return accepted;
        }
        private void OnTriggerExit(Collider other)
        {
            occupants.Remove(other);
            if (other != null && other.attachedRigidbody != null &&
                !System.Linq.Enumerable.Any(occupants, occupant => occupant != null && occupant.attachedRigidbody == other.attachedRigidbody))
                acceptedFallBodies.Remove(other.attachedRigidbody);
            if (occupants.Count == 0) acceptedThisOccupancy = false;
        }
        private void OnEnable()
        { if (independentHost != null && stage == null && kind == StageTriggerKind.Fall) independentHost.BindIndependentFall(this); }
        private void OnDisable() => UnbindIndependentFall();
        private void OnDestroy() { UnbindChase(); UnbindIndependentFall(); }
    }
}
