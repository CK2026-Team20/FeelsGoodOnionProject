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
        [Tooltip("체력·조각 HUD와 월드 상호작용 안내를 포함한 GameUiRoot입니다.")]
        [SerializeField] private GameUiRoot ui;
        private Camera gameplayCamera;
        [Tooltip("주변 기믹의 조작 설명을 표시하는 별도 안내 화면입니다.")]
        [SerializeField] private InstructionView instructionView;
        [Tooltip("플레이어 주변에서 조작 설명을 찾는 최대 거리입니다(월드 단위, 최소 1). 높이면 더 먼 설명도 표시됩니다. 여러 설명 중 가장 가까운 것을 선택합니다.")]
        [SerializeField, Min(1)] private float instructionDistance = 8;
        private readonly List<InstructionSource> instructions = new List<InstructionSource>();
        private GameSessionRuntime runtime;
        private InstructionViewModel instructionModel;
        public GameUiRoot Ui => ui;
        public Camera GameplayCamera => gameplayCamera;
        public void Configure(GameUiRoot ui, InstructionView instructionView)
        { this.ui = ui; this.instructionView = instructionView; }
        public void Bind(GameSessionRuntime value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (ui == null || instructionView == null)
                throw new InvalidOperationException("Core UI references are incomplete.");
            Unbind(); runtime = value;
            gameplayCamera = null;
            ui.Bind(value.Player, value.Session, null);
            instructionModel = new InstructionViewModel();
            instructionView.Bind(instructionModel);
            runtime.GameplayVisibilityChanged += SetVisible;
            runtime.ActorRemoving += OnActorRemoving;
            // Opening preparation starts hidden before any rendering frame can reveal gameplay.
            SetVisible(false);
        }
        public void SetActorCamera(Camera camera)
        {
            gameplayCamera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            ui.SetWorldCamera(camera);
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
            if (runtime == null || !runtime.Player.HasActor || gameplayCamera == null) { ClearPresentation(); return; }
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
        private void OnActorRemoving()
        { instructions.Clear(); ClearPresentation(); gameplayCamera = null; ui.SetWorldCamera(null); }
        public void Unbind()
        {
            if (runtime != null)
            {
                runtime.GameplayVisibilityChanged -= SetVisible;
                runtime.ActorRemoving -= OnActorRemoving;
            }
            runtime = null; gameplayCamera = null; instructions.Clear();
            if (instructionView != null) instructionView.Unbind();
            instructionModel?.Dispose(); instructionModel = null;
            if (ui != null) ui.Unbind();
        }
        private void OnDestroy() => Unbind();
    }
}
