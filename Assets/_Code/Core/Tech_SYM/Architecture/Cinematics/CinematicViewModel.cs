using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cooked.Cinematics
{
    public sealed class CinematicViewModel : IDisposable
    {
        private readonly CinematicService service;
        private bool disposed;
        public CinematicViewModel(CinematicService service)
        {
            this.service = service ?? throw new ArgumentNullException(nameof(service));
            service.Changed += OnChanged;
        }
        public Texture2D Artwork => disposed ? null : service.Artwork;
        public string Caption => disposed ? string.Empty : service.Caption;
        public bool IsVisible => !disposed && service.IsVisible;
        public bool CanSkip => !disposed && service.CanSkip;
        public event Action Changed;
        public void Skip() { if (CanSkip) service.Skip(); }
        private void OnChanged()
        {
            if (disposed) return;
            long version = service.PresentationVersion;
            var errors = new List<Exception>();
            var listeners = Changed;
            if (listeners != null)
                foreach (Action listener in listeners.GetInvocationList())
                {
                    if (disposed || version != service.PresentationVersion) break;
                    if (Changed == null || Array.IndexOf(Changed.GetInvocationList(), listener) < 0) continue;
                    try { listener(); } catch (Exception error) { errors.Add(error); }
                }
            if (errors.Count > 0) throw new AggregateException("Cinematic ViewModel observer failures.", errors);
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            try { service.Changed -= OnChanged; }
            finally { Changed = null; }
        }
    }
}
