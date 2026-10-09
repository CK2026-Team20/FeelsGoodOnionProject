using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cooked.Authoring;
using UnityEngine;
namespace Cooked.Contracts
{
    /// <summary>SceneAsset 연결과 숨은 실행 경로. 편집/빌드 소유자가 ResolvePath 후 자산을 저장한다.</summary>
    [Serializable]
    public sealed class SceneReference
    {
        [SerializeField, HideInInspector] private string runtimePath;
#if UNITY_EDITOR
        [SerializeField, InspectorLabel("씬"), Tooltip("사용할 SceneAsset를 연결하세요. 실행 경로는 이 자산에서 생성됩니다.")]
        private UnityEditor.SceneAsset scene;
        public UnityEditor.SceneAsset EditorAsset => scene;
#endif
        public string RuntimePath => ResolvePath("씬");
        public string StageId => SceneCatalog.StageIdFromPath(RuntimePath);
        public SceneReference() { }

        // 코드 구성/기존 경로 이관용. Inspector에는 문자열 입력을 노출하지 않는다.
        public SceneReference(string runtimePath)
        {
            SceneCatalog.StageIdFromPath(runtimePath);
#if UNITY_EDITOR
            scene = SceneCatalog.RequireSceneAsset(runtimePath, "씬");
#endif
            this.runtimePath = runtimePath;
        }
#if UNITY_EDITOR
        public SceneReference(UnityEditor.SceneAsset scene)
        {
            runtimePath = SceneCatalog.RequireScenePath(scene, "씬");
            this.scene = scene;
        }
#endif
        public string ResolvePath(string fieldName)
        {
#if UNITY_EDITOR
            string resolved = SceneCatalog.RequireScenePath(scene, fieldName);
            runtimePath = resolved;
#else
            try { SceneCatalog.StageIdFromPath(runtimePath); }
            catch (ArgumentException error) { throw new InvalidOperationException(fieldName + ": " + error.Message, error); }
#endif
            return runtimePath;
        }
    }

    public sealed class SceneCatalog
    {
        public string BootstrapPath { get; }
        public string TitlePath { get; }
        public string CorePath { get; }
        public IReadOnlyList<string> StagePaths { get; }
        public SceneCatalog(string bootstrapPath, string titlePath, string corePath, IEnumerable<string> stagePaths)
        {
            string[] stages = stagePaths?.ToArray() ?? throw new ArgumentNullException(nameof(stagePaths));
            ValidatePath(bootstrapPath, "Bootstrap"); ValidatePath(titlePath, "Title"); ValidatePath(corePath, "Core");
            if (stages.Length == 0) throw new ArgumentException("Stage 씬을 하나 이상 연결하세요.", nameof(stagePaths));
            var paths = new HashSet<string>(StringComparer.Ordinal) { bootstrapPath };
            if (!paths.Add(titlePath)) throw new ArgumentException("Title 씬이 Bootstrap과 중복됩니다.");
            if (!paths.Add(corePath)) throw new ArgumentException("Core 씬이 Bootstrap/Title과 중복됩니다.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < stages.Length; i++)
            {
                ValidatePath(stages[i], "Stage[" + i + "]");
                if (!paths.Add(stages[i])) throw new ArgumentException("Stage[" + i + "] 씬 중복: " + stages[i]);
                if (!ids.Add(StageIdFromPath(stages[i]))) throw new ArgumentException("Stage 식별 중복: " + stages[i]);
            }
            BootstrapPath = bootstrapPath; TitlePath = titlePath; CorePath = corePath;
            StagePaths = Array.AsReadOnly(stages);
        }

        public static string StageIdFromPath(string scenePath)
        {
            ValidatePath(scenePath, "Stage");
            return Path.GetFileNameWithoutExtension(scenePath);
        }

        public string GetStageId(string scenePath)
        {
            if (!StagePaths.Contains(scenePath)) throw new ArgumentException("등록되지 않은 Stage 씬: " + scenePath, nameof(scenePath));
            return StageIdFromPath(scenePath);
        }

        public string GetStagePath(string stageId)
        {
            foreach (string path in StagePaths) if (StageIdFromPath(path) == stageId) return path;
            throw new ArgumentException("등록되지 않은 Stage: " + stageId, nameof(stageId));
        }

        private static void ValidatePath(string path, string field)
        {
            if (string.IsNullOrWhiteSpace(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !path.EndsWith(".unity", StringComparison.Ordinal) || path.Contains('\\') ||
                path.Split('/').Any(segment => segment == ".." || segment == "." || segment.Length == 0) ||
                string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(path)))
                throw new ArgumentException(field + " 씬 경로가 유효하지 않습니다: " + path);
        }

#if UNITY_EDITOR
        // SceneAsset의 GUID 연결을 현재 경로로 해석한다. 이동 전 문자열로 대체하지 않는다.
        public static string RequireScenePath(UnityEditor.SceneAsset scene, string field)
        {
            if (scene == null) throw new InvalidOperationException(field + ": SceneAsset를 연결하세요.");
            string path = UnityEditor.AssetDatabase.GetAssetPath(scene);
            ValidatePath(path, field);
            return path;
        }

        public static UnityEditor.SceneAsset RequireSceneAsset(string path, string field)
        {
            ValidatePath(path, field);
            var scene = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEditor.SceneAsset>(path);
            if (scene == null) throw new InvalidOperationException(field + ": 씬이 없거나 이동했습니다: " + path);
            return scene;
        }
#endif
    }
    public interface ISceneService : IDisposable
    {
        bool IsLoaded(string scenePath);
        IEnumerator LoadAdditive(string scenePath, CancellationToken token, OperationResult result);
        IEnumerator Unload(string scenePath, CancellationToken token, OperationResult result);
        void SetActive(string scenePath);
        // Restores authored root activeSelf; never enables intentionally inactive children.
        void SetRootsActive(string scenePath, bool active);
    }
    public interface IGameRuntimeService
    {
        string ActiveStageId { get; }
        // Valid only after InitializeSession and before ShutdownSession. Never cached by app flow.
        ICinematicService Cinematics { get; }
        void SetGameplayVisible(bool visible);
        // Safe with no active session; resolves only the current session DialogueService.
        void CancelDialogue();
        IEnumerator PrepareCurrentStageForRetry(CancellationToken token, OperationResult result);
        IEnumerator InitializeSession(string entryStageId, CancellationToken token, OperationResult result);
        IEnumerator SpawnActorAtCheckpoint(CancellationToken token, OperationResult result);
        IEnumerator DespawnActor(CancellationToken token, OperationResult result);
        IEnumerator ShutdownSession(CancellationToken token, OperationResult result);
    }
}
