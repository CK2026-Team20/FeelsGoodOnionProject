using System;
using Cooked.Session;
using Cooked.UI;
using UnityEngine;
namespace Cooked.Integration
{
    /// <summary>Composition of the existing HUD and the single world prompt.</summary>
    public sealed class CoreUiBinding : MonoBehaviour
    {
        [Tooltip("체력·조각 HUD와 한 월드 F 안내를 포함한 GameUiRoot입니다. 통합 Core에서 연결합니다.")]
        [SerializeField] private GameUiRoot ui;
        [Tooltip("여섯 통합 씬 밖의 직접 Play에 사용할 기존 월드 F 안내입니다. Core UI와 동시에 연결하지 않습니다.")]
        [SerializeField] private WorldPromptView independentPrompt;
        private Camera gameplayCamera;
        private GameSessionRuntime runtime;
        private PlayerBridgeService independentPlayer;
        private PlayerInteractionService independentInteraction;
        private WorldPromptViewModel independentModel;
        public GameUiRoot Ui => ui;
        public Camera GameplayCamera => gameplayCamera;
        public void Configure(GameUiRoot gameUi) { ui = gameUi; }
        public void ConfigureIndependent(WorldPromptView promptView) { independentPrompt = promptView; }
        public void Bind(GameSessionRuntime value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (ui == null) throw new InvalidOperationException("Core UI requires GameUiRoot.");
            Unbind(); runtime = value;
            ui.Bind(value.Player, value.Session, null);
            runtime.GameplayVisibilityChanged += SetVisible;
            runtime.ActorRemoving += OnActorRemoving;
            SetVisible(false);
        }
        public void BindIndependent(PlayerBridgeService player, PlayerInteractionService interaction, Camera output)
        {
            if (player == null || interaction == null || output == null || independentPrompt == null)
                throw new ArgumentException("Independent prompt requires Player, interaction, View and output.");
            Unbind(); independentPlayer = player; independentInteraction = interaction; gameplayCamera = output;
            independentModel = new WorldPromptViewModel(); independentPrompt.Bind(independentModel);
        }
        public void SetActorCamera(Camera camera)
        {
            gameplayCamera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            ui.SetWorldCamera(camera);
        }
        private void LateUpdate()
        {
            var player = runtime?.Player ?? independentPlayer;
            var interaction = runtime?.ActorHost.Interaction ?? independentInteraction;
            var model = runtime != null ? ui.Prompt : independentModel;
            if (model == null) return;
            if (player == null || !player.HasActor || gameplayCamera == null || interaction == null ||
                !interaction.IsVisible || (runtime != null && !runtime.GameplayVisible)) { model.Clear(); return; }
            model.SetPrompt(true, interaction.PromptPosition, gameplayCamera.transform.rotation, "상호작용");
        }
        private void SetVisible(bool value) { ui.SetGameplayVisible(value); if (!value) ui.Prompt?.Clear(); }
        private void OnActorRemoving() { ui.Prompt?.Clear(); gameplayCamera = null; ui.SetWorldCamera(null); }
        public void Unbind()
        {
            if (runtime != null)
            {
                runtime.GameplayVisibilityChanged -= SetVisible; runtime.ActorRemoving -= OnActorRemoving;
            }
            runtime = null; gameplayCamera = null; independentPlayer = null; independentInteraction = null;
            var errors = new System.Collections.Generic.List<Exception>();
            SessionCleanup.Attempt(errors, () => { if (independentPrompt != null) independentPrompt.Unbind(); });
            SessionCleanup.Attempt(errors, () => independentModel?.Dispose()); independentModel = null;
            SessionCleanup.Attempt(errors, () => { if (ui != null) ui.Unbind(); });
            SessionCleanup.ThrowIfAny(errors, "Core UI unbind failed.");
        }
        private void OnDestroy() => Unbind();
    }
}
