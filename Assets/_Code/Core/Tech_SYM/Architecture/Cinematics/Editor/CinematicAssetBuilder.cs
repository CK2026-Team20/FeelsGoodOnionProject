using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Cooked.Cinematics.Editor
{
    public static class CinematicAssetBuilder
    {
        public const string AssetRoot = "Assets/_Scenes/Tech_SYM/Architecture/Cinematics";
        public const string PrefabPath = AssetRoot + "/Prefabs/CinematicPresentation.prefab";
        public const string CatalogPath = AssetRoot + "/Data/CinematicCatalog.asset";
        public const string KoreanFontPath = "Assets/_Scenes/Tech_SYM/Architecture/UI/Fonts/CookedKorean SDF.asset";

        [MenuItem("Tools/Cooked/Cinematics/Build Assets")]
        public static void BuildAll() { BuildAll(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath)); }

        public static void BuildAll(TMP_FontAsset font)
        {
            if (font == null || AssetDatabase.GetAssetPath(font) != KoreanFontPath)
                throw new InvalidOperationException("The canonical UI-owned Korean font dependency is required: " + KoreanFontPath);
            foreach (string folder in new[] { "Artwork", "Timelines", "Data", "Prefabs" })
                Directory.CreateDirectory(AssetRoot + "/" + folder);
            AssetDatabase.Refresh();
            // Only new, owned art importers are configured. Originals are never touched.
            foreach (string prefix in new[] { "Opening", "Ending" })
                for (int i = 1; i <= 4; i++)
                {
                    string path = AssetRoot + "/Artwork/" + prefix + "_0" + i + ".png";
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer == null) throw new FileNotFoundException("Generated artwork is required, no placeholder substitution.", path);
                    importer.textureType = TextureImporterType.Default;
                    importer.mipmapEnabled = false;
                    importer.wrapMode = TextureWrapMode.Clamp;
                    importer.alphaSource = TextureImporterAlphaSource.None;
                    importer.maxTextureSize = 2048;
                    importer.textureCompression = TextureImporterCompression.CompressedHQ;
                    importer.SaveAndReimport();
                }
            TimelineAsset opening = BuildTimeline("Opening");
            TimelineAsset ending = BuildTimeline("Ending");
            var catalog = AssetDatabase.LoadAssetAtPath<CinematicCatalog>(CatalogPath);
            if (catalog == null) { catalog = ScriptableObject.CreateInstance<CinematicCatalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath); }
            SetReference(catalog, "opening", opening);
            SetReference(catalog, "ending", ending);
            BuildPrefab(catalog, font);
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
        }

        private static TimelineAsset BuildTimeline(string name)
        {
            string path = AssetRoot + "/Timelines/" + name + ".playable";
            var timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
            if (timeline == null) { timeline = ScriptableObject.CreateInstance<TimelineAsset>(); AssetDatabase.CreateAsset(timeline, path); }
            foreach (var old in timeline.GetRootTracks().ToArray()) timeline.DeleteTrack(old);
            timeline.editorSettings.frameRate = 60;
            timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
            timeline.fixedDuration = 11.1;
            var track = timeline.CreateTrack<CinematicArtworkTrack>(null, name + " - Four Story Cuts");
            for (int i = 0; i < 4; i++)
            {
                TimelineClip clip = track.CreateClip<CinematicArtworkClip>();
                clip.displayName = name + "_0" + (i + 1);
                clip.start = i * 2.7;
                clip.duration = 3;
                clip.easeInDuration = 0;
                clip.easeOutDuration = 0;
                var artwork = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetRoot + "/Artwork/" + clip.displayName + ".png");
                SetReference(clip.asset, "artwork", artwork);
                var serialized = new SerializedObject(clip.asset);
                serialized.FindProperty("endScale").floatValue = 1.04f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            EditorUtility.SetDirty(track);
            EditorUtility.SetDirty(timeline);
            return timeline;
        }

        private static void BuildPrefab(CinematicCatalog catalog, TMP_FontAsset font)
        {
            var root = new GameObject("CinematicPresentation", typeof(RectTransform), typeof(CinematicHost));
            try
            {
                Stretch((RectTransform)root.transform);
                var directorObject = new GameObject("Director", typeof(PlayableDirector), typeof(CinematicFrameBridge));
                directorObject.transform.SetParent(root.transform, false);
                var director = directorObject.GetComponent<PlayableDirector>();
                director.playOnAwake = false;
                director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
                director.extrapolationMode = DirectorWrapMode.Hold;
                var presentation = Rect("PresentationRoot", root.transform);
                var group = presentation.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0;
                group.blocksRaycasts = false;
                group.interactable = false;
                var view = presentation.gameObject.AddComponent<CinematicView>();
                presentation.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
                var visual = Rect("Visual", presentation);
                var background = Rect("Background", visual).gameObject.AddComponent<UnityEngine.UI.Image>();
                background.color = new Color(.98f,.86f,.62f,1);
                background.raycastTarget = true; // Consume world/HUD clicks while cinematic is visible.
                var dots = Rect("HalftoneGutter", visual).gameObject.AddComponent<ComicHalftoneGraphic>();
                dots.color = new Color(.76f,.43f,.20f,.20f); dots.raycastTarget = false;
                var board = Rect("ComicBoard", visual);
                board.offsetMin = new Vector2(32, 28); board.offsetMax = new Vector2(-32, -104);
                var panels = new RectTransform[4];
                var artworks = new UnityEngine.UI.RawImage[4];
                for (int i = 0; i < 4; i++)
                {
                    var panel = Rect("Panel" + (i + 1), board);
                    panel.anchorMin = new Vector2((i % 2) * .5f, i < 2 ? .5f : 0);
                    panel.anchorMax = panel.anchorMin + new Vector2(.5f, .5f);
                    panel.offsetMin = new Vector2(9, 9); panel.offsetMax = new Vector2(-9, -9);
                    var frame = panel.gameObject.AddComponent<UnityEngine.UI.Image>();
                    frame.color = new Color(.035f,.028f,.023f,1); frame.raycastTarget = false;
                    var inside = Rect("Inner", panel); inside.offsetMin = new Vector2(8,8); inside.offsetMax = new Vector2(-8,-8);
                    var paper = inside.gameObject.AddComponent<UnityEngine.UI.Image>();
                    paper.color = new Color(1,.97f,.88f,1); paper.raycastTarget = false;
                    panels[i] = inside; artworks[i] = Artwork("Artwork" + (i + 1), inside);
                }
                var controls = Rect("Controls", presentation);
                var skipRect = Rect("SkipButton", controls);
                skipRect.anchorMin = skipRect.anchorMax = skipRect.pivot = Vector2.one;
                // Bootstrap Options occupies the top-right 190px. Keep SKIP alongside it.
                skipRect.anchoredPosition = new Vector2(-260, -28);
                skipRect.sizeDelta = new Vector2(176, 62);
                var buttonImage = skipRect.gameObject.AddComponent<UnityEngine.UI.Image>();
                buttonImage.color = new Color(0.10f, 0.08f, 0.06f, 0.90f);
                var skip = skipRect.gameObject.AddComponent<UnityEngine.UI.Button>();
                skip.targetGraphic = buttonImage;
                var label = Rect("Label", skipRect).gameObject.AddComponent<TextMeshProUGUI>();
                label.font = font;
                label.text = "건너뛰기";
                label.fontSize = 26;
                label.alignment = TextAlignmentOptions.Center;
                label.color = new Color(1, 0.96f, 0.86f, 1);
                label.raycastTarget = false;
                SetReference(view, "visibility", group);
                var viewData = new SerializedObject(view);
                var panelData = viewData.FindProperty("panelViewports"); panelData.arraySize = 4;
                var artData = viewData.FindProperty("panelArtwork"); artData.arraySize = 4;
                for (int i = 0; i < 4; i++) { panelData.GetArrayElementAtIndex(i).objectReferenceValue = panels[i]; artData.GetArrayElementAtIndex(i).objectReferenceValue = artworks[i]; }
                viewData.ApplyModifiedPropertiesWithoutUndo();
                SetReference(view, "skipButton", skip);
                var host = root.GetComponent<CinematicHost>();
                SetReference(host, "director", director);
                SetReference(host, "frameBridge", directorObject.GetComponent<CinematicFrameBridge>());
                SetReference(host, "catalog", catalog);
                SetReference(host, "view", view);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static UnityEngine.UI.RawImage Artwork(string name, Transform parent)
        {
            var rect = Rect(name, parent);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1920, 1080);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.RawImage>();
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            Stretch(rect);
            return rect;
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
        private static void SetReference(UnityEngine.Object owner, string name, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(owner);
            var property = serialized.FindProperty(name);
            if (property == null) throw new InvalidOperationException(owner.GetType().Name + "." + name + " is not serialized.");
            property.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
