using System;
using System.Collections.Generic;
using FeelsGoodOnion.TechSYM.Interaction;
using UnityEngine;

namespace Cooked.Session
{
    /// <summary>E interaction selector. Does not poll input or reference a View.</summary>
    public sealed class PlayerInteractionService : IDisposable
    {
        private readonly PlayerBridgeService player;
        private readonly float distance;
        private readonly int layers;
        private InteractionPromptAnchor target;
        private readonly List<IInteractionOverlay> overlays = new List<IInteractionOverlay>();
        public const string InputLabel = "E";
        public void BindOverlays(IEnumerable<IInteractionOverlay> values)
        { overlays.Clear(); if (values != null) overlays.AddRange(values); }
        public bool TryCloseOverlay()
        {
            foreach (var overlay in overlays)
                if (overlay != null && overlay.IsOpen) { overlay.RequestClose(); return true; }
            return false;
        }
        public InteractionPromptAnchor Target => target;
        public bool IsVisible => player.CanAct && target != null && target.CanInteract;
        public Vector3 PromptPosition => IsVisible ? target.WorldPosition : Vector3.zero;
        public event Action Changed;

        public PlayerInteractionService(PlayerBridgeService player, float distance = 2.5f, int layers = Physics.DefaultRaycastLayers)
        {
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            if (!float.IsFinite(distance) || distance <= 0) throw new ArgumentOutOfRangeException(nameof(distance));
            this.distance = distance; this.layers = layers;
        }
        // Legacy CheckInteract has a private selector and independently polls E. Until that
        // component is approved for refactoring, this isolated adapter reuses its public anchors.
        public void Refresh()
        {
            InteractionPromptAnchor nearest = null;
            if (player.CanAct)
            {
                Transform body = player.BodyTransform;
                float best = float.PositiveInfinity;
                foreach (Collider collider in Physics.OverlapSphere(body.position, distance, layers, QueryTriggerInteraction.Ignore))
                {
                    if (collider.transform.IsChildOf(body)) continue;
                    var candidate = collider.GetComponentInParent<InteractionPromptAnchor>();
                    if (candidate == null || !candidate.CanInteract) continue;
                    Vector3 delta = candidate.InteractionPosition - body.position;
                    float square = delta.sqrMagnitude;
                    if (square > distance * distance || square >= best) continue;
                    // CharacterMovement owns facing; it does not rotate ActorBody for side-view movement.
                    Vector3 forward = player.FacingDirection;
                    Vector3 horizontal = Vector3.ProjectOnPlane(delta, Vector3.up);
                    if (horizontal.sqrMagnitude > 0.0001f && Vector3.Dot(forward.normalized, horizontal.normalized) < .5f) continue;
                    if (!HasSight(body, candidate, delta)) continue;
                    nearest = candidate; best = square;
                }
            }
            if (target == nearest) return;
            target = nearest;
            Changed?.Invoke();
        }
        private bool HasSight(Transform body, InteractionPromptAnchor candidate, Vector3 delta)
        {
            if (delta.sqrMagnitude < 0.0001f) return true;
            foreach (RaycastHit hit in Physics.RaycastAll(body.position, delta.normalized, delta.magnitude, layers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider.transform.IsChildOf(body)) continue;
                if (hit.collider.GetComponentInParent<InteractionPromptAnchor>() == candidate) continue;
                return false;
            }
            return true;
        }
        public bool TryInteract()
        {
            if (TryCloseOverlay()) return true;
            if (!player.CanAct) return false;
            Refresh();
            return target != null && target.CanInteract && target.Interactable.Interact();
        }
        public void Dispose()
        {
            overlays.Clear();
            target = null; var handlers = Changed; Changed = null;
            var errors = new List<Exception>(); SessionCleanup.Notify(errors, handlers);
            SessionCleanup.ThrowIfAny(errors, "Interaction disposal observer failures.");
        }
    }
}


