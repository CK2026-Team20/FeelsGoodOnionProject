using UnityEngine;
namespace Cooked.Level
{
    /// <summary>Prototype adapter: preserves a pickup when HMS inventory is full. Remove after shared pickup adopts this rule.</summary>
    public sealed class LevelTearFragment : MonoBehaviour
    {
        [SerializeField] private GameObject pickupRoot;
        private bool collected;
        private void OnTriggerEnter(Collider other) => Collect(other);
        private void OnTriggerStay(Collider other) => Collect(other);
        private void Collect(Collider other)
        {
            if (collected || pickupRoot == null || other.attachedRigidbody == null ||
                !other.attachedRigidbody.TryGetComponent<PlayerFacade>(out var player) || player.IsDead) return;
            if (player.AddSkillFragments(1) <= 0) return;
            collected = true;
            pickupRoot.SetActive(false);
        }
    }
}
