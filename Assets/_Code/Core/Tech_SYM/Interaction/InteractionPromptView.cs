using UnityEngine;
namespace FeelsGoodOnion.TechSYM.Interaction
{
    /// <summary>고정 UI 루트에서 대상을 추적한다. 말풍선 자식만 켜고 끈다.</summary>
    public sealed class InteractionPromptView : MonoBehaviour
    {
        [Tooltip("상호작용 대상이 있을 때 표시할 말풍선 외형 오브젝트입니다.")]
        [SerializeField] private GameObject bubble;
        [Tooltip("말풍선 등장·퇴장 크기 표현을 담당할 UIScaleTween입니다.")]
        [SerializeField] private UIScaleTween scaleTween;
        private InteractionPromptViewModel model;
        private Camera viewCamera;
        public void Bind(InteractionPromptViewModel viewModel, Camera camera)
        {
            Unbind();
            model = viewModel;
            viewCamera = camera;
            model.Changed += Refresh;
            Refresh();
        }
        public void Unbind()
        {
            if (model != null) model.Changed -= Refresh;
            model = null;
            viewCamera = null;
            if (bubble != null) bubble.SetActive(false);
        }
        private void Refresh()
        {
            bool visible = model != null && model.IsVisible && viewCamera != null;
            if (visible)
            {
                FollowTarget();
                bool wasVisible = bubble.activeSelf;
                bubble.SetActive(true);
                if (!wasVisible) scaleTween.Show();
            }
            else bubble.SetActive(false);
        }
        private void LateUpdate()
        {
            if (model == null) return;
            if (!model.IsVisible || viewCamera == null) { Refresh(); return; }
            FollowTarget();
        }
        private void FollowTarget()
        {
            transform.position = model.WorldPosition;
            transform.rotation = viewCamera.transform.rotation;
        }
        private void OnDisable() => Unbind();
    }
}
