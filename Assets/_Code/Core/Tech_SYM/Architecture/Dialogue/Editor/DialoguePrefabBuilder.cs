using System;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Cooked.Dialogue.Editor
{
    /// <summary>Creates only the owned, independent prefab. Never opens/saves a scene or creates a Canvas/EventSystem.</summary>
    public static class DialoguePrefabBuilder
    {
        public const string PrefabPath = "Assets/_Scenes/Tech_SYM/Architecture/Dialogue/Prefabs/DialogueRoot.prefab";
        public static GameObject Build(TMP_FontAsset font)
        {
            if (font == null) throw new ArgumentNullException(nameof(font), "Integration must provide the approved Korean-capable TMP font.");
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            GameObject root = Rect("DialogueRoot", null, Vector2.zero, Vector2.one);
            try
            {
                var view = root.AddComponent<DialogueView>();
                var group = root.AddComponent<CanvasGroup>();
                var panel = Button("ContextPanel", root.transform, new Vector2(0.06f, 0.025f), new Vector2(0.94f, 0.30f), new Color(0.04f, 0.06f, 0.09f, 0.91f));
                var name = Text("Speaker", panel.transform, font, new Vector2(0.06f, 0.75f), new Vector2(0.94f, 0.96f), 32, TextAlignmentOptions.Center);
                name.color = new Color(1f, 0.85f, 0.35f);
                var body = Text("Context", panel.transform, font, new Vector2(0.06f, 0.17f), new Vector2(0.94f, 0.73f), 30, TextAlignmentOptions.TopLeft);
                var next = Text("NextMarker", panel.transform, font, new Vector2(0.46f, 0.015f), new Vector2(0.54f, 0.16f), 24, TextAlignmentOptions.Center);
                next.text = "▼";
                // Buttons are panel siblings, not ancestors/descendants of its click target.
                // Separate this row from Bootstrap's global Options at the top-right.
                var auto = LabeledButton("Auto", root.transform, font, new Vector2(0.50f, 0.84f), new Vector2(0.64f, 0.905f), "자동 OFF", out var autoLabel);
                var history = LabeledButton("History", root.transform, font, new Vector2(0.66f, 0.84f), new Vector2(0.80f, 0.905f), "이전 기록", out _);
                var skip = LabeledButton("Skip", root.transform, font, new Vector2(0.82f, 0.84f), new Vector2(0.96f, 0.905f), "SKIP", out _);
                GameObject log = Rect("HistoryOverlay", root.transform, Vector2.zero, Vector2.one);
                var logGroup = log.AddComponent<CanvasGroup>();
                var background = log.AddComponent<UnityEngine.UI.Image>();
                background.color = new Color(0.035f, 0.045f, 0.065f, 0.97f);
                background.raycastTarget = true;
                var heading = Text("Heading", log.transform, font, new Vector2(0.08f, 0.88f), new Vector2(0.68f, 0.96f), 36, TextAlignmentOptions.MidlineLeft);
                heading.text = "이전 대화 기록";
                var close = LabeledButton("Close", log.transform, font, new Vector2(0.80f, 0.88f), new Vector2(0.94f, 0.96f), "닫기", out _);
                GameObject scrollRoot = Rect("ScrollView", log.transform, new Vector2(0.08f, 0.08f), new Vector2(0.92f, 0.84f));
                var scroll = scrollRoot.AddComponent<UnityEngine.UI.ScrollRect>();
                scroll.horizontal = false;
                scroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
                scroll.scrollSensitivity = 35;
                GameObject viewport = Rect("Viewport", scrollRoot.transform, Vector2.zero, Vector2.one);
                var viewportImage = viewport.AddComponent<UnityEngine.UI.Image>();
                viewportImage.color = Color.white;
                viewport.AddComponent<UnityEngine.UI.Mask>().showMaskGraphic = false;
                GameObject content = Rect("Content", viewport.transform, new Vector2(0, 1), Vector2.one);
                var contentRect = (RectTransform)content.transform;
                contentRect.pivot = new Vector2(0.5f, 1);
                var layout = content.AddComponent<UnityEngine.UI.VerticalLayoutGroup>();
                layout.childControlWidth = true; layout.childControlHeight = true;
                layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
                layout.padding = new RectOffset(20, 20, 16, 16);
                var fitter = content.AddComponent<UnityEngine.UI.ContentSizeFitter>();
                fitter.horizontalFit = UnityEngine.UI.ContentSizeFitter.FitMode.Unconstrained;
                fitter.verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
                var historyText = Text("HistoryText", content.transform, font, Vector2.zero, Vector2.one, 29, TextAlignmentOptions.TopLeft);
                // Decorative text remains a hit target here so its parent ScrollRect receives drag/scroll events.
                historyText.raycastTarget = true;
                scroll.viewport = (RectTransform)viewport.transform;
                scroll.content = contentRect;
                view.Configure(group, logGroup, name, body, autoLabel, historyText, next.gameObject,
                    panel, auto, history, skip, close, scroll);
                group.alpha = 0; group.blocksRaycasts = group.interactable = false;
                logGroup.alpha = 0; logGroup.blocksRaycasts = logGroup.interactable = false;
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                if (saved == null) throw new InvalidOperationException("Dialogue prefab save failed: " + PrefabPath);
                return saved;
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static GameObject Rect(string name, Transform parent, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = min; rect.anchorMax = max;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return go;
        }
        private static UnityEngine.UI.Button Button(string name, Transform parent, Vector2 min, Vector2 max, Color color)
        {
            var go = Rect(name, parent, min, max);
            var image = go.AddComponent<UnityEngine.UI.Image>(); image.color = color;
            var button = go.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image;
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            return button;
        }
        private static UnityEngine.UI.Button LabeledButton(string name, Transform parent, TMP_FontAsset font, Vector2 min, Vector2 max, string label, out TMP_Text text)
        {
            var button = Button(name, parent, min, max, new Color(0.12f, 0.17f, 0.23f, 0.96f));
            text = Text("Label", button.transform, font, new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.96f), 26, TextAlignmentOptions.Center);
            text.text = label;
            return button;
        }
        private static TMP_Text Text(string name, Transform parent, TMP_FontAsset font, Vector2 min, Vector2 max, float size, TextAlignmentOptions alignment)
        {
            var text = Rect(name, parent, min, max).AddComponent<TextMeshProUGUI>();
            text.font = font; text.fontSize = size; text.color = Color.white;
            text.alignment = alignment; text.richText = false; text.raycastTarget = false;
            return text;
        }
    }
}
