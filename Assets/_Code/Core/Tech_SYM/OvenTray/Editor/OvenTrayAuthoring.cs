using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using FeelsGoodOnion.TechSYM.Interaction;
using FeelsGoodOnion.TechSYM.OvenTray;

namespace FeelsGoodOnion.TechSYM.EditorTools
{
    /// <summary>검증된 활성 씬만 편집하는 명시적 제작 도구. 자동 실행하지 않는다.</summary>
    public static class OvenTrayAuthoring
    {
        public const string Folder = "Assets/_Scenes/Tech_SYM/Prefabs/Oven";
        public static string Build()
        {
            var scene=SceneManager.GetActiveScene();
            if(Application.isPlaying || scene.path!="Assets/_Scenes/Tech_SYM/03_InGame.unity")
                throw new InvalidOperationException("확인된 활성 인게임 씬의 Edit Mode에서만 제작합니다.");
            if(GameObject.Find("Oven")!=null) throw new InvalidOperationException("기존 Oven을 덮어쓰지 않습니다.");
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var dark=Material("OvenFrame",new Color(.12f,.16f,.19f),.55f);
            var metal=Material("TrayMetal",new Color(.62f,.68f,.72f),.7f);
            var handle=Material("TrayHandle",new Color(.88f,.47f,.16f),.25f);
            var oven=new GameObject("Oven");
            SceneManager.MoveGameObjectToScene(oven,scene);
            Undo.RegisterCreatedObjectUndo(oven,"Create Oven test assembly");
            oven.transform.SetPositionAndRotation(Vector3.zero,Quaternion.Euler(0,90,0));
            oven.transform.localScale=Vector3.one*1.5f;
            Cube("Back",oven.transform,new Vector3(0,1,.8f),new Vector3(2.5f,2,.18f),dark,true);
            Cube("LeftFrame",oven.transform,new Vector3(-1.2f,1,0),new Vector3(.18f,2,1.6f),dark,true);
            Cube("RightFrame",oven.transform,new Vector3(1.2f,1,0),new Vector3(.18f,2,1.6f),dark,true);
            Cube("TopFrame",oven.transform,new Vector3(0,2.1f,0),new Vector3(2.6f,.15f,1.6f),dark,true);
            // 트레이의 Rigidbody/지지면은 단위 스케일 루트에 두고 외형만 크기를 조절한다.
            var tray=new GameObject("OvenTray");
            tray.transform.SetParent(oven.transform,false);
            tray.AddComponent<OvenTrayObject>();
            tray.GetComponent<BoxCollider>().size=new Vector3(2,.15f,1.2f);
            var anchor=tray.GetComponent<InteractionPromptAnchor>();
            Set(anchor,"localOffset",new Vector3(0,.65f,-1.15f));
            Set(anchor,"interactionLocalOffset",new Vector3(0,.10f,-.66f));
            Cube("Visual",tray.transform,Vector3.zero,new Vector3(2,.15f,1.2f),metal,false);
            Cube("Handle",tray.transform,new Vector3(0,.08f,-.66f),new Vector3(.9f,.12f,.12f),handle,false);
            var trayPrefab=PrefabUtility.SaveAsPrefabAssetAndConnect(tray,Folder+"/OvenTray.prefab",InteractionMode.AutomatedAction);
            for(int i=0;i<3;i++)
            {
                var item=i==0?tray:(GameObject)PrefabUtility.InstantiatePrefab(trayPrefab,scene);
                item.transform.SetParent(oven.transform,false);
                item.name="OvenTray_"+(i+1);
                item.transform.localPosition=new Vector3(0,.25f+i*.5f,0);
                Set(item.GetComponent<OvenTrayObject>(),"travelOffset",new Vector3(0,0,-(3-i)));
                PrefabUtility.RecordPrefabInstancePropertyModifications(item.transform);
                PrefabUtility.RecordPrefabInstancePropertyModifications(item.GetComponent<OvenTrayObject>());
            }
            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            return "Oven test assembly created; not yet added to MultiFuncPlatforms.";
        }

        public static string FinalizePrefabs()
        {
            if(Application.isPlaying) throw new InvalidOperationException("먼저 Play 검증을 종료하세요.");
            var scene=SceneManager.GetActiveScene();
            if(scene.path!="Assets/_Scenes/Tech_SYM/03_InGame.unity") throw new InvalidOperationException("활성 씬 변경 감지");
            var oven=scene.GetRootGameObjects().Single(g=>g.name=="Oven");
            PrefabUtility.SaveAsPrefabAssetAndConnect(oven,Folder+"/Oven.prefab",InteractionMode.AutomatedAction);
            var platforms=scene.GetRootGameObjects().Single(g=>g.name=="MultiFuncPlatforms");
            oven.transform.SetParent(platforms.transform,true);
            var source=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(platforms);
            if(string.IsNullOrEmpty(source)) throw new InvalidOperationException("MultiFuncPlatforms 원본 프리팹 누락");
            // 사용자/기존 Override 전체 적용을 피하고 이번에 추가한 Oven만 원본에 적용한다.
            PrefabUtility.ApplyAddedGameObject(oven,source,InteractionMode.AutomatedAction);

            string uiFolder="Assets/_Scenes/Tech_SYM/Prefabs/Interaction";
            Directory.CreateDirectory(uiFolder); AssetDatabase.Refresh();
            var names=new[]{"MemoSystem","InteractionSystem","MemoScreenCanvas","InteractionPromptCanvas","Memo_1001","Memo_1002"};
            foreach(string name in names)
            {
                var root=scene.GetRootGameObjects().Single(g=>g.name==name);
                SaveIndependentRoot(root,uiFolder+"/"+name+".prefab");
            }
            AssetDatabase.SaveAssets();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            return Validate();
        }

        private static void SaveIndependentRoot(GameObject root,string path)
        {
            if(AssetDatabase.LoadAssetAtPath<GameObject>(path)!=null)
                throw new InvalidOperationException("기존 에셋을 덮어쓰지 않습니다: "+path);
            var bindings=new List<(Component component,string property,UnityEngine.Object target)>();
            foreach(var c in root.GetComponentsInChildren<Component>(true))
            {
                if(c==null) continue;
                var so=new SerializedObject(c);
                var p=so.GetIterator();
                while(p.Next(true))
                {
                    if(p.propertyType!=SerializedPropertyType.ObjectReference) continue;
                    var target=p.objectReferenceValue;
                    Transform targetTransform=target is Component component?component.transform:(target as GameObject)?.transform;
                    if(targetTransform!=null && !EditorUtility.IsPersistent(target) && !targetTransform.IsChildOf(root.transform))
                        bindings.Add((c,p.propertyPath,target));
                }
            }
            PrefabUtility.SaveAsPrefabAssetAndConnect(root,path,InteractionMode.AutomatedAction);
            foreach(var binding in bindings)
            {
                var so=new SerializedObject(binding.component);
                so.FindProperty(binding.property).objectReferenceValue=binding.target;
                so.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.RecordPrefabInstancePropertyModifications(binding.component);
            }
        }

        public static string Validate()
        {
            var scene=SceneManager.GetActiveScene();
            var roots=scene.GetRootGameObjects();
            int missing=roots.Sum(r=>r.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)));
            if(missing!=0) throw new InvalidOperationException("Missing Script: "+missing);
            foreach(var controller in UnityEngine.Object.FindObjectsByType<CheckInteract>(FindObjectsSortMode.None))
                RequireReferences(controller,"player","promptView","viewCamera","overlaySource");
            foreach(var controller in UnityEngine.Object.FindObjectsByType<MemoUIController>(FindObjectsSortMode.None))
                RequireReferences(controller,"screenRoot","viewPrefab");
            foreach(var memo in UnityEngine.Object.FindObjectsByType<MemoObject>(FindObjectsSortMode.None))
                RequireReferences(memo,"memoModel","memoUI");
            foreach(string name in new[]{"MemoScreenCanvas","InteractionPromptCanvas"})
                if(!roots.Any(r=>r.name==name)) throw new InvalidOperationException("공용 Canvas는 씬 루트여야 합니다.");
            return "PASS missingScripts=0; scene bindings valid; shared canvases independent; "+MemoValidation.Validate();
        }
        private static void RequireReferences(UnityEngine.Object owner,params string[] fields)
        {
            var so=new SerializedObject(owner);
            foreach(var field in fields) if(so.FindProperty(field).objectReferenceValue==null)
                throw new InvalidOperationException(owner.name+" missing "+field);
        }
        public static void Set(UnityEngine.Object owner,string field,Vector3 value)
        {
            var so=new SerializedObject(owner); so.FindProperty(field).vector3Value=value; so.ApplyModifiedPropertiesWithoutUndo();
        }
        private static Material Material(string name,Color color,float metallic)
        {
            var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};
            mat.SetColor("_BaseColor",color); mat.SetFloat("_Metallic",metallic); mat.SetFloat("_Smoothness",.55f);
            AssetDatabase.CreateAsset(mat,Folder+"/"+name+".mat"); return mat;
        }
        private static GameObject Cube(string name,Transform parent,Vector3 position,Vector3 scale,Material material,bool solid)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube); g.name=name;
            g.transform.SetParent(parent,false);g.transform.localPosition=position;g.transform.localScale=scale;
            g.GetComponent<Renderer>().sharedMaterial=material;
            if(!solid) UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }
    }
}
