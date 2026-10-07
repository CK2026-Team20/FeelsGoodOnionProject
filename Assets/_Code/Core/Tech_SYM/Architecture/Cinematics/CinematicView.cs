using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Cooked.Cinematics
{
    public sealed class CinematicView : MonoBehaviour
    {
        [Tooltip("원화·문구·Skip 버튼을 포함한 화면의 CanvasGroup입니다. 연출 표시 중 아래 화면의 포인터 입력을 막습니다.")]
        [SerializeField] private CanvasGroup visibility;
        [Tooltip("원화 한 장이 들어갈 RectTransform 영역입니다. 하단 문구 영역을 제외하고 배치하면 원화 비율을 유지하며 이 영역 안에 맞춥니다.")]
        [SerializeField] private RectTransform artworkViewport;
        [Tooltip("기존 원화 Texture2D 한 장을 표시할 RawImage입니다. 지정한 원화 영역 아래의 이미지 하나를 연결하세요.")]
        [SerializeField] private RawImage artwork;
        [Tooltip("현재 컷의 문구를 표시할 TMP_Text입니다. 원화 아래에 배치하고 한국어 글꼴을 연결하세요.")]
        [SerializeField] private TMP_Text caption;
        [Tooltip("연출 전체를 한 번 건너뛰는 버튼입니다. 옵션을 열었거나 연출이 끝났으면 사용할 수 없습니다.")]
        [SerializeField] private Button skipButton;
        private CinematicViewModel viewModel;
        private bool bound;
        public bool IsConfigured => visibility != null && artworkViewport != null && artwork != null && caption != null && skipButton != null;

        public void Configure(CanvasGroup visibility, RectTransform artworkViewport, RawImage artwork, TMP_Text caption, Button skipButton)
        {
            if (visibility == null || artworkViewport == null || artwork == null || caption == null || skipButton == null)
                throw new ArgumentException("CinematicView needs CanvasGroup, artwork viewport, one RawImage, bottom TMP_Text and Skip Button.");
            Unbind();
            this.visibility = visibility;
            this.artworkViewport = artworkViewport;
            this.artwork = artwork;
            this.caption = caption;
            this.skipButton = skipButton;
            Refresh();
        }
        public void Bind(CinematicViewModel value)
        {
            if (!IsConfigured) throw new InvalidOperationException("CinematicView serialized bindings are incomplete.");
            Unsubscribe();
            viewModel = value;
            try { if (isActiveAndEnabled) Subscribe(); Refresh(); }
            catch { Unbind(); throw; }
        }
        public void Unbind()
        {
            try { Unsubscribe(); }
            finally { viewModel = null; Refresh(); }
        }
        private void OnEnable() { Subscribe(); Refresh(); }
        private void OnDisable() { try { Unsubscribe(); } finally { Clear(); } }
        private void OnDestroy() { Unbind(); }
        private void OnRectTransformDimensionsChange() { if (viewModel != null) Refresh(); }
        private void Subscribe()
        {
            if (bound || viewModel == null || !IsConfigured) return;
            viewModel.Changed += Refresh;
            skipButton.onClick.AddListener(OnSkip);
            bound = true;
        }
        private void Unsubscribe()
        {
            if (!bound) return;
            bound = false;
            try { if (viewModel != null) viewModel.Changed -= Refresh; }
            finally { if (skipButton != null) skipButton.onClick.RemoveListener(OnSkip); }
        }
        private void OnSkip() { if (bound && isActiveAndEnabled) viewModel?.Skip(); }
        private void Refresh()
        {
            if (!IsConfigured || !isActiveAndEnabled || viewModel == null || !viewModel.IsVisible) { Clear(); return; }
            visibility.alpha = 1;
            visibility.blocksRaycasts = true;
            visibility.interactable = true;
            skipButton.interactable = viewModel.CanSkip;
            artwork.texture = viewModel.Artwork;
            artwork.enabled = artwork.texture != null;
            artwork.raycastTarget = true;
            caption.text = viewModel.Caption;
            caption.raycastTarget = false;
            FitArtwork();
        }
        private void Clear()
        {
            if (visibility != null) { visibility.alpha = 0; visibility.blocksRaycasts = false; visibility.interactable = false; }
            if (skipButton != null) skipButton.interactable = false;
            if (artwork != null) { artwork.texture = null; artwork.enabled = false; }
            if (caption != null) caption.text = string.Empty;
        }
        private void FitArtwork()
        {
            var texture = artwork.texture;
            if (texture == null || texture.width <= 0 || texture.height <= 0) return;
            Vector2 size = artworkViewport.rect.size;
            float ratio = (float)texture.width / texture.height;
            float width = Mathf.Min(size.x, size.y * ratio);
            artwork.rectTransform.sizeDelta = new Vector2(width, width / ratio);
        }
    }
}
