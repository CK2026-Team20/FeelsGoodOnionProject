using System;
using UnityEngine;

namespace Cooked.Session
{
    [DisallowMultipleComponent]
    public sealed class SessionTransientLifetime : MonoBehaviour
    {
        [SerializeField] private SessionTransientScope scope;
        private SessionTransientService owner;
        private void OnEnable()
        {
            try
            {
                if (scope == null) throw new InvalidOperationException("Transient prefab requires a serialized session scope.");
                var next = scope.RequireService();
                if (owner != null && !ReferenceEquals(owner, next)) owner.Unregister(this);
                owner = next;
                owner.Register(this);
            }
            catch (InvalidOperationException error)
            {
                // A legacy spawner cannot receive a registry parameter. Missing composition is
                // an explicit error; fail closed so the malformed effect cannot outlive a retry.
                Debug.LogException(error, this);
                gameObject.SetActive(false);
                Destroy(gameObject);
            }
        }
        // Disabled effects remain owned until destroyed or session reset; pooling must not leak them.
        private void OnDestroy() { owner?.Unregister(this); owner = null; }
    }
}
