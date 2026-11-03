using System;
using Cooked.Contracts;
namespace Cooked.UI
{
    public sealed class TutorialPromptViewModel : IDisposable
    {
        private readonly TutorialProgress progress;
        private readonly object id;
        private readonly IGameplayControlService control;
        private bool disposed;
        public bool Visible => progress.IsRevealed(id);
        public bool Paused => control.State.WorldPaused || control.State.PresentationPaused;
        public event Action Changed;
        public TutorialPromptViewModel(TutorialProgress progress, object id, IGameplayControlService control)
        {
            this.progress = progress ?? throw new ArgumentNullException(nameof(progress));
            this.id = id;
            this.control = control ?? throw new ArgumentNullException(nameof(control));
            control.Changed += OnControl;
        }
        public bool TryReveal()
        {
            if (disposed || control.State.GameplayBlocked || Paused || !progress.TryReveal(id)) return false;
            Changed?.Invoke();
            return true;
        }
        private void OnControl(ControlState state) { if (!disposed) Changed?.Invoke(); }
        public void Dispose()
        { if (disposed) return; disposed = true; control.Changed -= OnControl; Changed = null; }
    }
}
