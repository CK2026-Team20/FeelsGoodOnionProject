using System.Collections.Generic;
using UnityEngine;
namespace Cooked.Level
{
    public enum StageTriggerKind { Enter, Checkpoint, UnlockForm, UnlockTear, Dialogue, Fall, Chase, Escape, Rescue }
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
    public sealed class StageTrigger : MonoBehaviour
    {
        [Tooltip("이 트리거의 체크포인트·대화·추격·탈출 요청을 처리할 소속 StageService입니다.")]
        [SerializeField] private StageService stage;
        [Tooltip("플레이어 접촉 시 요청할 기능입니다. 체크포인트·대화 등 선택한 종류에 맞게 Value를 지정하세요.")]
        [SerializeField] private StageTriggerKind kind;
        [Tooltip("요청 대상 ID입니다. Checkpoint는 체크포인트 ID, Dialogue는 대화 ID를 사용합니다. ID를 읽지 않는 종류에서는 값을 바꿔도 영향이 없습니다.")]
        [SerializeField] private string value;
        [Tooltip("켜면 요청이 성공한 뒤 다시 발동하지 않습니다. 끄면 플레이어가 완전히 나갔다 다시 들어왔을 때 재요청할 수 있습니다.")]
        [SerializeField] private bool once = true;
        private readonly HashSet<Collider> occupants = new HashSet<Collider>();
        private bool fired;
        private bool acceptedThisOccupancy;
        public StageTriggerKind Kind => kind;
        public string Value => value;
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
