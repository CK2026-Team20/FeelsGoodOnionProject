using System;
using Cooked.Contracts;

namespace Cooked.Cinematics
{
    public sealed class CinematicViewModel : IDisposable
    {
        private readonly ICinematicService service;
        private readonly CinematicPresentationState state;
        private bool disposed;
        public CinematicViewModel(ICinematicService service, CinematicPresentationState state)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            this.state = state ?? throw new ArgumentNullException(nameof(state));
            state.Changed += OnChanged;
        }
        public CinematicFrame Frame => state.Frame;
        public bool IsVisible => !disposed && state.IsVisible;
        public bool CanSkip => !disposed && state.CanSkip;
        public event Action Changed;
        public void Skip() { if (CanSkip) service.Skip(); }
        private void OnChanged() { if (!disposed) Changed?.Invoke(); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            state.Changed -= OnChanged;
            Changed = null;
        }
    }
}
