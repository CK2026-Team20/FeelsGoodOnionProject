using System;
using System.Collections.Generic;
using System.IO;
using Cooked.Contracts;
using Cooked.Level;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cooked.Integration
{
    /// <summary>Reads only the catalog's loaded Stage scenes. It never loads/unloads or owns their components.</summary>
    public sealed class LoadedStageRegistry
    {
        private readonly SceneCatalog catalog;
        private readonly Dictionary<string, StageService> stages = new Dictionary<string, StageService>(StringComparer.Ordinal);
        public LoadedStageRegistry(SceneCatalog catalog) { this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog)); }
        public IReadOnlyCollection<StageService> Stages => new List<StageService>(stages.Values).AsReadOnly();
        public void Refresh(string expectedStageId = null)
        {
            var next = new Dictionary<string, StageService>(StringComparer.Ordinal);
            foreach (string path in catalog.StagePaths)
            {
                var scene = SceneManager.GetSceneByPath(path);
                if (!scene.IsValid() || !scene.isLoaded) continue;
                StageService found = null;
                foreach (var behaviour in SceneComponentLookup.FindAll<MonoBehaviour>(scene))
                    if (behaviour is StageService stage)
                    {
                        if (found != null) throw new InvalidOperationException("Multiple Stage services: " + path);
                        found = stage;
                    }
                if (found == null || found.StageId != Path.GetFileNameWithoutExtension(path))
                    throw new InvalidOperationException("Stage service identity mismatch: " + path);
                _ = found.InitialPose;
                if (!found.ContainsCheckpoint(found.InitialCheckpoint))
                    throw new InvalidOperationException("시작 체크포인트가 같은 스테이지에 속하지 않습니다: " + found.StageId);
                if (next.ContainsKey(found.StageId)) throw new InvalidOperationException("Duplicate Stage: " + found.StageId);
                next.Add(found.StageId, found);
            }
            if (next.Count != catalog.StagePaths.Count || (expectedStageId != null && !next.ContainsKey(expectedStageId)))
                throw new InvalidOperationException("All catalog Stage scenes must be loaded, including selected Stage: " + expectedStageId);
            stages.Clear();
            foreach (var pair in next) stages.Add(pair.Key, pair.Value);
        }
        public StageService Require(string stageId)
        {
            if (!stages.TryGetValue(stageId, out var stage)) throw new InvalidOperationException("Unknown loaded Stage: " + stageId);
            if (stage == null) throw new InvalidOperationException("Stage registry contains a destroyed component: " + stageId);
            return stage;
        }
        public string ScenePath(string stageId)
        {
            foreach (string path in catalog.StagePaths)
                if (Path.GetFileNameWithoutExtension(path) == stageId) return path;
            throw new InvalidOperationException("Unregistered Stage: " + stageId);
        }
        public void Clear() => stages.Clear();
    }
}
