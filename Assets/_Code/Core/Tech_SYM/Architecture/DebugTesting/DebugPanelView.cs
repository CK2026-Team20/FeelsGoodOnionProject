#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Cooked.Level;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
namespace Cooked.DebugTesting
{
    /// <summary>Pointer-only presentation. No scene/service lookup or gameplay rules.</summary>
    public sealed class DebugPanelView : MonoBehaviour
    {
        public UnityEngine.UI.ScrollRect LogScroll { get; private set; }
        public TextMeshProUGUI LogLabel { get; private set; }
        public TextMeshProUGUI SummaryLabel { get; private set; }
        private DebugPanelViewModel model;
        private TMP_FontAsset font;
        private DebugLogFont logFont;
        private TextMeshProUGUI pointLabel, platformLabel, invincibleLabel, pauseLabel;
        private TextMeshProUGUI cameraLabel;
        private readonly List<UnityEngine.UI.Button> cameraButtons = new List<UnityEngine.UI.Button>();
        private UnityEngine.UI.Button damageButton, pointButton;
        private readonly List<UnityEngine.UI.Button> actorButtons = new List<UnityEngine.UI.Button>();
        private readonly List<Action> release = new List<Action>();
        private long renderedLogVersion = -1;
        public void Bind(DebugPanelViewModel value, TMP_FontAsset fontAsset, DebugLogFont logFontAsset)
        {
            if (model != null) throw new InvalidOperationException("디버그 View는 한 번만 연결합니다.");
            model = value ?? throw new ArgumentNullException(nameof(value));
            font = fontAsset;
            logFont = logFontAsset ?? throw new ArgumentNullException(nameof(logFontAsset));
            Build();
            model.Changed += Render;
            Render();
        }
        private static RectTransform Rect(string name, Transform parent, float x, float y, float width, float height)
        {
            var node = new GameObject(name, typeof(RectTransform));
            node.transform.SetParent(parent, false);
            var rect = (RectTransform)node.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
            return rect;
        }
        private TextMeshProUGUI Text(Transform parent, string text, float x, float y, float width, float height, float size = 15)
        {
            var rect = Rect("Label", parent, x, y, width, height);
            var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
            if (font != null) label.font = font;
            label.text = text; label.fontSize = size; label.color = Color.white;
            label.richText = false; label.raycastTarget = false;
            label.textWrappingMode = TextWrappingModes.Normal;
            return label;
        }
        private static void Background(RectTransform rect)
        {
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(0, 0, 0, .92f);
        }
        private UnityEngine.UI.Button Button(Transform parent, string label, float y, Action command, bool actorCommand = true)
        {
            var rect = Rect("Action", parent, 10, y, 300, 27);
            var image = rect.gameObject.AddComponent<UnityEngine.UI.Image>();
            image.color = new Color(.16f, .19f, .22f, 1);
            var button = rect.gameObject.AddComponent<DebugPointerButton>();
            button.targetGraphic = image;
            button.navigation = new UnityEngine.UI.Navigation { mode = UnityEngine.UI.Navigation.Mode.None };
            var text = Text(rect, label, 7, 3, 286, 23);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = true; text.fontSizeMin = 10; text.fontSizeMax = 15;
            text.overflowMode = TextOverflowModes.Ellipsis;
            UnityEngine.Events.UnityAction click = () => command();
            button.onClick.AddListener(click);
            release.Add(() => { if (button != null) button.onClick.RemoveListener(click); });
            if (actorCommand) actorButtons.Add(button);
            return button;
        }
        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 31000;
            var scaler = gameObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            var log = Rect("Log", transform, 12, 12, 590, 280); Background(log);
            Text(log, "로그 · F1 표시/숨김", 8, 5, 560, 22);
            var viewport = Rect("Viewport", log, 8, 30, 574, 242);
            viewport.gameObject.AddComponent<UnityEngine.UI.Image>().color = new Color(0, 0, 0, .01f);
            viewport.gameObject.AddComponent<UnityEngine.UI.RectMask2D>();
            LogLabel = Text(viewport, "", 0, 0, 560, 242, 14);
            LogLabel.font = logFont.Asset;
            LogLabel.parseCtrlCharacters = false; // 파일 경로의 \\n 등을 실제 줄바꿈으로 해석하지 않습니다.
            LogLabel.rectTransform.anchorMax = new Vector2(1, 1);
            LogLabel.rectTransform.sizeDelta = new Vector2(-14, 242);
            LogScroll = log.gameObject.AddComponent<UnityEngine.UI.ScrollRect>();
            LogScroll.viewport = viewport; LogScroll.content = LogLabel.rectTransform;
            LogScroll.horizontal = false; LogScroll.vertical = true;
            LogScroll.movementType = UnityEngine.UI.ScrollRect.MovementType.Clamped;
            LogScroll.inertia = false; LogScroll.scrollSensitivity = 25;
            var controls = Rect("Controls", transform, 12, 302, 320, 410); Background(controls);
            SummaryLabel = Text(controls, "", 10, 8, 300, 66, 14);
            Button(controls, "체력 회복", 78, () => model.Heal());
            Button(controls, "조각 채우기", 108, () => model.FillFragments());
            Button(controls, "능력 모두 해금", 138, () => model.UnlockAll());
            var inv = Button(controls, "", 168, () => model.ToggleInvincible()); invincibleLabel = inv.GetComponentInChildren<TextMeshProUGUI>();
            Button(controls, "시작점 이동", 198, () => model.MoveToStart());
            var pick = Button(controls, "", 228, model.NextPoint, false); pointLabel = pick.GetComponentInChildren<TextMeshProUGUI>();
            pointButton = Button(controls, "선택 지점 이동", 258, () => model.MoveToSelectedPoint());
            Button(controls, "플레이어 재생성", 288, () => model.Respawn());
            var platform = Button(controls, "", 318, model.NextPlatform, false); platformLabel = platform.GetComponentInChildren<TextMeshProUGUI>();
            damageButton = Button(controls, "선택 플랫폼 피해 적용", 348, () => model.ApplySelectedDamage());
            var pause = Button(controls, "", 378, () => model.TogglePause(), false); pauseLabel = pause.GetComponentInChildren<TextMeshProUGUI>();
            var cameras = Rect("Cameras", transform, 342, 302, 320, 132); Background(cameras);
            cameraLabel = Text(cameras, "", 10, 8, 300, 22);
            cameraButtons.Add(Button(cameras, "측면", 36, () => model.SelectCamera(CameraMovementMode.Side), false));
            cameraButtons.Add(Button(cameras, "통로", 66, () => model.SelectCamera(CameraMovementMode.Corridor), false));
            cameraButtons.Add(Button(cameras, "쿼터뷰", 96, () => model.SelectCamera(CameraMovementMode.Quarter), false));
        }
        private void Render()
        {
            gameObject.SetActive(model.Visible);
            SummaryLabel.text = model.Summary;
            cameraLabel.text = "카메라 · " + model.CameraLabel;
            foreach (var button in cameraButtons) button.interactable = model.CanSelectCamera;
            pointLabel.text = "지점 선택 ▶ " + model.PointLabel;
            platformLabel.text = "피해원 선택 ▶ " + model.PlatformLabel;
            invincibleLabel.text = (model.DebugInvincible ? "[ON]" : "[OFF]") + " 디버그 무적";
            pauseLabel.text = model.Paused ? "재개" : "일시정지";
            foreach (var button in actorButtons) button.interactable = model.CanCommand;
            pointButton.interactable = model.CanCommand && model.SelectedPoint != null;
            damageButton.interactable = model.CanDamage;
            if (!model.Visible || renderedLogVersion == model.LogVersion) return;
            bool atBottom = renderedLogVersion < 0 || LogScroll.content.rect.height <= LogScroll.viewport.rect.height || LogScroll.verticalNormalizedPosition <= .01f;
            var offset = LogScroll.content.anchoredPosition;
            LogLabel.text = logFont.ForDisplay(model.LogText);
            LogLabel.ForceMeshUpdate();
            LogScroll.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Max(242, LogLabel.preferredHeight + 8));
            if (atBottom) LogScroll.verticalNormalizedPosition = 0;
            else LogScroll.content.anchoredPosition = offset;
            renderedLogVersion = model.LogVersion;
        }
        public void Unbind()
        {
            if (model == null) return;
            model.Changed -= Render;
            foreach (var cleanup in release) cleanup();
            release.Clear(); model = null; logFont = null;
        }
        private void OnDestroy() => Unbind();
    }
    // Deliberately ignores keyboard navigation/submit, including after a pointer click.
    public sealed class DebugPointerButton : UnityEngine.UI.Button
    {
        public override void OnSubmit(BaseEventData eventData) { }
        public override void OnMove(AxisEventData eventData) { }
        public override void OnPointerDown(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || !IsActive() || !IsInteractable()) return;
            // Selectable's default OnPointerDown selects the button. Avoid that keyboard focus transfer.
        }
    }
}
#endif
