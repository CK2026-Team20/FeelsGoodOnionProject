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
