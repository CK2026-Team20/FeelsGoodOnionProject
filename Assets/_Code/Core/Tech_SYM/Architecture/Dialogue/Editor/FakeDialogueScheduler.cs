using System;
using System.Collections.Generic;
using System.Linq;
namespace Cooked.Dialogue.Tests
{
    // Test-only virtual time. Production contains no elapsed subtraction or per-frame polling.
    internal sealed class FakeDialogueScheduler : IDialogueDelayScheduler
    {
        private readonly List<Handle> handles = new List<Handle>();
        public int FrameId { get; private set; }
        public int PendingCount => handles.Count(h => !h.Stopped);
        public Action LastCallback { get; private set; }
        public IDialogueDelayHandle Schedule(float seconds, Action completed)
        {
            LastCallback = completed;
            var handle = new Handle(seconds, completed); handles.Add(handle); return handle;
        }
        public void Advance(float seconds, int frame)
        {
            FrameId = frame;
            foreach (var handle in handles.ToArray()) handle.Advance(seconds);
            handles.RemoveAll(h => h.Stopped);
        }
        private sealed class Handle : IDialogueDelayHandle
        {
            private Action callback;
            private bool paused;
            public float Remaining { get; private set; }
            public bool Stopped { get; private set; }
            public Handle(float seconds, Action callback) { Remaining = seconds; this.callback = callback; }
            public void SetPaused(bool paused) { this.paused = paused; }
            public void Advance(float seconds)
            {
                if (Stopped || paused) return;
                Remaining = Math.Max(0, Remaining - seconds);
                if (Remaining > 0) return;
                Stopped = true; var action = callback; callback = null; action?.Invoke();
            }
            public void Dispose() { Stopped = true; callback = null; Remaining = 0; }
        }
    }
}
