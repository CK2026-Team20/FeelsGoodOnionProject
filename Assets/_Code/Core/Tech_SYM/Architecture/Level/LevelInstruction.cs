using UnityEngine;
namespace Cooked.Level
{
    // Declarative sign content consumed by an MVVM prompt/HUD adapter; no keyboard polling here.
    public sealed class LevelInstruction : MonoBehaviour
    {
        [Tooltip("주변 플레이어에게 표시할 기믹 조작 설명입니다. 화면 표시용이며 실제 입력 키나 기능 규칙을 변경하지 않습니다.")]
        [SerializeField, TextArea] private string text;
        public string Text => text;
    }
}
