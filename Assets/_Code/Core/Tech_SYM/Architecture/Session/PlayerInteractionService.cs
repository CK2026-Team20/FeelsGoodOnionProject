using System;
using System.Collections.Generic;
using FeelsGoodOnion.TechSYM.Interaction;
using FeelsGoodOnion.TechSYM.OvenTray;
using UnityEngine;

namespace Cooked.Session
{
    /// <summary>Single F selector/acceptance path. Devices own Busy; this service owns its subscriptions.</summary>
    public sealed class PlayerInteractionService : IDisposable
    {
        private readonly PlayerBridgeService player;
        private readonly float distance;
        private readonly int layers;
        private InteractionPromptAnchor target;
        private readonly List<IInteractionOverlay> overlays = new List<IInteractionOverlay>();
        private readonly Dictionary<OvenTrayObject, Action<OvenTrayObject.OperationOutcome>> operations =
            new Dictionary<OvenTrayObject, Action<OvenTrayObject.OperationOutcome>>();
        private bool disposed, accepting;
        private long actorRevision;
        public const string InputLabel = "F";
        public void BindOverlays(IEnumerable<IInteractionOverlay> values)
        { if (disposed) return; overlays.Clear(); if (values != null) foreach (var value in values) if (value != null && !overlays.Contains(value)) overlays.Add(value); }
        public bool TryCloseOverlay()
        {
            if (disposed) return false;
            foreach (var overlay in overlays)
                if (overlay != null && overlay.IsOpen) { overlay.RequestClose(); return true; }
            return false;
        }
        public void ForceCloseOverlays()
        {
            if (disposed) return;
            ForceCloseBoundOverlays();
        }
        private void ForceCloseBoundOverlays()
        {
            var errors = new List<Exception>();
            foreach (var overlay in overlays.ToArray())
                if (overlay != null) SessionCleanup.Attempt(errors, overlay.ForceClose);
            SessionCleanup.ThrowIfAny(errors, "Forced overlay cleanup failed.");
        }
        public InteractionPromptAnchor Target => target;
        public bool IsVisible => !disposed && player.CanAct && target != null && target.CanInteract;
        public Vector3 PromptPosition => IsVisible ? target.WorldPosition : Vector3.zero;
        public event Action Changed;

        public PlayerInteractionService(PlayerBridgeService player, float distance = 2.5f, int layers = Physics.DefaultRaycastLayers)
        {
            this.player = player ?? throw new ArgumentNullException(nameof(player));
            if (!float.IsFinite(distance) || distance <= 0) throw new ArgumentOutOfRangeException(nameof(distance));
            this.distance = distance; this.layers = layers;
            player.ActorChanged += OnActorChanged;
        }
        public void Refresh()
        {
            if (disposed) return;
            PruneOperations();
            InteractionPromptAnchor nearest = null;
            var visited = new HashSet<InteractionPromptAnchor>();
            if (player.CanAct)
            {
                Transform body = player.BodyTransform;
                float best = float.PositiveInfinity;
                foreach (Collider collider in Physics.OverlapSphere(body.position, distance, layers, QueryTriggerInteraction.Ignore))
                {
                    if (IsSelf(collider, body) || collider.isTrigger) continue;
                    var candidate = collider.GetComponentInParent<InteractionPromptAnchor>();
                    if (candidate == null || !visited.Add(candidate) || !candidate.CanInteract) continue;
                    Vector3 delta = candidate.InteractionPosition - body.position;
                    float square = delta.sqrMagnitude;
                    if (square > distance * distance || square > best) continue;
                    // Entity ID is stable for each resident scene object, independent of PhysX hit order.
                    if (square == best && nearest != null && string.CompareOrdinal(candidate.GetEntityId().ToString(), nearest.GetEntityId().ToString()) >= 0) continue;
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
        private static bool IsSelf(Collider collider, Transform body) => collider.transform.IsChildOf(body) ||
            (collider.attachedRigidbody != null && collider.attachedRigidbody.transform == body);
        private bool HasSight(Transform body, InteractionPromptAnchor candidate, Vector3 delta)
        {
            if (delta.sqrMagnitude < 0.0001f) return true;
            foreach (RaycastHit hit in Physics.RaycastAll(body.position, delta.normalized, delta.magnitude, layers, QueryTriggerInteraction.Ignore))
            {
                if (IsSelf(hit.collider, body) || hit.collider.isTrigger) continue;
                if (hit.collider.GetComponentInParent<InteractionPromptAnchor>() == candidate) continue;
                return false;
            }
            return true;
        }
        public bool TryInteract()
        {
            if (disposed || accepting) return false;
            if (TryCloseOverlay()) return true;
            if (!player.CanAct) return false;
            Refresh();
            if (target == null || !target.CanInteract) return false;
            var actor = player.Actor;
            long revision = actorRevision;
            var interactable = target.Interactable;
            accepting = true;
            try
            {
                if (!interactable.Interact()) return false;
                // A reentrant teardown must not animate or register work against the next actor.
                if (!disposed && revision == actorRevision && player.Actor == actor)
                {
                    if (interactable is OvenTrayObject device && device.IsMoving) Observe(device);
                    actor.GetComponent<PlayerAnimationController>()?.TryRequestInteract();
                }
                else if (interactable is OvenTrayObject staleDevice) staleDevice.CancelOperation();
                return true;
            }
            finally { accepting = false; }
        }
        private void Observe(OvenTrayObject device)
        {
            if (operations.ContainsKey(device)) return;
            Action<OvenTrayObject.OperationOutcome> ended = null;
            ended = _ =>
            {
                if (operations.TryGetValue(device, out var current) && current == ended) ForgetOperation(device);
            };
            operations.Add(device, ended);
            device.OperationEnded += ended;
        }
        private void ForgetOperation(OvenTrayObject device)
        {
            if (!operations.TryGetValue(device, out var ended)) return;
            operations.Remove(device);
            device.OperationEnded -= ended;
        }
        private void PruneOperations()
        {
            foreach (var device in new List<OvenTrayObject>(operations.Keys))
                if (device == null || !device.isActiveAndEnabled || !device.IsMoving) ForgetOperation(device);
        }
        public void CancelOperations()
        {
            var errors = new List<Exception>();
            foreach (var device in new List<OvenTrayObject>(operations.Keys))
            {
                ForgetOperation(device);
                if (device != null) SessionCleanup.Attempt(errors, device.CancelOperation);
            }
            SessionCleanup.ThrowIfAny(errors, "Interaction operation cancellation failed.");
        }
        private void OnActorChanged()
        {
            if (disposed) return;
            actorRevision++;
            target = null;
            var errors = new List<Exception>();
            SessionCleanup.Attempt(errors, CancelOperations);
            SessionCleanup.Attempt(errors, ForceCloseOverlays);
            SessionCleanup.Notify(errors, Changed);
            SessionCleanup.ThrowIfAny(errors, "Interaction actor cleanup failed.");
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            var errors = new List<Exception>();
            // Invalidate callbacks before releasing devices; forced overlays must run while still bound.
            actorRevision++;
            SessionCleanup.Attempt(errors, ForceCloseBoundOverlays);
            player.ActorChanged -= OnActorChanged;
            SessionCleanup.Attempt(errors, CancelOperations);
            overlays.Clear(); target = null; Changed = null;
            SessionCleanup.ThrowIfAny(errors, "Interaction disposal observer failures.");
        }
    }
}


