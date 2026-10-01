using System;
using System.ComponentModel;
using System.Text;
using System.Threading;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace Cooked.Dialogue
{
    /// <summary>UGUI presentation only. Composition supplies a VM; no global lookups or model access.</summary>
    public sealed class DialogueView : MonoBehaviour
    {
        [SerializeField] private CanvasGroup rootGroup;
        [SerializeField] private CanvasGroup logGroup;
        [SerializeField] private TMP_Text speaker;
        [SerializeField] private TMP_Text body;
        [SerializeField] private TMP_Text autoLabel;
        [SerializeField] private TMP_Text history;
        [SerializeField] private GameObject nextMarker;
        [SerializeField] private UnityEngine.UI.Button panelButton;
        [SerializeField] private UnityEngine.UI.Button autoButton;
        [SerializeField] private UnityEngine.UI.Button historyButton;
        [SerializeField] private UnityEngine.UI.Button skipButton;
        [SerializeField] private UnityEngine.UI.Button logCloseButton;
        [SerializeField] private UnityEngine.UI.ScrollRect logScroll;
        private DialogueViewModel model;
        private Tween typing;
        private CancellationTokenSource lifetime;
        private long presentationGeneration;
        private long shownSession = -1;
        private long shownRow = -1;
        private string shownHistory;

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
        private void OnAdvance() { model?.Advance(Time.frameCount); }
        private void OnAuto() { model?.ToggleAuto(); }
        private void OnHistory() { model?.OpenLog(); }
        private void OnSkip() { model?.SkipAll(); }
        private void OnLogClose() { model?.CloseLog(); }

        private void Present()
        {
            if (model == null) return;
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
