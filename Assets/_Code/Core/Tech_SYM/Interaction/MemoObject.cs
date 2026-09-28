using UnityEngine;
namespace FeelsGoodOnion.TechSYM.Interaction
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(InteractionPromptAnchor))]
    public sealed class MemoObject : MonoBehaviour, IInteractable
    {
        [SerializeField] private MemoModel memoModel;
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
