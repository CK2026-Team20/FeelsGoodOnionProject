using UnityEngine;

namespace Cooked.Cinematics
{
    // Timeline binds this presentation adapter, never the View or a global locator.
    public sealed class CinematicFrameBridge : MonoBehaviour
    {
        private CinematicPresentationState state;
        internal void Bind(CinematicPresentationState value) { state = value; }
        internal void Publish(CinematicFrame frame) { state?.SetFrame(frame); }
    }
}
