using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
namespace Cooked.UI
{
    public sealed class OptionsView : BoundView<OptionsViewModel>
    {
        [SerializeField] private CanvasGroup modal;
        private InputAction escapeAction;
        [SerializeField] private UnityEngine.UI.Button close, retry, title;
        [SerializeField] private UnityEngine.UI.Slider master, music, sfx;
        [SerializeField] private TMP_Text values, error;
        public void Configure(CanvasGroup group, UnityEngine.UI.Button closeButton,
            UnityEngine.UI.Button retryButton, UnityEngine.UI.Button titleButton, UnityEngine.UI.Slider masterSlider,
            UnityEngine.UI.Slider musicSlider, UnityEngine.UI.Slider sfxSlider, TMP_Text volumeText, TMP_Text errorText)
        { modal = group; close = closeButton; retry = retryButton; title = titleButton;
          master = masterSlider; music = musicSlider; sfx = sfxSlider; values = volumeText; error = errorText; }
        protected override void ConnectInputs()
        {
            escapeAction = new InputAction("OptionsToggle", InputActionType.Button, "<Keyboard>/escape");
            escapeAction.performed += OnEscape; escapeAction.Enable();
            close.onClick.AddListener(Close); retry.onClick.AddListener(Retry); title.onClick.AddListener(Title);
            master.onValueChanged.AddListener(Master); music.onValueChanged.AddListener(Music); sfx.onValueChanged.AddListener(Sfx);
        }
        protected override void DisconnectInputs()
        {
            if (escapeAction != null) { escapeAction.performed -= OnEscape; escapeAction.Disable(); escapeAction.Dispose(); escapeAction = null; }
            close.onClick.RemoveListener(Close); retry.onClick.RemoveListener(Retry); title.onClick.RemoveListener(Title);
            master.onValueChanged.RemoveListener(Master); music.onValueChanged.RemoveListener(Music); sfx.onValueChanged.RemoveListener(Sfx);
        }
        private void OnEscape(InputAction.CallbackContext _) => Model?.Toggle();
        private void Close() => Model?.Close(); private void Retry() => Model?.Retry();
        private void Title() => Model?.ReturnToTitle(); private void Master(float v) => Model?.SetMaster(v);
        private void Music(float v) => Model?.SetMusic(v); private void Sfx(float v) => Model?.SetSfx(v);
        protected override void Refresh()
        {
            if (modal == null) return; bool visible = Model != null && Model.IsOpen;
            modal.alpha = visible ? 1 : 0; modal.interactable = visible; modal.blocksRaycasts = visible;
            retry.gameObject.SetActive(Model != null && Model.CanNavigate); title.gameObject.SetActive(Model != null && Model.CanNavigate);
            if (Model == null) return;
            master.SetValueWithoutNotify(Model.Master); music.SetValueWithoutNotify(Model.Music); sfx.SetValueWithoutNotify(Model.Sfx);
            values.text = $"전체 {Model.Master:P0}   음악 {Model.Music:P0}   효과 {Model.Sfx:P0}"; error.text = Model.Error ?? "";
        }
    }
}
