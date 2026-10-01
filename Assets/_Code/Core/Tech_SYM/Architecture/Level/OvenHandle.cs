using FeelsGoodOnion.TechSYM.OvenTray;
using UnityEngine;
namespace Cooked.Level
{
    // F is routed by the session input router to existing IInteractable. No second input reader.
    public sealed class OvenHandle : MonoBehaviour, IInteractable
    {
        [SerializeField] private OvenTrayObject tray;
        public bool Interact() => tray != null && tray.Interact();
    }
}
