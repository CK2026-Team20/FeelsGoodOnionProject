using UnityEngine;
namespace Cooked.UI
{
    public sealed class TitleView : BoundView<TitleViewModel>
    {
        [Tooltip("새 게임을 시작할 버튼입니다.")]
        [SerializeField] private UnityEngine.UI.Button newGame;
        [Tooltip("게임을 종료할 버튼입니다. Editor에서는 실행 환경에 따라 종료 동작이 다릅니다.")]
        [SerializeField] private UnityEngine.UI.Button quit;
        [Tooltip("타이틀에서 옵션 화면을 여는 버튼입니다.")]
        [SerializeField] private UnityEngine.UI.Button settings;
        public void Configure(UnityEngine.UI.Button start, UnityEngine.UI.Button exit) { newGame = start; quit = exit; }
        protected override void ConnectInputs() { newGame.onClick.AddListener(StartGame); quit.onClick.AddListener(Quit); if (settings != null) settings.onClick.AddListener(Settings); }
        protected override void DisconnectInputs() { newGame.onClick.RemoveListener(StartGame); quit.onClick.RemoveListener(Quit); if (settings != null) settings.onClick.RemoveListener(Settings); }
        private void Settings() => Model?.OpenOptions();
        private void StartGame() => Model?.NewGame(); private void Quit() => Model?.Quit();
        protected override void Refresh() { if (newGame != null) newGame.interactable = Model != null && Model.CanStart; if (quit != null) quit.interactable = Model != null && Model.CanStart; if (settings != null) settings.interactable = Model != null && Model.CanStart; }
    }
}
