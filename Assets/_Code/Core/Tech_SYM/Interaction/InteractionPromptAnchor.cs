using UnityEngine;
namespace FeelsGoodOnion.TechSYM.Interaction
{
    /// <summary>상호작용 루트의 로컬 위치 설정. Collider는 자식에 있어도 된다.</summary>
    [DisallowMultipleComponent]
    public sealed class InteractionPromptAnchor : MonoBehaviour
    {
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.8f, 0f);
        public Vector3 WorldPosition => transform.TransformPoint(localOffset);
        public IInteractable Interactable => GetComponent<IInteractable>();
        public bool CanInteract => isActiveAndEnabled && Interactable is Behaviour behaviour && behaviour.isActiveAndEnabled;
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, WorldPosition);
            Gizmos.DrawWireSphere(WorldPosition, 0.08f);
        }
    }
}
