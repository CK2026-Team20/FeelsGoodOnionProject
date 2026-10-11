using System;
using System.Collections.Generic;
using FeelsGoodOnion.TechSYM.Enemies;
using FeelsGoodOnion.TechSYM.Features;
using Cooked.Chase;
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
        /// <summary>Validates assigned objects without querying scene contents; safe during Awake/OnEnable.</summary>
        public static void RequireSameScene(Component owner, Component reference, string fieldName)
        {
            if (owner == null)
                throw new InvalidOperationException(fieldName + ": 참조를 검사할 컴포넌트가 없습니다.");
            if (reference == null)
                throw new InvalidOperationException(fieldName + ": 씬 오브젝트의 실제 컴포넌트를 연결하세요. 연결 대상이 비어 있습니다.");
            var ownerScene = owner.gameObject.scene;
            var referenceScene = reference.gameObject.scene;
            if (!ownerScene.IsValid() || !referenceScene.IsValid() || ownerScene != referenceScene)
                throw new InvalidOperationException(fieldName + ": 같은 씬의 컴포넌트를 연결하세요. 검사 대상 '" +
                    owner.name + "'의 씬: '" + ownerScene.path + "', 연결 대상 '" + reference.name +
                    "'의 씬: '" + referenceScene.path + "'.");
        }
        /// <summary>World targets may share a static rotated/scaled parent, but must not travel with an Actor or mover.</summary>
        public static void RequireFixedPoint(Component owner, Transform point, string fieldName)
        {
            RequireSameScene(owner, point, fieldName);
            if (!IsFixedPoint(owner, point))
                throw new InvalidOperationException(fieldName + ": 같은 씬의 유한한 고정 지점을 움직이는 몸체 밖에 연결하세요.");
        }
        /// <summary>Checks actual Transform writers. A static logical controller Root does not move its Body's siblings.</summary>
        public static bool IsFixedPoint(Component owner, Transform point)
        {
            if (owner == null || point == null || !owner.gameObject.scene.IsValid() ||
                owner.gameObject.scene != point.gameObject.scene || !global::Cooked.Level.CameraZoneRequest.IsFinite(point.position)) return false;
            for (var parent = point; parent != null; parent = parent.parent)
            {
                if (parent.GetComponent<Rigidbody>() != null || parent.GetComponent<Animator>() != null ||
                    parent.GetComponent<Animation>() != null ||
                    parent.GetComponent<PlayerFacade>() != null || parent.GetComponent<PlayerDebris>() != null ||
                    parent.GetComponent<ChaseRuntimeDriver>() != null || parent.GetComponent<ChaseSwarmPresentation>() != null) return false;
                var enemy = parent.GetComponent<EnemyActor>();
                if (enemy != null && enemy.MotionBody != null && point.IsChildOf(enemy.MotionBody)) return false;
                var shattered = parent.GetComponent<ShatteredPlatform>();
                if (shattered != null && shattered.OwnsMovingVisual(point)) return false;
            }
            // Tear's sensor may be a sibling of the point. Read its explicit moving Root once within this hierarchy,
            // rather than searching every loaded scene or inferring motion from controller type names.
            foreach (var pickup in point.root.GetComponentsInChildren<TearFragment>(true))
            {
                var movingRoot = pickup.MotionRoot;
                if (movingRoot != null && movingRoot.gameObject.scene == point.gameObject.scene &&
                    (point == movingRoot || point.IsChildOf(movingRoot))) return false;
            }
            return true;
        }
    }
}
