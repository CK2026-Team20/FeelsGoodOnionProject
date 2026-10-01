using System.Collections.Generic;
using UnityEngine;
namespace Cooked.Level
{
    public enum StageTriggerKind { Enter, Checkpoint, UnlockForm, UnlockTear, Dialogue, Fall, Chase, Escape, Rescue }
    [DisallowMultipleComponent, RequireComponent(typeof(BoxCollider))]
    public sealed class StageTrigger : MonoBehaviour
    {
        [SerializeField] private StageService stage;
        [SerializeField] private StageTriggerKind kind;
        [SerializeField] private string value;
        [SerializeField] private bool once = true;
        private readonly HashSet<Collider> occupants = new HashSet<Collider>();
        private bool fired;
        private bool acceptedThisOccupancy;
        public StageTriggerKind Kind => kind;
        public string Value => value;
        private void OnTriggerEnter(Collider other) => Contact(other);
        private void OnTriggerStay(Collider other) => Contact(other);
        private void Contact(Collider other)
        {
            var body = other.attachedRigidbody;
            if (body == null || !body.TryGetComponent<PlayerFacade>(out var player) || stage == null || stage.Actor != player) return;
            occupants.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
            if (occupants.Count == 0) acceptedThisOccupancy = false;
            occupants.Add(other);
            if (fired || acceptedThisOccupancy) return;
            if (stage.TryDispatch(this, player)) { fired = once; acceptedThisOccupancy = true; }
            // Busy/unknown dialogue returns false: keep eligible for a later valid contact.
        }
        private void OnTriggerExit(Collider other)
        {
            occupants.Remove(other);
            if (occupants.Count == 0) acceptedThisOccupancy = false;
        }
        private void OnDisable() { occupants.Clear(); acceptedThisOccupancy = false; }
    }
}
