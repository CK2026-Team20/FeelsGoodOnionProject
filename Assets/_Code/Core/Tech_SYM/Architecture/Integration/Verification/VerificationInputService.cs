#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace Cooked.Integration.Verification
{
    /// <summary>Explicitly created by a verification scenario, never auto-started in production.
    /// Queues device input through normal InputActions/EventSystem; does not invoke game commands,
    /// set Actor transforms, change puzzle state, or assert that an input succeeded.</summary>
    public sealed class VerificationInputService : IDisposable
    {
        private readonly Keyboard keyboard, previousKeyboard;
        private readonly Mouse mouse, previousMouse;
        private readonly Action<string> trace;
        private readonly InputSettings originalSettings, ownedSettings;
        private readonly HideFlags originalSettingsFlags;
        private readonly bool originalRunInBackground;
        private static bool settingsLeaseActive;
        private bool disposed;
        private Tween holdDelay;
        public VerificationInputService(Action<string> trace)
        {
            this.trace = trace ?? throw new ArgumentNullException(nameof(trace));
            if (!Application.isPlaying) throw new InvalidOperationException("Verification device input requires Play Mode.");
            if (settingsLeaseActive) throw new InvalidOperationException("Only one verification input lease may run at a time.");
            originalSettings = InputSystem.settings;
            originalSettingsFlags = originalSettings.hideFlags;
            originalRunInBackground = Application.runInBackground;
            // LoadSceneAsync may unload unreferenced native ScriptableObjects. Keep the borrowed
            // settings alive until restored; do not save or mutate the persistent configuration.
            // InputManager destroys the previous settings when flags equal HideAndDontSave.
            // Clear HideInHierarchy temporarily as well as pinning the borrowed object.
            originalSettings.hideFlags = (originalSettingsFlags | HideFlags.DontUnloadUnusedAsset) & ~HideFlags.HideInHierarchy;
            ownedSettings = UnityEngine.Object.Instantiate(originalSettings);
            ownedSettings.hideFlags = HideFlags.HideAndDontSave;
            ownedSettings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            ownedSettings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            InputSystem.settings = ownedSettings;
            Application.runInBackground = true;
            settingsLeaseActive = true;
            previousKeyboard = Keyboard.current; previousMouse = Mouse.current;
            keyboard = InputSystem.AddDevice<Keyboard>("CookedVerificationKeyboard");
            try { mouse = InputSystem.AddDevice<Mouse>("CookedVerificationMouse"); }
            catch { InputSystem.RemoveDevice(keyboard); previousKeyboard?.MakeCurrent(); RestoreSettings(); throw; }
            keyboard.MakeCurrent(); mouse.MakeCurrent();
            trace("Verification device input started; successful gameplay must be observed separately.");
        }
        public void SetKeys(params Key[] held)
        {
            ThrowIfDisposed();
            keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(held ?? Array.Empty<Key>()));
            trace("frame=" + Time.frameCount + " keys=" + string.Join(",", held ?? Array.Empty<Key>()));
        }
        public IEnumerator Hold(float unscaledSeconds, params Key[] held)
        {
            if (float.IsNaN(unscaledSeconds) || float.IsInfinity(unscaledSeconds) || unscaledSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(unscaledSeconds));
            ThrowIfDisposed();
            if (holdDelay != null)
                throw new InvalidOperationException("A key hold is already running on this verification device.");
            bool completed = false;
            Tween delay = null;
            try
            {
                // Test input duration uses wall-clock time even while game simulation is paused.
                // Retain the handle until finally so Kill cannot be mistaken for completion.
                delay = DOVirtual.DelayedCall(unscaledSeconds, () => completed = true, true)
                    .SetAutoKill(false);
                holdDelay = delay;
                SetKeys(held);
                do
                {
                    // A frame is also needed for the normal InputSystem to consume queued input.
                    yield return null;
                    ThrowIfDisposed();
                    if (!completed && !delay.IsActive())
                        throw new OperationCanceledException("Verification key hold Tween was cancelled.");
                }
                while (!completed);
            }
            finally
            {
                if (delay != null && delay.IsActive()) delay.Kill(false);
                if (ReferenceEquals(holdDelay, delay)) holdDelay = null;
                if (!disposed) SetKeys();
            }
            yield return null;
        }
        public IEnumerator Click(Button button)
        {
            ThrowIfDisposed();
            if (button == null || !button.isActiveAndEnabled || !button.IsInteractable())
                throw new InvalidOperationException("Cannot click an inactive/uninteractable button.");
            var rect = (RectTransform)button.transform;
            var canvas = button.GetComponentInParent<Canvas>();
            if (canvas == null) throw new InvalidOperationException("Button has no Canvas.");
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay && camera == null)
                throw new InvalidOperationException("Button Canvas has no event camera.");
            Vector2 position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center));
            mouse.MakeCurrent();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            yield return null;
            ThrowIfDisposed();
            InputSystem.QueueStateEvent(mouse, new MouseState { position = position }.WithButton(MouseButton.Left));
            trace("frame=" + Time.frameCount + " pointer-down button=" + button.name + " position=" + position);
            try { yield return null; }
            finally
            {
                if (!disposed) InputSystem.QueueStateEvent(mouse, new MouseState { position = position });
            }
            yield return null;
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            if (holdDelay != null && holdDelay.IsActive()) holdDelay.Kill(false);
            holdDelay = null;
            if (keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse.added) InputSystem.RemoveDevice(mouse);
            if (previousKeyboard != null && previousKeyboard.added) previousKeyboard.MakeCurrent();
            if (previousMouse != null && previousMouse.added) previousMouse.MakeCurrent();
            RestoreSettings();
            trace("Verification devices removed and borrowed input settings restored.");
        }
        private void RestoreSettings()
        {
            try
            {
                if (originalSettings == null)
                    throw new InvalidOperationException("Borrowed input settings were destroyed during verification.");
                InputSystem.settings = originalSettings;
                originalSettings.hideFlags = originalSettingsFlags;
                Application.runInBackground = originalRunInBackground;
            }
            finally
            {
                if (ownedSettings != null) UnityEngine.Object.Destroy(ownedSettings);
                settingsLeaseActive = false;
            }
        }
        private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(VerificationInputService)); }
    }
}
#endif
