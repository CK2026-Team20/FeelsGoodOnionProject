using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Enemies
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
    public sealed class EnemyContactSensor : MonoBehaviour
    {
        [Tooltip("이 실제 몸체 Trigger의 루트 EnemyActor를 연결합니다. 탐지 영역이 아니라 플레이어 몸체 접촉을 전달합니다.")]
        [SerializeField] private EnemyActor owner;
        private BoxCollider shape;
        private void Awake() => shape = GetComponent<BoxCollider>();
        private void OnTriggerEnter(Collider other) => owner?.RecordContact(other, true, shape);
        private void OnTriggerStay(Collider other) => owner?.RecordContact(other, true, shape);
        private void OnTriggerExit(Collider other) => owner?.RecordContact(other, false, shape);
    }
}
