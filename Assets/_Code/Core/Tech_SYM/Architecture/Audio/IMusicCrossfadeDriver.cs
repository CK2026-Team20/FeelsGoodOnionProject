using System;

namespace Cooked.Audio
{
    /// <summary>One owned two-channel envelope transition. Cancel never reports completion.</summary>
    public interface IMusicCrossfadeDriver : IDisposable
    {
        void Start(float fromA, float toA, float fromB, float toB, float seconds,
            bool paused, Action<float, float> apply, Action completed);
        void SetPaused(bool paused);
        void Cancel();
    }
}
