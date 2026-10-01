using Cooked.Contracts;
using UnityEngine;
namespace Cooked.UI
{
    public sealed class GameUiRoot : MonoBehaviour
    {
        [SerializeField] private HudView hud;
        [SerializeField] private WorldPromptView prompt;
        [SerializeField] private Canvas worldCanvas;
        [SerializeField] private RectTransform dialogueMount, cinematicMount;
        private HudViewModel hudModel; private WorldPromptViewModel promptModel;
        public RectTransform DialogueMount => dialogueMount;
        public RectTransform CinematicMount => cinematicMount;
        public WorldPromptViewModel Prompt => promptModel;
        public void Configure(HudView h, WorldPromptView p, Canvas world, RectTransform dialogue, RectTransform cinematic)
        { hud = h; prompt = p; worldCanvas = world; dialogueMount = dialogue; cinematicMount = cinematic; }
        public void Bind(IPlayerBridgeService bridge, IGameSessionService session, Camera camera)
        {
            Unbind(); worldCanvas.worldCamera = camera;
            hudModel = new HudViewModel(bridge, session); promptModel = new WorldPromptViewModel();
            hud.Bind(hudModel); prompt.Bind(promptModel);
        }
        public void SetGameplayVisible(bool visible)
        {
            hudModel?.SetVisible(visible); promptModel?.SetGameplayVisible(visible);
            dialogueMount.gameObject.SetActive(visible); // CinematicMount remains active and is last sibling.
        }
        public void Unbind()
        {
            if (hud != null) hud.Unbind(); if (prompt != null) prompt.Unbind();
            hudModel?.Dispose(); promptModel?.Dispose(); hudModel = null; promptModel = null;
        }
        private void OnDestroy() => Unbind();
    }
}
