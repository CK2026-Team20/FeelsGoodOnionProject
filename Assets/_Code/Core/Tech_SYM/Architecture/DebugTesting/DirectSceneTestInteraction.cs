#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Cooked.Contracts;
using Cooked.Integration;
using Cooked.Session;
using Cooked.UI;
using FeelsGoodOnion.TechSYM.Interaction;
using UnityEngine;

namespace Cooked.DebugTesting
{
    /// <summary>Borrows authored scene prompts and memo screens; never creates interaction objects.</summary>
    public sealed class DirectSceneTestInteraction : IDisposable
    {
        private readonly List<MemoUIController> screens = new List<MemoUIController>();
        private readonly WorldPromptViewModel promptModel = new WorldPromptViewModel();
        private WorldPromptView prompt;
        private PlayerInteractionService interaction;
        private Camera camera;
        private Canvas promptCanvas;
        private Camera previousCanvasCamera;
        private bool disposed;

        public DirectSceneTestInteraction(Transform owner, IGameplayControlService control)
        {
            try
            {
                var scene = owner.gameObject.scene;
                var prompts = SceneComponentLookup.FindAll<WorldPromptView>(scene);
                if (prompts.Length > 1)
                    throw new InvalidOperationException("직접 씬 테스트에는 WorldPromptView를 하나만 배치하세요.");
                if (prompts.Length == 1)
                {
                    prompt = prompts[0];
                    prompt.Bind(promptModel);
                    promptCanvas = prompt.GetComponentInParent<Canvas>();
                }
                promptModel.Clear();
                foreach (var memo in SceneComponentLookup.FindAll<MemoObject>(scene))
                {
                    var screen = memo.ScreenController;
                    if (screen == null || screen.gameObject.scene != scene)
                    {
                        Debug.LogWarning("직접 씬 테스트: 메모에 같은 씬의 MemoUIController를 직접 연결하세요. 자동 생성하지 않습니다.", memo);
                        continue;
                    }
                    if (screens.Contains(screen)) continue;
                    if (screen.ScreenRoot == null || screen.ScreenRoot.gameObject.scene != scene || screen.ViewPrefab == null)
                    {
                        Debug.LogWarning("직접 씬 테스트: 메모 화면 담당자에 씬의 화면 부모와 메모 화면을 연결하세요. 누락된 설정은 자동 보충하지 않습니다.", screen);
                        continue;
                    }
                    screens.Add(screen);
                    screen.Initialize(control);
                }
            }
            catch { Dispose(); throw; }
        }
        public void Bind(PlayerInteractionService service, Camera output)
        {
            if (disposed) throw new ObjectDisposedException(nameof(DirectSceneTestInteraction));
            Unbind();
            interaction = service ?? throw new ArgumentNullException(nameof(service));
            camera = output != null ? output : throw new ArgumentNullException(nameof(output));
            if (promptCanvas != null)
            {
                previousCanvasCamera = promptCanvas.worldCamera;
                promptCanvas.worldCamera = output;
            }
            interaction.BindOverlays(screens);
        }
        public void Tick()
        {
            if (disposed) return;
            if (interaction == null || camera == null || !interaction.IsVisible) { promptModel.Clear(); return; }
            promptModel.SetPrompt(true, interaction.PromptPosition, camera.transform.rotation, "상호작용");
        }
        public void Unbind()
        {
            if (promptCanvas != null && camera != null && promptCanvas.worldCamera == camera)
                promptCanvas.worldCamera = previousCanvasCamera;
            interaction = null; camera = null; previousCanvasCamera = null; promptModel.Clear();
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            var errors = new List<Exception>();
            SessionCleanup.Attempt(errors, Unbind);
            foreach (var screen in screens) if (screen != null) SessionCleanup.Attempt(errors, screen.Unbind);
            SessionCleanup.Attempt(errors, () => { if (prompt != null) prompt.Unbind(); });
            SessionCleanup.Attempt(errors, promptModel.Dispose);
            screens.Clear();
            SessionCleanup.ThrowIfAny(errors, "테스트 상호작용 화면 해제 실패");
        }
    }
}
#endif
