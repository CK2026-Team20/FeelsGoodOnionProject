using System;
using System.Collections.Generic;
using Cooked.Authoring;
using UnityEngine;

namespace Cooked.Audio
{
    [CreateAssetMenu(menuName = "Cooked/Audio/Cue Catalog")]
    public sealed class AudioCueCatalog : ScriptableObject
    {
        public const string AssetPath = "Assets/_Scenes/Tech_SYM/Architecture/Audio/Data/PrototypeAudioCatalog.asset";
        [Serializable]
        public sealed class Entry
        {
            // 기존 자산의 이관 값을 보존한다. 새 키/종류는 purpose에서 생성한다.
            [SerializeField, HideInInspector] private string id;
            [SerializeField, HideInInspector] private AudioCueKind kind;
            [SerializeField, InspectorLabel("소리 용도"), Tooltip("이 소리를 재생할 상황을 선택하세요. 음악과 효과음 채널은 용도로 결정됩니다.")]
            private AudioCuePurpose purpose;
            [InspectorLabel("재생 파일"), Tooltip("선택한 용도로 재생할 AudioClip을 연결하세요.")]
            [SerializeField] private AudioClip clip;
            [InspectorLabel("기본 음량"), Tooltip("이 소리의 기본 음량을 조절합니다(0~1). 0은 무음이며 사용자 음량이 추가로 적용됩니다.")]
            [SerializeField, Range(0, 1)] private float gain = 0.5f;
            public AudioCuePurpose Purpose => purpose;
            public string Id => AudioCueIds.ForPurpose(purpose);
            public AudioCueKind Kind => AudioCueIds.KindForPurpose(purpose);
            public AudioClip Clip => clip;
            public float Gain => gain;
            public Entry(AudioCuePurpose purpose, AudioClip clip, float gain)
            {
                var cue = new AudioCue(purpose, gain);
                this.purpose = purpose; this.clip = clip; this.gain = gain; id = cue.Id; kind = cue.Kind;
            }
            // 호환 종료 조건: T11의 기존 id/kind 이관 및 T12의 Builder 소비 제거.
            public Entry(string id, AudioCueKind kind, AudioClip clip, float gain)
                : this(AudioCueIds.RequirePurpose(id), clip, gain)
            {
                if (kind != Kind) throw new ArgumentException("소리 용도와 기존 종류가 다릅니다: " + id, nameof(kind));
            }

            public AudioCue CreateCue(string fieldName)
            {
                try
                {
                    var cue = new AudioCue(purpose, gain);
                    if (clip == null) throw new InvalidOperationException("AudioClip을 연결하세요.");
                    return cue;
                }
                catch (ArgumentException error) { throw new InvalidOperationException(fieldName + " (" + purpose + "): " + error.Message, error); }
                catch (InvalidOperationException error) { throw new InvalidOperationException(fieldName + " (" + purpose + "): " + error.Message, error); }
            }
#if UNITY_EDITOR
            internal void RefreshGeneratedValues() { id = Id; kind = Kind; }
#endif
        }
        [InspectorLabel("소리 목록"), Tooltip("게임에 사용할 소리 용도와 재생 파일을 연결하세요. 같은 용도는 한 번만 등록합니다.")]
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

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
        public void RefreshAuthoring()
        {
            CreateSettings();
            foreach (var entry in entries) entry.RefreshGeneratedValues();
        }

        private void OnValidate()
        {
            try { RefreshAuthoring(); }
            catch (Exception error) { Debug.LogError(name + " 작성 오류: " + error.Message, this); }
        }

        public void SetAuthoringEntries(Entry[] value)
        { entries = value == null ? throw new ArgumentNullException(nameof(value)) : (Entry[])value.Clone(); }
#endif
    }
}
