using UnityEngine;

namespace Cooked.Cinematics
{
    public sealed class CinematicView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup visibility;
        [SerializeField] private RectTransform[] panelViewports;
        [SerializeField] private UnityEngine.UI.RawImage[] panelArtwork;
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
            image.color = new Color(1,1,1,panel.Opacity);
            if (texture == null || viewport == null) return;
            Vector2 size = viewport.rect.size;
            float ratio = (float)texture.width / texture.height;
            // Reserve the full zoom envelope so artwork edges and speech balloons never crop.
            float width = Mathf.Min(size.x, size.y * ratio) / 1.04f;
            image.rectTransform.sizeDelta = new Vector2(width, width / ratio);
            image.rectTransform.localScale = Vector3.one * panel.Scale;
        }
    }
}
