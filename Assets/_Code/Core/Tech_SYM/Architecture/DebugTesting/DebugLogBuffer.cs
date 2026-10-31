#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
namespace Cooked.DebugTesting
{
    /// <summary>Thread-safe bounded Unity log subscription; never emits logs itself.</summary>
    public sealed class DebugLogBuffer : IDisposable
    {
        private readonly object gate = new object();
        private readonly Queue<string> entries = new Queue<string>();
        private readonly int capacity;
        private bool disposed;
        private long revision;
        public int Capacity => capacity;
        public int Count { get { lock (gate) return entries.Count; } }
        public DebugLogBuffer(int capacity = 200)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            this.capacity = capacity;
            Application.logMessageReceivedThreaded += Receive;
        }
        private void Receive(string message, string stack, LogType type)
        {
            lock (gate)
            {
                if (disposed) return;
                string text = message ?? string.Empty;
                if (type == LogType.Exception && !string.IsNullOrEmpty(stack)) text += "\n" + stack;
                if (text.Length > 2048) text = text.Substring(0, 2048) + "…";
                entries.Enqueue("[" + type + "] " + text);
                while (entries.Count > capacity) entries.Dequeue();
                revision++;
            }
        }
        public string[] Snapshot(out long version)
        {
            lock (gate) { version = revision; return entries.ToArray(); }
        }
        public void Dispose()
        {
            lock (gate) { if (disposed) return; disposed = true; entries.Clear(); }
            Application.logMessageReceivedThreaded -= Receive;
        }
    }
}
#endif
