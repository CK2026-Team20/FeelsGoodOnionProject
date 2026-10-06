using UnityEngine;
namespace FeelsGoodOnion.TechSYM.Interaction
{
    /// <summary>하나의 MemoObject가 독점 참조하는 메모 원본 데이터.</summary>
    [CreateAssetMenu(menuName = "Cooked/Memo Model")]
    public sealed class MemoModel : ScriptableObject
    {
        [Tooltip("메모의 고유 번호입니다(1 이상의 정수). 서로 다른 메모 오브젝트가 같은 번호를 사용하면 등록에 실패합니다.")]
        [SerializeField, Min(1)] private int memoID = 1;
        [Tooltip("메모 본문으로 보여 줄 Sprite입니다. 비워 두면 메모를 열 수 없습니다. 닫기는 E만 사용합니다.")]
        [SerializeField] private Sprite memoImage;
        public int MemoID => memoID;
        public Sprite MemoImage => memoImage;
    }
}
