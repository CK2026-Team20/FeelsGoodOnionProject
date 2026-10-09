using System;
using UnityEngine;
using Cooked.Authoring;
namespace FeelsGoodOnion.TechSYM.Interaction
{
    /// <summary>루트의 입력 계약과 실제 움직이는 조작/표시 지점을 연결한다.</summary>
    [DisallowMultipleComponent]
    public sealed class InteractionPromptAnchor : MonoBehaviour
    {
        [Tooltip("F 안내를 표시할 지점을 연결합니다. 장치는 움직이는 몸체 아래에 두며 노란 구로 표시됩니다.")]
        [SerializeField, InspectorLabel("안내 지점")] private Transform promptPoint;
        [Tooltip("거리·각도·차폐를 검사할 조작 지점을 연결합니다. 장치는 움직이는 몸체 아래에 두며 초록 박스로 표시됩니다.")]
        [SerializeField, InspectorLabel("조작 지점")] private Transform interactionPoint;
        public Transform PromptPoint => promptPoint;
        public Transform InteractionPoint => interactionPoint;
        public Vector3 WorldPosition => promptPoint != null ? promptPoint.position : throw new InvalidOperationException("F 안내 지점을 연결하세요.");
        public Vector3 InteractionPosition => interactionPoint != null ? interactionPoint.position : throw new InvalidOperationException("F 조작 지점을 연결하세요.");
        public IInteractable Interactable => GetComponent<IInteractable>();
        public bool CanInteract => isActiveAndEnabled && ValidPoints && Interactable is Behaviour behaviour && behaviour.isActiveAndEnabled;
        private bool ValidPoints => promptPoint != null && interactionPoint != null
            && promptPoint.IsChildOf(transform) && interactionPoint.IsChildOf(transform)
            && promptPoint.gameObject.scene == gameObject.scene && interactionPoint.gameObject.scene == gameObject.scene;
        private void Awake()
        {
            if (!ValidPoints) Debug.LogError("F 안내/조작 지점을 이 상호작용 루트 아래의 실제 지점으로 연결하세요.", this);
        }
        private void OnDrawGizmos()
        {
            Color previousColor = Gizmos.color;
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.identity;

            if (promptPoint != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(transform.position, WorldPosition);
                Gizmos.DrawWireSphere(WorldPosition, 0.08f);
            }

            if (interactionPoint != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, InteractionPosition);
                Gizmos.DrawWireCube(InteractionPosition, Vector3.one * 0.12f);
            }

            Gizmos.color = previousColor;
            Gizmos.matrix = previousMatrix;
        }

        private void OnDrawGizmosSelected()
        {
#if UNITY_EDITOR
            if (promptPoint != null) UnityEditor.Handles.Label(WorldPosition + Vector3.up * 0.12f, "F 안내");
            if (interactionPoint != null) UnityEditor.Handles.Label(InteractionPosition + Vector3.up * 0.12f, "조작 지점");
#endif
        }
    }
}
