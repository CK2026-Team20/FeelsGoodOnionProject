#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace Cooked.Audio.Editor
{
    public static class AudioPrefabBuilder
    {
        public const string AssetRoot = "Assets/_Scenes/Tech_SYM/Architecture/Audio";
        public const string CatalogPath = AssetRoot + "/Data/PrototypeAudioCatalog.asset";
        public const string PrefabPath = AssetRoot + "/Prefabs/AudioRoot.prefab";
        private static readonly string[] Music = { AudioCueIds.Title, AudioCueIds.Opening, AudioCueIds.Stage1,
            AudioCueIds.Stage2, AudioCueIds.Stage3, AudioCueIds.Chase, AudioCueIds.Ending };
        private static readonly string[] Sfx = { AudioCueIds.Click, AudioCueIds.Pickup, AudioCueIds.Form,
            AudioCueIds.Tear, AudioCueIds.Shell, AudioCueIds.Checkpoint, AudioCueIds.Unlock, AudioCueIds.Death, AudioCueIds.Escape };

        [MenuItem("Cooked/Audio/Build Prototype Audio Assets")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Build audio in Edit Mode.");
            // Preflight all files before creating or replacing owned assets.
            var entries = new AudioCueCatalog.Entry[Music.Length + Sfx.Length];
            for (int i = 0; i < Music.Length; i++)
                entries[i] = Load(Music[i], AudioCueKind.Music, 0.6f);
            for (int i = 0; i < Sfx.Length; i++)
            {
                string id = Sfx[i];
                var kind = id == AudioCueIds.Click ? AudioCueKind.UI :
                    id == AudioCueIds.Death || id == AudioCueIds.Escape ? AudioCueKind.Transition : AudioCueKind.World;
                entries[Music.Length + i] = Load(id, kind, kind == AudioCueKind.World ? 0.35f : 0.5f);
            }
            EnsureFolder(AssetRoot + "/Data"); EnsureFolder(AssetRoot + "/Prefabs");
            var catalog = AssetDatabase.LoadAssetAtPath<AudioCueCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AudioCueCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }
            catalog.SetAuthoringEntries(entries); catalog.CreateSettings(); EditorUtility.SetDirty(catalog);
            var root = new GameObject("AudioRoot");
            try
            {
                var host = root.AddComponent<AudioRuntimeHost>();
                var sfxRoot = new GameObject("SFX"); sfxRoot.transform.SetParent(root.transform, false);
                var sources = new AudioSource[AudioService.ChannelCount];
                for (int i = 0; i < sources.Length; i++)
                {
                    string label = i < 2 ? "Music" + (i == 0 ? "A" : "B") :
                        i == AudioService.UiChannel ? "UiVoice" : i == AudioService.TransitionChannel ? "TransitionVoice" : "WorldVoice" + (i - 3).ToString("00");
                    var child = new GameObject(label); child.transform.SetParent(sfxRoot.transform, false);
                    var source = child.AddComponent<AudioSource>();
                    source.playOnAwake = false; source.spatialBlend = 0; source.volume = 0;
                    source.dopplerLevel = 0; source.priority = i < 4 ? 64 : 128;
                    sources[i] = source;
                }
                host.ConfigureForAuthoring(catalog, sources);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();
                Debug.Log("Cooked Audio built: 16 clips, 10 sources, no AudioListener. " + PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static AudioCueCatalog.Entry Load(string id, AudioCueKind kind, float gain)
        {
            string path = AssetRoot + "/Clips/" + id + ".wav";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null) throw new InvalidOperationException("Missing prototype WAV: " + path);
            var importer = (AudioImporter)AssetImporter.GetAtPath(path);
            var settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = true;
            importer.defaultSampleSettings = settings; importer.forceToMono = true;
            importer.loadInBackground = false; importer.SaveAndReimport();
            clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            return new AudioCueCatalog.Entry(id, kind, clip, gain);
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int split = path.LastIndexOf('/'); string parent = path.Substring(0, split);
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, path.Substring(split + 1));
        }
    }
}
#endif
