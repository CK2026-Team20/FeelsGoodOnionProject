#if UNITY_EDITOR
using System;
using Cooked.UI;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
namespace Cooked.DebugTesting
{
    /// <summary>Owns temporary overlay, log subscription and bindings. Does not own runtime/cursor.</summary>
    public sealed class DebugPanelSession : MonoBehaviour
    {
        public DebugPanelViewModel ViewModel { get; private set; }
        public DebugPanelView View { get; private set; }
        public DebugLogBuffer Logs { get; private set; }
        private CursorPresentationBinding cursor;
        private GameObject ownedEventSystem;
        private float nextRefresh;
        private bool disposed;
        public void Bind(DirectSceneTestRuntime runtime, CursorPresentationBinding cursorBinding, int logCapacity = 200)
        {
            if (ViewModel != null || disposed) throw new InvalidOperationException("디버그 패널 세션은 한 번만 연결합니다.");
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));
            cursor = cursorBinding ?? throw new ArgumentNullException(nameof(cursorBinding));
            try
            {
                Logs = new DebugLogBuffer(logCapacity);
                ViewModel = new DebugPanelViewModel(runtime, Logs);
                ViewModel.VisibilityChanged += cursor.SetDebugVisible;
                cursor.SetDebugVisible(false);
                if (EventSystem.current == null)
                {
                    ownedEventSystem = new GameObject("Debug Pointer Events", typeof(EventSystem), typeof(InputSystemUIInputModule));
                    ownedEventSystem.hideFlags = HideFlags.DontSave;
                    ownedEventSystem.transform.SetParent(transform, false);
                    var module = ownedEventSystem.GetComponent<InputSystemUIInputModule>();
                    module.AssignDefaultActions();
                    module.move = null; module.submit = null; module.cancel = null;
                    ownedEventSystem.GetComponent<EventSystem>().sendNavigationEvents = false;
                }
                var viewRoot = new GameObject("Debug Panel", typeof(RectTransform));
                viewRoot.hideFlags = HideFlags.DontSave;
                viewRoot.transform.SetParent(transform, false);
                View = viewRoot.AddComponent<DebugPanelView>();
                View.Bind(ViewModel, AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Art/UI/Fonts/IM_Hyemin-Bold SDF.asset"));
            }
            catch { Dispose(); throw; }
        }
        private void Update()
        {
            if (disposed || ViewModel == null) return;
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame) ViewModel.ToggleVisible();
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + .1f;
            ViewModel.Refresh();
        }
        private void OnApplicationFocus(bool focused) { if (focused && !disposed) cursor?.RestoreOnFocus(); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (ViewModel != null && cursor != null) ViewModel.VisibilityChanged -= cursor.SetDebugVisible;
            View?.Unbind();
            try { ViewModel?.Dispose(); }
            finally
            {
                Logs?.Dispose();
                cursor?.SetDebugVisible(false); cursor = null;
                if (View != null) { if (Application.isPlaying) Destroy(View.gameObject); else DestroyImmediate(View.gameObject); }
                if (ownedEventSystem != null) { if (Application.isPlaying) Destroy(ownedEventSystem); else DestroyImmediate(ownedEventSystem); }
            }
        }
        private void OnDestroy() => Dispose();
    }
}
#endif
