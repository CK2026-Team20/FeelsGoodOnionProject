#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Cooked.Contracts;
using Cooked.Foundation;
using Cooked.Level;
using FeelsGoodOnion.TechSYM.Enemies;
using FeelsGoodOnion.TechSYM.Features;
using FeelsGoodOnion.TechSYM.Interaction;
using FeelsGoodOnion.TechSYM.Platforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
namespace Cooked.Integration.Editor
{
    // Owns only the new prototype scene. Never regenerates the historical three stages.
    public static class PrototypeStageAuthoring
    {
        public const string ScenePath = IntegrationSceneBuilder.Root + "/Prototype/1_Stage.unity";
        private const string Sym = "Assets/_Scenes/Tech_SYM/";
        private static Transform root;
        private static StageService stage;
        private static CameraZoneRequest side, quarter;
        public static string Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Edit Mode required.");
            for(int i=0;i<SceneManager.sceneCount;i++) if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Loaded dirty scene; authoring deferred.");
            if (SceneManager.GetSceneByPath(ScenePath).isLoaded) throw new InvalidOperationException("Close prototype explicitly before regenerating it.");
            EnsureFolder(IntegrationSceneBuilder.Root + "/Prototype");
            var previous=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                root=new GameObject("Prototype_1_Stage").transform;
                stage=root.gameObject.AddComponent<StageService>();
                Set(stage,"stageId","1_Stage"); Set(stage,"initialCheckpointId","P_START");
                var path=new[]{new Vector3(0,0,0),new Vector3(42,0,0),new Vector3(42,0,18),new Vector3(78,0,18),new Vector3(78,0,36),new Vector3(132,0,36)};
                Set(stage,"progressWaypoints",path);
                side=Zone("Side",CameraMovementMode.Side,Vector3.zero);
                quarter=Zone("QuarterA",CameraMovementMode.Quarter,new Vector3(42,0,18));
                // Nonoverlapping volumes with gaps exceed capsule diameter: no OnStay boundary ping-pong.
                CameraVolume(side,new Vector3(19,3,0),new Vector3(40,8,4));
                CameraVolume(Zone("CorridorA",CameraMovementMode.Corridor,new Vector3(42,0,0)),new Vector3(42,3,6.75f),new Vector3(3,8,15.5f));
                CameraVolume(side,new Vector3(42,3,-1.7f),new Vector3(4,8,1));
                CameraVolume(quarter,new Vector3(59,3,18),new Vector3(38,8,5));
                CameraVolume(Zone("CorridorB",CameraMovementMode.Corridor,new Vector3(78,0,18)),new Vector3(78,3,27),new Vector3(3,8,13));
                var final=Zone("QuarterB",CameraMovementMode.Quarter,new Vector3(78,0,36));
                CameraVolume(final,new Vector3(105,3,36),new Vector3(57,8,6));
                Box("SideFloor",new Vector3(21,-.5f,0),new Vector3(44,1,4));
                Box("PassageA",new Vector3(42,-.5f,9),new Vector3(4,1,18));
                Box("QuarterFloorA",new Vector3(60,-.5f,18),new Vector3(36,1,8));
                Box("PassageB",new Vector3(78,-.5f,27),new Vector3(4,1,18));
                Box("QuarterFloorB",new Vector3(105,-.5f,36),new Vector3(58,1,8));
                // Low side rails on passages guide entry without constraining quarter-view arenas.
                foreach(float x in new[]{39.8f,44.2f}) Box("PassageRailA",new Vector3(x,.5f,9),new Vector3(.3f,1,13));
                foreach(float x in new[]{75.8f,80.2f}) Box("PassageRailB",new Vector3(x,.5f,27),new Vector3(.3f,1,13));
                var checkpoints=new[]{Checkpoint("P_START",new Vector3(1,0,0),side,false,false),Checkpoint("P_FORM",new Vector3(7,0,0),side,true,false),Checkpoint("P_TEAR",new Vector3(47,0,18),quarter,true,true),Checkpoint("P_CHASE",new Vector3(100,0,36),final,true,true)};
                Set(stage,"checkpoints",checkpoints);
                Trigger("Enter",StageTriggerKind.Enter,"P_START",new Vector3(2,2,0),new Vector3(2,5,4));
                ShellPuzzle();
                var shattered=Instance(Sym+"Prefabs/ShatteredPlatform.prefab",new Vector3(24,.65f,0));
                foreach(var c in shattered.GetComponentsInChildren<MonoBehaviour>(true))
                    if(c.GetType().Name=="ShatteredPlatform") Set(c,"canRegenerate",false);
                Instance(Sym+"Prefabs/PeriodicPlatform.prefab",new Vector3(52,.7f,16));
                Instance(Sym+"Prefabs/LinearPlatform.prefab",new Vector3(59,1,18));
                Instance(Sym+"Prefabs/FloatingPlatform.prefab",new Vector3(67,1,20));
                // Drawer bodies retain their existing Rigidbody/contact component contract.
                for(int i=0;i<3;i++)
                {
                    var drawer=Instance(Sym+"Prefabs/Oven/OvenTray.prefab",new Vector3(31+i*2, .4f+i*.5f,0));
                    drawer.name="DrawerStep_"+(i+1);
                    foreach(var c in drawer.GetComponentsInChildren<MonoBehaviour>(true))
                        if(c.GetType().Name=="OvenTrayObject") Set(c,"travelOffset",new Vector3(-1.5f,0,0));
                }
                var damage=Box("GasBurner",new Vector3(88,.1f,36),new Vector3(2,.2f,3));
                damage.AddComponent<DamageFloorPlatform>();
                var oil=Box("Oil",new Vector3(94,.08f,36),new Vector3(3,.15f,3));
                oil.GetComponent<BoxCollider>().isTrigger=true; oil.AddComponent<OilSurface>();
                var enemies=new[]{"StationaryEnemy","PatrolEnemy","OilPatrolEnemy"}.Select((n,i)=>Instance(Sym+"Enemies/Prefabs/"+n+".prefab",new Vector3(53+i*9,1,18)).GetComponentInChildren<EnemyActor>(true)).ToArray();
                Set(stage,"enemies",enemies);
                for(int i=0;i<8;i++)
                {
                    var fragment=Instance(Sym+"Prefabs/TearFragment.prefab",new Vector3(46+i*.8f,1.1f,18));
                    foreach(var old in fragment.GetComponentsInChildren<TearFragment>(true))
                    { old.enabled=false; Set(old.gameObject.AddComponent<LevelTearFragment>(),"pickupRoot",fragment); }
                }
                BuildMemo();
                Instruction(new Vector3(2,1,0),"방향키 이동 · Space 점프 · E 상호작용");
                Instruction(new Vector3(9,1,0),"Q: 크기 전환과 껍질 자동 회수. 껍질을 압력판에 남겨 문을 여세요.");
                Instruction(new Vector3(35,1,0),"E: 서랍을 열고 닫습니다. 통로는 위·아래 방향키로 이동합니다.");
                Instruction(new Vector3(49,1,18),"눈물 조각 5개 수집 후 F: 반경 내 적 기절 (2초)");
                Instruction(new Vector3(101,1,36),"체크포인트 저장. 앞쪽 추격 무리에 닿으면 재시작합니다. 끝의 친구를 구출하세요.");
                Trigger("Dialogue",StageTriggerKind.Dialogue,"EXPLORE_OVEN_01",new Vector3(72,2,18),new Vector3(2,5,6));
                var gateRoot=Child("RetreatGate",root);var retreat=gateRoot.AddComponent<ChaseRetreatGate>();
                var blocker=Box("ClosedBarrier",new Vector3(101,2,36),new Vector3(.5f,4,8));
                blocker.transform.SetParent(gateRoot.transform,true); Set(retreat,"blocker",blocker.GetComponent<BoxCollider>());blocker.SetActive(false);
                Set(stage,"chaseRetreatGate",retreat);Set(stage,"chaseCheckpointId","P_CHASE");
                Set(stage,"chaseStartProgress",140f); // polyline progress at x104: 42+18+36+18+26
                Set(stage,"chaseWaypoints",new[]{new Vector3(92,0,36),new Vector3(132,0,36)});
                Trigger("Chase",StageTriggerKind.Chase,"",new Vector3(105,2,36),new Vector3(3,5,6));
                Instance(Sym+"Architecture/Level/Prefabs/PopoVisual.prefab",new Vector3(125,0,36));
                Trigger("Rescue",StageTriggerKind.Rescue,"POPO",new Vector3(125,2,36),new Vector3(2,5,8));
                Trigger("Escape",StageTriggerKind.Escape,"POPO",new Vector3(130,2,36),new Vector3(2,5,8));
                Trigger("Fall",StageTriggerKind.Fall,"",new Vector3(65,-8,18),new Vector3(150,3,65),false);
                stage.RebuildRegistry();
                if(!EditorSceneManager.SaveScene(scene,ScenePath)) throw new IOException("Prototype save failed.");
            }
            finally
            {
                root=null;stage=null;
                EditorSceneManager.CloseScene(scene,true);
                if(previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
            }
            ApplyConfiguration();
            Cooked.Chase.Editor.ChaseAssetBuilder.ApplyFront();
            return "Prototype scene saved; catalog/entry settings/front-contact prefab updated; original scenes untouched.";
        }
        public static void ApplyConfiguration()
        {
            var installation=AssetDatabase.LoadAssetAtPath<ArchitectureInstallation>(ArchitectureInstallation.AssetPath);
            var entries=AssetDatabase.LoadAssetAtPath<IntegrationEntrySettings>(IntegrationSceneBuilder.EntriesPath);
            if(installation==null || entries==null) throw new InvalidOperationException("Existing installation assets required.");
            installation.Configure(IntegrationSceneBuilder.Catalog);EditorUtility.SetDirty(installation);AssetDatabase.SaveAssetIfDirty(installation);
            entries.Configure(new[]{new IntegrationEntrySettings.Entry("1_Stage","P_START",0),new IntegrationEntrySettings.Entry("03_2_Stage","S2_ENTRY",5,AbilityId.FormChange,AbilityId.RecoverShell,AbilityId.Tear),new IntegrationEntrySettings.Entry("03_3_Stage","S3_ENTRY",5,AbilityId.FormChange,AbilityId.RecoverShell,AbilityId.Tear)});
            EditorUtility.SetDirty(entries);AssetDatabase.SaveAssetIfDirty(entries);
        }
        private static void Instruction(Vector3 position,string text)
        { var go=Child("Instruction",root);go.transform.position=position;Set(go.AddComponent<LevelInstruction>(),"text",text); }
        private static void BuildMemo()
        {
            var controller=Child("MemoSystem",root).AddComponent<MemoUIController>();
            var canvas=Instance(Sym+"Prefabs/Interaction/MemoScreenCanvas.prefab",Vector3.zero);
            if(canvas.GetComponent<UnityEngine.UI.GraphicRaycaster>()==null) canvas.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            Set(controller,"screenRoot",canvas.GetComponent<RectTransform>());
            Set(controller,"viewPrefab",AssetDatabase.LoadAssetAtPath<MemoView>(Sym+"Memo/Prefabs/MemoView.prefab"));
            var memo=Instance(Sym+"Prefabs/Interaction/Memo_1001.prefab",new Vector3(5,1,1.3f));
            foreach(var c in memo.GetComponentsInChildren<MemoObject>(true)) Set(c,"memoUI",controller);
        }
        private static void ShellPuzzle()
        {
            var plateObject=Box("ShellPressurePlate",new Vector3(11,.1f,0),new Vector3(2,.2f,3));
            var plate=plateObject.AddComponent<PressurePlatePlatform>();Set(plate,"pressDepth",.08f);Set(plate,"travelSeconds",.5f);
            var gate=Box("ShellGate",new Vector3(15,2,0),new Vector3(.5f,4,4));
            var linked=gate.AddComponent<PressureLinkedPlatform>();Set(linked,"pressurePlate",plate);Set(linked,"travelOffset",new Vector3(0,5,0));Set(plate,"linkedPlatform",linked);
            var latch=Child("ShellPuzzle",root).AddComponent<ShellPressureLatch>();
            Set(latch,"plate",plate);Set(latch,"plateSurface",plateObject.GetComponent<BoxCollider>());Set(latch,"raisedGate",gate);Set(latch,"stage",stage);Set(latch,"crossingProgress",16f);
            Box("SmallTunnelRoof",new Vector3(18,1.55f,0),new Vector3(3,1,4));
        }
        private static CheckpointMarker Checkpoint(string id,Vector3 position,CameraZoneRequest zone,bool form,bool tear)
        {
            var marker=Child(id,root);marker.transform.position=position;var c=marker.AddComponent<CheckpointMarker>();
            Set(c,"checkpointId",id);Set(c,"cameraZone",LocalZone(zone,marker.transform));Set(c,"unlockForm",form);Set(c,"unlockTear",tear);
            Trigger(id+"_Contact",StageTriggerKind.Checkpoint,id,position+Vector3.up*2,new Vector3(2,5,5));return c;
        }
        private static CameraZoneRequest Zone(string id,CameraMovementMode movement,Vector3 alignmentPosition)
            => new CameraZoneRequest(id,movement,alignmentPosition);
        private static CameraZoneData LocalZone(CameraZoneRequest zone,Transform owner)
            => new CameraZoneData{Id=zone.Id,Movement=zone.Movement,AlignmentOffset=owner.InverseTransformPoint(zone.AlignmentPosition)};
        private static void CameraVolume(CameraZoneRequest zone,Vector3 position,Vector3 size)
        {var go=Volume("Camera_"+zone.Id,position,size);Set(go.AddComponent<CameraZoneTrigger>(),"zone",LocalZone(zone,go.transform));}
        private static void Trigger(string name,StageTriggerKind kind,string value,Vector3 position,Vector3 size,bool once=true)
        {var t=Volume(name,position,size).AddComponent<StageTrigger>();Set(t,"stage",stage);Set(t,"kind",kind);Set(t,"value",value);Set(t,"once",once);}
        private static GameObject Volume(string name,Vector3 position,Vector3 size)
        {var go=Child(name,root);go.transform.position=position;var c=go.AddComponent<BoxCollider>();c.size=size;c.isTrigger=true;return go;}
        private static GameObject Box(string name,Vector3 position,Vector3 size)
        {
            var wrapper=Child(name,root);wrapper.transform.position=position;
            var body=Child("Body",wrapper.transform);body.AddComponent<BoxCollider>().size=size;
            var visual=GameObject.CreatePrimitive(PrimitiveType.Cube);visual.name="Visual";Object.DestroyImmediate(visual.GetComponent<Collider>());visual.transform.SetParent(body.transform,false);visual.transform.localScale=size;
            return body;
        }
        private static GameObject Instance(string path,Vector3 position)
        {var asset=AssetDatabase.LoadAssetAtPath<GameObject>(path);if(asset==null)throw new FileNotFoundException(path);var go=(GameObject)PrefabUtility.InstantiatePrefab(asset);go.transform.SetParent(root,false);go.transform.position=position;return go;}
        private static GameObject Child(string name,Transform parent)
        {var go=new GameObject(name);go.transform.SetParent(parent,false);return go;}
        private static void Set(Object target,string name,object value)
        {var field=target.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Public|BindingFlags.Instance);if(field==null)throw new MissingFieldException(target.GetType().Name,name);field.SetValue(target,value);EditorUtility.SetDirty(target);}
        private static void EnsureFolder(string path)
        {if(AssetDatabase.IsValidFolder(path))return;var parent=path.Substring(0,path.LastIndexOf('/'));EnsureFolder(parent);AssetDatabase.CreateFolder(parent,path.Substring(path.LastIndexOf('/')+1));}
    }
}
#endif
