using System;
using UnityEngine;

namespace Cooked.Cinematics
{
    public readonly struct CinematicPanel
    {
        public CinematicPanel(Texture2D artwork, float scale, float opacity) { Artwork = artwork; Scale = scale; Opacity = opacity; }
        public Texture2D Artwork { get; }
        public float Scale { get; }
        public float Opacity { get; }
        public bool IsRevealed => Artwork != null;
    }

    // Four source slots retained for serialization compatibility; only the current artwork is populated.
    public readonly struct CinematicFrame
    {
        private readonly CinematicPanel topLeft, topRight, bottomLeft, bottomRight;
        public CinematicFrame(CinematicPanel a, CinematicPanel b, CinematicPanel c, CinematicPanel d)
        { topLeft = a; topRight = b; bottomLeft = c; bottomRight = d; }
        public CinematicPanel this[int index] => index switch
        {
            0 => topLeft, 1 => topRight, 2 => bottomLeft, 3 => bottomRight,
            _ => throw new ArgumentOutOfRangeException(nameof(index))
        };
        public int RevealedCount => (topLeft.IsRevealed ? 1 : 0) + (topRight.IsRevealed ? 1 : 0)
            + (bottomLeft.IsRevealed ? 1 : 0) + (bottomRight.IsRevealed ? 1 : 0);
    }

    // A projection of the Director's sample, never an independent playback clock.
    public sealed class CinematicPresentationState
    {
        public CinematicFrame Frame { get; private set; }
        public bool IsVisible { get; private set; }
        public bool CanSkip { get; private set; }
        public event Action Changed;
        internal void SetFrame(CinematicFrame frame) { Frame = frame; Changed?.Invoke(); }
        internal void SetFlags(bool visible, bool canSkip)
        {
            if (visible == IsVisible && canSkip == CanSkip) return;
            IsVisible = visible; CanSkip = canSkip; Changed?.Invoke();
        }
    }
}
