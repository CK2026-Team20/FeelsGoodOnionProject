using UnityEngine;
namespace FeelsGoodOnion.TechSYM.Interaction
{
    /// <summary>하나의 MemoObject가 독점 참조하는 메모 원본 데이터.</summary>
    [CreateAssetMenu(menuName = "Cooked/Memo Model")]
    public sealed class MemoModel : ScriptableObject
    {
        [SerializeField, Min(1)] private int memoID = 1;
        [SerializeField] private Sprite memoImage;
        public int MemoID => memoID;
        public Sprite MemoImage => memoImage;
    }
}
