using System;
using System.Collections.Generic;
using Cooked.Authoring;
using Cooked.Session;
using Cooked.UI;
using UnityEngine;
namespace Cooked.Integration
{
    /// <summary>Owns the Core HUD/prompt references and their view models for one session.</summary>
    public sealed class CoreUiBinding : MonoBehaviour
    {
        [SerializeField, InspectorLabel("게임 HUD"), Tooltip("체력·조각·해금 상태를 표시할 HudView를 연결하세요.")]
        private HudView hud;
        [SerializeField, InspectorLabel("월드 F 안내"), Tooltip("통합 또는 독립 씬에서 사용할 하나의 WorldPromptView를 연결하세요.")]
        private WorldPromptView prompt;
        [SerializeField, InspectorLabel("월드 안내 Canvas"), Tooltip("월드 F 안내가 있는 Canvas를 연결하세요.")]
        private Canvas worldCanvas;
        [SerializeField, InspectorLabel("대화 부모"), Tooltip("게임 중 대화 화면을 표시할 RectTransform을 연결하세요.")]
        private RectTransform dialogueMount;
        [SerializeField, InspectorLabel("연출 부모"), Tooltip("게임 HUD와 독립적으로 유지할 연출 화면의 RectTransform을 연결하세요.")]
        private RectTransform cinematicMount;
        private Camera gameplayCamera;
        private GameSessionRuntime runtime;
        private PlayerBridgeService independentPlayer;
        private PlayerInteractionService independentInteraction;
        private HudViewModel hudModel;
        private WorldPromptViewModel promptModel;
        private bool unbinding;
        public WorldPromptViewModel Prompt => promptModel;
        public RectTransform DialogueMount => dialogueMount;
        public RectTransform CinematicMount => cinematicMount;
        public Camera GameplayCamera => gameplayCamera;
        public void Bind(GameSessionRuntime value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (unbinding) throw new InvalidOperationException("Core UI is unbinding.");
            if (hud == null || prompt == null || worldCanvas == null || dialogueMount == null || cinematicMount == null || dialogueMount == cinematicMount)
                throw new InvalidOperationException("CoreUiBinding의 HUD·월드 안내·Canvas·서로 다른 대화/연출 부모를 연결하세요.");
            foreach (var reference in new Component[] { hud, prompt, worldCanvas, dialogueMount, cinematicMount })
                SceneComponentLookup.RequireSameScene(this, reference, "Core 화면 연결");
            Unbind();
            try
            {
                runtime = value;
                hudModel = new HudViewModel(value.Player, value.Session);
                promptModel = new WorldPromptViewModel();
                hud.Bind(hudModel); prompt.Bind(promptModel);
                runtime.GameplayVisibilityChanged += SetVisible;
                runtime.ActorRemoving += OnActorRemoving;
                SetVisible(false);
            }
            catch (Exception error)
            {
                SessionCleanup.RethrowAfterCleanup(error, Unbind);
                throw;
            }
        }
        public void BindIndependent(PlayerBridgeService player, PlayerInteractionService interaction, Camera output)
        {
            if (unbinding) throw new InvalidOperationException("Core UI is unbinding.");
            if (player == null || interaction == null || output == null || prompt == null)
                throw new ArgumentException("Independent prompt requires Player, interaction, View and output.");
            SceneComponentLookup.RequireSameScene(this, prompt, "독립 월드 안내");
            if (worldCanvas != null) SceneComponentLookup.RequireSameScene(this, worldCanvas, "독립 안내 Canvas");
            Unbind();
            try
            {
                independentPlayer = player; independentInteraction = interaction; gameplayCamera = output;
                promptModel = new WorldPromptViewModel(); prompt.Bind(promptModel);
                if (worldCanvas != null) worldCanvas.worldCamera = output;
            }
            catch (Exception error)
            {
                SessionCleanup.RethrowAfterCleanup(error, Unbind);
                throw;
            }
        }
        public void SetActorCamera(Camera camera)
        {
            gameplayCamera = camera != null ? camera : throw new ArgumentNullException(nameof(camera));
            if (worldCanvas != null) worldCanvas.worldCamera = camera;
        }
        private void LateUpdate()
        {
            var player = runtime?.Player ?? independentPlayer;
            var interaction = runtime?.ActorHost.Interaction ?? independentInteraction;
            var model = promptModel;
            if (model == null) return;
            if (player == null || !player.HasActor || gameplayCamera == null || interaction == null ||
                !interaction.IsVisible || (runtime != null && !runtime.GameplayVisible)) { model.Clear(); return; }
            model.SetPrompt(true, interaction.PromptPosition, gameplayCamera.transform.rotation, "상호작용");
        }
        private void SetVisible(bool value)
        {
            hudModel?.SetVisible(value); promptModel?.SetGameplayVisible(value);
            if (!value) promptModel?.Clear();
            if (dialogueMount != null) dialogueMount.gameObject.SetActive(value);
            // Cinematic mount shares Core residency, not gameplay visibility.
        }
        private void OnActorRemoving()
        {
            promptModel?.Clear(); gameplayCamera = null;
            if (worldCanvas != null) worldCanvas.worldCamera = null;
        }
        public void Unbind()
        {
            if (unbinding) return;
            unbinding = true;
            var previousRuntime = runtime;
            var previousHud = hudModel;
            var previousPrompt = promptModel;
            runtime = null; gameplayCamera = null; independentPlayer = null; independentInteraction = null;
            hudModel = null; promptModel = null;
            var errors = new List<Exception>();
            try
            {
                if (previousRuntime != null)
                {
                    SessionCleanup.Attempt(errors, () => previousRuntime.GameplayVisibilityChanged -= SetVisible);
                    SessionCleanup.Attempt(errors, () => previousRuntime.ActorRemoving -= OnActorRemoving);
                }
                SessionCleanup.Attempt(errors, () => { if (hud != null) hud.Unbind(); });
                SessionCleanup.Attempt(errors, () => { if (prompt != null) prompt.Unbind(); });
                SessionCleanup.Attempt(errors, () => previousHud?.Dispose());
                SessionCleanup.Attempt(errors, () => previousPrompt?.Dispose());
                SessionCleanup.Attempt(errors, () => { if (worldCanvas != null) worldCanvas.worldCamera = null; });
                SessionCleanup.Attempt(errors, () => { if (dialogueMount != null) dialogueMount.gameObject.SetActive(false); });
            }
            finally { unbinding = false; }
            SessionCleanup.ThrowIfAny(errors, "Core UI unbind failed.");
        }
        private void OnDestroy() => Unbind();
    }
}
