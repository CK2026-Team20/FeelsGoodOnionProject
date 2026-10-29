#if UNITY_EDITOR
using System;
using Cooked.Integration;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Cooked.DebugTesting
{
    public static class DirectSceneTestEntry
    {
        private const string SelectionKey = "Cooked.DebugTesting.CurrentScene";
        public static bool IsSelected
        {
            get => SessionState.GetBool(SelectionKey, false);
            set => SessionState.SetBool(SelectionKey, value);
        }
        public static DirectSceneTestRuntime Current { get; private set; }
        public static event Action<DirectSceneTestRuntime> Changed;
        [InitializeOnLoadMethod]
        private static void ObserveEditorLifetime()
        {
            EditorApplication.playModeStateChanged -= OnPlayState;
            EditorApplication.playModeStateChanged += OnPlayState;
            AssemblyReloadEvents.beforeAssemblyReload -= ReleaseTemporarySessions;
            AssemblyReloadEvents.beforeAssemblyReload += ReleaseTemporarySessions;
        }
        private static void OnPlayState(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode) ReleaseTemporarySessions();
        }
        private static void ReleaseTemporarySessions()
        {
            // DontSave objects require explicit destruction, including an assembly reload in Play.
            foreach (var runtime in Resources.FindObjectsOfTypeAll<DirectSceneTestRuntime>())
                if (runtime != null && !EditorUtility.IsPersistent(runtime))
                    UnityEngine.Object.DestroyImmediate(runtime.gameObject);
            Current = null;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRuntime() { Current = null; Changed = null; }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Enter()
        {
            if (!IsSelected || Current != null) return;
            var scene = SceneManager.GetActiveScene();
            var settings = SceneComponentLookup.FindAll<DirectSceneTestSettings>(scene);
            if (settings.Length != 1 || settings[0].StartPoint == null)
            {
                Debug.LogError("현재 씬 테스트: 설정 하나와 플레이어 시작점을 지정하세요.");
                return;
            }
            var root = new GameObject("DirectSceneTestRuntime") { hideFlags = HideFlags.DontSave };
            SceneManager.MoveGameObjectToScene(root, scene);
            var runtime = root.AddComponent<DirectSceneTestRuntime>();
            try
            {
                runtime.Initialize(settings[0]);
                root.AddComponent<DirectSceneTestPresentation>().Initialize(runtime);
                Current = runtime;
                Changed?.Invoke(runtime);
            }
            catch (Exception error)
            {
                Debug.LogException(error, settings[0]);
                UnityEngine.Object.Destroy(root);
            }
        }
        internal static void Released(DirectSceneTestRuntime runtime)
        {
            if (Current != runtime) return;
            Current = null;
            Changed?.Invoke(null);
        }
    }
}
#endif
