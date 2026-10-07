using System;
using System.IO;
using Cooked.Contracts;
using Cooked.Integration.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using Unity.Cinemachine;
using UnityEngine;

namespace Cooked.Session.Editor
{
    /// <summary>Creates only owned assets through Unity APIs. Does not edit scene/build/package settings.</summary>
    public static class SessionActorBuilder
    {
        public const string SourceRoot = "Assets/_Scenes/Tech_SYM/Architecture/Session";
        public static string AssetRoot => AuthoringOutputScope.Map(SourceRoot);
        public static string ActorPath => AssetRoot + "/PlayerRoot.prefab";
        public static string HostPath => AssetRoot + "/SessionActorHost.prefab";
        private const string OnionPath = "Assets/_Art/Characters/Models/ch_onion.fbx";
        private const string OnionMaterialPath = "Assets/_Art/Characters/Materials/Ch_onion_default.mat";

        public const string HmsReferenceScene = "Assets/_Scenes/Tech_HMS/01_PlayerMovementTest.unity";
        /// <summary>Explicit independent-scene HMS assembly. Does not opt independent consumers into integrated routing.</summary>
        public static GameObject AssembleIndependentHmsCamera(GameObject owner, PlayerFacade facade)
        {
            AuthoringOutputScope.RequireEditMode();
            if(owner==null || facade==null) throw new ArgumentNullException(nameof(owner));
            if(facade.transform!=owner.transform && !facade.transform.IsChildOf(owner.transform)) throw new InvalidOperationException("Independent camera owner must own its actor.");
            if(owner.transform.Find("IndependentCameraRig")!=null) throw new InvalidOperationException("An independent rig already exists; main must explicitly migrate it, not duplicate it.");
            var visual=facade.transform.Find("VisualRoot");
            if(visual==null || visual.Find("NormalVisual")==null || visual.Find("SmallVisual")==null || visual.GetComponentsInChildren<Collider>(true).Length!=0 || visual.GetComponentsInChildren<Rigidbody>(true).Length!=0)
                throw new InvalidOperationException("Independent HMS requires its actual physics-free VisualRoot.");
            GameObject rig=null;
            UnityEngine.SceneManagement.Scene reference=default;
            try
            {
                reference=EditorSceneManager.OpenPreviewScene(HmsReferenceScene);
                PlayerCameraController source=null;
                foreach(var item in reference.GetRootGameObjects()) if((source=item.GetComponentInChildren<PlayerCameraController>(true))!=null) break;
                if(source==null) throw new InvalidOperationException("HMS independent camera reference is missing.");
                var mode=facade.GetComponent<PlayerMovementModeController>();
                if(mode==null) mode=facade.gameObject.AddComponent<PlayerMovementModeController>();
                var modeFields=new SerializedObject(mode);
                foreach(string field in new[]{"sideMoveSpeed","quarterMoveSpeed","backMoveSpeed"}) modeFields.FindProperty(field).floatValue=5f;
                modeFields.ApplyModifiedPropertiesWithoutUndo();
                var presentation=facade.GetComponent<PlayerVisualController>();
                if(presentation==null) presentation=facade.gameObject.AddComponent<PlayerVisualController>();
                var presentationFields=new SerializedObject(presentation);presentationFields.FindProperty("playerVisualRoot").objectReferenceValue=visual;presentationFields.ApplyModifiedPropertiesWithoutUndo();
                rig=new GameObject("IndependentCameraRig");rig.SetActive(false);rig.transform.SetParent(owner.transform,false);
                var sourceFields=new SerializedObject(source);var names=new[]{"sideCamera","quarterCamera","backCamera","backFixedCamera"};
                var cameras=new CinemachineCamera[names.Length];
                for(int i=0;i<names.Length;i++)
                {
                    var template=sourceFields.FindProperty(names[i]).objectReferenceValue as CinemachineCamera;
                    if(template==null) throw new InvalidOperationException("HMS independent camera reference missing: "+names[i]);
                    var clone=UnityEngine.Object.Instantiate(template.gameObject,rig.transform);
                    clone.name=template.name;clone.transform.localRotation=template.transform.rotation;
                    cameras[i]=clone.GetComponent<CinemachineCamera>();
                    cameras[i].Follow=i==2 ? visual : facade.transform;cameras[i].LookAt=cameras[i].Follow;
                }
                var output=UnityEngine.Object.Instantiate(source.gameObject,rig.transform);output.name="IndependentOutputCamera";output.tag="MainCamera";
                var controller=output.GetComponent<PlayerCameraController>();var fields=new SerializedObject(controller);
                fields.FindProperty("useSeparatedRig").boolValue=false; // preserve HMS default independent contract
                fields.FindProperty("movementModeController").objectReferenceValue=mode;
                for(int i=0;i<names.Length;i++) fields.FindProperty(names[i]).objectReferenceValue=cameras[i];fields.ApplyModifiedPropertiesWithoutUndo();
                var brain=output.GetComponent<CinemachineBrain>();brain.IgnoreTimeScale=false;
                brain.DefaultBlend=new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut,.6f);
                rig.SetActive(true);return rig;
            }
            catch { if(rig!=null) UnityEngine.Object.DestroyImmediate(rig);throw; }
            finally { if(reference.IsValid()) EditorSceneManager.ClosePreviewScene(reference); }
        }
        public static Vector3 AuthoredBodyOffset()
        {
            var source=AssetDatabase.LoadAssetAtPath<GameObject>(ActorPath) ?? AssetDatabase.LoadAssetAtPath<GameObject>(SourceRoot+"/PlayerRoot.prefab");
            var player=source!=null ? source.GetComponentInChildren<PlayerFacade>(true) : null;
            var capsule=player!=null ? player.GetComponent<CapsuleCollider>() : null;
            if(capsule==null || capsule.direction!=1) throw new InvalidOperationException("A real upright player Capsule is required for authored entry placement.");
            return Vector3.up*((capsule.height*.5f-capsule.center.y)*capsule.transform.lossyScale.y+.03f);
        }
        [MenuItem("Cooked/Session/Build Owned Actor Assets")]
        public static void Build()
        {
            using (AuthoringOutputScope.BeginScene())
            {
                AuthoringOutputScope.RequireEditMode();
                var sourceActor = AssetDatabase.LoadAssetAtPath<GameObject>(ActorPath) ?? AssetDatabase.LoadAssetAtPath<GameObject>(SourceRoot + "/PlayerRoot.prefab");
                if (sourceActor == null) throw new InvalidOperationException("Actual player geometry source is required; authoring will not invent collider dimensions.");
                var sourceFacade = sourceActor.GetComponentInChildren<PlayerFacade>(true);
                var sourceCapsule = sourceFacade != null ? sourceFacade.GetComponent<CapsuleCollider>() : null;
                var sourceForm = sourceFacade != null ? sourceFacade.GetComponent<PlayerFormController>() : null;
                if (sourceCapsule == null || sourceForm == null) throw new InvalidOperationException("Source actor requires its actual Capsule/Form contract.");
                var sourceFormFields = new SerializedObject(sourceForm);
                float smallHeight = sourceFormFields.FindProperty("smallHeight").floatValue;
                float smallRadius = sourceFormFields.FindProperty("smallRadius").floatValue;
                EnsureFolder(AssetRoot);
                var tear = GetOrCreate<TearSkillDefinition>(AssetRoot + "/TearSkill.asset");
                var shrink = GetOrCreate<ShrinkSkillDefinition>(AssetRoot + "/ShrinkSkill.asset");
                var restore = GetOrCreate<RestoreFormSkillDefinition>(AssetRoot + "/RestoreSkill.asset");
                ConfigureSkill(tear, 0f); ConfigureSkill(shrink, 1f); ConfigureSkill(restore, 1f);
                var ts = new SerializedObject(tear); ts.FindProperty("requiredFragments").intValue = 5;
                ts.FindProperty("effectRadius").floatValue = 3; ts.FindProperty("stunDuration").floatValue = 2;
                ts.ApplyModifiedPropertiesWithoutUndo();
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(OnionPath);
                if (model == null) throw new InvalidOperationException("Onion model missing: " + OnionPath);
                var debris = BuildDebris(model);
                GameObject root = new GameObject("PlayerRoot");
                root.SetActive(false);
                root.transform.localScale = sourceActor.transform.localScale;
                try
                {
                    root.AddComponent<GameplayInputRouter>();
                    var body = Child(root.transform, "ActorBody");
                    body.transform.localScale = sourceFacade.transform.localScale;
                    var capsule = body.AddComponent<CapsuleCollider>();
                    EditorUtility.CopySerialized(sourceCapsule, capsule);
                    var rigidbody = body.AddComponent<Rigidbody>(); rigidbody.useGravity = false;
                    var visual = Child(body.transform, "VisualRoot");
                    BuildVisual(visual.transform, "NormalVisual", model, capsule.height);
                    BuildVisual(visual.transform, "SmallVisual", model, smallHeight).transform.localPosition = Vector3.down * (capsule.height - smallHeight) * .5f;
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
                    fs.FindProperty("hitInvincibilitySeconds").floatValue = 2f;
                    fs.FindProperty("initialSkillFragment").intValue = 0; fs.ApplyModifiedPropertiesWithoutUndo();
                    var form = new SerializedObject(body.GetComponent<PlayerFormController>());
                    form.FindProperty("visualRoot").objectReferenceValue = visual.transform;
                    form.FindProperty("debrisPrefab").objectReferenceValue = debris;
                    form.FindProperty("smallHeight").floatValue = smallHeight; form.FindProperty("smallRadius").floatValue = smallRadius;
                    form.FindProperty("normalJumpHeight").floatValue = 3; form.FindProperty("smallJumpHeight").floatValue = 2;
                    form.ApplyModifiedPropertiesWithoutUndo();
                    var motion = new SerializedObject(body.GetComponent<CharacterMovement>());
                    motion.FindProperty("moveSpeed").floatValue = 5; motion.FindProperty("jumpHeight").floatValue = 3;
                    motion.ApplyModifiedPropertiesWithoutUndo();
                    ConfigureActorPresentation(facade, visual.transform);
                    var fall = new SerializedObject(root.AddComponent<ActorFallGuard>());
                    fall.FindProperty("actor").objectReferenceValue = facade; fall.ApplyModifiedPropertiesWithoutUndo();
                    SessionActorHost.ValidateInputOwnership(root);
                    PrefabUtility.SaveAsPrefabAsset(root, ActorPath);
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
                var host = new GameObject("SessionActorHost");
                try
                {
                    host.AddComponent<SessionActorHost>().Configure(AssetDatabase.LoadAssetAtPath<GameObject>(ActorPath));
                    PrefabUtility.SaveAsPrefabAsset(host, HostPath);
                }
                finally { UnityEngine.Object.DestroyImmediate(host); }
                Cooked.Integration.Editor.AuthoringOutputScope.SaveOwnedAssets();
            }
        }
        // Facade, movement/form/input stay on ActorBody for attachedRigidbody/GetComponent.
        // PlayerRoot is empty logic; Visual owns renderers/Animator only. T9 handles existing FX transfers.
        public static void ConfigureActorPresentation(PlayerFacade actor, Transform visual)
        {
            var modes = actor.GetComponent<PlayerMovementModeController>();
            if(modes==null) modes = actor.gameObject.AddComponent<PlayerMovementModeController>();
            var fields = new SerializedObject(modes);
            fields.FindProperty("sideMoveSpeed").floatValue = 5; fields.FindProperty("quarterMoveSpeed").floatValue = 5;
            fields.ApplyModifiedPropertiesWithoutUndo();
            var presentation = actor.GetComponent<PlayerVisualController>();
            if(presentation==null) presentation = actor.gameObject.AddComponent<PlayerVisualController>();
            fields = new SerializedObject(presentation); fields.FindProperty("playerVisualRoot").objectReferenceValue = visual; fields.ApplyModifiedPropertiesWithoutUndo();
            if(actor.GetComponent<PlayerAnimationController>()==null) actor.gameObject.AddComponent<PlayerAnimationController>();
            var output = actor.GetComponent<PlayerAnimatorOutput>();
            if(output==null) output = actor.gameObject.AddComponent<PlayerAnimatorOutput>();
            string animationPath = AssetRoot + "/PrototypePlayerAnimations.asset";
            var set = AssetDatabase.LoadAssetAtPath<PlayerAnimationSet>(animationPath);
            if (set == null)
            {
                var original = AssetDatabase.LoadAssetAtPath<PlayerAnimationSet>(SourceRoot + "/PrototypePlayerAnimations.asset");
                set = original != null ? UnityEngine.Object.Instantiate(original) : ScriptableObject.CreateInstance<PlayerAnimationSet>();
                AssetDatabase.CreateAsset(set, animationPath);
            }
            fields = new SerializedObject(output); fields.FindProperty("animationSet").objectReferenceValue = set; fields.ApplyModifiedPropertiesWithoutUndo();
            // Missing Hit/Dead/Interact/Knockback clips remain missing: existing 0ms fallback, no invented Motion.
            var damage = actor.GetComponent<PlayerDamagePresentation>();
            if(damage==null) damage = actor.gameObject.AddComponent<PlayerDamagePresentation>();
            fields = new SerializedObject(damage); fields.FindProperty("player").objectReferenceValue = actor;
            var targets = fields.FindProperty("targets"); var renderers = visual.GetComponentsInChildren<Renderer>(true);
            targets.arraySize = renderers.Length;
            for (int i = 0; i < renderers.Length; i++) targets.GetArrayElementAtIndex(i).objectReferenceValue = renderers[i];
            fields.ApplyModifiedPropertiesWithoutUndo();
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
        private static GameObject BuildVisual(Transform parent, string name, GameObject model, float height)
        {
            var visual = Child(parent, name);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model); instance.transform.SetParent(visual.transform, false);
            if (instance.GetComponentsInChildren<MonoBehaviour>(true).Length != 0) throw new InvalidOperationException("Source Visual contains scripts; explicit transfer is required.");
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

