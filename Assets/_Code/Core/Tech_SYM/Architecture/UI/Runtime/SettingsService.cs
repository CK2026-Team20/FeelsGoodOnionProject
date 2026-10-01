using System;
using Cooked.Contracts;
using UnityEngine;
namespace Cooked.UI
{
    public interface ISettingsStore { float Read(string key, float fallback); void Write(string key, float value); void Save(); }
    public sealed class PlayerPrefsSettingsStore : ISettingsStore
    {
        public float Read(string key, float fallback) => PlayerPrefs.GetFloat(key, fallback);
        public void Write(string key, float value) => PlayerPrefs.SetFloat(key, value);
        public void Save() => PlayerPrefs.Save();
    }
    public sealed class SettingsService : IDisposable
    {
        public const string Prefix = "Cooked.TechSYM.UI.v1.";
        private readonly ISettingsStore store;
        private readonly IAudioService audio;
        private bool dirty, disposed;
        public float Master { get; private set; }
        public float Music { get; private set; }
        public float Sfx { get; private set; }
        public event Action Changed;
        public SettingsService(ISettingsStore store, IAudioService audio)
        {
            this.store = store ?? throw new ArgumentNullException(nameof(store));
            this.audio = audio ?? throw new ArgumentNullException(nameof(audio));
            Master = Load("Master", 1); Music = Load("Music", .7f); Sfx = Load("Sfx", 1);
            audio.SetVolumes(Master, Music, Sfx);
        }
        private float Load(string key, float fallback)
        { float value = store.Read(Prefix + key, fallback); return Valid(value) ? value : fallback; }
        private static bool Valid(float v) => !float.IsNaN(v) && !float.IsInfinity(v) && v >= 0 && v <= 1;
        public void SetVolumes(float master, float music, float sfx)
        {
            if (disposed) return;
            if (!Valid(master) || !Valid(music) || !Valid(sfx)) throw new ArgumentOutOfRangeException(nameof(master));
            if (Master == master && Music == music && Sfx == sfx) return;
            Master = master; Music = music; Sfx = sfx; dirty = true;
            audio.SetVolumes(master, music, sfx); Changed?.Invoke();
        }
        public void Save()
        {
            if (!dirty || disposed) return;
            store.Write(Prefix + "Master", Master); store.Write(Prefix + "Music", Music); store.Write(Prefix + "Sfx", Sfx);
            store.Save(); dirty = false;
        }
        public void Dispose() { if (disposed) return; try { Save(); } finally { disposed = true; Changed = null; } }
    }
}
