using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Features
{
    /// <summary>활성 PlayerFacade의 Trigger 접촉을 조각 획득으로 처리한다.</summary>
    [DisallowMultipleComponent]
    public sealed class TearFragment : MonoBehaviour
    {
        [Tooltip("획득 후 비활성화할 이 눈물조각의 Root. 씬 전체 루트가 아닙니다.")]
        [SerializeField] private GameObject pickupRoot;
        private bool collected;

        private void OnEnable() => collected = false;

        private void Awake()
        {
            var sensor = GetComponent<Collider>();
            if (sensor == null || !sensor.isTrigger || pickupRoot == null
                || (pickupRoot != gameObject && !transform.IsChildOf(pickupRoot.transform)))
            {
                Debug.LogError("눈물조각에는 같은 객체의 Trigger Collider와 해당 아이템의 Root 참조가 필요합니다.", this);
                enabled = false;
            }
        }

        private void OnTriggerEnter(Collider other) => TryCollect(other);
        private void OnTriggerStay(Collider other) => TryCollect(other);

        private void TryCollect(Collider other)
        {
            if (!isActiveAndEnabled || collected || other == null || pickupRoot == null) return;
            GameObject owner = other.attachedRigidbody != null
                ? other.attachedRigidbody.gameObject : other.gameObject;
            if (!owner.TryGetComponent<PlayerFacade>(out var player) || !player.isActiveAndEnabled) return;
            collected = true;
            player.AddSkillFragments(1);
            pickupRoot.SetActive(false);
        }
    }
}
