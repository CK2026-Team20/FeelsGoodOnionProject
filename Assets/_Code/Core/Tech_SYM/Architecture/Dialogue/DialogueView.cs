using System;
using System.ComponentModel;
using System.Text;
using System.Threading;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic;

namespace Cooked.Dialogue
{
    /// <summary>UGUI presentation only. Composition supplies a VM; no global lookups or model access.</summary>
    public sealed class DialogueView : MonoBehaviour
    {
        [Tooltip("대화 본문 화면의 표시·입력을 제어할 CanvasGroup입니다. 로그 그룹과 구분해 연결하세요.")]
        [SerializeField] private CanvasGroup rootGroup;
        [Tooltip("지난 대화 로그 화면의 표시·입력을 제어할 CanvasGroup입니다.")]
        [SerializeField] private CanvasGroup logGroup;
        [Tooltip("현재 문장의 화자 이름을 표시할 TMP 텍스트입니다.")]
        [SerializeField] private TMP_Text speaker;
        [Tooltip("현재 대화 문장을 순차 표시할 TMP 텍스트입니다.")]
        [SerializeField] private TMP_Text body;
        [Tooltip("자동 진행 상태를 표시할 TMP 텍스트입니다. 자동 버튼의 상태 안내에 사용됩니다.")]
        [SerializeField] private TMP_Text autoLabel;
        [Tooltip("이전에 표시한 문장 로그를 출력할 TMP 텍스트입니다.")]
        [SerializeField] private TMP_Text history;
        [Tooltip("문장 표시가 끝나 다음 입력을 기다릴 때 보이는 안내 오브젝트입니다.")]
        [SerializeField] private GameObject nextMarker;
        [Tooltip("대화 본문 클릭으로 표시 완료 또는 다음 문장 진행을 요청하는 버튼입니다. 한 입력에 한 번만 진행합니다.")]
        [SerializeField] private UnityEngine.UI.Button panelButton;
        [Tooltip("대화 자동 진행을 켜고 끄는 버튼입니다.")]
        [SerializeField] private UnityEngine.UI.Button autoButton;
        [Tooltip("현재 대화의 지난 문장 로그를 여는 버튼입니다.")]
        [SerializeField] private UnityEngine.UI.Button historyButton;
        [Tooltip("현재 대화 시퀀스를 건너뛰는 버튼입니다.")]
        [SerializeField] private UnityEngine.UI.Button skipButton;
        [Tooltip("대화 로그만 닫고 본문으로 돌아가는 버튼입니다. 메모 닫기 기능과는 다릅니다.")]
        [SerializeField] private UnityEngine.UI.Button logCloseButton;
        [Tooltip("긴 대화 로그를 스크롤할 ScrollRect입니다. 로그 화면의 스크롤 영역을 연결하세요.")]
        [SerializeField] private UnityEngine.UI.ScrollRect logScroll;
        private DialogueViewModel model;
        private Tween typing;
        private CancellationTokenSource lifetime;
        private long presentationGeneration;
        private long shownSession = -1;
        private long shownRow = -1;
        private string shownHistory;
        private int consumedFrame = -1;
        private bool lastOptionsPaused;

        // Called by the editor builder; field references are serialized into the independent prefab.
        public void Configure(CanvasGroup root, CanvasGroup log, TMP_Text speakerText, TMP_Text contextText,
            TMP_Text autoText, TMP_Text historyText, GameObject marker, UnityEngine.UI.Button panel,
            UnityEngine.UI.Button auto, UnityEngine.UI.Button historyOpen, UnityEngine.UI.Button skip,
            UnityEngine.UI.Button logClose, UnityEngine.UI.ScrollRect scroll)
        {
            rootGroup = root; logGroup = log; speaker = speakerText; body = contextText;
            autoLabel = autoText; history = historyText; nextMarker = marker;
            panelButton = panel; autoButton = auto; historyButton = historyOpen; skipButton = skip;
            logCloseButton = logClose; logScroll = scroll;
        }
        public void Bind(DialogueViewModel viewModel)
        {
            if (viewModel == null) throw new ArgumentNullException(nameof(viewModel));
            if (ReferenceEquals(model, viewModel)) { Present(); return; }
            ValidateReferences();
            Unbind();
            lifetime = new CancellationTokenSource();
            model = viewModel;
            model.PropertyChanged += OnChanged;
            panelButton.onClick.AddListener(OnAdvance);
            autoButton.onClick.AddListener(OnAuto);
            historyButton.onClick.AddListener(OnHistory);
            skipButton.onClick.AddListener(OnSkip);
            logCloseButton.onClick.AddListener(OnLogClose);
            Present();
        }
        private void Awake() { if (rootGroup != null) SetGroup(rootGroup, false); }
        private void OnChanged(object sender, PropertyChangedEventArgs args) { Present(); }
        private void OnAdvance() { consumedFrame = Time.frameCount; model?.Advance(Time.frameCount); }
        private void OnAuto() { consumedFrame = Time.frameCount; model?.ToggleAuto(); }
        private void OnHistory() { consumedFrame = Time.frameCount; model?.OpenLog(); }
        private void OnSkip() { consumedFrame = Time.frameCount; model?.SkipAll(); }
        private void OnLogClose() { consumedFrame = Time.frameCount; model?.CloseLog(); }

        private void LateUpdate()
        {
            if (model == null || !model.CanAdvance || model.LogOpen || consumedFrame == Time.frameCount) return;
            // Escape belongs to the options command even on the frame that options closes.
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) return;
            bool key = HasNewKeyPress(keyboard);
            bool click = Mouse.current != null && Mouse.current.leftButton.wasReleasedThisFrame;
            if (!key && !click) return;
            if (EventSystem.current != null)
            {
                if (key && EventSystem.current.currentSelectedGameObject != null &&
                    EventSystem.current.currentSelectedGameObject.GetComponentInParent<UnityEngine.UI.Selectable>() != null &&
                    (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)) return;
                if (click)
                {
                    var hits = new List<RaycastResult>();
                    EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = Mouse.current.position.ReadValue() }, hits);
                    if (hits.Count > 0)
                    {
                        var selectable = hits[0].gameObject.GetComponentInParent<UnityEngine.UI.Selectable>();
                        // Buttons own their release/submit command, including the dialogue panel.
                        if (selectable != null) return;
                    }
                }
            }
            consumedFrame = Time.frameCount;
            model.Advance(Time.frameCount);
        }
        // anyKey is an aggregate button: a second key while the first stays held is not a 0->1 edge.
        public static bool HasNewKeyPress(Keyboard keyboard)
        {
            if (keyboard == null) return false;
            foreach (var key in keyboard.allKeys)
                if (key.wasPressedThisFrame) return true;
            return false;
        }

        private void Present()
        {
            if (model == null) return;
            if (lastOptionsPaused != model.OptionsPaused) consumedFrame = Time.frameCount;
            lastOptionsPaused = model.OptionsPaused;
            SetGroup(rootGroup, model.IsActive);
            if (!model.IsActive)
            {
                StopTyping(); shownSession = shownRow = -1; shownHistory = null;
                body.text = speaker.text = history.text = string.Empty;
                SetGroup(logGroup, false);
                return;
            }
            bool newRow = shownSession != model.SessionId || shownRow != model.RowId;
            if (newRow)
            {
                consumedFrame = Time.frameCount;
                StopTyping();
                shownSession = model.SessionId;
                shownRow = model.RowId;
                speaker.text = model.Name;
                if (model.State == DialogueState.Revealing)
                {
                    body.text = string.Empty;
                    var bound = model;
                    long session = shownSession, row = shownRow, generation = ++presentationGeneration;
                    var token = lifetime.Token;
                    typing = body.DOText(model.Context, model.RevealDuration, false).SetEase(Ease.Linear)
                        .SetUpdate(true).OnComplete(() =>
                        {
                            if (!token.IsCancellationRequested && generation == presentationGeneration && ReferenceEquals(model, bound))
                                bound.NotifyRevealCompleted(session, row, Time.frameCount);
                        });
                }
            }
            if (model.State == DialogueState.AwaitingAdvance)
            {
                StopTyping();
                body.text = model.Context;
            }
            else if (typing != null && typing.IsActive())
            {
                if (model.PresentationPaused) typing.Pause(); else typing.Play();
            }
            panelButton.interactable = model.CanAdvance;
            autoButton.interactable = !model.OptionsPaused && !model.LogOpen;
            historyButton.interactable = !model.OptionsPaused && !model.LogOpen;
            skipButton.interactable = !model.OptionsPaused && !model.LogOpen;
            logCloseButton.interactable = !model.OptionsPaused;
            autoLabel.text = model.AutoEnabled ? "자동 ON" : "자동 OFF";
            nextMarker.SetActive(model.State == DialogueState.AwaitingAdvance && !model.LogOpen);
            SetGroup(logGroup, model.LogOpen);
            if (model.LogOpen)
            {
                var text = new StringBuilder();
                foreach (var row in model.History) text.Append(row.Name).Append("\n").Append(row.Context).Append("\n\n");
                string next = text.Length == 0 ? "아직 전체 출력된 대화가 없습니다." : text.ToString();
                if (shownHistory != next)
                {
                    shownHistory = next;
                    history.text = next;
                    UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(logScroll.content);
                    logScroll.verticalNormalizedPosition = 0;
                }
            }
        }
        private void StopTyping()
        {
            presentationGeneration++;
            typing?.Kill(false);
            typing = null;
        }
        private static void SetGroup(CanvasGroup group, bool visible)
        {
            group.alpha = visible ? 1 : 0;
            group.interactable = visible;
            group.blocksRaycasts = visible;
        }
        public void Unbind()
        {
            lifetime?.Cancel(); lifetime?.Dispose(); lifetime = null;
            StopTyping();
            var previous = model;
            if (previous != null) previous.PropertyChanged -= OnChanged;
            model = null;
            if (panelButton != null) panelButton.onClick.RemoveListener(OnAdvance);
            if (autoButton != null) autoButton.onClick.RemoveListener(OnAuto);
            if (historyButton != null) historyButton.onClick.RemoveListener(OnHistory);
            if (skipButton != null) skipButton.onClick.RemoveListener(OnSkip);
            if (logCloseButton != null) logCloseButton.onClick.RemoveListener(OnLogClose);
            shownSession = shownRow = -1; shownHistory = null;
            if (rootGroup != null) SetGroup(rootGroup, false);
            // Rendering disappeared: end its conversation rather than leave the player's lease stranded.
            previous?.CancelPresentation();
        }
        private void ValidateReferences()
        {
            if (rootGroup == null || logGroup == null || speaker == null || body == null || autoLabel == null || history == null || nextMarker == null ||
                panelButton == null || autoButton == null || historyButton == null || skipButton == null || logCloseButton == null || logScroll == null)
                throw new InvalidOperationException("DialogueView prefab references are incomplete.");
        }
        private void OnDisable() { Unbind(); }
        private void OnDestroy() { Unbind(); }
    }
}
