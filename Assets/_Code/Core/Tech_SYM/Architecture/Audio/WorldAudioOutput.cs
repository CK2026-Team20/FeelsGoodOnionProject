using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cooked.Audio
{
    /// <summary>중앙 Host의 2D/위치 SFX 풀. 음악 채널을 빌리거나 재생 중인 슬롯을 선점하지 않는다.</summary>
    public sealed class WorldAudioOutput : ISfxOutput
    {
        private sealed class Slot
        {
            public AudioSource Source;
            public int Token;
            public string CueId;
            public bool Paused;
        }
        private readonly Transform root;
        private readonly Dictionary<string, AudioClip> clips;
        private readonly List<Slot> slots = new List<Slot>();
        private readonly Dictionary<int, Slot> activeSlots = new Dictionary<int, Slot>();
        private int sequence;
        private bool disposed;
        public GameplayAudioSettings Gameplay { get; }
        public int ActiveSourceCount { get { int n = 0; foreach (var s in slots) if (s.Token != 0 && s.Source != null) n++; return n; } }
        public int PooledSourceCount { get { int n = 0; foreach (var s in slots) if (s.Token == 0 && s.Source != null) n++; return n; } }
        public int CreatedSourceCount => ActiveSourceCount + PooledSourceCount;

        public WorldAudioOutput(Transform root, AudioCueCatalog catalog)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
            this.root = root;
            clips = catalog.CreateClipMap(); Gameplay = catalog.Gameplay; Gameplay.Validate();
        }
        public float ClipLength(string cueId)
        {
            RequireAlive();
            if (!clips.TryGetValue(cueId, out var clip) || clip == null || !float.IsFinite(clip.length) || clip.length <= 0)
                throw new InvalidOperationException("재생 가능한 효과음 파일이 없습니다: " + cueId);
            return clip.length;
        }
        public int Start(AudioCue cue, bool spatial, Vector3 position, float gain, float seekSeconds, bool paused)
        {
            RequireAlive();
            if (cue == null || cue.Kind == AudioCueKind.Music) throw new ArgumentException("효과음 설정이 필요합니다.", nameof(cue));
            float length = ClipLength(cue.Id);
            AudioCue.ValidateUnit(gain, nameof(gain));
            if (!float.IsFinite(seekSeconds) || seekSeconds < 0 || seekSeconds >= length) throw new ArgumentOutOfRangeException(nameof(seekSeconds));
            if (root == null) throw new InvalidOperationException("효과음 출력의 소유자가 파괴되었습니다.");
            int token = checked(++sequence);
            var slot = slots.Find(s => s.Token == 0 && s.Source != null);
            if (slot == null)
            {
                var node = new GameObject("Pooled SFX " + slots.Count);
                node.transform.SetParent(root, false);
                var source = node.AddComponent<AudioSource>();
                source.playOnAwake = false; source.dopplerLevel = 0; source.ignoreListenerPause = false;
                source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, AnimationCurve.Linear(0, 1, 1, 1));
                source.rolloffMode = AudioRolloffMode.Custom;
                slot = new Slot { Source = source }; slots.Add(slot);
            }
            activeSlots.Add(token, slot);
            slot.Token = token; slot.CueId = cue.Id; slot.Paused = paused;
            try
            {
                var source = slot.Source;
                source.transform.position = position; source.spatialBlend = spatial ? 1 : 0;
                source.priority = cue.PlaybackPolicy == SfxPlaybackPolicy.AlwaysPlay ? AudioOutputPriority.AlwaysPlay :
                    cue.PlaybackPolicy == SfxPlaybackPolicy.VirtualLoop ? AudioOutputPriority.VirtualLoop : AudioOutputPriority.LimitedOneShot;
                source.volume = gain; source.clip = clips[cue.Id]; source.loop = cue.Loop;
                // 압축 Clip의 seek 정밀도는 Unity import 설정에 따른다.
                source.time = seekSeconds; source.Play();
                if (paused) source.Pause();
                return token;
            }
            catch { Stop(token); throw; }
        }
        public bool IsActive(int token)
        {
            var slot = Find(token);
            return slot != null && slot.Source != null && (slot.Paused || slot.Source.isPlaying);
        }
        public int CountActive(string cueId)
        {
            int count = 0;
            foreach (var slot in slots) if (slot.CueId == cueId && IsActive(slot.Token)) count++;
            return count;
        }
        public void SetGain(int token, float gain)
        { AudioCue.ValidateUnit(gain, nameof(gain)); var slot = Find(token); if (slot?.Source != null) slot.Source.volume = gain; }
        public void SetPosition(int token, Vector3 position)
        { var slot = Find(token); if (slot?.Source != null) slot.Source.transform.position = position; }
        public void SetPaused(int token, bool paused)
        {
            var slot = Find(token);
            if (slot?.Source == null || slot.Paused == paused) return;
            if (paused) slot.Source.Pause(); else slot.Source.UnPause();
            slot.Paused = paused;
        }
        public void Stop(int token)
        {
            var slot = Find(token);
            if (slot == null) return;
            if (slot.Source != null) { slot.Source.Stop(); slot.Source.clip = null; slot.Source.volume = 0; slot.Source.loop = false; }
            // 실제 반환에 성공한 뒤에만 토큰을 제거한다. Stop 실패는 같은 토큰으로 재시도한다.
            activeSlots.Remove(token);
            slot.Token = 0; slot.CueId = null; slot.Paused = false;
        }
        public void Clear() { foreach (var slot in slots) Stop(slot.Token); }
        private Slot Find(int token) => activeSlots.TryGetValue(token, out var slot) ? slot : null;
        private void RequireAlive() { if (disposed) throw new ObjectDisposedException(nameof(WorldAudioOutput)); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true; Clear();
            foreach (var slot in slots) if (slot.Source != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(slot.Source.gameObject);
                else UnityEngine.Object.DestroyImmediate(slot.Source.gameObject);
            }
            slots.Clear(); activeSlots.Clear();
        }
    }
}
