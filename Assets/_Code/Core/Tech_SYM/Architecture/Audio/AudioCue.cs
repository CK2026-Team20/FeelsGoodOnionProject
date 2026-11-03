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
        [InspectorName("일반 스테이지 음악")] Stage1Music = 3,
        [InspectorName("추격 음악")] ChaseMusic = 6,
        [InspectorName("엔딩 음악")] EndingMusic = 7,
        [InspectorName("버튼 클릭")] Click = 8,
        [InspectorName("눈물 조각 획득")] Pickup = 9,
        [InspectorName("크기 전환 (작아짐)")] FormChange = 10,
        [InspectorName("눈물 발사")] Tear = 11,
        [InspectorName("체크포인트 저장")] Checkpoint = 12,
        [InspectorName("눈물 스킬 준비 (4→5)")] Unlock = 13,
        [InspectorName("사망")] Death = 14,
        [InspectorName("리스폰")] Respawn = 15,
        [InspectorName("크기 전환 (커짐)")] FormChangeUp = 16,
        [InspectorName("점프")] Jump = 17,
        [InspectorName("착지")] Land = 18,
        [InspectorName("이동 발소리")] Run = 19,
        [InspectorName("피격")] Hit = 20,
        [InspectorName("몬스터 기본음")] MonsterIdle = 21,
        [InspectorName("몬스터 눈물 피격")] MonsterHit = 22,
        [InspectorName("무게 장치 작동")] WeightOn = 23,
        [InspectorName("무게 장치 해제")] WeightOff = 24,
        [InspectorName("붕괴 발판 붕괴")] PlatformBreak = 25,
        [InspectorName("기름 상태")] Oil = 26,
        [InspectorName("장치 상호작용")] Interaction = 27,
        [InspectorName("가스레인지 불")] Fire = 28,
        [InspectorName("냄비 소리")] Pot = 29,
        [InspectorName("몬스터 접촉 피격")] HitMonster = 30,
        [InspectorName("연동 발판 상승")] PlatformSpawn = 31,
        [InspectorName("피해 넉백")] Knockback = 32,
        // 직렬화 번호 호환용. 새 카탈로그는 씬별 stages 연결을 사용한다.
        [InspectorName("이전 2 스테이지 용도")] Stage2Music = 4,
        [InspectorName("이전 3 스테이지 용도")] Stage3Music = 5
    }

    /// <summary>Validated immutable playback settings; the driver owns clip references.</summary>
    public sealed class AudioCue
    {
        public string Id { get; }
        public AudioCueKind Kind { get; }
        public float Gain { get; }
        public bool Loop { get; }
        public int MaxConcurrent { get; }
        public float MinimumInterval { get; }
        public AudioCue(AudioCuePurpose purpose, float gain, int maxConcurrent = 2, float minimumInterval = .08f)
            : this(AudioCueIds.ForPurpose(purpose), AudioCueIds.KindForPurpose(purpose), gain, maxConcurrent, minimumInterval) { }
        public AudioCue(string id, AudioCueKind kind, float gain, int maxConcurrent = 2, float minimumInterval = .08f)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("Cue ID required.", nameof(id));
            if (!Enum.IsDefined(typeof(AudioCueKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            ValidateUnit(gain, nameof(gain));
            if (maxConcurrent < 1) throw new ArgumentOutOfRangeException(nameof(maxConcurrent));
            if (!float.IsFinite(minimumInterval) || minimumInterval < 0) throw new ArgumentOutOfRangeException(nameof(minimumInterval));
            MaxConcurrent = maxConcurrent; MinimumInterval = minimumInterval;
            Id = id; Kind = kind; Gain = gain;
            Loop = kind == AudioCueKind.Music ? id != AudioCueIds.Opening && id != AudioCueIds.Ending : id == AudioCueIds.Fire || id == AudioCueIds.Pot;
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
        public const string Title = "music.title";
        public const string Opening = "music.opening";
        public const string Stage1 = "music.stage1";
        public const string Chase = "music.chase";
        public const string Ending = "music.ending";
        public const string Click = "sfx.ui.click";
        public const string Pickup = "sfx.pickup";
        public const string FormDown = "sfx.form";
        public const string HitMonster = "sfx.hit.monster";
        public const string PlatformSpawn = "sfx.platform.spawn";
        public const string Knockback = "sfx.knockback";
        public const string Tear = "sfx.tear";
        public const string Checkpoint = "sfx.checkpoint";
        public const string Unlock = "sfx.unlock";
        public const string Death = "sfx.death";
        public const string Respawn = "sfx.respawn";
        public const string FormUp = "sfx.form.up";
        public const string Jump = "sfx.jump";
        public const string Land = "sfx.land";
        public const string Run = "sfx.run";
        public const string Hit = "sfx.hit";
        public const string MonsterIdle = "sfx.monster.idle";
        public const string MonsterHit = "sfx.monster.hit";
        public const string WeightOn = "sfx.weight.on";
        public const string WeightOff = "sfx.weight.off";
        public const string PlatformBreak = "sfx.platform.break";
        public const string Oil = "sfx.oil";
        public const string Interaction = "sfx.interaction";
        public const string Fire = "sfx.fire";
        public const string Pot = "sfx.pot";
        public const string Stage2 = "music.stage2", Stage3 = "music.stage3";
        // 기존 외부 코드 호환. 새 호출은 축소·확대를 구분한다.
        public const string Form = FormDown;
        public static string ForPurpose(AudioCuePurpose purpose)
        {
            switch (purpose)
            {
                case AudioCuePurpose.TitleMusic: return Title;
                case AudioCuePurpose.OpeningMusic: return Opening;
                case AudioCuePurpose.Stage1Music: return Stage1;
                case AudioCuePurpose.ChaseMusic: return Chase;
                case AudioCuePurpose.EndingMusic: return Ending;
                case AudioCuePurpose.Click: return Click;
                case AudioCuePurpose.Pickup: return Pickup;
                case AudioCuePurpose.FormChange: return FormDown;
                case AudioCuePurpose.HitMonster: return HitMonster;
                case AudioCuePurpose.PlatformSpawn: return PlatformSpawn;
                case AudioCuePurpose.Knockback: return Knockback;
                case AudioCuePurpose.Tear: return Tear;
                case AudioCuePurpose.Checkpoint: return Checkpoint;
                case AudioCuePurpose.Unlock: return Unlock;
                case AudioCuePurpose.Death: return Death;
                case AudioCuePurpose.Respawn: return Respawn;
                case AudioCuePurpose.FormChangeUp: return FormUp;
                case AudioCuePurpose.Jump: return Jump;
                case AudioCuePurpose.Land: return Land;
                case AudioCuePurpose.Run: return Run;
                case AudioCuePurpose.Hit: return Hit;
                case AudioCuePurpose.MonsterIdle: return MonsterIdle;
                case AudioCuePurpose.MonsterHit: return MonsterHit;
                case AudioCuePurpose.WeightOn: return WeightOn;
                case AudioCuePurpose.WeightOff: return WeightOff;
                case AudioCuePurpose.PlatformBreak: return PlatformBreak;
                case AudioCuePurpose.Oil: return Oil;
                case AudioCuePurpose.Interaction: return Interaction;
                case AudioCuePurpose.Fire: return Fire;
                case AudioCuePurpose.Pot: return Pot;
                case AudioCuePurpose.Stage2Music: return Stage2;
                case AudioCuePurpose.Stage3Music: return Stage3;
                default: throw new ArgumentOutOfRangeException(nameof(purpose));
            }
        }
        public static string ResourceForPurpose(AudioCuePurpose purpose)
        {
            switch (purpose)
            {
                case AudioCuePurpose.TitleMusic: return "Bgm_Title";
                case AudioCuePurpose.OpeningMusic: return "Bgm_Opening";
                case AudioCuePurpose.Stage1Music: return "Bgm_Normal_Stage";
                case AudioCuePurpose.ChaseMusic: return "Bgm_Final_Stage";
                case AudioCuePurpose.EndingMusic: return "Bgm_Ending";
                case AudioCuePurpose.Click: return "Ui_Button";
                case AudioCuePurpose.Pickup: return "Sfx_Teardrop";
                case AudioCuePurpose.FormChange: return "Sfx_Pc_Size_Down";
                case AudioCuePurpose.HitMonster: return "Sfx_Pc_Hit_Monster";
                case AudioCuePurpose.PlatformSpawn: return "Sfx_Platform_Spawn";
                case AudioCuePurpose.Knockback: return "Sfx_Pc_Knockback";
                case AudioCuePurpose.Tear: return "Sfx_Pc_Tear";
                case AudioCuePurpose.Checkpoint: return "Sfx_Checkpoint";
                case AudioCuePurpose.Unlock: return "Sfx_Ability";
                case AudioCuePurpose.Death: return "Sfx_Pc_Death";
                case AudioCuePurpose.Respawn: return "Sfx_Pc_Respawn";
                case AudioCuePurpose.FormChangeUp: return "Sfx_Pc_Size_Up";
                case AudioCuePurpose.Jump: return "Sfx_Pc_Jump";
                case AudioCuePurpose.Land: return "Sfx_Pc_Land";
                case AudioCuePurpose.Run: return "Sfx_Pc_Run";
                case AudioCuePurpose.Hit: return "Sfx_Pc_Hit";
                case AudioCuePurpose.MonsterIdle: return "Sfx_Monster_Idle";
                case AudioCuePurpose.MonsterHit: return "Sfx_Monster_Hit";
                case AudioCuePurpose.WeightOn: return "Sfx_Weight_On";
                case AudioCuePurpose.WeightOff: return "Sfx_Weight_Off";
                case AudioCuePurpose.PlatformBreak: return "Sfx_Platform_Break";
                case AudioCuePurpose.Oil: return "Sfx_Oil";
                case AudioCuePurpose.Interaction: return "Sfx_Interaction";
                case AudioCuePurpose.Fire: return "Sfx_Fire";
                case AudioCuePurpose.Pot: return "Sfx_Pot";
                case AudioCuePurpose.Stage2Music:
                case AudioCuePurpose.Stage3Music: return "Bgm_Normal_Stage";
                default: throw new ArgumentOutOfRangeException(nameof(purpose));
            }
        }
        public static AudioCueKind KindForPurpose(AudioCuePurpose purpose)
        {
            ForPurpose(purpose);
            if ((int)purpose >= 1 && (int)purpose <= 7) return AudioCueKind.Music;
            if (purpose == AudioCuePurpose.Click) return AudioCueKind.UI;
            return purpose == AudioCuePurpose.Death || purpose == AudioCuePurpose.Respawn ? AudioCueKind.Transition : AudioCueKind.World;
        }
    }
}
