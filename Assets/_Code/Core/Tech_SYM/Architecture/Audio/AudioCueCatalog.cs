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
            [SerializeField] private string id;
            [SerializeField] private AudioCueKind kind;
            [SerializeField] private AudioClip clip;
            [SerializeField, Range(0, 1)] private float gain = 0.5f;
            public string Id => id;
            public AudioCueKind Kind => kind;
            public AudioClip Clip => clip;
            public float Gain => gain;
            public Entry(string id, AudioCueKind kind, AudioClip clip, float gain)
            { this.id = id; this.kind = kind; this.clip = clip; this.gain = gain; }
        }
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
