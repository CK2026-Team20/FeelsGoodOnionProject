using UnityEngine;
using Cooked.Authoring;
namespace FeelsGoodOnion.TechSYM.Interaction
{
    /// <summary>하나의 MemoObject가 독점 참조하는 메모 원본 데이터.</summary>
    [CreateAssetMenu(menuName = "Cooked/Memo Model")]
    public sealed class MemoModel : ScriptableObject
    {
        // Identity is this actual asset reference, never an authored integer or a runtime cache.
        [SerializeField, InspectorLabel("메모 이미지"), Tooltip("메모 본문에 표시할 실제 Sprite를 연결하세요.")]
        private Sprite memoImage;
        public Sprite MemoImage => memoImage;
    }
}
