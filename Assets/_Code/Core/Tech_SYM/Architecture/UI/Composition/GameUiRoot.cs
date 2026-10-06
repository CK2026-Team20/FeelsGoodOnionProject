using Cooked.Contracts;
using UnityEngine;
namespace Cooked.UI
{
    public sealed class GameUiRoot : MonoBehaviour
    {
        [Tooltip("체력·조각·해금 상태를 표시할 HUD입니다.")]
        [SerializeField] private HudView hud;
        [Tooltip("상호작용 가능한 월드 대상 위에 표시할 안내 화면입니다.")]
        [SerializeField] private WorldPromptView prompt;
        [Tooltip("월드 공간 안내를 표시할 Canvas입니다. 메모 본문용 스크린 Canvas와 구분하세요.")]
        [SerializeField] private Canvas worldCanvas;
        [Tooltip("대화 화면을 배치할 화면 UI 부모입니다.")]
        [SerializeField] private RectTransform dialogueMount;
        [Tooltip("전체 화면 컷신을 배치할 화면 UI 부모입니다.")]
        [SerializeField] private RectTransform cinematicMount;
        private HudViewModel hudModel; private WorldPromptViewModel promptModel;
        public RectTransform DialogueMount => dialogueMount;
        public RectTransform CinematicMount => cinematicMount;
        public WorldPromptViewModel Prompt => promptModel;
        public void Configure(HudView h, WorldPromptView p, Canvas world, RectTransform dialogue, RectTransform cinematic)
        { hud = h; prompt = p; worldCanvas = world; dialogueMount = dialogue; cinematicMount = cinematic; }
        public void Bind(IPlayerBridgeService bridge, IGameSessionService session, Camera camera)
        {
            Unbind(); SetWorldCamera(camera);
            hudModel = new HudViewModel(bridge, session); promptModel = new WorldPromptViewModel();
            hud.Bind(hudModel); prompt.Bind(promptModel);
        }
        public void SetWorldCamera(Camera camera) { if (worldCanvas != null) worldCanvas.worldCamera = camera; }
        public void SetGameplayVisible(bool visible)
        {
            hudModel?.SetVisible(visible); promptModel?.SetGameplayVisible(visible);
            dialogueMount.gameObject.SetActive(visible); // CinematicMount remains active and is last sibling.
        }
        public void Unbind()
        {
            if (hud != null) hud.Unbind(); if (prompt != null) prompt.Unbind();
            hudModel?.Dispose(); promptModel?.Dispose(); hudModel = null; promptModel = null;
            SetWorldCamera(null);
        }
        private void OnDestroy() => Unbind();
    }
}
