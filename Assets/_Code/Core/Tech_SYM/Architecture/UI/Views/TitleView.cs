using UnityEngine;
using Cooked.Authoring;
namespace Cooked.UI
{
    public sealed class TitleView : BoundView<TitleViewModel>
    {
        [Tooltip("새 게임을 시작할 버튼입니다.")]
        [SerializeField, InspectorLabel("새 게임")] private UnityEngine.UI.Button newGame;
        [Tooltip("게임을 종료할 버튼입니다. Editor에서는 실행 환경에 따라 종료 동작이 다릅니다.")]
        [SerializeField, InspectorLabel("종료")] private UnityEngine.UI.Button quit;
        [Tooltip("타이틀에서 옵션 화면을 여는 버튼입니다.")]
        [SerializeField, InspectorLabel("설정")] private UnityEngine.UI.Button settings;
        protected override void ConnectInputs()
        {
            if (newGame == null || quit == null || newGame == quit || (settings != null && (settings == newGame || settings == quit)))
                throw new System.InvalidOperationException("TitleView의 새 게임·종료·설정에는 서로 다른 버튼을 연결하세요.");
            newGame.onClick.AddListener(StartGame); quit.onClick.AddListener(Quit);
            if (settings != null) settings.onClick.AddListener(Settings);
        }
        protected override void DisconnectInputs()
        {
            if (newGame != null) newGame.onClick.RemoveListener(StartGame);
            if (quit != null) quit.onClick.RemoveListener(Quit);
            if (settings != null) settings.onClick.RemoveListener(Settings);
        }
        private void Settings() => Model?.OpenOptions();
        private void StartGame() => Model?.NewGame(); private void Quit() => Model?.Quit();
        protected override void Refresh() { if (newGame != null) newGame.interactable = Model != null && Model.CanStart; if (quit != null) quit.interactable = Model != null && Model.CanStart; if (settings != null) settings.interactable = Model != null && Model.CanStart; }
    }
}
