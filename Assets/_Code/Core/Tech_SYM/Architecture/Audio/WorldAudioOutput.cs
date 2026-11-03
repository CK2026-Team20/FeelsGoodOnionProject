using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cooked.Audio
{
    /// <summary>중앙 Host가 소유하는 위치 기반 출력. 반복음은 핸들로 반환하고 완료된 단발 출력은 재사용한다.</summary>
    public sealed class WorldAudioOutput : IDisposable
    {
        private sealed class Voice
        {
            public AudioSource Source;
            public Transform Follow;
            public Vector3 Position, LocalOffset;
            public float Gain;
            public int Handle;
            public string CueId;
            public bool Paused;
        }
        private readonly Transform root;
        private readonly GameplayAudioSettings settings;
        private readonly Dictionary<string, AudioClip> clips;
        private readonly List<Voice> voices = new List<Voice>();
        private int sequence;
        private Transform listener;
        private float volume = 1;
        private bool paused, disposed;
        public WorldAudioOutput(Transform root, AudioCueCatalog catalog)
        { this.root = root; clips = catalog.CreateClipMap(); settings = catalog.Gameplay; settings.Validate(); }
        public void SetListener(Transform value) { listener = value; Refresh(); }
        public int Play(AudioCue cue, Vector3 position, Transform follow = null)
        {
            if (disposed) throw new ObjectDisposedException(nameof(WorldAudioOutput));
            Voice voice = voices.Find(v => v.Handle == 0);
            if (voice == null)
            {
                var node = new GameObject("World SFX " + voices.Count); node.transform.SetParent(root, false);
                var source = node.AddComponent<AudioSource>(); source.playOnAwake = false; source.spatialBlend = 1;
                // 거리 감쇠는 카메라 AudioListener가 아니라 PC 위치로 계산한다. Unity는 방향감을 담당한다.
                source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, AnimationCurve.Linear(0, 1, 1, 1));
                source.rolloffMode = AudioRolloffMode.Custom; source.dopplerLevel = 0;
                voice = new Voice { Source = source }; voices.Add(voice);
            }
            voice.Handle = checked(++sequence); voice.Position = position; voice.Follow = follow;
            voice.LocalOffset = follow != null ? follow.InverseTransformPoint(position) : Vector3.zero;
            voice.CueId = cue.Id;
            voice.Gain = cue.Gain; voice.Paused = false;
            voice.Source.clip = clips[cue.Id]; voice.Source.loop = cue.Loop;
            Refresh(voice); voice.Source.Play();
            if (paused) { voice.Source.Pause(); voice.Paused = true; }
            return voice.Handle;
        }
        public int CountActive(string cueId)
        {
            int count = 0;
            foreach (var voice in voices)
                if (voice.Handle != 0 && voice.CueId == cueId && voice.Source != null &&
                    (voice.Paused || voice.Source.isPlaying)) count++;
            return count;
        }
        public void Stop(int handle)
        {
            var voice = voices.Find(v => v.Handle == handle && handle != 0);
            if (voice == null) return;
            if (voice.Source != null) { voice.Source.Stop(); voice.Source.clip = null; }
            voice.CueId = null;
            voice.Handle = 0; voice.Follow = null; voice.Paused = false;
        }
        public void Clear() { foreach (var v in voices) if (v.Handle != 0) Stop(v.Handle); }
        public void SetState(float gain, bool pause)
        {
            volume = gain; paused = pause;
            foreach (var v in voices)
            {
                if (v.Handle == 0 || v.Source == null) continue;
                if (v.Paused != pause) { if (pause) v.Source.Pause(); else v.Source.UnPause(); v.Paused = pause; }
            }
            Refresh();
        }
        public void Tick()
        {
            foreach (var v in voices)
            {
                if (v.Handle == 0) continue;
                if (v.Source == null || (!v.Paused && !v.Source.isPlaying)) Stop(v.Handle);
                else Refresh(v);
            }
        }
        private void Refresh() { foreach (var v in voices) if (v.Handle != 0 && v.Source != null) Refresh(v); }
        private void Refresh(Voice v)
        {
            if (v.Follow != null) v.Position = v.Follow.TransformPoint(v.LocalOffset);
            v.Source.transform.position = v.Position;
            v.Source.volume = listener == null ? 0 : volume * v.Gain * settings.Attenuation(Vector3.Distance(listener.position, v.Position));
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true; Clear();
            foreach (var v in voices) if (v.Source != null)
            { if (Application.isPlaying) UnityEngine.Object.Destroy(v.Source.gameObject); else UnityEngine.Object.DestroyImmediate(v.Source.gameObject); }
            voices.Clear(); listener = null;
        }
    }
}
