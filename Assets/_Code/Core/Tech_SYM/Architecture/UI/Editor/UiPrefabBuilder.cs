#if UNITY_EDITOR
using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TextCore.LowLevel;
namespace Cooked.UI.Editor
{
    public static class UiPrefabBuilder
    {
        public const string AssetRoot = "Assets/_Scenes/Tech_SYM/Architecture/UI";
        public const string FontPath = AssetRoot + "/Fonts/CookedKorean SDF.asset";
        public const string GlobalPath = AssetRoot + "/Prefabs/GlobalUiRoot.prefab";
        public const string TitlePath = AssetRoot + "/Prefabs/TitleUiRoot.prefab";
        public const string GamePath = AssetRoot + "/Prefabs/GameUiRoot.prefab";
        private static readonly Color Ink = new Color(.13f, .19f, .16f);
        private static readonly Color Cream = new Color(.97f, .94f, .84f);
        private static readonly Color Green = new Color(.20f, .39f, .29f);
        [MenuItem("Cooked/UI/Build production prefabs")]
        public static void BuildAll()
        {
            Directory.CreateDirectory(AssetRoot + "/Prefabs"); AssetDatabase.Refresh();
            var font = BuildFont();
            var scratch = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            try { Save(CreateGlobal(font), GlobalPath, scratch); Save(CreateTitle(font), TitlePath, scratch); Save(CreateGame(font), GamePath, scratch); AssetDatabase.SaveAssets(); }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scratch);
            }
            Debug.Log("Cooked UI prefabs saved: " + AssetRoot);
        }
        public static TMP_FontAsset BuildFont()
        {
            var existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath); if (existing != null) return existing;
            var source = AssetDatabase.LoadAssetAtPath<Font>(AssetRoot + "/Fonts/NotoSansCJKkr-Regular.otf");
            if (source == null) throw new InvalidOperationException("Static Korean source font missing or not imported.");
            var font = TMP_FontAsset.CreateFontAsset(source, 48, 6, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            if (font == null) throw new InvalidOperationException("Unity could not create Korean TMP font.");
            font.name = "CookedKorean SDF"; font.isMultiAtlasTexturesEnabled = true;
            font.TryAddCharacters("Cooked! 새 게임 옵션 닫기 체크포인트 재시도 타이틀 전체 음악 효과 체력 눈물 조각 이동 점프 상호작용 형태 전환 껍질 회수 자동 이전 기록 건너뛰기 어니 포포 0123456789 WASDFERQSpace/—·%", out string missing);
            if (!string.IsNullOrEmpty(missing)) throw new InvalidOperationException("Missing Korean UI glyphs: " + missing);
            AssetDatabase.CreateAsset(font, FontPath);
            if (font.material != null) AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var atlas in font.atlasTextures) if (atlas != null) AssetDatabase.AddObjectToAsset(atlas, font);
            EditorUtility.SetDirty(font); return font;
        }
        public static GameObject CreateEventSystem()
        {
            var root = new GameObject("EventSystemRoot");
            var child = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            child.transform.SetParent(root.transform, false);
            child.GetComponent<InputSystemUIInputModule>().AssignDefaultActions(); return root;
        }
        private static void Save(GameObject root, string path, UnityEngine.SceneManagement.Scene preview)
        { try { UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, preview); PrefabUtility.SaveAsPrefabAsset(root, path); } finally { UnityEngine.Object.DestroyImmediate(root); } }
        private static RectTransform Rect(string name, Transform parent, Vector2 min, Vector2 max, Vector2 offsetMin, Vector2 offsetMax)
        {
            var r = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent, false);
            r.anchorMin = min; r.anchorMax = max; r.offsetMin = offsetMin; r.offsetMax = offsetMax; return r;
        }
        private static RectTransform Fill(string name, Transform parent) => Rect(name, parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        private static RectTransform Box(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        { var r = Rect(name, parent, anchor, anchor, Vector2.zero, Vector2.zero); r.pivot = anchor; r.sizeDelta = size; r.anchoredPosition = pos; return r; }
        private static Canvas Canvas(string name, Transform parent, int order)
        {
            var rect = Fill(name, parent); var canvas = rect.gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = order;
            var scaler = rect.gameObject.AddComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            rect.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>(); return canvas;
        }
        private static UnityEngine.UI.Image Image(RectTransform r, Color color, bool raycast = false)
        { var image = r.gameObject.AddComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = raycast; return image; }
        private static TMP_Text Text(RectTransform r, TMP_FontAsset font, string text, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center)
        { var t = r.gameObject.AddComponent<TextMeshProUGUI>(); t.font = font; t.text = text; t.fontSize = size; t.color = color; t.alignment = align; t.raycastTarget = false; return t; }
        private static UnityEngine.UI.Button Button(string name, Transform parent, TMP_FontAsset font, string text, Vector2 pos, Vector2 size, Vector2? anchor = null)
        {
            var r = Box(name, parent, anchor ?? new Vector2(.5f, .5f), pos, size); var image = Image(r, Green, true);
            var button = r.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
            var colors = button.colors; colors.highlightedColor = new Color(.83f, 1f, .84f); colors.pressedColor = new Color(.6f, .8f, .64f); colors.disabledColor = new Color(.65f,.65f,.65f,.65f); button.colors = colors;
            Text(Fill("Label", r), font, text, 30, Color.white); return button;
        }
        private static UnityEngine.UI.Slider Slider(string name, Transform parent, TMP_FontAsset font, string title, float y)
        {
            Text(Box(name + "Label", parent, new Vector2(.5f,.5f), new Vector2(-225,y), new Vector2(150,42)), font, title, 28, Ink);
            var r = Box(name, parent, new Vector2(.5f,.5f), new Vector2(80,y), new Vector2(420,42));
            var slider = r.gameObject.AddComponent<UnityEngine.UI.Slider>();
            Image(Rect("Track", r, new Vector2(0,.35f), new Vector2(1,.65f), Vector2.zero, Vector2.zero), new Color(.65f,.7f,.6f), true);
            var area = Rect("FillArea", r, Vector2.zero, Vector2.one, new Vector2(12,12), new Vector2(-12,-12));
            var fill = Fill("Fill", area); Image(fill, Green); slider.fillRect = fill;
            var handles = Rect("HandleArea", r, Vector2.zero, Vector2.one, new Vector2(12,0), new Vector2(-12,0));
            var handle = Box("Handle", handles, new Vector2(.5f,.5f), Vector2.zero, new Vector2(28,42)); var graphic = Image(handle, Green, true);
            handle.sizeDelta = new Vector2(28,0); slider.handleRect = handle; slider.targetGraphic = graphic; slider.minValue = 0; slider.maxValue = 1; slider.value = 1;
            return slider;
        }
        public static GameObject CreateGlobal(TMP_FontAsset font)
        {
            var root = new GameObject("GlobalUiRoot"); var composition = root.AddComponent<GlobalUiRoot>();
            var options = root.AddComponent<OptionsView>(); var fade = root.AddComponent<FadeView>();
            var optionsCanvas = Canvas("OptionsCanvas", root.transform, 200);
            var modal = Fill("OptionsModal", optionsCanvas.transform); Image(modal, new Color(0,0,0,.75f), true); var group = modal.gameObject.AddComponent<CanvasGroup>(); group.alpha = 0; group.blocksRaycasts = false; group.interactable = false;
            var panel = Box("Panel", modal, new Vector2(.5f,.5f), Vector2.zero, new Vector2(800,780)); Image(panel, Cream, true);
            Text(Box("Heading", panel, new Vector2(.5f,.5f), new Vector2(0,320), new Vector2(600,70)), font, "옵션", 48, Ink);
            var master = Slider("Master", panel, font, "전체 음량", 220); var music = Slider("Music", panel, font, "음악", 130); var sfx = Slider("Sfx", panel, font, "효과음", 40);
            var values = Text(Box("Values", panel, new Vector2(.5f,.5f), new Vector2(0,-40), new Vector2(720,45)), font, "", 24, Ink);
            var retry = Button("Retry", panel, font, "체크포인트 재시도", new Vector2(0,-125), new Vector2(600,62));
            var title = Button("ReturnToTitle", panel, font, "타이틀로", new Vector2(0,-205), new Vector2(600,62));
            var close = Button("Close", panel, font, "닫기", new Vector2(0,-285), new Vector2(600,62));
            var error = Text(Box("Error", panel, new Vector2(.5f,.5f), new Vector2(0,-350), new Vector2(750,45)), font, "", 20, new Color(.65f,.12f,.08f));
            options.Configure(group, close, retry, title, master, music, sfx, values, error);
            var fadeCanvas = Canvas("FadeCanvas", root.transform, 300); var cover = Fill("BlackCover", fadeCanvas.transform); Image(cover, Color.black, true);
            var coverGroup = cover.gameObject.AddComponent<CanvasGroup>(); coverGroup.alpha = 1; coverGroup.blocksRaycasts = true;
            fade.Configure(coverGroup); composition.Configure(options, fade); return root;
        }
        public static GameObject CreateTitle(TMP_FontAsset font)
        {
            var root = new GameObject("TitleUiRoot"); var composition = root.AddComponent<TitleUiRoot>(); var view = root.AddComponent<TitleView>();
            var canvas = Canvas("TitleCanvas", root.transform, 10); Image(Fill("Background", canvas.transform), Cream);
            var band = Rect("KitchenBand", canvas.transform, new Vector2(0,0), new Vector2(1,.20f), Vector2.zero, Vector2.zero); Image(band, Green);
            Text(Box("Heading", canvas.transform, new Vector2(.5f,.5f), new Vector2(0,240), new Vector2(1100,170)), font, "Cooked!", 120, Green);
            Text(Box("Subtitle", canvas.transform, new Vector2(.5f,.5f), new Vector2(0,105), new Vector2(1100,80)), font, "어니와 포포의 주방 탈출", 36, Ink);
            var start = Button("NewGame", canvas.transform, font, "새 게임", new Vector2(0,-45), new Vector2(460,90));
            var quit = Button("Quit", canvas.transform, font, "게임 종료", new Vector2(0,-160), new Vector2(460,80));
            view.Configure(start, quit); composition.Configure(view); PrototypeUiAuthoring.ConfigureTitle(root); return root;
        }
        public static GameObject CreateGame(TMP_FontAsset font)
        {
            var root = new GameObject("GameUiRoot"); var composition = root.AddComponent<GameUiRoot>(); var hud = root.AddComponent<HudView>(); var prompt = root.AddComponent<WorldPromptView>();
            var screen = Canvas("ScreenCanvas", root.transform, 20); var h = Fill("HudRoot", screen.transform); var group = h.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = false;
            var hearts = new HudIconGraphic[3];
            for (int i = 0; i < hearts.Length; i++)
                hearts[i] = Icon(Box("Heart" + (i + 1), h, new Vector2(0,1), new Vector2(30 + i * 68,-30), new Vector2(64,64)), HudIconGraphic.Symbol.Heart);
            Icon(Box("TearIcon", h, new Vector2(0,1), new Vector2(248,-24), new Vector2(62,76)), HudIconGraphic.Symbol.Tear);
            var count = Text(Box("FragmentCount", h, new Vector2(0,1), new Vector2(322,-36), new Vector2(170,60)), font, "—/—", 38, Color.white, TextAlignmentOptions.MidlineLeft);
            count.fontStyle = FontStyles.Bold;
            var countShadow = count.gameObject.AddComponent<UnityEngine.UI.Shadow>(); countShadow.effectColor = new Color(0,0,0,.65f); countShadow.effectDistance = new Vector2(2,-2);
            var form = AbilityCard(h,font,"Form",HudIconGraphic.Symbol.Form,"Q",30);
            var tear = AbilityCard(h,font,"Tear",HudIconGraphic.Symbol.Tear,"F",146);
            CanvasGroup recover = null;
            var dialogue = Fill("DialogueMount", screen.transform); var cinematic = Fill("CinematicMount", screen.transform); cinematic.SetAsLastSibling();
            var worldRect = Fill("WorldCanvas", root.transform); var world = worldRect.gameObject.AddComponent<Canvas>(); world.renderMode = RenderMode.WorldSpace;
            worldRect.sizeDelta = new Vector2(1000,1000); worldRect.localScale = Vector3.one;
            var anchor = Box("PromptAnchor", worldRect, new Vector2(.5f,.5f), Vector2.zero, new Vector2(420,80)); anchor.localScale = Vector3.one * .004f;
            Image(anchor, new Color(.1f,.15f,.12f,.9f)); var pg = anchor.gameObject.AddComponent<CanvasGroup>(); pg.alpha = 0; pg.blocksRaycasts = false;
            var label = Text(Fill("Label", anchor), font, "E  상호작용", 30, Color.white);
            hud.ConfigureIcons(group, hearts, count, form, tear, recover); prompt.Configure(pg, anchor, label);
            composition.Configure(hud, prompt, world, dialogue, cinematic); return root;
        }
        private static HudIconGraphic Icon(RectTransform rect, HudIconGraphic.Symbol symbol)
        { var icon = rect.gameObject.AddComponent<HudIconGraphic>(); icon.Configure(symbol); return icon; }
        private static CanvasGroup AbilityCard(Transform parent, TMP_FontAsset font, string name, HudIconGraphic.Symbol symbol, string key, float x)
        {
            var root = Box(name + "Ability",parent,Vector2.zero,new Vector2(x,28),new Vector2(100,142));
            var group = root.gameObject.AddComponent<CanvasGroup>(); group.blocksRaycasts = false; group.interactable = false;
            var frame = Box("Frame",root,new Vector2(.5f,1),Vector2.zero,new Vector2(100,100));
            Image(frame,new Color(1,.98f,.83f,.95f));
            var interior = Rect("Interior",frame,Vector2.zero,Vector2.one,new Vector2(2,2),new Vector2(-2,-2)); Image(interior,new Color(.07f,.13f,.10f,.82f));
            Icon(Rect("Icon",interior,Vector2.zero,Vector2.one,new Vector2(8,8),new Vector2(-8,-8)),symbol);
            var keycap = Box("Keycap",root,new Vector2(.5f,0),Vector2.zero,new Vector2(42,34)); Image(keycap,new Color(.94f,.94f,.9f,.98f));
            Text(Fill("Key",keycap),font,key,26,Ink);
            return group;
        }
    }
}
#endif
