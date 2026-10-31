#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Cooked.Contracts;
using Cooked.Integration;
using Cooked.Session;
using Cooked.UI;
using FeelsGoodOnion.TechSYM.Interaction;
using UnityEditor;
using UnityEngine;

namespace Cooked.DebugTesting
{
    /// <summary>Temporary prompt and missing memo-screen wiring; interaction rules remain in gameplay services.</summary>
    public sealed class DirectSceneTestInteraction : IDisposable
    {
        private readonly List<GameObject> owned = new List<GameObject>();
        private readonly List<Action> restore = new List<Action>();
        private readonly List<MemoUIController> screens = new List<MemoUIController>();
        private readonly WorldPromptViewModel promptModel = new WorldPromptViewModel();
        private WorldPromptView prompt;
        private PlayerInteractionService interaction;
        private Camera camera;
        private bool disposed;

        public DirectSceneTestInteraction(Transform owner, IGameplayControlService control)
        {
            try
            {
                var promptRoot = Create("Assets/_Scenes/Tech_SYM/Prefabs/Interaction/InteractionPromptCanvas.prefab", owner);
                prompt = promptRoot.GetComponentInChildren<WorldPromptView>(true);
                if (prompt == null) throw new InvalidOperationException("기존 F 안내 프리팹에 WorldPromptView가 없습니다.");
                prompt.Bind(promptModel);
                promptModel.Clear();
                var memos = SceneComponentLookup.FindAll<MemoObject>(owner.gameObject.scene);
                if (memos.Length == 0) return;
                var canvas = Create("Assets/_Scenes/Tech_SYM/Prefabs/Interaction/MemoScreenCanvas.prefab", owner);
                var mount = canvas.GetComponent<RectTransform>();
                var view = AssetDatabase.LoadAssetAtPath<MemoView>("Assets/_Prefabs/UI/Memo/MemoView.prefab");
                if (mount == null || view == null) throw new InvalidOperationException("기존 메모 Canvas와 MemoView 프리팹을 확인하세요.");
                MemoUIController fallback = null;
                foreach (var memo in memos)
                {
                    var screen = memo.ScreenController;
                    if (screen == null)
                    {
                        if (fallback == null)
                            fallback = Create("Assets/_Scenes/Tech_SYM/Prefabs/Interaction/MemoSystem.prefab", owner).GetComponent<MemoUIController>();
                        screen = fallback;
                    }
                    if (screen == null) throw new InvalidOperationException("기존 MemoSystem에 화면 담당 컴포넌트가 없습니다.");
                    if (!screens.Contains(screen))
                    {
                        var oldRoot = screen.ScreenRoot;
                        var oldView = screen.ViewPrefab;
                        var actualRoot = oldRoot != null ? oldRoot : mount;
                        var actualView = oldView != null ? oldView : view;
                        screen.ConfigureScreen(actualRoot, actualView);
                        screens.Add(screen);
                        restore.Add(() =>
                        {
                            if (screen != null && screen.ScreenRoot == actualRoot && screen.ViewPrefab == actualView)
                                screen.ConfigureScreen(oldRoot, oldView);
                        });
                        screen.Initialize(control);
                    }
                    if (memo.ScreenController == null && memo.BindScreen(screen))
                    {
                        var temporary = screen;
                        restore.Add(() => { if (memo != null && memo.ScreenController == temporary) memo.BindScreen(null); });
                    }
                }
            }
            catch { Dispose(); throw; }
        }
        private GameObject Create(string path, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("테스트 상호작용 UI 프리팹이 없습니다: " + path);
            var instance = UnityEngine.Object.Instantiate(prefab, parent);
            instance.hideFlags = HideFlags.DontSave;
            owned.Add(instance);
            return instance;
        }
        public void Bind(PlayerInteractionService service, Camera output)
        {
            interaction = service ?? throw new ArgumentNullException(nameof(service));
            camera = output != null ? output : throw new ArgumentNullException(nameof(output));
            var canvas = prompt.GetComponentInParent<Canvas>();
            if (canvas != null) canvas.worldCamera = output;
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
            interaction = null; camera = null; promptModel.Clear();
            if (prompt != null)
            {
                var canvas = prompt.GetComponentInParent<Canvas>();
                if (canvas != null) canvas.worldCamera = null;
            }
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            var errors = new List<Exception>();
            SessionCleanup.Attempt(errors, Unbind);
            foreach (var screen in screens) if (screen != null) SessionCleanup.Attempt(errors, screen.Unbind);
            for (int i = restore.Count - 1; i >= 0; i--) SessionCleanup.Attempt(errors, restore[i]);
            SessionCleanup.Attempt(errors, () => { if (prompt != null) prompt.Unbind(); });
            SessionCleanup.Attempt(errors, promptModel.Dispose);
            foreach (var instance in owned)
                if (instance != null)
                {
                    instance.SetActive(false);
                    if (Application.isPlaying) UnityEngine.Object.Destroy(instance);
                    else UnityEngine.Object.DestroyImmediate(instance);
                }
            restore.Clear(); screens.Clear(); owned.Clear();
            SessionCleanup.ThrowIfAny(errors, "테스트 상호작용 화면 해제 실패");
        }
    }
}
#endif
