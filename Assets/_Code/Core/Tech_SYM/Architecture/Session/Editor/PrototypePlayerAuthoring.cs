using System;
using UnityEditor;
using UnityEngine;

namespace Cooked.Session.Editor
{
    /// <summary>주 작업자만 호출. Prefab contents에서 기존 구성을 보존하여 증분 연결합니다.</summary>
    public static class PrototypePlayerAuthoring
    {
        public const string AnimationPath = SessionActorBuilder.AssetRoot + "/PrototypePlayerAnimations.asset";

        [MenuItem("Cooked/Session/Apply Prototype Player Connections")]
        public static void ApplyOwnedActor() => Apply(SessionActorBuilder.ActorPath);

        public static void Apply(string prefabPath)
        {
            if (Application.isPlaying) throw new InvalidOperationException("Edit Mode에서만 연결합니다.");
            RequireOwned(prefabPath);
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                SessionActorHost.ValidateInputOwnership(root);
                if (root.activeSelf) throw new InvalidOperationException("PlayerRoot는 비활성 프리팹이어야 합니다.");
                var actors = root.GetComponentsInChildren<PlayerFacade>(true);
                if (actors.Length != 1) throw new InvalidOperationException("PlayerFacade가 하나여야 합니다.");
                var actor = actors[0];
                var data = new SerializedObject(actor);
                var tear = RequireDefinition<TearSkillDefinition>(data, "tearSkillDefinition");
                var shrink = RequireDefinition<ShrinkSkillDefinition>(data, "shrinkSkillDefinition");
                var restore = RequireDefinition<RestoreFormSkillDefinition>(data, "restoreFormSkillDefinition");
                var visual = actor.transform.Find("VisualRoot");
                if (visual == null) throw new InvalidOperationException("기존 VisualRoot 연결이 없습니다.");
                var renderers = visual.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0) throw new InvalidOperationException("피격 표시 Renderer가 없습니다.");

                ConfigureSkill(tear); ConfigureSkill(shrink); ConfigureSkill(restore);
                var tearData = new SerializedObject(tear);
                tearData.FindProperty("requiredFragments").intValue = 5;
                tearData.FindProperty("effectRadius").floatValue = 3f;
                tearData.FindProperty("stunDuration").floatValue = 2f;
                tearData.ApplyModifiedPropertiesWithoutUndo();

                // 실제 모델/Animator/클립은 추가하거나 교체하지 않습니다.
                var requests = actor.GetComponent<PlayerAnimationController>() ?? actor.gameObject.AddComponent<PlayerAnimationController>();
                var output = actor.GetComponent<PlayerAnimatorOutput>() ?? actor.gameObject.AddComponent<PlayerAnimatorOutput>();
                requests.enabled = true; output.enabled = true;
                var outputData = new SerializedObject(output);
                if (outputData.FindProperty("animationSet").objectReferenceValue == null)
                {
                    var set = AssetDatabase.LoadAssetAtPath<PlayerAnimationSet>(AnimationPath);
                    if (set == null)
                    {
                        if (AssetDatabase.LoadMainAssetAtPath(AnimationPath) != null)
                            throw new InvalidOperationException("AnimationPath에 다른 종류의 자산이 있습니다.");
                        set = ScriptableObject.CreateInstance<PlayerAnimationSet>();
                        AssetDatabase.CreateAsset(set, AnimationPath);
                    }
                    outputData.FindProperty("animationSet").objectReferenceValue = set;
                    outputData.ApplyModifiedPropertiesWithoutUndo();
                    AssetDatabase.SaveAssetIfDirty(set);
                }
                var damage = actor.GetComponent<PlayerDamagePresentation>() ?? actor.gameObject.AddComponent<PlayerDamagePresentation>();
                damage.enabled = true;
                var damageData = new SerializedObject(damage);
                damageData.FindProperty("player").objectReferenceValue = actor;
                var targets = damageData.FindProperty("targets");
                targets.arraySize = renderers.Length;
                for (int i = 0; i < renderers.Length; i++) targets.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
                damageData.ApplyModifiedPropertiesWithoutUndo();

                AssetDatabase.SaveAssetIfDirty(tear);
                AssetDatabase.SaveAssetIfDirty(shrink);
                AssetDatabase.SaveAssetIfDirty(restore);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath, out bool saved);
                if (!saved) throw new InvalidOperationException("PlayerRoot 저장 실패: " + prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static T RequireDefinition<T>(SerializedObject actor, string property) where T : SkillDefinition
        {
            var definition = actor.FindProperty(property).objectReferenceValue as T;
            if (definition == null) throw new InvalidOperationException("누락된 스킬 설정: " + property);
            RequireOwned(AssetDatabase.GetAssetPath(definition));
            return definition;
        }

        private static void ConfigureSkill(SkillDefinition definition)
        {
            var data = new SerializedObject(definition);
            data.FindProperty("cooldown").floatValue = 0;
            data.FindProperty("initCooldown").floatValue = 0;
            data.FindProperty("preparationTime").floatValue = 0;
            data.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RequireOwned(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Contains("..") || path.Contains("\\") ||
                !path.StartsWith(SessionActorBuilder.AssetRoot + "/", StringComparison.Ordinal))
                throw new InvalidOperationException("SYM Session 소유 자산만 연결할 수 있습니다: " + path);
        }
    }
}
