#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Cooked.Audio;
using Cooked.UI;
using UnityEditor;
using UnityEngine;
using SettingsService = Cooked.UI.SettingsService;

namespace Cooked.DebugTesting
{
    /// <summary>테스트 세션의 옵션·음량·커서·디버그 화면을 조립하고 자기 자원만 정리합니다.</summary>
    public sealed class DirectSceneTestPresentation : MonoBehaviour, IDisposable
    {
        public OptionsService Options { get; private set; }
        public CursorPresentationBinding Cursor { get; private set; }
        public DebugPanelSession Panel { get; private set; }
        private OptionsView view;
        private OptionsViewModel model;
        private SettingsService settings;
        private AudioRuntimeHost audio;
        private GameObject ui;
        private bool disposed;

        public void Initialize(DirectSceneTestRuntime runtime)
        {
            if (Options != null) throw new InvalidOperationException("테스트 화면은 한 번만 조립합니다.");
            try
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/UI/GlobalUiRoot.prefab");
                if (prefab == null) throw new InvalidOperationException("기존 옵션 UI 프리팹이 없습니다.");
                ui = Instantiate(prefab, transform);
                ui.hideFlags = HideFlags.DontSave;
                view = ui.GetComponentInChildren<OptionsView>(true);
                if (view == null) throw new InvalidOperationException("옵션 View 연결이 없습니다.");
                foreach (var fade in ui.GetComponentsInChildren<FadeView>(true))
                {
                    // FadeView와 OptionsView는 같은 루트에 있으므로 페이드 표시 대상만 숨깁니다.
                    using var serializedFade = new SerializedObject(fade);
                    var cover = serializedFade.FindProperty("cover").objectReferenceValue as CanvasGroup;
                    if (cover == null || cover.gameObject == ui)
                        throw new InvalidOperationException("테스트 옵션과 분리된 페이드 화면이 필요합니다.");
                    cover.gameObject.SetActive(false);
                }
                audio = gameObject.AddComponent<AudioRuntimeHost>();
                var channels = new AudioSource[10];
                for (int i = 0; i < channels.Length; i++)
                {
                    var node = new GameObject("Test Audio " + i);
                    node.transform.SetParent(transform, false);
                    channels[i] = node.AddComponent<AudioSource>();
                    channels[i].playOnAwake = false;
                }
                using (var serialized = new SerializedObject(audio))
                {
                    serialized.FindProperty("catalog").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioCueCatalog>(AudioCueCatalog.AssetPath);
                    var sources = serialized.FindProperty("sources"); sources.arraySize = channels.Length;
                    for (int i = 0; i < channels.Length; i++) sources.GetArrayElementAtIndex(i).objectReferenceValue = channels[i];
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
                settings = new SettingsService(new TestSettingsStore(), audio.CreateService());
                Options = new OptionsService(runtime.Flow, runtime.Control, settings);
                Cursor = new CursorPresentationBinding(runtime.Flow, Options);
                model = new OptionsViewModel(Options, settings);
                view.Bind(model);
                Panel = gameObject.AddComponent<DebugPanelSession>();
                Panel.Bind(runtime, Cursor, runtime.Settings.LogCapacity);
            }
            catch { Dispose(); throw; }
        }
        private void OnApplicationFocus(bool focused) { if (focused) Cursor?.RestoreOnFocus(); }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            var errors = new List<Exception>();
            void Release(Action action) { try { action(); } catch (Exception e) { errors.Add(e); } }
            Release(() => Panel?.Dispose());
            Release(() => { if (view != null) view.Unbind(); });
            Release(() => model?.Dispose());
            Release(() => Cursor?.Dispose());
            Release(() => Options?.Dispose());
            Release(() => settings?.Dispose());
            Release(() => { if (audio != null) audio.Shutdown(); });
            if (ui != null) { if (Application.isPlaying) Destroy(ui); else DestroyImmediate(ui); }
            foreach (var error in errors) Debug.LogException(error, this);
        }
        private void OnDestroy() => Dispose();
        private sealed class TestSettingsStore : ISettingsStore
        {
            private readonly Dictionary<string, float> values = new Dictionary<string, float>();
            public float Read(string key, float fallback) => values.TryGetValue(key, out var value) ? value : PlayerPrefs.GetFloat(key, fallback);
            public void Write(string key, float value) => values[key] = value;
            public void Save() { } // 테스트 음량은 이 세션에만 적용하며 사용자 설정 파일을 덮지 않습니다.
        }
    }
}
#endif
