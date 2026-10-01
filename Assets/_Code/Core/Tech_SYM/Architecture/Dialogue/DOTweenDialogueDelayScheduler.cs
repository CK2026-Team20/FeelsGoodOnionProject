using System;
using DG.Tweening;
using UnityEngine;
namespace Cooked.Dialogue
{
    /// <summary>Only produces owned one-shot delay handles. No global registry, Update loop, or UI dependency.</summary>
    public sealed class DOTweenDialogueDelayScheduler : IDialogueDelayScheduler
    {
        public int FrameId => Time.frameCount;
        public IDialogueDelayHandle Schedule(float seconds, Action completed)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (completed == null) throw new ArgumentNullException(nameof(completed));
            return new DelayHandle(seconds, completed);
        }
        private sealed class DelayHandle : IDialogueDelayHandle
        {
            private Sequence tween;
            private Action completed;
            private readonly float seconds;
            private bool stopped;
            public DelayHandle(float seconds, Action completed)
            {
                this.seconds = seconds;
                this.completed = completed;
                tween = DOTween.Sequence().AppendInterval(seconds).SetUpdate(UpdateType.Normal, true)
                    .SetAutoKill(true).OnComplete(Finish);
            }
            public float Remaining => stopped || tween == null || !tween.IsActive() ? 0 : Mathf.Max(0, seconds - tween.Elapsed(false));
            public void SetPaused(bool paused)
            {
                if (stopped || tween == null || !tween.IsActive()) return;
                if (paused) tween.Pause(); else tween.Play();
            }
            private void Finish()
            {
                if (stopped) return;
                stopped = true;
                var callback = completed;
                completed = null;
                callback?.Invoke();
            }
            public void Dispose()
            {
                if (stopped) return;
                stopped = true;
                completed = null;
                tween?.Kill(false);
                tween = null;
            }
        }
    }
}
