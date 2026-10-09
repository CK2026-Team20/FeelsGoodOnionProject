using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cooked.Audio
{
    public enum AudioCueKind
    {
        [InspectorName("음악")] Music = 0,
        [InspectorName("월드 효과음")] World = 1,
        [InspectorName("화면 효과음")] UI = 2,
        [InspectorName("전환 효과음")] Transition = 3
    }

    // 기존 AudioCueKind와 Cue ID 의미는 유지한다. 새 용도의 번호도 이후 재정렬하지 않는다.
    public enum AudioCuePurpose
    {
        [InspectorName("선택 필요")] None = 0,
        [InspectorName("타이틀 음악")] TitleMusic = 1,
        [InspectorName("오프닝 음악")] OpeningMusic = 2,
        [InspectorName("프로토타입 음악")] Stage1Music = 3,
        [InspectorName("2 스테이지 음악")] Stage2Music = 4,
        [InspectorName("3 스테이지 음악")] Stage3Music = 5,
        [InspectorName("추격 음악")] ChaseMusic = 6,
        [InspectorName("엔딩 음악")] EndingMusic = 7,
        [InspectorName("버튼 클릭")] Click = 8,
        [InspectorName("조각 수집")] Pickup = 9,
        [InspectorName("크기 전환")] FormChange = 10,
        [InspectorName("눈물 발동")] Tear = 11,
        [InspectorName("체크포인트 저장")] Checkpoint = 12,
        [InspectorName("능력 해금")] Unlock = 13,
        [InspectorName("사망 전환")] Death = 14
    }

    /// <summary>Validated immutable playback settings; the driver owns clip references.</summary>
    public sealed class AudioCue
    {
        public string Id { get; }
        public AudioCueKind Kind { get; }
        public float Gain { get; }
        public AudioCue(AudioCuePurpose purpose, float gain)
            : this(AudioCueIds.ForPurpose(purpose), AudioCueIds.KindForPurpose(purpose), gain) { }
        public AudioCue(string id, AudioCueKind kind, float gain)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Cue ID required.", nameof(id));
            if (!Enum.IsDefined(typeof(AudioCueKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            ValidateUnit(gain, nameof(gain));
            Id = id; Kind = kind; Gain = gain;
        }

        internal static void ValidateUnit(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0 || value > 1)
                throw new ArgumentOutOfRangeException(name, "Expected a finite value between zero and one.");
        }

        internal static Dictionary<string, AudioCue> Index(IEnumerable<AudioCue> cues)
        {
            if (cues == null) throw new ArgumentNullException(nameof(cues));
            var result = new Dictionary<string, AudioCue>(StringComparer.Ordinal);
            foreach (var cue in cues)
            {
                if (cue == null) throw new ArgumentException("Null audio cue.", nameof(cues));
                if (result.ContainsKey(cue.Id)) throw new ArgumentException("Duplicate audio cue: " + cue.Id);
                result.Add(cue.Id, cue);
            }
            if (result.Count == 0) throw new ArgumentException("Audio catalog is empty.", nameof(cues));
            return result;
        }
    }

    public static class AudioCueIds
    {
        public const string Title = "music.title", Opening = "music.opening", Stage1 = "music.stage1",
            Stage2 = "music.stage2", Stage3 = "music.stage3", Chase = "music.chase", Ending = "music.ending";
        public const string Click = "sfx.ui.click", Pickup = "sfx.pickup", Form = "sfx.form",
            Tear = "sfx.tear", Checkpoint = "sfx.checkpoint", Unlock = "sfx.unlock",
            Death = "sfx.death";

        public static string ForPurpose(AudioCuePurpose purpose)
        {
            switch (purpose)
            {
                case AudioCuePurpose.TitleMusic: return Title;
                case AudioCuePurpose.OpeningMusic: return Opening;
                case AudioCuePurpose.Stage1Music: return Stage1;
                case AudioCuePurpose.Stage2Music: return Stage2;
                case AudioCuePurpose.Stage3Music: return Stage3;
                case AudioCuePurpose.ChaseMusic: return Chase;
                case AudioCuePurpose.EndingMusic: return Ending;
                case AudioCuePurpose.Click: return Click;
                case AudioCuePurpose.Pickup: return Pickup;
                case AudioCuePurpose.FormChange: return Form;
                case AudioCuePurpose.Tear: return Tear;
                case AudioCuePurpose.Checkpoint: return Checkpoint;
                case AudioCuePurpose.Unlock: return Unlock;
                case AudioCuePurpose.Death: return Death;
                default: throw new ArgumentOutOfRangeException(nameof(purpose), purpose, "소리 용도를 선택하세요.");
            }
        }

        public static AudioCueKind KindForPurpose(AudioCuePurpose purpose)
        {
            ForPurpose(purpose);
            if (purpose >= AudioCuePurpose.TitleMusic && purpose <= AudioCuePurpose.EndingMusic) return AudioCueKind.Music;
            if (purpose == AudioCuePurpose.Click) return AudioCueKind.UI;
            return purpose == AudioCuePurpose.Death ? AudioCueKind.Transition : AudioCueKind.World;
        }

        // 기존 Builder 및 T11 직렬화 이관에만 사용한다. 새 Inspector는 문자열 키를 받지 않는다.
        public static AudioCuePurpose RequirePurpose(string id)
        {
            foreach (AudioCuePurpose purpose in Enum.GetValues(typeof(AudioCuePurpose)))
                if (purpose != AudioCuePurpose.None && ForPurpose(purpose) == id) return purpose;
            throw new ArgumentException("기존 소리 ID에 대응하는 용도가 없습니다: " + id, nameof(id));
        }
    }
}
