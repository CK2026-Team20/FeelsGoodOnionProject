using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cooked.Cinematics
{
    // Atomic service projection. No playback clock or independent progression.
    public sealed class CinematicPresentationState
    {
        public Texture2D Artwork { get; private set; }
        public string Caption { get; private set; } = string.Empty;
        public int CutIndex { get; private set; } = -1;
        public bool IsVisible { get; private set; }
        public bool CanSkip { get; private set; }
        internal long Version { get; private set; }
        public event Action Changed;

        internal void Publish(CinematicCut cut, int index, bool visible, bool canSkip)
        {
            Texture2D artwork = cut?.Artwork;
            string caption = cut?.Caption ?? string.Empty;
            if (Artwork == artwork && Caption == caption && CutIndex == index && IsVisible == visible && CanSkip == canSkip) return;
            Artwork = artwork;
            Caption = caption;
            CutIndex = index;
            IsVisible = visible;
            CanSkip = canSkip;
            long version = ++Version;
            var errors = new List<Exception>();
            var listeners = Changed;
            if (listeners != null)
                foreach (Action listener in listeners.GetInvocationList())
                {
                    // A reentrant Hide/Prepare supersedes the old publication.
                    if (Version != version) break;
                    if (Changed == null || Array.IndexOf(Changed.GetInvocationList(), listener) < 0) continue;
                    try { listener(); } catch (Exception error) { errors.Add(error); }
                }
            if (errors.Count > 0) throw new AggregateException("Cinematic presentation observer failures.", errors);
        }
    }
}
