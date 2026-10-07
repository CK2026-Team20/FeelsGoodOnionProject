using System;
using System.IO;
using Cooked.Contracts;
using Cooked.Integration.Editor;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Cooked.Cinematics.Editor
{
    /// <summary>Authors ordered cuts and one artwork view; existing artwork/importers are read-only.</summary>
    public static class CinematicAssetBuilder
    {
        public const string SourceRoot = "Assets/_Scenes/Tech_SYM/Architecture/Cinematics";
        public static string AssetRoot => AuthoringOutputScope.Map(SourceRoot);
        public static string PrefabPath => AssetRoot + "/Prefabs/CinematicPresentation.prefab";
        public static string CatalogPath => AssetRoot + "/Data/CinematicCatalog.asset";
        public const string KoreanFontPath = "Assets/_Scenes/Tech_SYM/Architecture/UI/Fonts/CookedKorean SDF.asset";

        [MenuItem("Tools/Cooked/Cinematics/Build Assets")]
        public static void BuildAll() => BuildAll(AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(KoreanFontPath));
        // User-selected temporary descriptions of the eight unchanged artworks, 2026-10-08.
        // These are descriptive defaults, not a final narrative script; explicit arrays/catalog cuts replace them.
        public static string[] TemporaryOpeningCaptions => new[] {
            "양파와 포포가 주방에 함께 있었습니다.", "주방 한가운데에 낯선 틈이 생겼습니다.",
            "포포가 틈 너머에 남았습니다.", "양파는 포포를 향해 발걸음을 옮겼습니다." };
        public static string[] TemporaryEndingCaptions => new[] {
            "양파는 마침내 포포와 다시 만났습니다.", "두 친구는 함께 길을 걸었습니다.",
            "두 친구는 나란히 주방을 바라보았습니다.", "두 친구는 나란히 앉아 쉬었습니다." };
        public static void BuildAll(TMP_FontAsset font)
        {
            // Preserve an existing valid arbitrary-N catalog on repeat authoring. Only a absent or
            // old Timeline-only catalog receives the user's temporary descriptive defaults.
            var source = AssetDatabase.LoadAssetAtPath<CinematicCatalog>(CatalogPath);
            if (source == null && AuthoringOutputScope.IsTemporary)
                source = AssetDatabase.LoadAssetAtPath<CinematicCatalog>(SourceRoot + "/Data/CinematicCatalog.asset");
            if (source != null)
            {
                var fields = new SerializedObject(source);
                bool hasOpening = fields.FindProperty("openingCuts").arraySize > 0;
                bool hasEnding = fields.FindProperty("endingCuts").arraySize > 0;
                if (hasOpening || hasEnding)
                {
                    BuildAll(font, Copy(source.Get(CinematicId.Opening)), Copy(source.Get(CinematicId.Ending)));
                    return; // a partial/invalid existing catalog fails validation rather than being silently replaced
                }
            }
            BuildExistingArtwork(font, TemporaryOpeningCaptions, TemporaryEndingCaptions);
        }
        public static void BuildExistingArtwork(TMP_FontAsset font, string[] openingCaptions, string[] endingCaptions)
            => BuildAll(font, ExistingCuts("Opening", openingCaptions), ExistingCuts("Ending", endingCaptions));
        private static CinematicCut[] ExistingCuts(string prefix, string[] captions)
        {
            if (captions == null || captions.Length != 4)
                throw new ArgumentException("The existing artwork source has four " + prefix + " images; supply their approved captions in order.");
            var cuts = new CinematicCut[captions.Length];
            for (int i = 0; i < cuts.Length; i++)
            {
                string path = SourceRoot + "/Artwork/" + prefix + "_" + (i + 1).ToString("00") + ".png";
                cuts[i] = new CinematicCut();
                cuts[i].Configure(AssetDatabase.LoadAssetAtPath<Texture2D>(path), captions[i]);
            }
            return cuts;
        }
        private static CinematicCut[] Copy(System.Collections.Generic.IReadOnlyList<CinematicCut> cuts)
        { var result = new CinematicCut[cuts.Count]; for (int i = 0; i < result.Length; i++) result[i] = cuts[i]; return result; }
        public static void BuildAll(TMP_FontAsset font, CinematicCut[] openingCuts, CinematicCut[] endingCuts)
        {
            using (AuthoringOutputScope.BeginScene())
            {
                AuthoringOutputScope.RequireEditMode();
                if (font == null) throw new ArgumentNullException(nameof(font));
                // Validate both lists before creating/updating any asset.
                var validated = ScriptableObject.CreateInstance<CinematicCatalog>();
                try
                {
                    validated.Configure(CinematicId.Opening, openingCuts);
                    validated.Configure(CinematicId.Ending, endingCuts);
                    AuthoringOutputScope.EnsureFolder(AssetRoot + "/Data");
                    AuthoringOutputScope.EnsureFolder(AssetRoot + "/Prefabs");
                    var catalog = AssetDatabase.LoadAssetAtPath<CinematicCatalog>(CatalogPath);
                    if (catalog == null) { catalog = ScriptableObject.CreateInstance<CinematicCatalog>(); AssetDatabase.CreateAsset(catalog, CatalogPath); }
                    catalog.Configure(CinematicId.Opening, openingCuts);
                    catalog.Configure(CinematicId.Ending, endingCuts);
                    BuildPrefab(catalog, font);
                    EditorUtility.SetDirty(catalog); AssetDatabase.SaveAssetIfDirty(catalog);
                }
                finally { UnityEngine.Object.DestroyImmediate(validated); }
            }
        }
        private static void BuildPrefab(CinematicCatalog catalog, TMP_FontAsset font)
        {
            var root = new GameObject("CinematicPresentation", typeof(RectTransform)); root.SetActive(false);
            try
            {
                var host = root.AddComponent<CinematicHost>();
                var presentation = Rect("PresentationRoot", root.transform);
                var group = presentation.gameObject.AddComponent<CanvasGroup>();
                var view = presentation.gameObject.AddComponent<CinematicView>();
                var background = Rect("Background", presentation).gameObject.AddComponent<Image>();
                background.color = Color.black; background.raycastTarget = true;
                var viewport = Rect("ArtworkViewport", presentation);
                viewport.anchorMin = new Vector2(.02f, .18f); viewport.anchorMax = new Vector2(.98f, .98f);
                viewport.gameObject.AddComponent<RectMask2D>();
                var artRect = Rect("Artwork", viewport);
                artRect.anchorMin = artRect.anchorMax = artRect.pivot = new Vector2(.5f, .5f);
                var artwork = artRect.gameObject.AddComponent<RawImage>();
                var captionRect = Rect("Caption", presentation);
                captionRect.anchorMin = new Vector2(.06f, .025f); captionRect.anchorMax = new Vector2(.94f, .155f);
                var caption = captionRect.gameObject.AddComponent<TextMeshProUGUI>();
                caption.font = font; caption.fontSize = 30; caption.color = Color.white;
                caption.alignment = TextAlignmentOptions.Center; caption.richText = false; caption.raycastTarget = false;
                var skipRect = Rect("SkipButton", presentation);
                skipRect.anchorMin = skipRect.anchorMax = skipRect.pivot = Vector2.one;
                skipRect.anchoredPosition = new Vector2(-260, -28); skipRect.sizeDelta = new Vector2(176, 62);
                var buttonImage = skipRect.gameObject.AddComponent<Image>(); buttonImage.color = new Color(.1f,.08f,.06f,.9f);
                var skip = skipRect.gameObject.AddComponent<Button>(); skip.targetGraphic = buttonImage;
                skip.navigation = new Navigation { mode = Navigation.Mode.None };
                var label = Rect("Label", skipRect).gameObject.AddComponent<TextMeshProUGUI>();
                label.font = font; label.text = "건너뛰기"; label.fontSize = 26;
                label.alignment = TextAlignmentOptions.Center; label.raycastTarget = false;
                view.Configure(group, viewport, artwork, caption, skip);
                host.Configure(catalog, view);
                root.SetActive(true);
                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null) throw new IOException("Cinematic save failed: " + PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static RectTransform Rect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero; return rect;
        }
    }
}
