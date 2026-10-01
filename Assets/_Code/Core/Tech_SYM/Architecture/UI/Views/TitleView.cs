using UnityEngine;
namespace Cooked.UI
{
    public sealed class TitleView : BoundView<TitleViewModel>
    {
        [SerializeField] private UnityEngine.UI.Button newGame, quit;
        public void Configure(UnityEngine.UI.Button start, UnityEngine.UI.Button exit) { newGame = start; quit = exit; }
        protected override void ConnectInputs() { newGame.onClick.AddListener(StartGame); quit.onClick.AddListener(Quit); }
        protected override void DisconnectInputs() { newGame.onClick.RemoveListener(StartGame); quit.onClick.RemoveListener(Quit); }
        private void StartGame() => Model?.NewGame(); private void Quit() => Model?.Quit();
        protected override void Refresh() { if (newGame != null) newGame.interactable = Model != null && Model.CanStart; if (quit != null) quit.interactable = Model != null && Model.CanStart; }
    }
}
