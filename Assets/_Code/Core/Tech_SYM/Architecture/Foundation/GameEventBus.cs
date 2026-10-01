using System;
using System.Collections.Generic;
using System.Threading;
using Cooked.Contracts;
namespace Cooked.Foundation
{
    public sealed class GameEventBus : IGameEventBus
    {
        private sealed class Subscription : IDisposable
        {
            internal Delegate Handler;
            internal bool Active = true;
            private Action<Subscription> remove;
            internal Subscription(Delegate handler, Action<Subscription> remove) { Handler = handler; this.remove = remove; }
            public void Dispose() { if (!Active) return; Active = false; remove?.Invoke(this); remove = null; Handler = null; }
        }
        private readonly Dictionary<Type, List<Subscription>> handlers = new Dictionary<Type, List<Subscription>>();
        private readonly int threadId = Thread.CurrentThread.ManagedThreadId;
        private bool disposed;
        public IDisposable Subscribe<T>(Action<T> handler) where T : struct, IGameEvent
        {
            Check();
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            if (!handlers.TryGetValue(typeof(T), out var list)) handlers.Add(typeof(T), list = new List<Subscription>());
            var item = new Subscription(handler, s => list.Remove(s)); list.Add(item); return item;
        }
        public void Publish<T>(T message) where T : struct, IGameEvent
        {
            Check();
            if (!handlers.TryGetValue(typeof(T), out var list)) return;
            List<Exception> errors = null;
            foreach (var item in list.ToArray())
            {
                if (!item.Active) continue;
                try { ((Action<T>)item.Handler)(message); }
                catch (Exception e) { if (errors == null) errors = new List<Exception>(); errors.Add(new InvalidOperationException($"Event {typeof(T).FullName} handler failed.", e)); }
            }
            if (errors != null) throw new AggregateException($"Dispatch of {typeof(T).Name} failed.", errors);
        }
        public void Dispose()
        {
            if (disposed) return;
            Check(); disposed = true;
            foreach (var list in handlers.Values) foreach (var item in list.ToArray()) item.Dispose();
            handlers.Clear();
        }
        private void Check()
        {
            if (disposed) throw new ObjectDisposedException(nameof(GameEventBus));
            if (Thread.CurrentThread.ManagedThreadId != threadId) throw new InvalidOperationException("GameEventBus is main-thread-only.");
        }
    }
}
