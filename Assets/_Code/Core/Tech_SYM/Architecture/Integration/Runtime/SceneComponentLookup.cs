using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cooked.Integration
{
    /// <summary>Composition-only lookup constrained to one catalog scene; never selects another session by name.</summary>
    public static class SceneComponentLookup
    {
        public static T RequireSingle<T>(string scenePath) where T : Component
        {
            var scene = SceneManager.GetSceneByPath(scenePath);
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Scene not loaded: " + scenePath);
            T found = null;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var component in root.GetComponentsInChildren<T>(true))
                {
                    if (found != null) throw new InvalidOperationException("Multiple " + typeof(T).Name + " in " + scenePath);
                    found = component;
                }
            return found != null ? found : throw new InvalidOperationException("Missing " + typeof(T).Name + " in " + scenePath);
        }
    }
}
