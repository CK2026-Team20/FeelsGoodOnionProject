#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Cooked.Chase.Editor
{
    /// <summary>Run only in the assigned Editor slot. Never edits Level scenes or source model assets.</summary>
    public static class ChaseAssetBuilder
    {
        public const string AssetRoot = "Assets/_Scenes/Tech_SYM/Architecture/Chase";
        public const string PrefabPath = AssetRoot + "/ChaseSwarm.prefab";
        public const string DefinitionPath = AssetRoot + "/ChaseDefinition.asset";
        private const string MonsterPath = "Assets/_Art/Characters/Models/ch_monster.fbx";
        private const string MaterialPath = "Assets/_Art/Characters/Materials/monster_default.mat";

        [MenuItem("Cooked/Architecture/Build Chase Assets")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Chase authoring requires Edit Mode.");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(MonsterPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (model == null || material == null)
                throw new InvalidOperationException("Original monster model/material unavailable; restore LFS assets before authoring.");
            EnsureFolder(AssetRoot);
            var definition = AssetDatabase.LoadAssetAtPath<ChaseDefinition>(DefinitionPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<ChaseDefinition>();
                AssetDatabase.CreateAsset(definition, DefinitionPath);
            }
            definition.CreateSettings(); // validates saved tuning; a repeat build preserves intentional settings
            Scene preview = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                root = new GameObject("ChaseSwarm");
                SceneManager.MoveGameObjectToScene(root, preview);
                var driver = root.AddComponent<ChaseRuntimeDriver>();
                var presentation = root.AddComponent<ChaseSwarmPresentation>();
                var visual = Child(root.transform, "Visual");
                ConfigureFront(root);
                Child(root.transform, "VFX");
                Child(root.transform, "SFX"); // cue playback is owned by the injected Audio service
                for (int i = 0; i < 3; i++)
                {
                    var wrapper = Child(visual.transform, "Monster" + (i + 1).ToString("00"));
                    wrapper.transform.localPosition = new Vector3(-.65f - .6f * i, 0, (i - 1) * .6f);
                    var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, preview);
                    instance.transform.SetParent(wrapper.transform, false);
                    instance.transform.localRotation = Quaternion.Euler(0, 90, 0);
                    if (instance.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                        throw new InvalidOperationException("Imported monster Visual contains scripts; review before using it.");
                    Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
                    if (renderers.Length == 0) throw new InvalidOperationException("Monster has no renderers.");
                    foreach (Renderer renderer in renderers)
                    {
                        var materials = renderer.sharedMaterials;
                        for (int slot = 0; slot < materials.Length; slot++) materials[slot] = material;
                        renderer.sharedMaterials = materials;
                    }
                    Bounds bounds = CombinedBounds(renderers);
                    float size = Math.Max(bounds.size.x, Math.Max(bounds.size.y, bounds.size.z));
                    if (size <= 0 || !ChaseSettings.IsFinite(size)) throw new InvalidOperationException("Invalid monster bounds.");
                    instance.transform.localScale *= 1.2f / size;
                    bounds = CombinedBounds(renderers);
                    instance.transform.position += new Vector3(wrapper.transform.position.x - bounds.center.x,
                        wrapper.transform.position.y - bounds.min.y, wrapper.transform.position.z - bounds.center.z);
                    // Idle in the chase visual must obey Options/world pause like the chase itself.
                    foreach (Animator animator in instance.GetComponentsInChildren<Animator>(true))
                    { animator.applyRootMotion = false; animator.updateMode = AnimatorUpdateMode.Normal; }
                }
                SetReference(driver, "definition", definition);
                SetReference(driver, "presentation", presentation);
                SetReference(presentation, "visual", visual);
                visual.SetActive(false);
                if (PrefabUtility.SaveAsPrefabAsset(root, PrefabPath) == null) throw new IOException("Failed saving " + PrefabPath);
                AssetDatabase.SaveAssetIfDirty(definition);
                Debug.Log("[Cooked.Chase] Saved " + PrefabPath + ". Runtime traversal remains unverified.");
            }
            finally
            {
                if (root != null) Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(preview);
            }
        }

        public static void ConfigureFront(GameObject root)
        {
            var child = root.transform.Find("FrontContact");
            if (child == null) child = Child(root.transform, "FrontContact").transform;
            child.localPosition = new Vector3(.4f,1,0);
            var box = child.GetComponent<BoxCollider>();
            if (box == null) box = child.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true; box.size = new Vector3(.8f,2f,4f);
            var contact = child.GetComponent<ChaseFrontContact>();
            if (contact == null) contact = child.gameObject.AddComponent<ChaseFrontContact>();
            SetReference(contact,"driver",root.GetComponent<ChaseRuntimeDriver>());
        }
        public static void ApplyFront()
        {
            var root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try { ConfigureFront(root); PrefabUtility.SaveAsPrefabAsset(root,PrefabPath); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        private static GameObject Child(Transform parent, string name)
        { var child = new GameObject(name); child.transform.SetParent(parent, false); return child; }
        private static Bounds CombinedBounds(Renderer[] renderers)
        {
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }
        private static void SetReference(Object target, string property, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = path.Substring(0, path.LastIndexOf('/'));
            EnsureFolder(parent);
            if (string.IsNullOrEmpty(AssetDatabase.CreateFolder(parent, path.Substring(path.LastIndexOf('/') + 1))))
                throw new IOException("Cannot create Chase asset folder: " + path);
        }
    }
}
#endif
