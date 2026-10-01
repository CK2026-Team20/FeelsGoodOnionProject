using UnityEngine;
namespace Cooked.Level
{
    // Declarative sign content consumed by an MVVM prompt/HUD adapter; no keyboard polling here.
    public sealed class LevelInstruction : MonoBehaviour
    {
        [SerializeField, TextArea] private string text;
        public string Text => text;
    }
}
