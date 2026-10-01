using System;
using UnityEngine;

namespace Cooked.Session
{
    /// <summary>Serialized injection channel for legacy Instantiate(prefab) without factory parameters.
    /// Runtime binding is nonserialized, one session owner, and never calls ServiceLocator.</summary>
    public sealed class SessionTransientScope : ScriptableObject
    {
        [NonSerialized] private SessionTransientService service;
        public void Bind(SessionTransientService value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (service != null && service != value) throw new InvalidOperationException("Transient scope already belongs to a session.");
            service = value;
        }
        public void Unbind(SessionTransientService owner) { if (ReferenceEquals(service, owner)) service = null; }
        internal SessionTransientService RequireService() => service ?? throw new InvalidOperationException("Session transient scope was not bound before spawning an effect.");
    }
}
