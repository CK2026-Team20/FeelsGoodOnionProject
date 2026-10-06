using UnityEngine;

namespace Cooked.Cinematics
{
    public sealed class CinematicView : MonoBehaviour
    {
        [Tooltip("컷신 화면 전체의 표시와 입력 허용을 제어하는 CanvasGroup입니다. 원화와 스킵 버튼을 포함한 그룹을 연결하세요.")]
        [SerializeField] private CanvasGroup visibility;
        [Tooltip("원화를 표시할 화면 영역 목록입니다. 타임라인에 지정된 순서에 맞는 한 영역만 전체 화면으로 표시하고 다른 영역은 숨깁니다.")]
        [SerializeField] private RectTransform[] panelViewports;
        [Tooltip("원화 텍스처를 표시할 RawImage 목록입니다. 타임라인에 지정된 순서에 맞는 이미지 한 장만 표시하고 나머지는 숨깁니다.")]
        [SerializeField] private UnityEngine.UI.RawImage[] panelArtwork;
        [Tooltip("현재 컷신을 완료 처리하고 건너뛰는 버튼입니다. 메모 닫기 버튼과는 별도 기능입니다.")]
        [SerializeField] private UnityEngine.UI.Button skipButton;
        private CinematicViewModel viewModel;
        private bool bound;
        public bool IsConfigured => visibility != null && panelViewports != null && panelViewports.Length == 4 && panelArtwork != null && panelArtwork.Length == 4 && skipButton != null;

        public void Bind(CinematicViewModel value)
        {
            Unsubscribe();
            viewModel = value;
            if (isActiveAndEnabled) Subscribe();
            Refresh();
        }
        public void Unbind() { Unsubscribe(); viewModel = null; Refresh(); }
        private void OnEnable() { Subscribe(); Refresh(); }
        private void OnDisable() { Unsubscribe(); }
        private void OnDestroy() { Unbind(); }
        private void OnRectTransformDimensionsChange() { if (viewModel != null) Refresh(); }
        private void Subscribe()
        {
            if (bound || viewModel == null) return;
            viewModel.Changed += Refresh;
            skipButton.onClick.AddListener(OnSkip);
            bound = true;
        }
        private void Unsubscribe()
        {
            if (!bound) return;
            viewModel.Changed -= Refresh;
            if (skipButton != null) skipButton.onClick.RemoveListener(OnSkip);
            bound = false;
        }
        private void OnSkip() { viewModel?.Skip(); }
        private void Refresh()
        {
            if (visibility == null) return;
            bool visible = viewModel != null && viewModel.IsVisible;
            visibility.alpha = visible ? 1 : 0;
            visibility.blocksRaycasts = visible;
            visibility.interactable = visible;
            if (skipButton != null) skipButton.interactable = visible && viewModel.CanSkip;
            if (!visible) return;
            var frame = viewModel.Frame;
            for (int i = 0; i < 4; i++) SetArtwork(panelArtwork[i], panelViewports[i], frame[i]);
        }
        private void SetArtwork(UnityEngine.UI.RawImage image, RectTransform viewport, CinematicPanel panel)
        {
            var texture = panel.Artwork;
            image.texture = texture;
            image.enabled = texture != null;
            viewport.gameObject.SetActive(texture != null);
            image.color = new Color(1,1,1,panel.Opacity);
            if (texture == null || viewport == null) return;
            Vector2 size = viewport.rect.size;
            float ratio = (float)texture.width / texture.height;
            // Fit the single full-screen artwork without animation or cropping.
            float width = Mathf.Min(size.x, size.y * ratio);
            image.rectTransform.sizeDelta = new Vector2(width, width / ratio);
            image.rectTransform.localScale = Vector3.one;
        }
    }
}
