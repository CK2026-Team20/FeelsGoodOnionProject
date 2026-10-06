using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cooked.Audio
{
    [CreateAssetMenu(menuName = "Cooked/Audio/Cue Catalog")]
    public sealed class AudioCueCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [Tooltip("소리를 요청할 때 사용하는 고유 ID입니다. 호출 코드의 이름과 정확히 일치해야 하며 중복하면 등록에 실패합니다.")]
            [SerializeField] private string id;
            [Tooltip("음악 또는 효과음 분류입니다. 선택한 분류의 음량 설정이 이 소리에 적용됩니다.")]
            [SerializeField] private AudioCueKind kind;
            [Tooltip("이 항목에서 재생할 AudioClip 파일입니다. 비워 두면 소리 목록 등록에 실패합니다.")]
            [SerializeField] private AudioClip clip;
            [Tooltip("이 소리의 기본 음량 배율입니다(0~1). 0은 무음, 1은 최대 기본 음량입니다. 사용자 음량 설정이 추가로 적용됩니다.")]
            [SerializeField, Range(0, 1)] private float gain = 0.5f;
            public string Id => id;
            public AudioCueKind Kind => kind;
            public AudioClip Clip => clip;
            public float Gain => gain;
            public Entry(string id, AudioCueKind kind, AudioClip clip, float gain)
            { this.id = id; this.kind = kind; this.clip = clip; this.gain = gain; }
        }
        [Tooltip("게임에서 사용할 소리 목록입니다. 각 항목에 고유 ID와 재생 파일을 연결하세요. 빈 목록이면 재생할 소리가 없습니다.")]
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public AudioCue[] CreateSettings()
        {
            if (entries == null) throw new InvalidOperationException("Audio catalog entries missing: " + name);
            var result = new AudioCue[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry == null || entry.Clip == null) throw new InvalidOperationException("Audio clip missing at catalog row " + i);
                result[i] = new AudioCue(entry.Id, entry.Kind, entry.Gain);
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
        public void SetAuthoringEntries(Entry[] value)
        { entries = value == null ? throw new ArgumentNullException(nameof(value)) : (Entry[])value.Clone(); }
#endif
    }
}
