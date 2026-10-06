using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Enemies
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
    public sealed class EnemyContactSensor : MonoBehaviour
    {
        [Tooltip("이 센서의 생존/전투 상태를 소유한 적입니다.")]
        [SerializeField] private EnemyActor owner;
        private BoxCollider shape;
        private void Awake() => shape = GetComponent<BoxCollider>();
        private void OnTriggerEnter(Collider other) => owner?.RecordContact(other, true, shape);
        private void OnTriggerStay(Collider other) => owner?.RecordContact(other, true, shape);
        private void OnTriggerExit(Collider other) => owner?.RecordContact(other, false, shape);
    }
}
