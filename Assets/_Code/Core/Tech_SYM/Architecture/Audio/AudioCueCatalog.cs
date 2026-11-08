using System;
using System.Collections.Generic;
using Cooked.Authoring;
using Cooked.Contracts;
using UnityEngine;

namespace Cooked.Audio
{
    [CreateAssetMenu(menuName = "Cooked/Audio/Cue Catalog")]
    public sealed class AudioCueCatalog : ScriptableObject
    {
        [SerializeField, InspectorLabel("게임 효과음 설정"), Tooltip("반복 간격과 PC 기준 거리 감쇠를 조절합니다.")]
        private GameplayAudioSettings gameplay = new GameplayAudioSettings();
        public GameplayAudioSettings Gameplay => gameplay;
        public const string AssetPath = "Assets/_Data/Tech_SYM/Audio/AudioCueCatalog.asset";
        [Serializable]
        public sealed class Entry
        {
            [SerializeField, InspectorLabel("소리 용도"), Tooltip("이 소리를 재생할 상황을 선택하세요. 음악과 효과음 채널은 용도로 결정됩니다.")]
            private AudioCuePurpose purpose;
            [InspectorLabel("재생 파일"), Tooltip("선택한 용도로 재생할 AudioClip을 연결하세요.")]
            [SerializeField] private AudioClip clip;
            [InspectorLabel("기본 음량"), Tooltip("이 소리의 기본 음량을 조절합니다(0~1). 0은 무음이며 사용자 음량이 추가로 적용됩니다.")]
            [SerializeField, Range(0, 1)] private float gain = 0.5f;
            [SerializeField, Min(1), InspectorLabel("최대 동시 재생 수"), Tooltip("동일 효과음 ID가 전체 출력에서 동시에 재생될 수 있는 개수입니다. 최소 1이며 음악에는 적용하지 않습니다.")]
            private int maxConcurrent = 2;
            [SerializeField, Min(0), InspectorLabel("최소 재생 간격"), Tooltip("동일 효과음의 재생 수락 후 추가 요청을 무시하는 시간입니다(초). 0은 간격 제한 없음이며 음악에는 적용하지 않습니다. 초과 요청은 예약하지 않습니다.")]
            private float minimumInterval = .08f;
            [SerializeField, InspectorLabel("효과음 재생 정책"), Tooltip("제한 단발음은 동일 ID의 간격과 동시 수를 제한합니다. 요청마다 재생은 제한 없이 독립 출력하며, 거리 기반 반복음은 PC 청취 범위에서만 출력을 사용합니다. 음악에는 적용하지 않습니다.")]
            private SfxPlaybackPolicy playbackPolicy;
            public AudioCuePurpose Purpose => purpose;
            public string ResourceId => AudioCueIds.ResourceForPurpose(purpose);
            public string Id => AudioCueIds.ForPurpose(purpose);
            public AudioCueKind Kind => AudioCueIds.KindForPurpose(purpose);
            public AudioClip Clip => clip;
            public float Gain => gain;
            public SfxPlaybackPolicy PlaybackPolicy => playbackPolicy;
            public Entry(AudioCuePurpose purpose, AudioClip clip, float gain)
            {
                playbackPolicy = AudioCueIds.DefaultPolicy(AudioCueIds.ForPurpose(purpose));
                var cue = new AudioCue(purpose, gain, maxConcurrent, minimumInterval, playbackPolicy);
                this.purpose = purpose; this.clip = clip; this.gain = gain;
            }

            public AudioCue CreateCue(string fieldName)
            {
                try
                {
                    var cue = new AudioCue(purpose, gain, maxConcurrent, minimumInterval, playbackPolicy);
                    if (clip == null) throw new InvalidOperationException("AudioClip을 연결하세요.");
                    if (!float.IsFinite(clip.length) || clip.length <= 0 || clip.samples <= 0 || clip.frequency <= 0 || clip.channels <= 0)
                        throw new InvalidOperationException("유효한 길이와 샘플을 가진 AudioClip을 연결하세요.");
                    return cue;
                }
                catch (ArgumentException error) { throw new InvalidOperationException(fieldName + " (" + purpose + "): " + error.Message, error); }
                catch (InvalidOperationException error) { throw new InvalidOperationException(fieldName + " (" + purpose + "): " + error.Message, error); }
            }
        }
        [InspectorLabel("소리 목록"), Tooltip("게임에 사용할 소리 용도와 재생 파일을 연결하세요. 같은 용도는 한 번만 등록합니다.")]
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        [Serializable]
        public sealed class StageMusic
        {
            [SerializeField, InspectorLabel("스테이지 씬"), Tooltip("일반 음악을 사용할 스테이지 SceneAsset을 연결하세요.")]
            private SceneReference scene = new SceneReference();
            [SerializeField, InspectorLabel("일반 음악"), Tooltip("이 스테이지에서 반복 재생할 음악을 연결하세요.")]
            private AudioClip clip;
            [SerializeField, Range(0,1), InspectorLabel("기본 음량"), Tooltip("사용자 음악 음량에 곱하는 값입니다. 0은 무음입니다.")]
            private float gain = .6f;
            [SerializeField, Min(0), InspectorLabel("전환 시간"), Tooltip("이전 음악이 사라지는 시간입니다(초). 0은 즉시 전환합니다.")]
            private float transitionSeconds = .3f;
            public string StageId => scene.StageId;
            public AudioClip Clip => clip;
            public float Gain => gain;
            public float TransitionSeconds => transitionSeconds;
            public void Validate()
            {
                _ = scene.ResolvePath("음악 스테이지");
                if (clip == null) throw new InvalidOperationException("스테이지 음악을 연결하세요: " + StageId);
                AudioCue.ValidateUnit(gain, nameof(gain));
                if (!float.IsFinite(transitionSeconds) || transitionSeconds < 0)
                    throw new InvalidOperationException("음악 전환 시간은 0 이상의 유한값이어야 합니다.");
            }
        }
        [SerializeField, InspectorLabel("스테이지 음악"), Tooltip("씬별 일반 BGM을 미리 연결하세요. 구간 트리거는 곡을 선택하지 않습니다.")]
        private StageMusic[] stages = Array.Empty<StageMusic>();
        public bool HasStage(string id)
        { foreach (var item in stages) if (item != null && item.StageId == id) return true; return false; }
        public StageMusic RequireStage(string id)
        {
            StageMusic found = null;
            foreach (var item in stages)
            {
                if (item == null) throw new InvalidOperationException("빈 스테이지 음악 항목입니다.");
                item.Validate();
                if (item.StageId != id) continue;
                if (found != null) throw new InvalidOperationException("스테이지 음악 중복: " + id);
                found = item;
            }
            return found ?? throw new InvalidOperationException("Bootstrapper 소리 목록에 스테이지 음악을 연결하세요: " + id);
        }
        public AudioCue[] CreateSettings()
        {
            if (entries == null || entries.Length == 0) throw new InvalidOperationException(name + " / 소리 목록이 비어 있습니다.");
            var result = new AudioCue[entries.Length];
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                string field = name + " / entries[" + i + "]";
                if (entry == null) throw new InvalidOperationException(field + ": 소리 항목이 없습니다.");
                result[i] = entry.CreateCue(field);
                if (!ids.Add(result[i].Id)) throw new InvalidOperationException(field + ": 소리 용도 중복: " + entry.Purpose);
            }
            AudioCue.Index(result);
            return result;
        }
        public Dictionary<string, AudioClip> CreateClipMap()
        {
            CreateSettings();
            var result = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
            foreach (var entry in entries) result.Add(entry.Id, entry.Clip);
            return result;
        }
#if UNITY_EDITOR

        private void OnValidate()
        {
            try { CreateSettings(); }
            catch (Exception error) { Debug.LogError(name + " 작성 오류: " + error.Message, this); }
        }

#endif
    }
}
