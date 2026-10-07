using System.Collections.Generic;
using UnityEngine;
namespace Cooked.Level
{
    public enum StageTriggerKind { Enter = 0, Checkpoint = 1, UnlockForm = 2, UnlockTear = 3, Dialogue = 4, Fall = 5, Chase = 6, Escape = 7 }
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
    public sealed class StageTrigger : MonoBehaviour
    {
        [Tooltip("이 트리거의 체크포인트·대화·추격·탈출 요청을 처리할 소속 StageService입니다.")]
        [SerializeField] private StageService stage;
        [Tooltip("플레이어 접촉 시 요청할 기능입니다. 체크포인트·대화 등 선택한 종류에 맞게 Value를 지정하세요.")]
        [SerializeField] private StageTriggerKind kind;
        [Tooltip("Checkpoint는 시작/끝 ID, Dialogue·Escape는 대화 코드, UnlockForm/UnlockTear는 연결 안내 코드입니다. 해금 안내를 생략하면 빈 값으로 둡니다.")]
        [SerializeField] private string value;
        [Tooltip("켜면 요청이 성공한 뒤 다시 발동하지 않습니다. 끄면 플레이어가 완전히 나갔다 다시 들어왔을 때 재요청할 수 있습니다.")]
        [SerializeField] private bool once = true;
        private readonly HashSet<Collider> occupants = new HashSet<Collider>();
        private bool fired;
        private bool acceptedThisOccupancy;
        public StageTriggerKind Kind => kind;
        public StageService Owner => stage;
        public string Value => value;
        public void Configure(StageService owner, StageTriggerKind request, string idOrDialogueCode, bool once = true)
        { stage = owner; kind = request; value = idOrDialogueCode; this.once = once; ResetForRetry(); }
        public void ResetForRetry() { fired = false; occupants.Clear(); acceptedThisOccupancy = false; }
        private void OnTriggerEnter(Collider other) => Contact(other);
        private void OnTriggerStay(Collider other) => Contact(other);
        private void Contact(Collider other)
        {
            var body = other.attachedRigidbody;
            if (body == null || !body.TryGetComponent<PlayerFacade>(out var player) || stage == null || stage.Actor != player) return;
            occupants.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
            if (occupants.Count == 0) acceptedThisOccupancy = false;
            occupants.Add(other);
            if (fired || acceptedThisOccupancy) return;
            if (stage.TryDispatch(this, player)) { fired = once; acceptedThisOccupancy = true; }
            // Busy/unknown dialogue returns false: keep eligible for a later valid contact.
        }
        private void OnTriggerExit(Collider other)
        {
            occupants.Remove(other);
            if (occupants.Count == 0) acceptedThisOccupancy = false;
        }
        private void OnDisable() { occupants.Clear(); acceptedThisOccupancy = false; }
    }
}
