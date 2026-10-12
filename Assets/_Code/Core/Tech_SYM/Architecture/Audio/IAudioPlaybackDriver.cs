namespace Cooked.Audio
{
    /// <summary>Borrowed output device. AudioService stops voices, but does not destroy the device.</summary>
    public interface IAudioPlaybackDriver
    {
        void Start(int channel, string cueId, bool loop);
        // 종료 시 먼저 파괴된 빌린 출력만 허용한다. 정상 재생 중 누락은 오류다.
        void Stop(int channel, bool allowDestroyedSource = false);
        void SetGain(int channel, float gain);
        void SetPaused(int channel, bool paused);
        bool IsPlaying(int channel);
    }
}
