using System;
using System.Collections.Generic;
using System.IO;
using Cooked.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cooked.Integration
{
    /// <summary>Reads only the catalog's loaded Stage scenes. It never loads/unloads or owns their components.</summary>
    public sealed class LoadedStageRegistry
    {
        private readonly SceneCatalog catalog;
        private readonly Dictionary<string, IStageService> stages = new Dictionary<string, IStageService>(StringComparer.Ordinal);
        public LoadedStageRegistry(SceneCatalog catalog) { this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog)); }
        public IReadOnlyCollection<IStageService> Stages => new List<IStageService>(stages.Values).AsReadOnly();
        public void Refresh()
        {
            var next = new Dictionary<string, IStageService>(StringComparer.Ordinal);
            foreach (string path in catalog.StagePaths)
            {
                var scene = SceneManager.GetSceneByPath(path);
                if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Stage not loaded: " + path);
                IStageService found = null;
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
                        if (behaviour is IStageService stage)
                        {
                            if (found != null) throw new InvalidOperationException("Multiple Stage services: " + path);
                            found = stage;
                        }
                if (found == null || found.StageId != Path.GetFileNameWithoutExtension(path))
                    throw new InvalidOperationException("Stage service identity mismatch: " + path);
                if (!found.TryGetCheckpointPose(found.InitialCheckpointId, out _))
                    throw new InvalidOperationException("Initial checkpoint missing: " + found.StageId);
                if (next.ContainsKey(found.StageId)) throw new InvalidOperationException("Duplicate Stage: " + found.StageId);
                next.Add(found.StageId, found);
            }
            stages.Clear();
            foreach (var pair in next) stages.Add(pair.Key, pair.Value);
        }
        public IStageService Require(string stageId)
        {
            if (!stages.TryGetValue(stageId, out var stage)) throw new InvalidOperationException("Unknown loaded Stage: " + stageId);
            if (stage is UnityEngine.Object obj && obj == null) throw new InvalidOperationException("Stage registry contains a destroyed component: " + stageId);
            return stage;
        }
        public void Clear() => stages.Clear();
    }
}
