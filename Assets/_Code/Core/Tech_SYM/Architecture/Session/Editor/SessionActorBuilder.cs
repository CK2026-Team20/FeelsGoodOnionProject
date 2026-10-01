using System;
using System.IO;
using Cooked.Contracts;
using FeelsGoodOnion.TechSYM.Enemies;
using UnityEditor;
using UnityEngine;

namespace Cooked.Session.Editor
{
    /// <summary>Creates only owned assets through Unity APIs. Does not edit scene/build/package settings.</summary>
    public static class SessionActorBuilder
    {
        public const string AssetRoot = "Assets/_Scenes/Tech_SYM/Architecture/Session";
        public const string ActorPath = AssetRoot + "/PlayerRoot.prefab";
        public const string HostPath = AssetRoot + "/SessionActorHost.prefab";
        public const string OilPath = AssetRoot + "/SessionOil.prefab";
        public const string ScopePath = AssetRoot + "/SessionTransientScope.asset";
        private const string OnionPath = "Assets/_Art/Characters/Models/ch_onion.fbx";
        private const string OnionMaterialPath = "Assets/_Art/Characters/Materials/Ch_onion_default.mat";

        [MenuItem("Cooked/Session/Build Owned Actor Assets")]
        public static void Build()
        {
            EnsureFolder(AssetRoot);
            var scope = GetOrCreate<SessionTransientScope>(ScopePath);
            var tear = GetOrCreate<TearSkillDefinition>(AssetRoot + "/TearSkill.asset");
            var shrink = GetOrCreate<ShrinkSkillDefinition>(AssetRoot + "/ShrinkSkill.asset");
            var restore = GetOrCreate<RestoreFormSkillDefinition>(AssetRoot + "/RestoreSkill.asset");
            ConfigureSkill(tear, 3f); ConfigureSkill(shrink, .25f); ConfigureSkill(restore, .25f);
            var ts = new SerializedObject(tear); ts.FindProperty("requiredFragments").intValue = 1;
            ts.FindProperty("effectRadius").floatValue = 3; ts.FindProperty("stunDuration").floatValue = 2;
            ts.ApplyModifiedPropertiesWithoutUndo();
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(OnionPath);
            if (model == null) throw new InvalidOperationException("Onion model missing: " + OnionPath);
            var debris = BuildDebris(model);
            GameObject root = new GameObject("PlayerRoot");
            root.SetActive(false);
            try
            {
                root.AddComponent<GameplayInputRouter>();
                var body = Child(root.transform, "ActorBody");
                var capsule = body.AddComponent<CapsuleCollider>(); capsule.center = Vector3.zero; capsule.height = 2; capsule.radius = .5f;
                var rigidbody = body.AddComponent<Rigidbody>(); rigidbody.useGravity = false;
                var visual = Child(body.transform, "VisualRoot");
                BuildVisual(visual.transform, "NormalVisual", model, 2f);
                BuildVisual(visual.transform, "SmallVisual", model, 1f).transform.localPosition = Vector3.down * .5f;
                Child(body.transform, "CameraAnchor").transform.localPosition = Vector3.up * .4f;
                Child(body.transform, "VFX"); Child(body.transform, "SFX");
                var facade = body.AddComponent<PlayerFacade>();
                body.AddComponent<PlayerInputReader>();
                body.AddComponent<CharacterSlipperyEffect>();
                var fs = new SerializedObject(facade);
                fs.FindProperty("tearSkillDefinition").objectReferenceValue = tear;
                fs.FindProperty("shrinkSkillDefinition").objectReferenceValue = shrink;
                fs.FindProperty("restoreFormSkillDefinition").objectReferenceValue = restore;
                fs.FindProperty("initialHP").intValue = 3; fs.FindProperty("maxHP").intValue = 3;
                fs.FindProperty("initialSkillFragment").intValue = 0; fs.ApplyModifiedPropertiesWithoutUndo();
                var form = new SerializedObject(body.GetComponent<PlayerFormController>());
                form.FindProperty("visualRoot").objectReferenceValue = visual.transform;
                form.FindProperty("debrisPrefab").objectReferenceValue = debris;
                form.FindProperty("smallHeight").floatValue = 1; form.FindProperty("smallRadius").floatValue = .25f;
                form.FindProperty("normalJumpHeight").floatValue = 2; form.FindProperty("smallJumpHeight").floatValue = 3;
                form.ApplyModifiedPropertiesWithoutUndo();
                var motion = new SerializedObject(body.GetComponent<CharacterMovement>());
                motion.FindProperty("moveSpeed").floatValue = 5; motion.FindProperty("jumpHeight").floatValue = 2;
                motion.ApplyModifiedPropertiesWithoutUndo();
                var facing = new SerializedObject(root.AddComponent<ActorFacingPresentation>());
                facing.FindProperty("movement").objectReferenceValue = body.GetComponent<CharacterMovement>();
                facing.FindProperty("visualRoot").objectReferenceValue = visual.transform;
                facing.ApplyModifiedPropertiesWithoutUndo();
                var fall = new SerializedObject(root.AddComponent<ActorFallGuard>());
                fall.FindProperty("actor").objectReferenceValue = facade; fall.ApplyModifiedPropertiesWithoutUndo();
                SessionActorHost.ValidateInputOwnership(root);
                PrefabUtility.SaveAsPrefabAsset(root, ActorPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            BuildOil(scope);
            var host = new GameObject("SessionActorHost");
            try
            {
                var serialized = new SerializedObject(host.AddComponent<SessionActorHost>());
                serialized.FindProperty("actorPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(ActorPath);
                serialized.FindProperty("transientScope").objectReferenceValue = scope;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(host, HostPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(host); }
            AssetDatabase.SaveAssets();
        }
        private static PlayerDebris BuildDebris(GameObject model)
        {
            // HMS Instantiate(PlayerDebris) clones the component GO; it must be the empty prefab root.
            var root = new GameObject("ShellRoot"); root.SetActive(false);
            try
            {
                var box = root.AddComponent<BoxCollider>(); box.size = new Vector3(1, .7f, 1); box.center = Vector3.up * .35f;
                root.AddComponent<Rigidbody>();
                root.AddComponent<PlayerDebris>(); root.AddComponent<PlatformRigidbodyMovement>();
                BuildVisual(root.transform, "Visual", model, .7f).transform.localPosition = Vector3.up * .35f;
                root.SetActive(true);
                var prefab = PrefabUtility.SaveAsPrefabAsset(root, AssetRoot + "/ShellRoot.prefab");
                return prefab.GetComponent<PlayerDebris>();
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static void BuildOil(SessionTransientScope scope)
        {
            // EnemyOilOnDeath validates OilSurface and EnemyOilLifetime on the prefab root.
            var root = new GameObject("SessionOil"); root.SetActive(false);
            try
            {
                var collider = root.AddComponent<BoxCollider>(); collider.size = new Vector3(1, .08f, 1); collider.isTrigger = true;
                root.AddComponent<OilSurface>(); root.AddComponent<EnemyOilLifetime>();
                var lifetime = new SerializedObject(root.AddComponent<SessionTransientLifetime>());
                lifetime.FindProperty("scope").objectReferenceValue = scope; lifetime.ApplyModifiedPropertiesWithoutUndo();
                var visual = GameObject.CreatePrimitive(PrimitiveType.Cube); visual.name = "Visual"; visual.transform.SetParent(root.transform, false);
                UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>()); visual.transform.localScale = new Vector3(1, .025f, 1);
                var material = AssetDatabase.LoadAssetAtPath<Material>(AssetRoot + "/OilMaterial.mat");
                if (material == null)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    if (shader == null) throw new InvalidOperationException("No supported prototype shader.");
                    material = new Material(shader); AssetDatabase.CreateAsset(material, AssetRoot + "/OilMaterial.mat");
                }
                material.color = new Color(.14f, .09f, .015f); EditorUtility.SetDirty(material);
                visual.GetComponent<Renderer>().sharedMaterial = material;
                root.SetActive(true); PrefabUtility.SaveAsPrefabAsset(root, OilPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        private static GameObject BuildVisual(Transform parent, string name, GameObject model, float height)
        {
            var visual = Child(parent, name);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model); instance.transform.SetParent(visual.transform, false);
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) throw new InvalidOperationException("Character has no renderers.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(OnionMaterialPath);
            if (material == null) throw new InvalidOperationException("Onion material missing: " + OnionMaterialPath);
            foreach (var renderer in renderers)
            {
                var materials = renderer.sharedMaterials;
                for (int i = 0; i < materials.Length; i++) materials[i] = material;
                renderer.sharedMaterials = materials;
            }
            Bounds bounds = renderers[0].bounds; foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            if (bounds.size.y < .0001f) throw new InvalidOperationException("Character bounds are empty.");
            float scale = height / bounds.size.y;
            Vector3 offset = bounds.center - visual.transform.position;
            instance.transform.localScale *= scale;
            instance.transform.localPosition -= offset * scale;
            return visual;
        }
        private static GameObject Child(Transform parent, string name) { var child = new GameObject(name); child.transform.SetParent(parent, false); return child; }
        private static T GetOrCreate<T>(string path) where T : ScriptableObject
        {
            var value = AssetDatabase.LoadAssetAtPath<T>(path);
            if (value != null) return value;
            value = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(value, path); return value;
        }
        private static void ConfigureSkill(SkillDefinition definition, float cooldown)
        {
            var serialized = new SerializedObject(definition);
            serialized.FindProperty("cooldown").floatValue = cooldown;
            serialized.FindProperty("initCooldown").floatValue = 0;
            serialized.FindProperty("preparationTime").floatValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void EnsureFolder(string path)
        {
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}

