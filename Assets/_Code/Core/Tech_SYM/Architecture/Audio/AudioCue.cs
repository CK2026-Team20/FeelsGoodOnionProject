using System;
using System.Collections.Generic;

namespace Cooked.Audio
{
    public enum AudioCueKind { Music, World, UI, Transition }

    /// <summary>Validated immutable playback settings; the driver owns clip references.</summary>
    public sealed class AudioCue
    {
        public string Id { get; }
        public AudioCueKind Kind { get; }
        public float Gain { get; }
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
            Tear = "sfx.tear", Shell = "sfx.shell", Checkpoint = "sfx.checkpoint", Unlock = "sfx.unlock",
            Death = "sfx.death", Escape = "sfx.escape";
    }
}
