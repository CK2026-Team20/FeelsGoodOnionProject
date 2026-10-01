using System;
using System.Collections;
using System.Threading;
using Cooked.Contracts;
using DG.Tweening;
namespace Cooked.UI
{
    public sealed class FadeService : IFadeService, IDisposable
    {
        private readonly float duration;
        private readonly UpdateType updateType;
        private Tween ownedTween;
        private bool running, disposed;
        private long generation;
        public float Alpha { get; private set; } = 1;
        public bool BlocksInput => running || Alpha > 0;
        public event Action Changed;
        // Production uses Normal + independent update. Manual is for deterministic Unity tests.
        public FadeService(float duration = .3f, UpdateType updateType = UpdateType.Normal)
        {
            if (float.IsNaN(duration) || float.IsInfinity(duration) || duration < 0) throw new ArgumentOutOfRangeException(nameof(duration));
            this.duration = duration; this.updateType = updateType;
        }
        public IEnumerator Cover(CancellationToken token, OperationResult result) => Animate(1, token, result);
        public IEnumerator Reveal(CancellationToken token, OperationResult result) => Animate(0, token, result);
        public void SetCoveredImmediate()
        {
            if (disposed) return;
            generation++; KillOwnedTween(); running = false; Alpha = 1; Changed?.Invoke();
        }
        private IEnumerator Animate(float target, CancellationToken token, OperationResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (result.Status != OperationStatus.Pending) throw new ArgumentException("Expected pending result.", nameof(result));
            if (disposed || token.IsCancellationRequested) { result.Cancel(); yield break; }
            if (running) { result.Fail("A full-screen fade is already running."); yield break; }
            running = true; long id = ++generation;
            Tween tween = null; bool completed = false, killed = false; Exception callbackError = null;
            try
            {
                Changed?.Invoke();
                tween = DOTween.To(() => Alpha, value =>
                {
                    if (disposed || id != generation || token.IsCancellationRequested) return;
                    Alpha = value;
                    try { Changed?.Invoke(); } catch (Exception e) { callbackError = e; }
                }, target, duration)
                    .SetEase(Ease.Linear).SetUpdate(updateType, true).SetAutoKill(false)
                    .OnComplete(() => { if (!disposed && id == generation && !token.IsCancellationRequested) completed = true; })
                    .OnKill(() => killed = true);
                ownedTween = tween;
                // Thin orchestration only: DOTween owns all time and interpolation.
                while (!completed)
                {
                    if (disposed || token.IsCancellationRequested || id != generation || killed || !tween.IsActive()) { result.Cancel(); yield break; }
                    if (callbackError != null) { result.Fail("Fade state observer failed: " + callbackError); yield break; }
                    yield return null;
                }
                if (disposed || token.IsCancellationRequested || id != generation) { result.Cancel(); yield break; }
                if (callbackError != null) { result.Fail("Fade state observer failed: " + callbackError); yield break; }
                result.Succeed();
            }
            finally
            {
                // Never complete while killing. Never touch a newer generation's tween.
                if (tween != null && tween.IsActive()) tween.Kill(false);
                if (ReferenceEquals(ownedTween, tween)) ownedTween = null;
                if (id == generation) { running = false; if (!disposed) Changed?.Invoke(); }
                if (result.Status == OperationStatus.Pending) result.Cancel();
            }
        }
        private void KillOwnedTween()
        { var tween = ownedTween; ownedTween = null; if (tween != null && tween.IsActive()) tween.Kill(false); }
        public void Dispose()
        { if (disposed) return; disposed = true; generation++; KillOwnedTween(); running = false; Changed = null; }
    }
}
