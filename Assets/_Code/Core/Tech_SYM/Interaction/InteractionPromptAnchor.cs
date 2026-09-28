using UnityEngine;
namespace FeelsGoodOnion.TechSYM.Interaction
{
    /// <summary>상호작용 루트의 로컬 위치 설정. Collider는 자식에 있어도 된다.</summary>
    [DisallowMultipleComponent]
    public sealed class InteractionPromptAnchor : MonoBehaviour
    {
        [Tooltip("말풍선 UI를 표시할 로컬 위치입니다. Scene 뷰의 노란 구로 표시됩니다.")]
        [SerializeField] private Vector3 localOffset = new Vector3(0f, 0.8f, 0f);
        [Tooltip("거리·전방 각도·차폐 검사에 사용하는 조작부의 로컬 위치입니다. 손잡이 등에 맞추세요. " +
                 "Scene 뷰의 초록 박스로 표시되며, 박스 크기는 상호작용 범위를 뜻하지 않습니다.")]
        [SerializeField] private Vector3 interactionLocalOffset;
        public Vector3 WorldPosition => transform.TransformPoint(localOffset);
        public Vector3 InteractionPosition => transform.TransformPoint(interactionLocalOffset);
        public IInteractable Interactable => GetComponent<IInteractable>();
        public bool CanInteract => isActiveAndEnabled && Interactable is Behaviour behaviour && behaviour.isActiveAndEnabled;
        private void OnDrawGizmos()
        {
            Color previousColor = Gizmos.color;
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.identity;

            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, WorldPosition);
            Gizmos.DrawWireSphere(WorldPosition, 0.08f);

            Gizmos.color = Color.green;
            Gizmos.DrawLine(transform.position, InteractionPosition);
            Gizmos.DrawWireCube(InteractionPosition, Vector3.one * 0.12f);

            Gizmos.color = previousColor;
            Gizmos.matrix = previousMatrix;
        }

        private void OnDrawGizmosSelected()
        {
#if UNITY_EDITOR
            UnityEditor.Handles.Label(WorldPosition + Vector3.up * 0.12f, "Prompt UI");
            UnityEditor.Handles.Label(InteractionPosition + Vector3.up * 0.12f, "Interaction Point");
#endif
        }
    }
}
