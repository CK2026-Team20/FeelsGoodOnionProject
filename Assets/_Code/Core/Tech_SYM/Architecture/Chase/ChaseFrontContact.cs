using UnityEngine;
namespace Cooked.Chase
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ChaseFrontContact : MonoBehaviour
    {
        [Tooltip("이 앞쪽 접촉 트리거의 포획을 처리하는 추격 담당입니다. 동일 추격 무리의 ChaseRuntimeDriver를 연결하세요.")]
        [SerializeField] private ChaseRuntimeDriver driver;
        private BoxCollider front;
        private void Awake() => front = GetComponent<BoxCollider>();
        private void OnTriggerEnter(Collider other) => Contact(other);
        private void OnTriggerStay(Collider other) => Contact(other);
        private void Contact(Collider other)
        {
            if (driver == null || other.attachedRigidbody == null ||
                !other.attachedRigidbody.TryGetComponent<PlayerFacade>(out var actor)) return;
            // A retry reuses the swarm. Its new pose can precede physics broadphase synchronization,
            // so queued contacts from the previous attempt are not evidence of current contact.
            if (front == null) front = GetComponent<BoxCollider>();
            if (!front.enabled || !Physics.ComputePenetration(front, front.transform.position, front.transform.rotation,
                other, other.transform.position, other.transform.rotation, out _, out _)) return;
            driver.ReportFrontContact(actor);
        }
    }
}
