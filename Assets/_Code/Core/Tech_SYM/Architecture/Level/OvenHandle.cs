using FeelsGoodOnion.TechSYM.OvenTray;
using UnityEngine;
namespace Cooked.Level
{
    // F is routed by the session input router to existing IInteractable. No second input reader.
    public sealed class OvenHandle : MonoBehaviour, IInteractable
    {
        [Tooltip("이 손잡이를 E로 조작했을 때 열고 닫을 오븐 트레이입니다.")]
        [SerializeField] private OvenTrayObject tray;
        public bool Interact() => tray != null && tray.Interact();
    }
}
