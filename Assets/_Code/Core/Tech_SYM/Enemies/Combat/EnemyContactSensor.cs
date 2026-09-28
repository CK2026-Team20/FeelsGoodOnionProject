using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Enemies
{
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
    public sealed class EnemyContactSensor : MonoBehaviour
    {
        [Tooltip("이 센서의 생존/전투 상태를 소유한 적입니다.")]
        [SerializeField] private EnemyActor owner;
        [Tooltip("머리 윗면 센서이면 켭니다.")]
        [SerializeField] private bool isHead;
        private void OnTriggerEnter(Collider other) => owner.RecordContact(other, true, isHead);
        private void OnTriggerStay(Collider other) => owner.RecordContact(other, true, isHead);
        private void OnTriggerExit(Collider other) => owner.RecordContact(other, false, isHead);
    }
}
