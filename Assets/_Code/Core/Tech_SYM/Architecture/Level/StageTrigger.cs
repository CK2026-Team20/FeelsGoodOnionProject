using System;
using System.Collections.Generic;
using Cooked.Authoring;
using Cooked.Contracts;
using Cooked.Integration;
using Cooked.Session;
using UnityEngine;
namespace Cooked.Level
{
    public enum StageTriggerKind
    {
        [InspectorName("구간 진입")] Enter = 0,
        [InspectorName("체크포인트")] Checkpoint = 1,
        [InspectorName("폼 전환 해금")] UnlockForm = 2,
        [InspectorName("눈물 해금")] UnlockTear = 3,
        [InspectorName("대화")] Dialogue = 4,
        [InspectorName("데드존")] Fall = 5,
        [InspectorName("추격 시작")] Chase = 6,
        [InspectorName("포포 도착")] Escape = 7
    }
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
    public sealed class StageTrigger : MonoBehaviour
    {
        [SerializeField, InspectorLabel("소속 스테이지"), Tooltip("같은 씬에서 요청을 처리할 StageService를 연결하세요.")]
        private StageService stage;
        [SerializeField, InspectorLabel("요청 종류"), Tooltip("접촉으로 요청할 기능 또는 해금할 능력을 선택하세요.")]
        private StageTriggerKind kind;
        [SerializeField, InspectorLabel("체크포인트 지점"), Tooltip("이 접촉에 대응하는 같은 씬의 CheckpointMarker를 연결하세요. 저장 위치는 현재 ActorBody 중심입니다.")]
        private CheckpointMarker checkpoint;
        [SerializeField, InspectorLabel("대화 선택"), Tooltip("원본 JSON TextAsset를 연결하고 그 표의 Code를 선택하세요. 해금 안내는 비워도 됩니다.")]
        private DialogueSelection dialogue = new DialogueSelection();
        [SerializeField, InspectorLabel("독립 플레이어 담당"), Tooltip("독립 씬 데드존의 SessionActorHost를 연결하세요. 현재 Player와 소유 Shell은 실행 중 주입받습니다.")]
        private SessionActorHost independentHost;
        [SerializeField, InspectorLabel("한 시도에 한 번"), Tooltip("켜면 요청 수락 뒤 재발동하지 않습니다. 끄면 완전히 나간 뒤 다시 요청할 수 있습니다.")]
        private bool once = true;
        // T11 migrates Marker/JSON selection; T10 removes legacy consumers, then T12 removes this field/API.
        [SerializeField, HideInInspector] private string value;
        private readonly HashSet<Collider> occupants = new HashSet<Collider>();
        private readonly HashSet<Rigidbody> acceptedFallBodies = new HashSet<Rigidbody>();
        private bool fired;
        private bool acceptedThisOccupancy;
        private PlayerFacade independentActor;
        private Func<PlayerFacade, PlayerDebris, bool> independentFall;
        private long contactRevision;
        public StageTriggerKind Kind => kind;
        public StageService Owner => stage;
        public string Value => value;
        public CheckpointMarker Checkpoint => checkpoint;
        public DialogueSelection Dialogue => dialogue;
        public AbilityId Ability => kind == StageTriggerKind.UnlockTear ? AbilityId.Tear : AbilityId.FormChange;
        public SessionActorHost IndependentHost => independentHost;
        public void Configure(StageService owner, StageTriggerKind request, string idOrDialogueCode, bool once = true)
        { stage = owner; kind = request; value = idOrDialogueCode; this.once = once; ResetForRetry(); }
        public void ConfigureReferences(StageService owner, StageTriggerKind request, CheckpointMarker marker,
            DialogueSelection conversation = null, bool once = true)
        {
            if (owner != null) SceneComponentLookup.RequireSameScene(this, owner, "소속 스테이지");
            if (marker != null) SceneComponentLookup.RequireSameScene(this, marker, "체크포인트 지점");
            stage = owner; kind = request; checkpoint = marker; dialogue = conversation ?? new DialogueSelection();
            value = null; this.once = once; ResetForRetry();
        }
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
        { independentActor = null; independentFall = null; ResetForRetry(); }
        public void ResetForRetry() { contactRevision++; fired = false; occupants.Clear(); acceptedFallBodies.Clear(); acceptedThisOccupancy = false; }
        private void OnTriggerEnter(Collider other) => TryHandleContact(other);
        private void OnTriggerStay(Collider other) => TryHandleContact(other);
        /// <summary>Physics sensor lives with its BoxCollider. PlayerFacade is on the attached Rigidbody; Shell is a separate owned body.</summary>
        public bool TryHandleContact(Collider other)
        {
            if (!isActiveAndEnabled || other == null || !other.enabled || !other.gameObject.activeInHierarchy) return false;
            var body = other.attachedRigidbody;
            if (body == null) return false;
            var current = stage != null ? stage.Actor : independentActor;
            if (current == null || current.IsDead) return false;
            PlayerDebris shell = null;
            if (body.TryGetComponent<PlayerDebris>(out var debris))
            {
                if (kind != StageTriggerKind.Fall) return false;
                var form = current.GetComponent<PlayerFormController>();
                if (form == null || form.OwnedDebris != debris || debris.Owner != form) return false;
                shell = debris;
            }
            else if (!body.TryGetComponent<PlayerFacade>(out var player) || player != current) return false;
            occupants.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
            acceptedFallBodies.RemoveWhere(acceptedBody => acceptedBody == null ||
                !System.Linq.Enumerable.Any(occupants, occupant => occupant.attachedRigidbody == acceptedBody));
            if (occupants.Count == 0) acceptedThisOccupancy = false;
            occupants.Add(other);
            if (kind == StageTriggerKind.Fall ? acceptedFallBodies.Contains(body) : fired || acceptedThisOccupancy) return false;
            long revision = contactRevision;
            bool accepted;
            if (stage != null)
                accepted = kind == StageTriggerKind.Fall ? stage.TryDispatchFall(this, current, shell) : stage.TryDispatch(this, current);
            else
                accepted = kind == StageTriggerKind.Fall && independentHost != null && independentHost.ActorRoot != null &&
                    current.transform.IsChildOf(independentHost.ActorRoot.transform) && independentFall != null && independentFall(current, shell);
            if (accepted && revision == contactRevision)
            {
                if (kind == StageTriggerKind.Fall) acceptedFallBodies.Add(body);
                else { fired = once; acceptedThisOccupancy = true; }
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
        private void OnDisable() => UnbindIndependentFall();
        private void OnDestroy() => UnbindIndependentFall();
    }
}
