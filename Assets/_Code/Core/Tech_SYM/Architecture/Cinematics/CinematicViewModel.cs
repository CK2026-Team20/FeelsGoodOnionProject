using System;
using System.Collections.Generic;
using Cooked.Contracts;
using UnityEngine;

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
        public Texture2D Artwork => disposed ? null : state.Artwork;
        public string Caption => disposed ? string.Empty : state.Caption;
        public bool IsVisible => !disposed && state.IsVisible;
        public bool CanSkip => !disposed && state.CanSkip;
        public event Action Changed;
        public void Skip() { if (CanSkip) service.Skip(); }
        private void OnChanged()
        {
            if (disposed) return;
            long version = state.Version;
            var errors = new List<Exception>();
            var listeners = Changed;
            if (listeners != null)
                foreach (Action listener in listeners.GetInvocationList())
                {
                    if (disposed || version != state.Version) break;
                    if (Changed == null || Array.IndexOf(Changed.GetInvocationList(), listener) < 0) continue;
                    try { listener(); } catch (Exception error) { errors.Add(error); }
                }
            if (errors.Count > 0) throw new AggregateException("Cinematic ViewModel observer failures.", errors);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { state.Changed -= OnChanged; }
            finally { Changed = null; }
        }
    }
}
