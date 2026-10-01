using System;
using System.Collections.Generic;
using Cooked.UI;
using UnityEngine;

namespace Cooked.Integration
{
    /// <summary>Core composition adapter. Feeds existing prompt/HUD VMs without View-to-service lookups.</summary>
    public sealed class CoreUiBinding : MonoBehaviour
    {
        public readonly struct InstructionSource
        {
            public InstructionSource(Transform anchor, string text) { Anchor = anchor; Text = text; }
            public Transform Anchor { get; }
            public string Text { get; }
        }
        [SerializeField] private GameUiRoot ui;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private InstructionView instructionView;
        [SerializeField, Min(1)] private float instructionDistance = 8;
        private readonly List<InstructionSource> instructions = new List<InstructionSource>();
        private GameSessionRuntime runtime;
        private InstructionViewModel instructionModel;
        public GameUiRoot Ui => ui;
        public void Configure(GameUiRoot ui, Camera camera, InstructionView instructionView)
        { this.ui = ui; gameplayCamera = camera; this.instructionView = instructionView; }
        public void Bind(GameSessionRuntime value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (ui == null || gameplayCamera == null || instructionView == null)
                throw new InvalidOperationException("Core UI references are incomplete.");
            Unbind(); runtime = value;
            ui.Bind(value.Player, value.Session, gameplayCamera);
            instructionModel = new InstructionViewModel();
            instructionView.Bind(instructionModel);
            runtime.GameplayVisibilityChanged += SetVisible;
            runtime.ActorRemoving += OnActorRemoving;
            // Opening preparation starts hidden before any rendering frame can reveal gameplay.
            SetVisible(false);
        }
        public void SetInstructions(IEnumerable<InstructionSource> sources)
        {
            instructions.Clear();
            if (sources == null) throw new ArgumentNullException(nameof(sources));
            foreach (var source in sources)
            {
                if (source.Anchor == null || string.IsNullOrWhiteSpace(source.Text))
                    throw new InvalidOperationException("Instruction source has no anchor or content.");
                instructions.Add(source);
            }
        }
        private void LateUpdate()
        {
            if (runtime == null || !runtime.Player.HasActor) { ClearPresentation(); return; }
            var interaction = runtime.ActorHost.Interaction;
            if (interaction != null && interaction.IsVisible)
                ui.Prompt.SetPrompt(true, interaction.PromptPosition, gameplayCamera.transform.rotation, "상호작용");
            else ui.Prompt.Clear();
            // The existing interaction prompt prefixes F; general instructions use a separate small
            // bound HUD text so R/Q/E instructions never receive a misleading F action label.
            string text = null;
            float best = instructionDistance * instructionDistance;
            Vector3 position = runtime.Player.BodyTransform.position;
            foreach (var source in instructions)
            {
                if (source.Anchor == null) continue;
                float distance = (source.Anchor.position - position).sqrMagnitude;
                if (distance > best) continue;
                best = distance; text = source.Text;
            }
            instructionModel.Present(text, runtime.GameplayVisible && !runtime.Dialogue.IsActive);
        }
        private void SetVisible(bool value)
        {
            ui.SetGameplayVisible(value);
            if (!value) instructionModel?.Present(null, false);
        }
        private void ClearPresentation()
        { ui?.Prompt?.Clear(); instructionModel?.Present(null, false); }
        private void OnActorRemoving() { instructions.Clear(); ClearPresentation(); }
        public void Unbind()
        {
            if (runtime != null)
            {
                runtime.GameplayVisibilityChanged -= SetVisible;
                runtime.ActorRemoving -= OnActorRemoving;
            }
            runtime = null; instructions.Clear();
            if (instructionView != null) instructionView.Unbind();
            instructionModel?.Dispose(); instructionModel = null;
            if (ui != null) ui.Unbind();
        }
        private void OnDestroy() => Unbind();
    }
}
