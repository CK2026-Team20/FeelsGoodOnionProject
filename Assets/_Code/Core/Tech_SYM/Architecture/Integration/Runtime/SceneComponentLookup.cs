using System;
using System.Collections.Generic;
using FeelsGoodOnion.TechSYM.Enemies;
using FeelsGoodOnion.TechSYM.Features;
using FeelsGoodOnion.TechSYM.OvenTray;
using FeelsGoodOnion.TechSYM.Platforms;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cooked.Integration
{
    /// <summary>Composition-only lookup constrained to one catalog scene; never selects another session by name.</summary>
    public static class SceneComponentLookup
    {
        public static T[] FindAll<T>(Scene scene) where T : Component
        {
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("Scene not loaded: " + scene.path);
            var found = new List<T>();
            foreach (var root in scene.GetRootGameObjects())
                foreach (var component in root.GetComponentsInChildren<T>(true))
                    if (component != null && component.gameObject.scene == scene) found.Add(component);
            return found.ToArray();
        }
        public static T RequireSingle<T>(string scenePath) where T : Component
            => RequireSingle<T>(SceneManager.GetSceneByPath(scenePath));
        public static T RequireSingle<T>(Scene scene) where T : Component
        {
            T found = null;
            foreach (var component in FindAll<T>(scene))
            {
                if (found != null) throw new InvalidOperationException("Multiple " + typeof(T).Name + " in " + scene.path);
                found = component;
            }
            return found != null ? found : throw new InvalidOperationException("Missing " + typeof(T).Name + " in " + scene.path);
        }
        public static void RequireSameScene(Component owner, Component reference, string fieldName)
        {
            if (owner == null || reference == null || !owner.gameObject.scene.IsValid() ||
                !owner.gameObject.scene.isLoaded || owner.gameObject.scene != reference.gameObject.scene)
                throw new InvalidOperationException(fieldName + ": 같은 로드된 씬의 실물 참조를 연결하세요.");
        }
        /// <summary>World targets may share a static rotated/scaled parent, but must not travel with an Actor or mover.</summary>
        public static void RequireFixedPoint(Component owner, Transform point, string fieldName)
        {
            RequireSameScene(owner, point, fieldName);
            for (var parent = point; parent != null; parent = parent.parent)
                if (parent.GetComponent<Rigidbody>() != null || parent.GetComponent<KinematicPlatformMotion>() != null ||
                    parent.GetComponent<LinearShuttlePlatform>() != null || parent.GetComponent<FloatingPlatform>() != null ||
                    parent.GetComponent<PressurePlatePlatform>() != null || parent.GetComponent<PeriodicPlatform>() != null ||
                    parent.GetComponent<ShatteredPlatform>() != null || parent.GetComponent<Animator>() != null ||
                    parent.GetComponent<Animation>() != null ||
                    parent.GetComponent<PressureLinkedPlatform>() != null || parent.GetComponent<OvenTrayObject>() != null ||
                    parent.GetComponent<EnemyActor>() != null || parent.GetComponent<PlayerFacade>() != null ||
                    parent.GetComponent<PlayerDebris>() != null)
                    throw new InvalidOperationException(fieldName + ": 움직이는 몸체 밖의 고정 지점을 연결하세요.");
        }
    }
}
