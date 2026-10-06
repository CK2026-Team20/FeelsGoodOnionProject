using UnityEngine;
namespace FeelsGoodOnion.TechSYM.Interaction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(InteractionPromptAnchor))]
    public sealed class MemoObject : MonoBehaviour, IInteractable
    {
        [Tooltip("이 오브젝트가 독점 사용할 메모 데이터입니다. 다른 메모와 고유 번호를 공유하지 마세요.")]
        [SerializeField] private MemoModel memoModel;
        [Tooltip("E로 메모를 열고 닫으며 월드 정지를 관리할 화면 담당입니다.")]
        [SerializeField] private MemoUIController memoUI;
        public MemoModel Data => memoModel;
        private void OnEnable()
        {
            if (memoUI == null) Debug.LogError("MemoObject에 씬의 MemoUIController를 연결하세요.", this);
            else memoUI.Register(this);
        }
        private void OnDisable()
        {
            if (memoUI != null) memoUI.Unregister(this);
        }
        public bool Interact() => isActiveAndEnabled && memoUI != null && memoUI.TryOpen(this);
    }
}
