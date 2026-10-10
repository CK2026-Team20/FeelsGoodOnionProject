using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using Cooked.Authoring;
namespace Cooked.UI
{
    public sealed class OptionsView : BoundView<OptionsViewModel>
    {
        [Tooltip("옵션 화면의 표시·입력 허용을 제어할 CanvasGroup입니다.")]
        [SerializeField, InspectorLabel("옵션 화면")] private CanvasGroup modal;
        private InputAction escapeAction;
        [Tooltip("옵션을 닫는 버튼입니다. 메모 닫기 버튼이 아닙니다.")]
        [SerializeField, InspectorLabel("닫기")] private UnityEngine.UI.Button close;
        [Tooltip("현재 세션을 종료하고 타이틀로 이동할 버튼입니다.")]
        [SerializeField, InspectorLabel("타이틀 이동")] private UnityEngine.UI.Button title;
        [Tooltip("전체 음량을 조절할 Slider입니다. 값 0~1을 사용하며 0은 무음입니다.")]
        [SerializeField, InspectorLabel("전체 음량")] private UnityEngine.UI.Slider master;
        [Tooltip("음악 음량을 조절할 Slider입니다. 값 0~1이며 전체 음량과 함께 적용됩니다.")]
        [SerializeField, InspectorLabel("음악 음량")] private UnityEngine.UI.Slider music;
        [Tooltip("효과음 음량을 조절할 Slider입니다. 값 0~1이며 전체 음량과 함께 적용됩니다.")]
        [SerializeField, InspectorLabel("효과음 음량")] private UnityEngine.UI.Slider sfx;
        [Tooltip("현재 음량 설정값을 표시할 TMP 텍스트입니다.")]
        [SerializeField, InspectorLabel("음량 글자")] private TMP_Text values;
        [Tooltip("설정 저장 실패 등 옵션 오류를 표시할 TMP 텍스트입니다.")]
        [SerializeField, InspectorLabel("저장 오류")] private TMP_Text error;
#if UNITY_EDITOR
        // Temporary Editor field setter for the retained Builder. T12 removes both after T11 reference migration.
        public void Configure(CanvasGroup group, UnityEngine.UI.Button closeButton,
            UnityEngine.UI.Button titleButton, UnityEngine.UI.Slider masterSlider,
            UnityEngine.UI.Slider musicSlider, UnityEngine.UI.Slider sfxSlider, TMP_Text volumeText, TMP_Text errorText)
        { modal = group; close = closeButton; title = titleButton;
          master = masterSlider; music = musicSlider; sfx = sfxSlider; values = volumeText; error = errorText; }
#endif
        protected override void ConnectInputs()
        {
            if (modal == null || close == null || title == null || master == null || music == null || sfx == null || values == null || error == null ||
                close == title || master == music || master == sfx || music == sfx)
                throw new System.InvalidOperationException("OptionsView의 화면·서로 다른 버튼/음량 Slider·표시 글자를 연결하세요.");
            escapeAction = new InputAction("OptionsToggle", InputActionType.Button, "<Keyboard>/escape");
            escapeAction.performed += OnEscape; escapeAction.Enable();
            close.onClick.AddListener(Close); title.onClick.AddListener(Title);
            master.onValueChanged.AddListener(Master); music.onValueChanged.AddListener(Music); sfx.onValueChanged.AddListener(Sfx);
        }
        protected override void DisconnectInputs()
        {
            if (escapeAction != null) { escapeAction.performed -= OnEscape; escapeAction.Disable(); escapeAction.Dispose(); escapeAction = null; }
            if (close != null) close.onClick.RemoveListener(Close);
            if (title != null) title.onClick.RemoveListener(Title);
            if (master != null) master.onValueChanged.RemoveListener(Master);
            if (music != null) music.onValueChanged.RemoveListener(Music);
            if (sfx != null) sfx.onValueChanged.RemoveListener(Sfx);
        }
        private void OnEscape(InputAction.CallbackContext _)
        { if (Model == null) return; if (Model.IsOpen) Close(); else Model.Open(); }
        private void Close() => Model?.Close();
        private void Title() => Model?.ReturnToTitle(); private void Master(float v) => Model?.SetMaster(v);
        private void Music(float v) => Model?.SetMusic(v); private void Sfx(float v) => Model?.SetSfx(v);
        protected override void Refresh()
        {
            if (modal == null) return; bool visible = Model != null && Model.IsOpen;
            modal.alpha = visible ? 1 : 0; modal.interactable = visible; modal.blocksRaycasts = visible;
            if (title != null) title.gameObject.SetActive(visible && Model.CanNavigate);
            if (Model == null) return;
            master.SetValueWithoutNotify(Model.Master); music.SetValueWithoutNotify(Model.Music); sfx.SetValueWithoutNotify(Model.Sfx);
            values.text = $"전체 {Model.Master:P0}   음악 {Model.Music:P0}   효과 {Model.Sfx:P0}"; error.text = Model.Error ?? "";
        }
    }
}
