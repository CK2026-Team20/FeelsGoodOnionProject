using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FeelsGoodOnion.TechSYM.Enemies.Editor
{
    /// <summary>주 작업자가 실행하는 세 원본 전용 갱신. 씬/공유 원본/정의 자산을 저장하지 않는다.</summary>
    public static class EnemyPrefabAuthoring
    {
        private static readonly string[] PrefabPaths =
        {
            "Assets/_Scenes/Tech_SYM/Enemies/Prefabs/StationaryEnemy.prefab",
            "Assets/_Scenes/Tech_SYM/Enemies/Prefabs/PatrolEnemy.prefab",
            "Assets/_Scenes/Tech_SYM/Enemies/Prefabs/OilPatrolEnemy.prefab"
        };
        private const string DummyPath = "Assets/_Prefabs/Characters/Enemy/Enemy_Dummy.prefab";
        private const string EffectPath = "Assets/_Prefabs/Characters/Enemy/StunEffect_Dummy.prefab";

        public static string ProfileRoot => Cooked.Integration.Editor.AuthoringOutputScope.Map("Assets/_Scenes/Tech_SYM/Enemies");
        public static string[] ProfilePaths => new[] { ProfileRoot + "/Prefabs/IdleEnemy.prefab", ProfileRoot + "/Prefabs/PatrolXEnemy.prefab", ProfileRoot + "/Prefabs/PatrolYEnemy.prefab" };
        [MenuItem("Cooked/Enemies/Build Finalized Profiles")]
        public static void BuildProfiles()
        {
            using (Cooked.Integration.Editor.AuthoringOutputScope.BeginScene())
            {
                Cooked.Integration.Editor.AuthoringOutputScope.RequireEditMode();
                var model = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Art/Characters/Models/ch_monster.fbx");
                var material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Art/Characters/Materials/monster_default.mat");
                var dummy = AssetDatabase.LoadAssetAtPath<GameObject>(DummyPath);
                var effect = AssetDatabase.LoadAssetAtPath<GameObject>(EffectPath);
                var stunTemplate = dummy != null ? dummy.GetComponentInChildren<EnemyStunVisual>(true) : null;
                Require(model != null && material != null && stunTemplate != null && effect != null, "Actual enemy artwork/stun references are required.");
                Require(effect.GetComponentsInChildren<Collider>(true).Length == 0, "Stun VFX must be physics-free.");
                var templateFields = new SerializedObject(stunTemplate);
                var stunnedTexture = templateFields.FindProperty("stunnedTexture").objectReferenceValue;
                Require(stunnedTexture != null, "Actual stunned texture is required.");
                Cooked.Integration.Editor.AuthoringOutputScope.EnsureFolder(ProfileRoot + "/Prefabs");
                Cooked.Integration.Editor.AuthoringOutputScope.EnsureFolder(ProfileRoot + "/Data");
                string[] names = { "IdleEnemy", "PatrolXEnemy", "PatrolYEnemy" };
                for (int i = 0; i < names.Length; i++)
                {
                    string definitionPath = ProfileRoot + "/Data/" + names[i] + ".asset";
                    var definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(definitionPath);
                    if (definition == null) { definition = ScriptableObject.CreateInstance<EnemyDefinition>(); AssetDatabase.CreateAsset(definition, definitionPath); }
                    var fields = new SerializedObject(definition);
                    fields.FindProperty("kind").intValue = i == 0 ? (int)EnemyKind.Stationary : (int)EnemyKind.Patrol;
                    fields.FindProperty("speed").floatValue = 2f; fields.FindProperty("initialIdle").floatValue = .5f;
                    fields.FindProperty("knockbackSpeed").floatValue = 10f; fields.ApplyModifiedPropertiesWithoutUndo();
                    var owner = new GameObject(names[i]); owner.SetActive(false);
                    try
                    {
                        var actor = owner.AddComponent<EnemyActor>();
                        var body = new GameObject("Body"); body.transform.SetParent(owner.transform, false);
                        var collider = body.AddComponent<BoxCollider>(); collider.isTrigger = true; collider.size = new Vector3(1, 1.4f, 1);
                        var sensor = body.AddComponent<EnemyContactSensor>();
                        fields = new SerializedObject(sensor); fields.FindProperty("owner").objectReferenceValue = actor; fields.ApplyModifiedPropertiesWithoutUndo();
                        fields = new SerializedObject(actor); fields.FindProperty("definition").objectReferenceValue = definition;
                        fields.FindProperty("body").objectReferenceValue = collider;
                        fields.FindProperty("initialDirection").intValue = i == 2 ? (int)PatrolDirection.Up : (int)PatrolDirection.Right;
                        fields.FindProperty("terrainMask").intValue = 1; fields.ApplyModifiedPropertiesWithoutUndo();
                        var visual = new GameObject("Visual"); visual.transform.SetParent(body.transform, false);
                        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model); instance.transform.SetParent(visual.transform, false);
                        Require(instance.GetComponentsInChildren<MonoBehaviour>(true).Length == 0 && instance.GetComponentsInChildren<Collider>(true).Length == 0 &&
                            instance.GetComponentsInChildren<Rigidbody>(true).Length == 0, "Enemy Visual must contain only actual artwork/rendering.");
                        var renderers = instance.GetComponentsInChildren<Renderer>(true); Require(renderers.Length == 1, "Explicit single enemy Renderer is required.");
                        var renderer = renderers[0]; var slots = renderer.sharedMaterials;
                        for (int slot = 0; slot < slots.Length; slot++) slots[slot] = material; renderer.sharedMaterials = slots;
                        Require(renderer.bounds.size.y > .0001f, "Invalid actual enemy mesh bounds.");
                        instance.transform.localScale *= collider.size.y / renderer.bounds.size.y;
                        instance.transform.position += body.transform.position - renderer.bounds.center;
                        var fx = (GameObject)PrefabUtility.InstantiatePrefab(effect, body.transform); fx.name = "StunEffect";
                        fx.transform.localPosition = Vector3.up * (collider.size.y * .5f + .5f); fx.SetActive(false);
                        var stun = owner.AddComponent<EnemyStunVisual>(); fields = new SerializedObject(stun);
                        fields.FindProperty("stunEffect").objectReferenceValue = fx; fields.FindProperty("targetRenderer").objectReferenceValue = renderer;
                        string property = TextureProperty(renderer, names[i]);
                        fields.FindProperty("normalTexture").objectReferenceValue = material.GetTexture(property) ?? templateFields.FindProperty("normalTexture").objectReferenceValue;
                        fields.FindProperty("stunnedTexture").objectReferenceValue = stunnedTexture;
                        fields.FindProperty("materialIndex").intValue = 0; fields.FindProperty("texturePropertyName").stringValue = property;
                        var extra = fields.FindProperty("additionalTexturePropertyNames"); var shades = ShadeProperties(material); extra.arraySize = shades.Count;
                        for (int slot = 0; slot < shades.Count; slot++) extra.GetArrayElementAtIndex(slot).stringValue = shades[slot];
                        fields.ApplyModifiedPropertiesWithoutUndo();
                        // EnemyActor validation requires unit/nonrotated root/body and no Rigidbody anywhere.
                        Require(owner.GetComponentsInChildren<Rigidbody>(true).Length == 0, "Enemies cannot own Rigidbody.");
                        owner.SetActive(true); Validate(owner, names[i]);
                        Require(PrefabUtility.SaveAsPrefabAsset(owner, ProfilePaths[i]) != null, "Enemy profile save failed.");
                        EditorUtility.SetDirty(definition); AssetDatabase.SaveAssetIfDirty(definition);
                    }
                    finally { Object.DestroyImmediate(owner); }
                }
            }
        }

        [MenuItem("Cooked/Enemies/Apply R14 R19 To Three Prefabs")]
        public static void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("Play와 Prefab Mode를 종료한 뒤 실행하세요. 열린 씬은 저장하지 않습니다.");
            var dummy = AssetDatabase.LoadAssetAtPath<GameObject>(DummyPath);
            var effect = AssetDatabase.LoadAssetAtPath<GameObject>(EffectPath);
            Require(dummy != null && effect != null, "스턴 참조 프리팹이 없습니다.");
            var template = dummy.GetComponentInChildren<EnemyStunVisual>(true);
            Require(template != null, "더미의 EnemyStunVisual이 없습니다.");
            var source = new SerializedObject(template);
            var normal = source.FindProperty("normalTexture").objectReferenceValue as Texture;
            var stunned = source.FindProperty("stunnedTexture").objectReferenceValue as Texture;
            Require(normal != null && stunned != null && normal != stunned, "서로 다른 일반/기절 텍스처가 필요합니다.");
            Require(effect.GetComponentsInChildren<Collider>(true).Length == 0, "시각 효과에 전투 Collider가 포함되어 있습니다.");

            // Check all targets before the first save. Unexpected hierarchy fails without rebuilding it.
            foreach (string path in PrefabPaths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Require(asset != null, path + ": 원본이 없습니다.");
                InspectBody(asset, path, out _, out var renderer);
                TextureProperty(renderer, path);
            }
            foreach (string path in PrefabPaths)
            {
                string guid = AssetDatabase.AssetPathToGUID(path);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    InspectBody(root, path, out var body, out var renderer);
                    Transform oldHead = body.transform.Find("HeadSensor");
                    if (oldHead != null)
                    {
                        // Preserve the Transform and any unrelated children/components/references.
                        foreach (var sensor in oldHead.GetComponents<EnemyContactSensor>()) Object.DestroyImmediate(sensor);
                        foreach (var collider in oldHead.GetComponents<Collider>()) Object.DestroyImmediate(collider);
                    }
                    Transform effectTransform = body.transform.Find("StunEffect");
                    if (effectTransform == null)
                    {
                        var instance = (GameObject)PrefabUtility.InstantiatePrefab(effect, body.transform);
                        instance.name = "StunEffect";
                        effectTransform = instance.transform;
                    }
                    effectTransform.localPosition = body.center + Vector3.up * (body.size.y * .5f + .5f);
                    effectTransform.gameObject.SetActive(false);
                    var visual = root.GetComponent<EnemyStunVisual>() ?? root.AddComponent<EnemyStunVisual>();
                    var settings = new SerializedObject(visual);
                    settings.FindProperty("stunEffect").objectReferenceValue = effectTransform.gameObject;
                    settings.FindProperty("targetRenderer").objectReferenceValue = renderer;
                    string property = TextureProperty(renderer, path);
                    // Restore the actual visible material's base texture when it has one.
                    settings.FindProperty("normalTexture").objectReferenceValue = renderer.sharedMaterials[0].GetTexture(property) ?? normal;
                    settings.FindProperty("stunnedTexture").objectReferenceValue = stunned;
                    settings.FindProperty("materialIndex").intValue = 0;
                    settings.FindProperty("texturePropertyName").stringValue = property;
                    var additional = settings.FindProperty("additionalTexturePropertyNames");
                    var shadeProperties = ShadeProperties(renderer.sharedMaterials[0]);
                    additional.arraySize = shadeProperties.Count;
                    for (int index = 0; index < shadeProperties.Count; index++)
                        additional.GetArrayElementAtIndex(index).stringValue = shadeProperties[index];
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    visual.enabled = true;
                    Validate(root, path);
                    PrefabUtility.SaveAsPrefabAsset(root, path, out bool success);
                    Require(success, path + ": 저장 실패. 앞서 저장된 원본은 Console을 확인하세요.");
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                Require(AssetDatabase.AssetPathToGUID(path) == guid, path + ": GUID가 변경되었습니다.");
                Validate(AssetDatabase.LoadAssetAtPath<GameObject>(path), path);
                Debug.Log(path + ": R14/R19 저장 후 참조 검증 완료. 씬 외형 override 및 Play 검증은 별도입니다.");
            }
        }

        [MenuItem("Cooked/Enemies/Verify Three Authored Prefabs")]
        public static void Verify()
        {
            foreach (string path in PrefabPaths) Validate(AssetDatabase.LoadAssetAtPath<GameObject>(path), path);
            Debug.Log("실제 세 적 프리팹 직렬화 검사 통과. 동작/표시 Play 검사는 포함하지 않습니다.");
        }

        private static void InspectBody(GameObject root, string path, out BoxCollider body, out Renderer renderer)
        {
            Require(root != null, path + ": 원본 누락");
            var actor = root.GetComponent<EnemyActor>();
            Require(actor != null, path + ": EnemyActor 누락");
            var settings = new SerializedObject(actor);
            body = settings.FindProperty("body").objectReferenceValue as BoxCollider;
            Require(body != null && body.isTrigger && body.transform.parent == root.transform, path + ": 몸통 계약 오류");
            var definition = settings.FindProperty("definition").objectReferenceValue as EnemyDefinition;
            Require(definition != null && definition.IsValid, path + ": 정의/접촉 피해 1 확인 필요");
            var sensor = body.GetComponent<EnemyContactSensor>();
            Require(sensor != null && new SerializedObject(sensor).FindProperty("owner").objectReferenceValue == actor,
                path + ": 몸통 센서 소유자 오류");
            renderer = VisibleRenderer(body, path);
        }

        private static Renderer VisibleRenderer(BoxCollider body, string context)
        {
            // Scene builders can replace Body/Visual with MonsterVisual. Never silently bind a hidden cube.
            Transform monster = body.transform.Find("MonsterVisual");
            if (monster != null && ActiveUnder(monster, body.transform))
            {
                Renderer selected = null;
                foreach (var candidate in monster.GetComponentsInChildren<Renderer>(true))
                {
                    if (!(candidate is MeshRenderer) && !(candidate is SkinnedMeshRenderer)) continue;
                    if (!candidate.enabled || !ActiveUnder(candidate.transform, body.transform)) continue;
                    Require(selected == null, context + ": MonsterVisual의 활성 Renderer가 복수입니다. 명시적 슬롯 연결이 필요합니다.");
                    selected = candidate;
                }
                Require(selected != null, context + ": MonsterVisual에 활성 외형 Renderer가 없습니다.");
                return selected;
            }
            Transform shape = body.transform.Find("Visual");
            Renderer renderer = null;
            if (shape != null) foreach (var candidate in shape.GetComponentsInChildren<Renderer>(true))
            {
                if (!candidate.enabled || !ActiveUnder(candidate.transform, body.transform)) continue;
                Require(renderer == null, context + ": Visual에 활성 Renderer가 복수입니다."); renderer = candidate;
            }
            Require(renderer != null && renderer.enabled && ActiveUnder(shape, body.transform),
                context + ": 활성 외형 Renderer가 없습니다. 숨겨진 Body/Visual에는 연결하지 않습니다.");
            return renderer;
        }

        private static bool ActiveUnder(Transform child, Transform body)
        {
            for (Transform current = child; current != null; current = current.parent)
            {
                if (!current.gameObject.activeSelf) return false;
                if (current == body) return true;
            }
            return false;
        }

        private static List<string> ShadeProperties(Material material)
        {
            var result = new List<string>();
            foreach (string property in new[] { "_1st_ShadeMap", "_2nd_ShadeMap" })
                if (material.HasProperty(property)) result.Add(property);
            return result;
        }

        private static string TextureProperty(Renderer renderer, string path)
        {
            var materials = renderer.sharedMaterials;
            Require(materials.Length > 0 && materials[0] != null, path + ": 슬롯 0 Material 누락");
            // Unity Toon exposes several compatibility properties; its actual base input is _MainTex.
            if (materials[0].HasProperty("_1st_ShadeMap") && materials[0].HasProperty("_MainTex")) return "_MainTex";
            if (materials[0].HasProperty("_BaseMap")) return "_BaseMap";
            if (materials[0].HasProperty("_MainTex")) return "_MainTex";
            throw new InvalidOperationException(path + ": 슬롯 0의 유효한 텍스처 속성이 없습니다.");
        }

        private static void Validate(GameObject root, string path)
        {
            InspectBody(root, path, out var body, out var renderer);
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) == 0, path + ": Missing Script");
            var colliders = root.GetComponentsInChildren<Collider>(true);
            Require(colliders.Length == 1 && colliders[0] == body, path + ": 몸통 외 Collider가 남아 있습니다.");
            Require(root.GetComponentsInChildren<EnemyContactSensor>(true).Length == 1, path + ": 추가 센서가 남아 있습니다.");
            var visuals = root.GetComponentsInChildren<EnemyStunVisual>(true);
            Require(visuals.Length == 1 && visuals[0].enabled, path + ": 스턴 표시가 없거나 중복/비활성입니다.");
            var settings = new SerializedObject(visuals[0]);
            var effect = settings.FindProperty("stunEffect").objectReferenceValue as GameObject;
            Require(effect != null && effect.transform.IsChildOf(body.transform) && !effect.activeSelf,
                path + ": 효과 계층/초기 활성 오류");
            Require(settings.FindProperty("targetRenderer").objectReferenceValue == renderer, path + ": Renderer 연결 오류");
            var normal = settings.FindProperty("normalTexture").objectReferenceValue;
            var stunned = settings.FindProperty("stunnedTexture").objectReferenceValue;
            Require(normal != null && stunned != null && normal != stunned, path + ": 텍스처 연결 오류");
            Require(settings.FindProperty("materialIndex").intValue == 0 &&
                settings.FindProperty("texturePropertyName").stringValue == TextureProperty(renderer, path), path + ": 슬롯/셰이더 속성 오류");
            ValidateVisibleBinding(root.GetComponent<EnemyActor>());
        }

        /// <summary>읽기 전용. 원본 검사와 별도로 씬 외형 override의 실제 표시 연결을 확인한다.</summary>
        public static void ValidateVisibleBinding(EnemyActor actor)
        {
            Require(actor != null, "EnemyActor 누락");
            var actorSettings = new SerializedObject(actor);
            var body = actorSettings.FindProperty("body").objectReferenceValue as BoxCollider;
            Require(body != null, actor.name + ": 몸통 누락");
            var renderer = VisibleRenderer(body, actor.name);
            var visual = actor.GetComponentInChildren<EnemyStunVisual>(true);
            Require(visual != null, actor.name + ": EnemyStunVisual 누락");
            var settings = new SerializedObject(visual);
            Require(settings.FindProperty("targetRenderer").objectReferenceValue == renderer,
                actor.name + ": 실제 활성 외형과 targetRenderer가 다릅니다. 씬 외형 override의 연결을 수정하세요.");
            string property = TextureProperty(renderer, actor.name);
            Require(settings.FindProperty("materialIndex").intValue == 0 &&
                settings.FindProperty("texturePropertyName").stringValue == property, actor.name + ": 실제 셰이더/슬롯 불일치");
            var material = renderer.sharedMaterials[0];
            var original = material.GetTexture(property);
            if (original != null)
                Require(settings.FindProperty("normalTexture").objectReferenceValue == original,
                    actor.name + ": 일반 텍스처가 실제 외형의 shared Material 텍스처와 다릅니다.");
            var expected = ShadeProperties(material);
            var additional = settings.FindProperty("additionalTexturePropertyNames");
            Require(additional.arraySize == expected.Count, actor.name + ": 음영 텍스처 슬롯 누락");
            for (int index = 0; index < expected.Count; index++)
                Require(additional.GetArrayElementAtIndex(index).stringValue == expected[index], actor.name + ": 음영 텍스처 속성 불일치");
        }

        [MenuItem("Cooked/Enemies/Verify Loaded Scene Visible Bindings (Read Only)")]
        public static void VerifyLoadedSceneVisibleBindings()
        {
            int count = 0;
            foreach (var actor in Resources.FindObjectsOfTypeAll<EnemyActor>())
            {
                if (EditorUtility.IsPersistent(actor) || !actor.gameObject.scene.IsValid() ||
                    EditorSceneManager.IsPreviewScene(actor.gameObject.scene)) continue;
                ValidateVisibleBinding(actor);
                count++;
            }
            Require(count > 0, "열린 실제 씬에 검증할 적이 없습니다.");
            Debug.Log($"열린 씬 적 {count}개의 활성 외형 연결 검사 통과. 씬은 수정/저장하지 않았습니다.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
