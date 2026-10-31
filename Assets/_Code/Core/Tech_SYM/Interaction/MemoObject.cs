using UnityEngine;
using Cooked.Authoring;
namespace FeelsGoodOnion.TechSYM.Interaction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(InteractionPromptAnchor))]
    public sealed class MemoObject : MonoBehaviour, IInteractable
    {
        [SerializeField, InspectorLabel("메모 원본"), Tooltip("이 메모가 독점 사용할 MemoModel 자산을 연결하세요.")]
        private MemoModel memoModel;
        [SerializeField, InspectorLabel("메모 화면 담당"), Tooltip("메모 화면과 자기 월드 정지를 관리할 MemoUIController를 연결하세요.")]
        private MemoUIController memoUI;
        public MemoModel Data => memoModel;
        public MemoUIController ScreenController => memoUI;
        private void OnEnable()
        {
            if (memoUI != null) memoUI.Register(this);
        }
        private void Start()
        {
            // A session may inject a screen after scene loading and before the first frame.
            if (memoUI == null) Debug.LogError("MemoObject에 씬의 MemoUIController를 연결하세요.", this);
        }
        public bool BindScreen(MemoUIController value)
        {
            if (memoUI == value) return true;
            var previous = memoUI;
            if (previous != null) previous.Unregister(this);
            memoUI = value;
            if (!isActiveAndEnabled || value == null || value.Register(this)) return true;
            memoUI = previous;
            if (previous != null && isActiveAndEnabled) previous.Register(this);
            return false;
        }
        private void OnDisable()
        {
            if (memoUI != null) memoUI.Unregister(this);
        }
        public bool Interact() => isActiveAndEnabled && memoUI != null && memoUI.TryOpen(this);
    }
}
