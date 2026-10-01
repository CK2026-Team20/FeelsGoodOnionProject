using System;
using System.Collections.Generic;
namespace Cooked.Foundation
{
    /// <summary>Composition-root-only scoped registry. Registration transfers IDisposable ownership.</summary>
    public sealed class ServiceLocator : IDisposable
    {
        private readonly ServiceLocator parent;
        private readonly Dictionary<Type, object> services = new Dictionary<Type, object>();
        private readonly List<object> owned = new List<object>();
        private readonly List<ServiceLocator> children = new List<ServiceLocator>();
        private bool disposed;
        public ServiceLocator(ServiceLocator parent = null)
        {
            this.parent = parent;
            if (parent != null) { parent.ThrowIfDisposed(); parent.children.Add(this); }
        }
        public void Register<T>(T service) where T : class
        {
            ThrowIfDisposed();
            if (service == null) throw new ArgumentNullException(nameof(service));
            if (services.ContainsKey(typeof(T))) throw new InvalidOperationException($"Service {typeof(T).FullName} is already registered in this scope.");
            for (var scope = parent; scope != null; scope = scope.parent)
                if (scope.owned.Exists(o => ReferenceEquals(o, service))) throw new InvalidOperationException("Cannot take ownership of an ancestor's service.");
            services.Add(typeof(T), service);
            if (!owned.Exists(o => ReferenceEquals(o, service))) owned.Add(service);
        }
        public T Get<T>() where T : class => TryGet<T>(out var service) ? service : throw new InvalidOperationException($"Missing service {typeof(T).FullName}.");
        public bool TryGet<T>(out T service) where T : class
        {
            ThrowIfDisposed();
            if (services.TryGetValue(typeof(T), out var value)) { service = (T)value; return true; }
            if (parent != null) return parent.TryGet(out service);
            service = null; return false;
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            var errors = new List<Exception>();
            for (int i = children.Count - 1; i >= 0; i--)
                try { children[i].Dispose(); } catch (Exception e) { errors.Add(e); }
            children.Clear();
            for (int i = owned.Count - 1; i >= 0; i--)
                try { (owned[i] as IDisposable)?.Dispose(); } catch (Exception e) { errors.Add(e); }
            owned.Clear(); services.Clear();
            parent?.children.Remove(this);
            if (errors.Count != 0) throw new AggregateException("Service scope disposal failed.", errors);
        }
        private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(ServiceLocator)); }
    }
}
