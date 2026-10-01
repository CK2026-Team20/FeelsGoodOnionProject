using System;
using DG.Tweening;
using UnityEngine;

namespace Cooked.Audio
{
    /// <summary>Owns one Sequence. DOTween supplies interpolation and unscaled timing.</summary>
    public sealed class DotweenMusicCrossfadeDriver : IMusicCrossfadeDriver
    {
        private readonly UpdateType updateType;
        private readonly GameObject lifetimeOwner;
        private Sequence current;
        private long revision;
        private bool disposed;

        public DotweenMusicCrossfadeDriver(GameObject lifetimeOwner = null, UpdateType updateType = UpdateType.Normal)
        { this.lifetimeOwner = lifetimeOwner; this.updateType = updateType; }

        public void Start(float fromA, float toA, float fromB, float toB, float seconds,
            bool paused, Action<float, float> apply, Action completed)
        {
            RequireAlive();
            if (apply == null) throw new ArgumentNullException(nameof(apply));
            if (completed == null) throw new ArgumentNullException(nameof(completed));
            AudioCue.ValidateUnit(fromA, nameof(fromA)); AudioCue.ValidateUnit(toA, nameof(toA));
            AudioCue.ValidateUnit(fromB, nameof(fromB)); AudioCue.ValidateUnit(toB, nameof(toB));
            if (float.IsNaN(seconds) || float.IsInfinity(seconds) || seconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            Cancel();
            long ticket = revision;
            float a = fromA, b = fromB;
            Sequence owned = DOTween.Sequence().SetAutoKill(true).SetRecyclable(false).Pause();
            current = owned;
            try
            {
                owned.SetUpdate(updateType, true);
                if (lifetimeOwner != null) owned.SetLink(lifetimeOwner, LinkBehaviour.KillOnDestroy);
                owned.Join(DOTween.To(() => a, value =>
                {
                    if (disposed || revision != ticket) return;
                    a = value;
                }, toA, seconds).SetEase(Ease.Linear));
                owned.Join(DOTween.To(() => b, value =>
                {
                    if (disposed || revision != ticket) return;
                    b = value;
                }, toB, seconds).SetEase(Ease.Linear));
                // Publish a coherent pair after both channel tweens have advanced.
                owned.OnUpdate(() =>
                {
                    if (!disposed && revision == ticket) apply(a, b);
                });
                owned.OnComplete(() =>
                {
                    if (disposed || revision != ticket) return;
                    if (ReferenceEquals(current, owned)) current = null;
                    completed();
                });
                owned.OnKill(() =>
                {
                    if (ReferenceEquals(current, owned)) current = null;
                });
                if (!paused) owned.Play();
            }
            catch { Cancel(); throw; }
        }

        public void SetPaused(bool paused)
        {
            RequireAlive();
            if (current == null) return;
            if (paused) current.Pause(); else current.Play();
        }
        public void Cancel()
        {
            revision++;
            Sequence owned = current; current = null;
            if (owned != null) owned.Kill(false);
        }
        public void Dispose()
        { if (disposed) return; disposed = true; Cancel(); }

#if UNITY_EDITOR
        /// <summary>Advances only this handle, never DOTween's global tween list.</summary>
        public void AdvanceForVerification(float scaledDelta, float unscaledDelta)
        {
            RequireAlive();
            if (updateType != UpdateType.Manual) throw new InvalidOperationException("Verification requires Manual update mode.");
            if (current != null && current.IsPlaying()) current.ManualUpdate(scaledDelta, unscaledDelta);
        }
        public bool HasOwnedTweenForVerification => current != null && current.IsActive();
#endif
        private void RequireAlive()
        { if (disposed) throw new ObjectDisposedException(nameof(DotweenMusicCrossfadeDriver)); }
    }
}
