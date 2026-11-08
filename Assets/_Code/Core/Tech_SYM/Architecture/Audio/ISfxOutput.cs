using System;
using UnityEngine;

namespace Cooked.Audio
{
    /// <summary>Unity의 음성 한계 내 선택 우선순위(작을수록 높음). 무제한 실제 음성 수를 보장하지 않는다.</summary>
    public static class AudioOutputPriority
    {
        public const int Music = 32;
        public const int AlwaysPlay = 64;
        public const int LimitedOneShot = 128;
        public const int VirtualLoop = 192;
    }

    /// <summary>실제 재생 슬롯만 소유한다. 정책과 가상 반복음의 논리 수명은 AudioService 소유다.</summary>
    public interface ISfxOutput : IDisposable
    {
        int ActiveSourceCount { get; }
        int PooledSourceCount { get; }
        float ClipLength(string cueId);
        int Start(AudioCue cue, bool spatial, Vector3 position, float gain, float seekSeconds, bool paused);
        bool IsActive(int token);
        void SetGain(int token, float gain);
        void SetPosition(int token, Vector3 position);
        void SetPaused(int token, bool paused);
        void Stop(int token);
    }
}
