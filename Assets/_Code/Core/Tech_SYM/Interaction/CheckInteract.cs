using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FeelsGoodOnion.TechSYM.Interaction
{
    /// <summary>플레이어 전방의 가장 가까운 대상을 찾고 E 입력을 한 번 전달한다.</summary>
    [DisallowMultipleComponent]
    public sealed class CheckInteract : MonoBehaviour
    {
        [SerializeField] private PlayerFacade player;
        [SerializeField, Min(0.01f)] private float distance = 2.5f;
        [Tooltip("상호작용 대상과 차폐할 벽을 포함합니다. 플레이어 Collider는 코드에서 제외합니다.")]
        [SerializeField] private LayerMask detectionLayers = Physics.DefaultRaycastLayers;
        [SerializeField] private InteractionPromptView promptView;
        [SerializeField] private Camera viewCamera;
        [Tooltip("IInteractionOverlay를 구현한 화면 UI 담당. 없어도 일반 상호작용은 가능합니다.")]
        [SerializeField] private MonoBehaviour overlaySource;
        private readonly InteractionPromptViewModel prompt = new InteractionPromptViewModel();
        private RaycastHit[] hits = new RaycastHit[16];
        private IInteractionOverlay overlay;
        public InteractionPromptAnchor CurrentTarget { get; private set; }

        private void Awake()
        {
            overlay = overlaySource as IInteractionOverlay;
            if (player == null || promptView == null || viewCamera == null ||
                (overlaySource != null && overlay == null))
            {
                Debug.LogError("CheckInteract의 PlayerFacade, Prompt View, Camera와 Overlay 계약을 확인하세요.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (promptView != null && viewCamera != null) promptView.Bind(prompt, viewCamera);
        }

        private void Update()
        {
            bool pressed = Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
            // 열린 화면이 E를 소비한다. 플레이어/레이 대상이 없어도 닫기는 가능하다.
            if (overlaySource != null && overlay != null && overlay.IsOpen)
            {
                SetTarget(null);
                if (pressed) overlay.RequestClose();
                return;
            }
            SetTarget(player != null && player.isActiveAndEnabled ? FindTarget() : null);
            if (!pressed || CurrentTarget == null) return;
            CurrentTarget.Interactable.Interact();
            if (overlaySource != null && overlay != null && overlay.IsOpen) SetTarget(null);
        }

        private InteractionPromptAnchor FindTarget()
        {
            int count;
            // 포화된 버퍼는 확장하여 가까운 벽/대상을 누락하지 않는다.
            while ((count = Physics.RaycastNonAlloc(player.transform.position, player.transform.forward,
                hits, distance, detectionLayers, QueryTriggerInteraction.Ignore)) == hits.Length)
                Array.Resize(ref hits, hits.Length * 2);
            Collider nearest = null;
            float nearestDistance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider candidate = hits[i].collider;
                if (candidate.transform.IsChildOf(player.transform)) continue;
                if (hits[i].distance >= nearestDistance) continue;
                nearest = candidate;
                nearestDistance = hits[i].distance;
            }
            if (nearest == null) return null;
            var anchor = nearest.GetComponentInParent<InteractionPromptAnchor>();
            return anchor != null && anchor.CanInteract ? anchor : null;
        }

        private void SetTarget(InteractionPromptAnchor target)
        {
            CurrentTarget = target;
            prompt.SetTarget(target);
        }

        private void OnDisable()
        {
            SetTarget(null);
            if (promptView != null) promptView.Unbind();
        }

        private void OnValidate() => distance = float.IsFinite(distance) ? Mathf.Max(0.01f, distance) : 2.5f;

        private void OnDrawGizmosSelected()
        {
            if (player == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(player.transform.position, player.transform.forward * distance);
        }
    }
}
