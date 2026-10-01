namespace Cooked.Audio
{
    /// <summary>Borrowed output device. AudioService stops voices, but does not destroy the device.</summary>
    public interface IAudioPlaybackDriver
    {
        void Start(int channel, string cueId, bool loop);
        void Stop(int channel);
        void SetGain(int channel, float gain);
        void SetPaused(int channel, bool paused);
        bool IsPlaying(int channel);
    }
}
