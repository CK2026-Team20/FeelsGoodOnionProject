using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
namespace Cooked.UI
{
    public sealed class OptionsView : BoundView<OptionsViewModel>
    {
        [Tooltip("옵션 화면의 표시·입력 허용을 제어할 CanvasGroup입니다.")]
        [SerializeField] private CanvasGroup modal;
        private InputAction escapeAction;
        [Tooltip("옵션을 닫는 버튼입니다. 메모 닫기 버튼이 아닙니다.")]
        [SerializeField] private UnityEngine.UI.Button close;
        [Tooltip("저장 체크포인트부터 다시 시작할 버튼입니다.")]
        [SerializeField] private UnityEngine.UI.Button retry;
        [Tooltip("현재 세션을 종료하고 타이틀로 이동할 버튼입니다.")]
        [SerializeField] private UnityEngine.UI.Button title;
        [Tooltip("전체 음량을 조절할 Slider입니다. 값 0~1을 사용하며 0은 무음입니다.")]
        [SerializeField] private UnityEngine.UI.Slider master;
        [Tooltip("음악 음량을 조절할 Slider입니다. 값 0~1이며 전체 음량과 함께 적용됩니다.")]
        [SerializeField] private UnityEngine.UI.Slider music;
        [Tooltip("효과음 음량을 조절할 Slider입니다. 값 0~1이며 전체 음량과 함께 적용됩니다.")]
        [SerializeField] private UnityEngine.UI.Slider sfx;
        [Tooltip("현재 음량 설정값을 표시할 TMP 텍스트입니다.")]
        [SerializeField] private TMP_Text values;
        [Tooltip("설정 저장 실패 등 옵션 오류를 표시할 TMP 텍스트입니다.")]
        [SerializeField] private TMP_Text error;
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
