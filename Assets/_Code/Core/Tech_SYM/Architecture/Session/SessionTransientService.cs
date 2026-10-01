using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cooked.Session
{
    public sealed class SessionTransientService : IDisposable
    {
        private readonly HashSet<SessionTransientLifetime> owned = new HashSet<SessionTransientLifetime>();
        private bool disposed;
        public int Count => owned.Count;
        internal void Register(SessionTransientLifetime instance)
        {
            if (disposed) throw new ObjectDisposedException(nameof(SessionTransientService));
            owned.Add(instance);
        }
        internal void Unregister(SessionTransientLifetime instance) => owned.Remove(instance);
        public void Clear()
        {
            var instances = new List<SessionTransientLifetime>(owned);
            owned.Clear();
            var errors = new List<Exception>();
            foreach (var instance in instances)
            {
                if (instance == null) continue;
                SessionCleanup.Attempt(errors, () => instance.gameObject.SetActive(false));
                SessionCleanup.Attempt(errors, () => UnityEngine.Object.Destroy(instance.gameObject));
            }
            SessionCleanup.ThrowIfAny(errors, "Transient cleanup failures.");
        }
        public void Dispose() { if (disposed) return; disposed = true; Clear(); }
    }
}

