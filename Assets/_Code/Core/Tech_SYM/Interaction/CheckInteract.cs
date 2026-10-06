using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FeelsGoodOnion.TechSYM.Interaction
{
    /// <summary>플레이어 전방의 가까운 조작부를 선택하고 E 입력을 한 번 전달한다.</summary>
    [DisallowMultipleComponent]
    public sealed class CheckInteract : MonoBehaviour
    {
        [Tooltip("상호작용 거리·시선 방향·입력 가능 여부를 읽을 플레이어입니다.")]
        [SerializeField] private PlayerFacade player;
        [Tooltip("상호작용 대상을 찾는 최대 거리입니다(월드 단위, 최소 0.01). 높이면 더 먼 대상을 E로 조작할 수 있습니다. 각도와 차폐 조건도 만족해야 합니다.")]
        [SerializeField, Min(0.01f)] private float distance = 2.5f;
        [Tooltip("플레이어 전방 기준 대상 탐색의 전체 수평각입니다(도, 1~360). 좌우에는 절반씩 적용하며 360은 뒤쪽까지 포함합니다. 거리·차폐 조건도 적용됩니다.")]
        [SerializeField, Range(1f, 360f)] private float horizontalAngle = 120f;
        [Tooltip("상호작용 대상과 차폐할 벽을 포함합니다. 플레이어 Collider는 코드에서 제외합니다.")]
        [SerializeField] private LayerMask detectionLayers = Physics.DefaultRaycastLayers;
        [Tooltip("선택한 상호작용 대상 위에 표시할 말풍선 안내입니다.")]
        [SerializeField] private InteractionPromptView promptView;
        [Tooltip("월드 말풍선을 바라보게 할 인게임 카메라입니다.")]
        [SerializeField] private Camera viewCamera;
        [Tooltip("IInteractionOverlay를 구현한 화면 UI 담당. 없어도 일반 상호작용은 가능합니다.")]
        [SerializeField] private MonoBehaviour overlaySource;
        private readonly InteractionPromptViewModel prompt = new InteractionPromptViewModel();
        private RaycastHit[] hits = new RaycastHit[16];
        private Collider[] candidates = new Collider[16];
        private readonly HashSet<InteractionPromptAnchor> visited = new HashSet<InteractionPromptAnchor>();
        private IInteractionOverlay overlay;
        private Cooked.Session.GameplayControlService standaloneControl;
        private PlayerInputReader standaloneReader;
        private bool readerDisabledByOwner;
        private int restoreReaderAfterFrame = -1;
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
            // This legacy composition is excluded from integrated PlayerRoot by SessionActorHost.
            // Remove when this independent scene adopts the shared session composition.
            if (overlaySource is MemoUIController memo && player != null)
            {
                standaloneReader = player.GetComponent<PlayerInputReader>();
                standaloneControl = new Cooked.Session.GameplayControlService();
                standaloneControl.Changed += OnStandaloneControl;
                memo.Initialize(standaloneControl);
            }
        }
        private void OnStandaloneControl(Cooked.Contracts.ControlState state)
        {
            if (state.GameplayBlocked || state.WorldPaused)
            {
                restoreReaderAfterFrame = -1;
                if (standaloneReader != null && standaloneReader.enabled)
                { readerDisabledByOwner = true; standaloneReader.enabled = false; }
                player?.ClearInput();
            }
            else if (readerDisabledByOwner) restoreReaderAfterFrame = Time.frameCount;
        }
        private void LateUpdate()
        {
            if (restoreReaderAfterFrame < 0 || Time.frameCount <= restoreReaderAfterFrame) return;
            RestoreReader();
        }
        private void RestoreReader()
        {
            if (readerDisabledByOwner && standaloneReader != null) standaloneReader.enabled = true;
            readerDisabledByOwner = false; restoreReaderAfterFrame = -1;
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
            SetTarget(player != null && player.CanReceiveInput ? FindTarget() : null);
            if (!pressed || CurrentTarget == null) return;
            CurrentTarget.Interactable.Interact();
            if (overlaySource != null && overlay != null && overlay.IsOpen) SetTarget(null);
            else if (CurrentTarget != null && !CurrentTarget.CanInteract) SetTarget(null);
        }

        private InteractionPromptAnchor FindTarget()
        {
            int count;
            Vector3 origin = player.transform.position;
            while ((count = Physics.OverlapSphereNonAlloc(origin, distance, candidates,
                detectionLayers, QueryTriggerInteraction.Ignore)) == candidates.Length)
                Array.Resize(ref candidates, candidates.Length * 2);
            visited.Clear();
            InteractionPromptAnchor nearest = null;
            float nearestDistance = float.PositiveInfinity;
            Vector3 forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up).normalized;
            float minimumDot = Mathf.Cos(horizontalAngle * 0.5f * Mathf.Deg2Rad);
            for (int i = 0; i < count; i++)
            {
                if (candidates[i].transform.IsChildOf(player.transform)) continue;
                var candidate = candidates[i].GetComponentInParent<InteractionPromptAnchor>();
                if (candidate == null || !visited.Add(candidate) || !candidate.CanInteract) continue;
                Vector3 delta = candidate.InteractionPosition - origin;
                float squaredDistance = delta.sqrMagnitude;
                if (squaredDistance > distance * distance) continue;
                Vector3 horizontal = Vector3.ProjectOnPlane(delta, Vector3.up);
                if (horizontal.sqrMagnitude > 0.000001f && Vector3.Dot(forward, horizontal.normalized) < minimumDot) continue;
                bool tied = Mathf.Approximately(squaredDistance, nearestDistance);
                if (squaredDistance > nearestDistance && !tied) continue;
                if (tied && nearest != null && candidate != CurrentTarget) continue;
                if (!HasLineOfSight(origin, candidate, delta)) continue;
                nearest = candidate;
                nearestDistance = squaredDistance;
            }
            return nearest;
        }

        private bool HasLineOfSight(Vector3 origin, InteractionPromptAnchor candidate, Vector3 delta)
        {
            float length = delta.magnitude;
            if (length < 0.0001f) return true;
            int count;
            while ((count = Physics.RaycastNonAlloc(origin, delta / length, hits, length,
                detectionLayers, QueryTriggerInteraction.Ignore)) == hits.Length)
                Array.Resize(ref hits, hits.Length * 2);
            for (int i = 0; i < count; i++)
            {
                Transform hit = hits[i].collider.transform;
                if (hit.IsChildOf(player.transform)) continue;
                if (hit.GetComponentInParent<InteractionPromptAnchor>() == candidate) continue;
                return false;
            }
            return true;
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
            try { if (standaloneControl != null && overlaySource is MemoUIController memo) memo.Unbind(); }
            finally
            {
                if (standaloneControl != null)
                { standaloneControl.Changed -= OnStandaloneControl; standaloneControl.Dispose(); standaloneControl = null; }
                RestoreReader(); standaloneReader = null;
            }
        }

        private void OnValidate() => distance = float.IsFinite(distance) ? Mathf.Max(0.01f, distance) : 2.5f;

        private void OnDrawGizmosSelected()
        {
            if (player == null) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(player.transform.position, player.transform.forward * distance);
            Gizmos.DrawWireSphere(player.transform.position, distance);
        }
    }
}
