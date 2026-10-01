using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cooked.Level;
using FeelsGoodOnion.TechSYM.Enemies;
using FeelsGoodOnion.TechSYM.Features;
using FeelsGoodOnion.TechSYM.Interaction;
using FeelsGoodOnion.TechSYM.OvenTray;
using FeelsGoodOnion.TechSYM.Platforms;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Cooked.Level.Editor
{
    /// <summary>Only authors Level assets and three additive Stage scenes. Core remains integration-owned.</summary>
    public static class LevelAssetBuilder
    {
        public const string AssetRoot = "Assets/_Scenes/Tech_SYM/Architecture/Level";
        public const string StageRoot = "Assets/_Scenes/Tech_SYM/Architecture/Stages";
        private static Material floorMaterial, accentMaterial, wallMaterial;
        private static string meshPrefix;
        private static readonly List<CheckpointMarker> checkpoints = new List<CheckpointMarker>();
        private static readonly List<EnemyActor> enemies = new List<EnemyActor>();
        private static StageService stage;
        private static Transform root;

        [MenuItem("Cooked/Level/Build Approved Prototype Assets")]
        public static void Build()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before Level authoring.");
            // Do not close or implicitly save anybody's dirty scenes.
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save your own scene before Level authoring.");
            // Batch startup has an untitled empty scene; open an existing saved scene read-only before additive authoring.
            if (string.IsNullOrEmpty(SceneManager.GetActiveScene().path))
                EditorSceneManager.OpenScene("Assets/_Scenes/Tech_SYM/03_InGame.unity", OpenSceneMode.Single);
            EnsureFolder(AssetRoot + "/Prefabs"); EnsureFolder(AssetRoot + "/Meshes");
            EnsureFolder(AssetRoot + "/Materials"); EnsureFolder(StageRoot);
            floorMaterial = MaterialAsset("WarmCounter", new Color(.71f,.45f,.24f));
            accentMaterial = MaterialAsset("MintInteractable", new Color(.27f,.73f,.61f));
            wallMaterial = MaterialAsset("WarmCeramic", new Color(.95f,.83f,.60f));
            Scene previous = SceneManager.GetActiveScene();
            Scene scratch = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scratch);
                BuildVisualPrefabs(); BuildCameraPrefab(); // Backdrop is authored by Content Dev.
            }
            finally { EditorSceneManager.CloseScene(scratch, true); if (previous.IsValid()) SceneManager.SetActiveScene(previous); }
            for (int number = 1; number <= 3; number++) BuildStage(number);
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
            ValidateSavedAssets();
            Debug.Log("[Cooked.Level] Authoring completed. Runtime traversal remains a separate mandatory gate.");
        }

        private static void BuildStage(int number)
        {
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                meshPrefix = "S" + number;
                root = Empty("03_" + number + "_Stage", null, Vector3.zero).transform;
                stage = root.gameObject.AddComponent<StageService>();
                checkpoints.Clear(); enemies.Clear();
                if (number == 1) StageOne(); else if (number == 2) StageTwo(); else StageThree();
                Set(stage, "stageId", root.name);
                Set(stage, "initialCheckpointId", number == 1 ? "S1_START" : number == 2 ? "S2_ENTRY" : "S3_ENTRY");
                SetReferences(stage, "checkpoints", checkpoints.Cast<Object>().ToArray());
                SetReferences(stage, "enemies", enemies.Cast<Object>().ToArray());
                if (number == 3)
                {
                    var so = new SerializedObject(stage); var array = so.FindProperty("chaseWaypoints");
                    array.arraySize = 4;
                    var points = new[] { new Vector3(74,0,0), new Vector3(84,0,0), new Vector3(96,0,0), new Vector3(108,0,0) };
                    for (int i=0;i<points.Length;i++) array.GetArrayElementAtIndex(i).vector3Value=points[i];
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                // Author a local +X strip, then place it in the approved kitchen route.
                float authoredStart = number == 1 ? 0 : number == 2 ? 36 : 66;
                foreach (Transform child in root) child.localPosition -= Vector3.right * authoredStart;
                root.SetPositionAndRotation(number == 1 ? Vector3.zero : number == 2 ? new Vector3(36,0,0) : new Vector3(36,0,30),
                    Quaternion.Euler(0, number == 1 ? 0 : number == 2 ? -90 : 180, 0));
                if (number == 3)
                {
                    var so = new SerializedObject(stage); var path = so.FindProperty("chaseWaypoints");
                    for (int i=0;i<path.arraySize;i++)
                    {
                        Vector3 old = path.GetArrayElementAtIndex(i).vector3Value;
                        path.GetArrayElementAtIndex(i).vector3Value = root.TransformPoint(old - Vector3.right * authoredStart);
                    }
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                stage.RebuildRegistry();
                if (!EditorSceneManager.SaveScene(scene, StageRoot + "/" + root.name + ".unity")) throw new IOException("Stage save failed.");
            }
            finally { EditorSceneManager.CloseScene(scene, true); if (previous.IsValid()) SceneManager.SetActiveScene(previous); }
        }

        private static void StageOne()
        {
            Floor(0,7,0,4); Floor(9,36,0,4);
            SideWalls(0,33,2.35f); // Open the final 3m for the physical kitchen corner.
            Checkpoint("S1_START", new Vector3(2,0,0));
            Trigger("Enter", StageTriggerKind.Enter, "SIDE_1", new Vector3(18,4,0), new Vector3(36,8,4), false);
            Sign("MOVE",new Vector3(3,0,0),"이동 · Space 점프 / 작은 틈을 건너세요");
            Block("Step",new Vector3(5,.3f,0),new Vector3(2,.6f,4));
            Trigger("FormUnlock",StageTriggerKind.UnlockForm,"",new Vector3(10.5f,1.5f,0),new Vector3(2,3,4));
            ShellPuzzle("Shell1",12,14,0,4);
            // Leave 1.5m clear run-up: the small capsule must clear the roof before its 3m jump.
            Tunnel("SmallTunnel",15,17,0,4);
            Block("SmallHighStep",new Vector3(20,1.15f,0),new Vector3(3,2.3f,4));
            Checkpoint("S1_SKILL",new Vector3(20,2.3f,0));
            Trigger("TearUnlock",StageTriggerKind.UnlockTear,"",new Vector3(21,3.3f,0),new Vector3(2,4,4));
            Pickup(new Vector3(22,.6f,0)); Pickup(new Vector3(23,.6f,0)); Pickup(new Vector3(24,.6f,0));
            var enemy = Enemy(new Vector3(27,1.2f,0));
            var door = Block("TearBarrier",new Vector3(29,4,0),new Vector3(.6f,8,4));
            // The low canopy prevents killing the tutorial enemy by a stomp before required tear use.
            Block("StunArenaCanopy",new Vector3(26.5f,3.7f,0),new Vector3(5,3,4));
            var gate = Empty("RealTearPuzzle",root,Vector3.zero).AddComponent<TearStunGate>();
            Set(gate,"enemy",enemy); Set(gate,"barrier",door);
            Sign("TEAR",new Vector3(24,0,0),"E 눈물: 실제 적을 기절시키면 문이 열립니다. 조각 부족 시 체크포인트 재시도");
            Fall(0,36,4);
        }
        private static void StageTwo()
        {
            Floor(36,53,0,12); Floor(59,66,0,12);
            SideWalls(39,63,6.35f); // Entry/exit corners must not cut across adjacent side-view lanes.
            Checkpoint("S2_ENTRY",new Vector3(38,0,0));
            Trigger("Enter",StageTriggerKind.Enter,"QUARTER",new Vector3(51,4,0),new Vector3(26,8,12),false);
            Trigger("SafeDialogue",StageTriggerKind.Dialogue,"EXPLORE_OVEN_01",new Vector3(40,1.2f,0),new Vector3(2,2.4f,2));
            // A full-width wall leaves a single lower-left puzzle doorway, no Z bypass.
            Block("PuzzleWallA",new Vector3(45,4,2.25f),new Vector3(.6f,8,7.5f));
            Block("PuzzleWallB",new Vector3(45,4,-5.75f),new Vector3(.6f,8,.5f));
            ShellPuzzle("Shell2",43,45,-3.5f,4);
            Tunnel("ExplorationTunnel",46,48,-3.5f,4);
            Block("ExploreStep",new Vector3(49,.4f,-1),new Vector3(2,.8f,2));
            Checkpoint("S2_OVEN",new Vector3(51,0,0));
            BuildOven();
            // Centre the Actor before the last corner; leave the last 3m open to turn into Stage 3.
            ExitFunnel("ExitFunnelFront",new Vector3(62.5f,4,-3.3f));
            ExitFunnel("ExitFunnelBack",new Vector3(62.5f,4,3.3f));
            Sign("OVEN",new Vector3(51,0,0),"F 트레이 손잡이 조작 · 트레이를 펼치고 건너세요");
            Fall(36,66,14);
        }
        private static readonly Vector3 ExitFunnelSize = new Vector3(1,8,5.4f);
        private const float FunnelRailThickness = .18f;
        private const float FunnelBarThickness = .10f;
        // Small Actor diameter is .5m. An opening narrower than .35m reads as a fence,
        // not a doorway, while most of its projected area remains open to kitchen scenery.
        private const float MaximumFunnelVisualGap = .35f;
        private static void ExitFunnel(string name, Vector3 centre)
        {
            var item = Empty(name, root, centre);
            var physics = Empty("Collider", item.transform, centre); physics.transform.localPosition = Vector3.zero;
            physics.AddComponent<BoxCollider>().size = ExitFunnelSize;
            BuildExitFunnelVisual(item.transform, floorMaterial);
        }
        private static ProBuilderMesh BuildExitFunnelVisual(Transform parent, Material material)
        {
            var positions = new List<Vector3>(); var faces = new List<Face>();
            void Box(Vector3 centre, Vector3 size)
            {
                var cube = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
                try
                {
                    int start = positions.Count;
                    foreach (var p in cube.positions) positions.Add(p + centre);
                    foreach (var face in cube.faces) faces.Add(new Face(face.indexes.Select(index => index + start)));
                }
                finally { Object.DestroyImmediate(cube.gameObject); }
            }
            float edge = (ExitFunnelSize.z - FunnelRailThickness) * .5f;
            float railY = (ExitFunnelSize.y - FunnelRailThickness) * .5f;
            foreach (float y in new[]{-railY, railY})
                Box(new Vector3(0,y,0), new Vector3(ExitFunnelSize.x,FunnelRailThickness,ExitFunnelSize.z));
            int intervals = Mathf.CeilToInt((edge * 2) / (MaximumFunnelVisualGap + FunnelBarThickness));
            for (int i=0;i<=intervals;i++)
                Box(new Vector3(0,0,-edge + edge * 2 * i / intervals),
                    new Vector3(ExitFunnelSize.x,ExitFunnelSize.y-FunnelRailThickness*2,
                        i==0 || i==intervals ? FunnelRailThickness : FunnelBarThickness));
            var mesh = ProBuilderMesh.Create(positions, faces);
            mesh.gameObject.name = "Geometry_" + parent.name;
            mesh.transform.SetParent(parent,false); mesh.transform.localPosition = Vector3.zero;
            mesh.GetComponent<MeshRenderer>().sharedMaterial = material;
            foreach(var collider in mesh.GetComponents<Collider>()) Object.DestroyImmediate(collider);
            // Reuse the original two native mesh assets/GUIDs; do not create orphan replacement assets.
            PersistGeometryMesh(mesh, AssetRoot+"/Meshes/S2_"+parent.name+".asset");
            return mesh;
        }
        [Serializable] private sealed class FunnelColliderSnapshot
        {
            public string entity, path, serialized, matrix;
            public Vector3 centre, size;
        }
        [Serializable] private sealed class FunnelRepairEvidence
        {
            public string utc, scenePath, error;
            public bool passed, dirtyAfterSave;
            public float passageWidthBefore, passageWidthAfter, maximumVisualBarGap;
            public FunnelColliderSnapshot[] before, after;
            public string[] changedAssets;
            public int missingScripts, editableFrameCount;
        }
        private static FunnelColliderSnapshot[] CaptureColliders(Scene scene)
        {
            return scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<Collider>(true))
                .Select(c=>new FunnelColliderSnapshot {
                    entity=c.GetEntityId().ToString(), path=HierarchyPath(c.transform),
                    serialized=EditorJsonUtility.ToJson(c), matrix=c.transform.localToWorldMatrix.ToString("R"),
                    centre=c.bounds.center, size=c.bounds.size
                }).OrderBy(c=>c.entity,StringComparer.Ordinal).ToArray();
        }
        private static string HierarchyPath(Transform item) => item.parent==null ? item.name : HierarchyPath(item.parent)+"/"+item.name;
        private static void RequireSameColliders(FunnelColliderSnapshot[] before, FunnelColliderSnapshot[] after)
        {
            if(before.Length!=after.Length) throw new InvalidOperationException("Stage2 Collider count changed.");
            for(int i=0;i<before.Length;i++)
                if(before[i].entity!=after[i].entity || before[i].path!=after[i].path ||
                    before[i].serialized!=after[i].serialized || before[i].matrix!=after[i].matrix ||
                    (before[i].centre-after[i].centre).sqrMagnitude>1e-8f || (before[i].size-after[i].size).sqrMagnitude>1e-8f)
                    throw new InvalidOperationException("Stage2 Collider changed: "+before[i].path);
        }
        [MenuItem("Cooked/Level/Repair Stage2 Exit Funnel Visuals Only")]
        public static void RepairStageTwoExitFunnelVisuals()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("Stable Edit Mode required.");
            for(int i=0;i<SceneManager.sceneCount;i++)
                if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Dirty scene must be saved by its owner first.");
            const string scenePath=StageRoot+"/03_2_Stage.unity";
            var report=new FunnelRepairEvidence { utc=DateTime.UtcNow.ToString("O"),scenePath=scenePath,
                changedAssets=new[]{scenePath,AssetRoot+"/Meshes/S2_ExitFunnelFront.asset",AssetRoot+"/Meshes/S2_ExitFunnelBack.asset"} };
            var previous=SceneManager.GetActiveScene(); var scene=SceneManager.GetSceneByPath(scenePath);
            bool opened=!scene.isLoaded;
            try
            {
                if(opened) scene=EditorSceneManager.OpenScene(scenePath,OpenSceneMode.Additive);
                SceneManager.SetActiveScene(scene); // Temporary PB objects must never dirty the user's active scene.
                var stageRoot=scene.GetRootGameObjects().Single(go=>go.name=="03_2_Stage").transform;
                var parents=new[]{stageRoot.Find("ExitFunnelFront"),stageRoot.Find("ExitFunnelBack")};
                if(parents.Any(p=>p==null)) throw new InvalidOperationException("Expected Stage2 exit funnels are missing.");
                var boxes=parents.Select(p=>p.Find("Collider")?.GetComponent<BoxCollider>()).ToArray();
                var visuals=parents.Select(p=>p.Find("Geometry_"+p.name)).ToArray();
                for(int i=0;i<parents.Length;i++)
                    if(boxes[i]==null || boxes[i].size!=ExitFunnelSize || visuals[i]==null ||
                        visuals[i].GetComponent<ProBuilderMesh>()==null || visuals[i].GetComponent<MeshRenderer>()?.sharedMaterial==null ||
                        visuals[i].GetComponentsInChildren<Collider>(true).Length!=0 ||
                        AssetDatabase.LoadAssetAtPath<Mesh>(report.changedAssets[i+1])==null)
                        throw new InvalidOperationException("Unexpected funnel layout; refusing broad repair.");
                Physics.SyncTransforms(); report.before=CaptureColliders(scene);
                report.passageWidthBefore=boxes[0].bounds.min.x-boxes[1].bounds.max.x;
                var frames=new List<ProBuilderMesh>();
                for(int i=0;i<parents.Length;i++)
                {
                    var material=visuals[i].GetComponent<MeshRenderer>().sharedMaterial;
                    Object.DestroyImmediate(visuals[i].gameObject);
                    frames.Add(BuildExitFunnelVisual(parents[i],material));
                }
                Physics.SyncTransforms(); report.after=CaptureColliders(scene);
                RequireSameColliders(report.before,report.after);
                for(int i=0;i<frames.Count;i++)
                {
                    var visualBounds=frames[i].GetComponent<MeshRenderer>().bounds;
                    if((visualBounds.center-boxes[i].bounds.center).sqrMagnitude>1e-8f ||
                        (visualBounds.size-boxes[i].bounds.size).sqrMagnitude>1e-8f)
                        throw new InvalidOperationException("Frame no longer marks the original collision boundary: "+parents[i].name);
                }
                report.passageWidthAfter=boxes[0].bounds.min.x-boxes[1].bounds.max.x;
                if(!Mathf.Approximately(report.passageWidthBefore,report.passageWidthAfter) ||
                    Mathf.Abs(report.passageWidthAfter-1.2f)>1e-4f) throw new InvalidOperationException("Exit passage width changed.");
                float span=ExitFunnelSize.z-FunnelRailThickness;
                report.maximumVisualBarGap=span/Mathf.CeilToInt(span/(MaximumFunnelVisualGap+FunnelBarThickness))-FunnelBarThickness;
                if(report.maximumVisualBarGap>MaximumFunnelVisualGap) throw new InvalidOperationException("Fence appears passable.");
                report.editableFrameCount=frames.Count(m=>m.vertexCount>0 && m.faceCount>0 && m.GetComponent<MeshFilter>().sharedMesh!=null);
                report.missingScripts=scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<Transform>(true))
                    .Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
                if(report.editableFrameCount!=2 || report.missingScripts!=0) throw new InvalidOperationException("Invalid frame or missing script.");
                foreach(var frame in frames) AssetDatabase.SaveAssetIfDirty(frame.GetComponent<MeshFilter>().sharedMesh);
                EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene,scenePath)) throw new IOException("Stage2 scene save failed.");
                report.dirtyAfterSave=scene.isDirty;
                if(report.dirtyAfterSave) throw new InvalidOperationException("Stage2 remained dirty after save.");
                report.passed=true;
                Debug.Log("[Cooked.Level] Stage2 frame repair saved. All Collider serialized data/world bounds and 1.2m passage preserved. Visual capture still required.");
            }
            catch(Exception error) { report.error=error.ToString(); throw; }
            finally
            {
                if(opened && scene.IsValid() && scene.isLoaded) EditorSceneManager.CloseScene(scene,true);
                if(previous.IsValid() && previous.isLoaded) SceneManager.SetActiveScene(previous);
                Directory.CreateDirectory("output/Tech_SYM/level/evidence");
                File.WriteAllText("output/Tech_SYM/level/evidence/exit-funnel-visual-repair.json",JsonUtility.ToJson(report,true));
            }
        }

        private static void StageThree()
        {
            Floor(66,87,0,4); Floor(89,95,0,4); Floor(97,110,0,4);
            SideWalls(69,110,2.35f); // Keep the first corner open for the incoming quarter-view route.
            Checkpoint("S3_ENTRY",new Vector3(68,0,0));
            Trigger("Enter",StageTriggerKind.Enter,"SIDE_3",new Vector3(88,4,0),new Vector3(44,8,4),false);
            ShellPuzzle("Shell3",71,73,0,4); Tunnel("ComboTunnel",74,77,0,4);
            Block("ComboStep",new Vector3(79,.5f,0),new Vector3(2,1,4));
            Checkpoint("S3_CHASE",new Vector3(82,0,0));
            BuildRetreatGate();
            Trigger("StartChase",StageTriggerKind.Chase,"CHASE_3",new Vector3(95.5f,4,0),new Vector3(23,8,4));
            Sign("CHASE",new Vector3(82,0,0),"앞으로 탈출! 점프와 작은 형태로 통과하세요");
            Tunnel("ChaseTunnel",99,102,0,4);
            var popo = PrefabInstance("PopoVisual",root,new Vector3(105,0,0));
            popo.name = "PopoRescue";
            Trigger("Rescue",StageTriggerKind.Rescue,"POPO",new Vector3(105,1.5f,0),new Vector3(2,3,4));
            Trigger("Escape",StageTriggerKind.Escape,"POPO",new Vector3(107,1.5f,0),new Vector3(2,3,4));
            Fall(66,110,4);
        }

        private static void BuildRetreatGate()
        {
            var gateRoot=Empty("ChaseRetreatGate",root,Vector3.zero);
            var gate=gateRoot.AddComponent<ChaseRetreatGate>();
            var blockerObject=Empty("Blocker",gateRoot.transform,new Vector3(82.4f,4,0));
            var collider=blockerObject.AddComponent<BoxCollider>(); collider.size=new Vector3(.6f,8,4.8f);
            Geometry("ChaseRetreatGate",blockerObject.transform,Vector3.zero,collider.size,accentMaterial);
            Set(gate,"blocker",collider); Set(stage,"chaseRetreatGate",gate); Set(stage,"chaseStartProgress",18f);
            blockerObject.SetActive(false);
        }
        // Targeted repair: checkpoint offsets only in every Stage, plus approved Stage3 chase changes.
        public static void UpdateChaseAssets()
        {
            if(Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before authoring.");
            for(int i=0;i<SceneManager.sceneCount;i++) if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save your own dirty scene before authoring.");
            UpdateCheckpointSpawnPoses();
            var scene=EditorSceneManager.OpenScene(StageRoot+"/03_3_Stage.unity",OpenSceneMode.Single);
            root=scene.GetRootGameObjects().Single(go=>go.GetComponent<StageService>()!=null).transform;
            stage=root.GetComponent<StageService>(); meshPrefix="S3";
            accentMaterial=AssetDatabase.LoadAssetAtPath<Material>(AssetRoot+"/Materials/MintInteractable.mat");
            if(root.GetComponentInChildren<ChaseRetreatGate>(true)==null) BuildRetreatGate();
            Set(stage,"chaseRetreatGate",root.GetComponentInChildren<ChaseRetreatGate>(true));
            Set(stage,"chaseStartProgress",18f);
            foreach(var trigger in root.GetComponentsInChildren<StageTrigger>(true))
            {
                if(trigger.Kind==StageTriggerKind.Chase)
                { trigger.transform.position=new Vector3(95.5f,4,0); trigger.GetComponent<BoxCollider>().size=new Vector3(23,8,4); }
                if(trigger.Kind==StageTriggerKind.Checkpoint && trigger.Value=="S3_CHASE")
                { trigger.transform.position=new Vector3(82,4,0); trigger.GetComponent<BoxCollider>().size=new Vector3(1.5f,8,4); }
            }
            if(!EditorSceneManager.SaveScene(scene)) throw new IOException("Stage3 chase repair save failed.");
            AssetDatabase.SaveAssets();
        }
        private static void UpdateCheckpointSpawnPoses()
        {
            for(int number=1;number<=3;number++)
            {
                var scene=EditorSceneManager.OpenScene(StageRoot+"/03_"+number+"_Stage.unity",OpenSceneMode.Single);
                var markers=scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<CheckpointMarker>(true)).ToArray();
                if(markers.Length!=2) throw new InvalidOperationException("Expected two checkpoint markers: "+scene.path);
                foreach(var marker in markers)
                {
                    // Only this serialized field changes. Marker, beacon and trigger transforms are untouched.
                    Set(marker,"bodyOffset",new Vector3(0f,1.03f,0f));
                }
                EditorSceneManager.MarkSceneDirty(scene);
                if(!EditorSceneManager.SaveScene(scene)) throw new IOException("Checkpoint offset save failed: "+scene.path);
            }
        }
        private static void BuildOven()
        {
            var oven = PrefabInstance("KitchenOven",root,new Vector3(56,0,-9));
            oven.name = "OvenWithIndependentTray";
            var trayRoot = Empty("FunctionalOvenTray",root,Vector3.zero);
            var body = Empty("PhysicsBody",trayRoot.transform,new Vector3(56,0,8));
            body.AddComponent<BoxCollider>().size = new Vector3(6,.4f,3);
            body.AddComponent<KinematicPlatformMotion>();
            var tray = body.AddComponent<OvenTrayObject>();
            Set(tray,"travelOffset",new Vector3(0,0,-8)); Set(tray,"moveDuration",1.2f);
            Geometry("Tray",body.transform,Vector3.zero,new Vector3(6,.4f,3),accentMaterial);
            var handleRoot = Empty("TrayHandle",root,new Vector3(51.8f,.7f,0));
            var handle = handleRoot.AddComponent<OvenHandle>(); Set(handle,"tray",tray);
            handleRoot.AddComponent<InteractionPromptAnchor>();
            var shape = Empty("Collider",handleRoot.transform,Vector3.zero); shape.transform.localPosition=Vector3.zero;
            shape.AddComponent<BoxCollider>().size=new Vector3(.4f,1,.4f);
            Geometry("Handle",handleRoot.transform,Vector3.zero,new Vector3(.3f,.8f,.3f),accentMaterial);
            // The existing tray anchor moves with its body; disable it so only the fixed handle is offered.
            body.GetComponent<InteractionPromptAnchor>().enabled=false;
        }
        private static void ShellPuzzle(string name,float plateX,float doorX,float z,float width)
        {
            var puzzle = Empty(name,root,Vector3.zero);
            var plateBody = Empty("PlatePhysics",puzzle.transform,new Vector3(plateX,.1f,z));
            var plateCollider = plateBody.AddComponent<BoxCollider>(); plateCollider.size=new Vector3(1.8f,.2f,1.8f);
            var plate = plateBody.AddComponent<PressurePlatePlatform>();
            Set(plate,"travelSeconds",.5f); Set(plate,"pressDepth",.08f);
            Geometry(name+"Plate",plateBody.transform,Vector3.zero,plateCollider.size,accentMaterial);
            var gateBody = Empty("GatePhysics",puzzle.transform,new Vector3(doorX,4,z));
            gateBody.AddComponent<BoxCollider>().size=new Vector3(.5f,8,width);
            var linked = gateBody.AddComponent<PressureLinkedPlatform>();
            Set(linked,"pressurePlate",plate); Set(linked,"travelOffset",new Vector3(0,8,0)); Set(linked,"returnSpeed",4f);
            Set(plate,"linkedPlatform",linked);
            Geometry(name+"Door",gateBody.transform,Vector3.zero,new Vector3(.5f,8,width),accentMaterial);
            var latch = puzzle.AddComponent<ShellPressureLatch>();
            Set(latch,"plate",plate); Set(latch,"plateSurface",plateCollider); Set(latch,"raisedGate",gateBody); Set(latch,"stage",stage); Set(latch,"crossingProgress",doorX+.7f-(meshPrefix=="S1"?0:meshPrefix=="S2"?36:66));
            Sign(name+"Guide",new Vector3(plateX-1,0,z),"압력판에서 작은 형태로! 껍질을 남기고 문을 통과한 뒤 되돌아와 Q 회수");
            // After latching the same unobstructed floor returns to plate centre. Recovery is possible at distance 0..1m.
        }
        private static EnemyActor Enemy(Vector3 position)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Scenes/Tech_SYM/Enemies/Prefabs/StationaryEnemy.prefab");
            if(prefab==null) throw new InvalidOperationException("Missing original enemy prefab.");
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab); go.transform.SetParent(root,false); go.transform.position=position;
            var actor=go.GetComponent<EnemyActor>(); Set(actor,"initiallyInvincible",true);
            // Reuse the existing sensors; replace only this instance's dummy visual.
            var body=new SerializedObject(actor).FindProperty("body").objectReferenceValue as BoxCollider;
            var old=body.transform.Find("Visual"); if(old!=null) old.gameObject.SetActive(false);
            PrefabInstance("MonsterVisual",body.transform,body.transform.position-Vector3.up*.4f);
            enemies.Add(actor); return actor;
        }
        private static void Pickup(Vector3 position)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Scenes/Tech_SYM/Prefabs/TearFragment.prefab");
            if(prefab==null) throw new InvalidOperationException("Missing TearFragment prefab.");
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab); instance.transform.SetParent(root,false); instance.transform.position=position;
            foreach(var legacy in instance.GetComponentsInChildren<TearFragment>(true))
            {
                legacy.enabled=false;
                var pickup=legacy.gameObject.AddComponent<LevelTearFragment>();
                Set(pickup,"pickupRoot",instance);
            }
        }
        private static void Checkpoint(string id,Vector3 foot)
        {
            var go=Empty(id,root,foot); var point=go.AddComponent<CheckpointMarker>(); Set(point,"checkpointId",id); Set(point,"bodyOffset",new Vector3(0f,1.03f,0f)); checkpoints.Add(point);
            Geometry(id+"Beacon",go.transform,new Vector3(0,.06f,0),new Vector3(1.5f,.12f,1.5f),accentMaterial);
            float height=id=="S3_CHASE" ? 8f : 2f;
            float depth=id=="S3_CHASE" ? 4f : 1.5f;
            Trigger(id+"Trigger",StageTriggerKind.Checkpoint,id,foot+Vector3.up*(height*.5f),new Vector3(1.5f,height,depth));
        }
        private static void Trigger(string name,StageTriggerKind kind,string value,Vector3 position,Vector3 size,bool once=true)
        {
            var go=Empty(name+"Trigger",root,position); var box=go.AddComponent<BoxCollider>(); box.isTrigger=true; box.size=size;
            var trigger=go.AddComponent<StageTrigger>(); Set(trigger,"stage",stage); Set(trigger,"kind",(int)kind); Set(trigger,"value",value); Set(trigger,"once",once);
        }
        private static void Sign(string name,Vector3 position,string text)
        {
            var sign=Empty(name+"Instruction",root,position).AddComponent<LevelInstruction>(); Set(sign,"text",text);
        }
        private static void Floor(float from,float to,float z,float width) => Block("Floor"+from,new Vector3((from+to)/2,-.5f,z),new Vector3(to-from,1,width));
        private static void SideWalls(float from,float to,float z)
        {
            // Collision-only guards keep the camera's near wall from occluding the route.
            foreach(float depth in new[]{-z,z})
            { var go=Empty("Boundary",root,new Vector3((from+to)/2,4,depth)); go.AddComponent<BoxCollider>().size=new Vector3(to-from,8,.7f); }
        }
        private static void Tunnel(string name,float from,float to,float z,float width)
            => Block(name,new Vector3((from+to)/2,3.2f,z),new Vector3(to-from,4f,width));
        private static void Fall(float from,float to,float width) => Trigger("Fall",StageTriggerKind.Fall,"",new Vector3((from+to)/2,-6,0),new Vector3(to-from+3,2,width+3));
        private static GameObject Block(string name,Vector3 centre,Vector3 size)
        {
            var go=Empty(name,root,centre); var physics=Empty("Collider",go.transform,centre); physics.transform.localPosition=Vector3.zero;
            physics.AddComponent<BoxCollider>().size=size;
            Geometry(name,go.transform,Vector3.zero,size,floorMaterial); return go;
        }

        private static void BuildVisualPrefabs()
        {
            meshPrefix="Visual";
            VisualPrefab("OniVisual","Assets/_Art/Characters/Models/ch_onion.fbx","Assets/_Art/Characters/Materials/Ch_onion_default.mat",1.8f);
            VisualPrefab("PopoVisual","Assets/_Art/Characters/Models/ch_potato.fbx","Assets/_Art/Characters/Materials/ch_potato_default.mat",1.5f);
            VisualPrefab("MonsterVisual","Assets/_Art/Characters/Models/ch_monster.fbx","Assets/_Art/Characters/Materials/monster_default.mat",1.4f);
            VisualPrefab("KitchenTable","Assets/_Art/Environment/dummy/table.fbx",null,6);
            VisualPrefab("KitchenTable2","Assets/_Art/Environment/dummy/table2.fbx",null,6);
            VisualPrefab("KitchenOven","Assets/_Art/Environment/Models/bg_oven.fbx","Assets/_Scenes/Tech_SYM/Architecture/Background/Materials/bg_oven_basecolor.mat",8);
            VisualPrefab("KitchenStove","Assets/_Art/Environment/dummy/gas_stove2.fbx",null,7);
        }
        private static void VisualPrefab(string name,string source,string materialPath,float height)
        {
            var rootObject=Empty(name,null,Vector3.zero);
            try
            {
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>(source);
                if(asset==null) throw new InvalidOperationException("Missing model: "+source);
                var visual=(GameObject)PrefabUtility.InstantiatePrefab(asset); visual.name="Visual"; visual.transform.SetParent(rootObject.transform,false);
                var renderers=visual.GetComponentsInChildren<Renderer>(true);
                if(renderers.Length==0) throw new InvalidOperationException("No renderer: "+source);
                var bounds=renderers[0].bounds; foreach(var renderer in renderers) bounds.Encapsulate(renderer.bounds);
                if(bounds.size.y<.00001f) throw new InvalidOperationException("Invalid model bounds: "+source);
                float factor=height/bounds.size.y; visual.transform.localScale*=factor;
                visual.transform.localPosition=new Vector3(-bounds.center.x,-bounds.min.y,-bounds.center.z)*factor;
                if(materialPath!=null)
                {
                    var mat=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                    if(mat==null) throw new InvalidOperationException("Missing material: "+materialPath);
                    foreach(var renderer in renderers) renderer.sharedMaterials=Enumerable.Repeat(mat,renderer.sharedMaterials.Length).ToArray();
                }
                SavePrefab(rootObject,name);
            }
            finally { Object.DestroyImmediate(rootObject); }
        }
        private static void BuildCameraPrefab()
        {
            var go=Empty("CoreLevelCamera",null,Vector3.zero);
            try
            {
                var rig=Empty("Camera",go.transform,Vector3.zero); var camera=rig.AddComponent<Camera>(); camera.fieldOfView=50; camera.nearClipPlane=.1f; camera.farClipPlane=200;
                camera.tag="MainCamera";
                var follow=go.AddComponent<LevelCameraService>(); Set(follow,"cameraRig",rig.transform);
                var so=new SerializedObject(follow); var zones=so.FindProperty("zones"); zones.arraySize=3;
                string[] ids={"SIDE_1","QUARTER","SIDE_3"}; float[] min={-3,0,0}, max={36,30,44};
                for(int i=0;i<3;i++)
                {
                    var z=zones.GetArrayElementAtIndex(i); z.FindPropertyRelative("Id").stringValue=ids[i];
                    z.FindPropertyRelative("MinimumProgress").floatValue=min[i]; z.FindPropertyRelative("MaximumProgress").floatValue=max[i];
                    z.FindPropertyRelative("Origin").vector3Value=i==0?Vector3.zero:i==1?new Vector3(36,0,0):new Vector3(36,0,30);
                    z.FindPropertyRelative("Forward").vector3Value=i==0?Vector3.right:i==1?Vector3.forward:Vector3.left;
                    z.FindPropertyRelative("HalfWidth").floatValue=i==1?6:2;
                    z.FindPropertyRelative("AllowDepth").boolValue=i==1;
                    z.FindPropertyRelative("Offset").vector3Value=i==1?new Vector3(-10,12,-7):i==0?new Vector3(1,3,-12):new Vector3(-1,3,12);
                    z.FindPropertyRelative("LookOffset").vector3Value=new Vector3(i==2?-2:2,0,0);
                }
                so.ApplyModifiedPropertiesWithoutUndo(); SavePrefab(go,"CoreLevelCamera");
            }
            finally { Object.DestroyImmediate(go); }
        }
        private static void BuildBackdropPrefab()
        {
            meshPrefix="Backdrop"; var go=Empty("KitchenBackdrop",null,Vector3.zero);
            try
            {
                Geometry("BackWall",go.transform,new Vector3(55,7,17),new Vector3(140,24,1),wallMaterial);
                Geometry("BackCounter",go.transform,new Vector3(55,-4,6),new Vector3(140,6,18),floorMaterial);
                Geometry("Shelf",go.transform,new Vector3(55,10,13),new Vector3(140,.5f,6),floorMaterial);
                for(int i=0;i<5;i++) PrefabInstance(i%2==0?"KitchenTable":"KitchenTable2",go.transform,new Vector3(i*28-4,-6,6));
                PrefabInstance("KitchenOven",go.transform,new Vector3(56,-3,13));
                PrefabInstance("KitchenStove",go.transform,new Vector3(88,-3,12));
                SavePrefab(go,"KitchenBackdrop");
            }
            finally { Object.DestroyImmediate(go); }
        }
        private static GameObject PrefabInstance(string name,Transform parent,Vector3 world)
        {
            var asset=AssetDatabase.LoadAssetAtPath<GameObject>(AssetRoot+"/Prefabs/"+name+".prefab");
            if(asset==null) throw new InvalidOperationException("Missing Level prefab "+name);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(asset); instance.transform.SetParent(parent,false); instance.transform.position=world; return instance;
        }
        private static void SavePrefab(GameObject go,string name)
        {
            if(PrefabUtility.SaveAsPrefabAsset(go,AssetRoot+"/Prefabs/"+name+".prefab")==null) throw new IOException("Prefab save failed: "+name);
        }
        private static GameObject Empty(string name,Transform parent,Vector3 position)
        {
            var go=new GameObject(name); go.transform.SetParent(parent,false); go.transform.position=position; return go;
        }
        private static void Geometry(string name,Transform parent,Vector3 local,Vector3 size,Material material)
        {
            var mesh=ShapeGenerator.GenerateCube(PivotLocation.Center,size);
            mesh.gameObject.name="Geometry_"+name; mesh.transform.SetParent(parent,false); mesh.transform.localPosition=local;
            mesh.GetComponent<MeshRenderer>().sharedMaterial=material;
            // PB editable vertices/faces remain on the component. Persist the generated mesh separately too.
            PersistGeometryMesh(mesh,AssetRoot+"/Meshes/"+meshPrefix+"_"+name+".asset");
            foreach(var collider in mesh.GetComponents<Collider>()) Object.DestroyImmediate(collider);
        }
        private static void PersistGeometryMesh(ProBuilderMesh mesh,string path)
        {
            var filter=mesh.GetComponent<MeshFilter>();
            var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(existing==null) { AssetDatabase.CreateAsset(filter.sharedMesh,path); }
            else { EditorUtility.CopySerialized(filter.sharedMesh,existing); filter.sharedMesh=existing; EditorUtility.SetDirty(existing); }
        }
        private static Material MaterialAsset(string name,Color color)
        {
            string path=AssetRoot+"/Materials/"+name+".mat"; var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material==null)
            {
                var shader=Shader.Find("Universal Render Pipeline/Lit"); if(shader==null) throw new InvalidOperationException("URP Lit unavailable.");
                material=new Material(shader); AssetDatabase.CreateAsset(material,path);
            }
            material.SetColor("_BaseColor",color); EditorUtility.SetDirty(material); return material;
        }
        private static void EnsureFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path)) return;
            var parent=path.Substring(0,path.LastIndexOf('/')); EnsureFolder(parent); AssetDatabase.CreateFolder(parent,path.Substring(path.LastIndexOf('/')+1));
        }
        private static void Set(Object target,string field,object value)
        {
            var so=new SerializedObject(target); var p=so.FindProperty(field);
            if(p==null) throw new InvalidOperationException(target.GetType().Name+" missing serialized field "+field);
            if(value is Object reference) p.objectReferenceValue=reference;
            else if(value is string text) p.stringValue=text;
            else if(value is float f) p.floatValue=f;
            else if(value is int i) p.intValue=i;
            else if(value is bool b) p.boolValue=b;
            else if(value is Vector3 vector) p.vector3Value=vector;
            else throw new ArgumentException("Unsupported serialized value "+field);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        private static void SetReferences(Object target,string field,Object[] values)
        {
            var so=new SerializedObject(target); var p=so.FindProperty(field); p.arraySize=values.Length;
            for(int i=0;i<values.Length;i++) p.GetArrayElementAtIndex(i).objectReferenceValue=values[i]; so.ApplyModifiedPropertiesWithoutUndo();
        }
        public static void ValidateSavedAssets()
        {
            var report=new List<string> { "Stage authoring validation (not runtime traversal)",DateTime.UtcNow.ToString("O") };
            Scene previous=SceneManager.GetActiveScene();
            for(int i=1;i<=3;i++)
            {
                string path=StageRoot+"/03_"+i+"_Stage.unity";
                Scene scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
                try
                {
                    var roots=scene.GetRootGameObjects();
                    var all=roots.SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
                    int missing=all.Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
                    int pb=all.Count(t=>t.GetComponent<ProBuilderMesh>()!=null);
                    int badMesh=all.Select(t=>t.GetComponent<ProBuilderMesh>()).Where(m=>m!=null).Count(m=>m.vertexCount==0 || m.faceCount==0);
                    var registry=roots.SelectMany(r=>r.GetComponentsInChildren<StageService>()).Single(); registry.RebuildRegistry();
                    if(missing!=0 || pb==0 || badMesh!=0) throw new InvalidOperationException($"Invalid Stage {path}: missing {missing}, PB {pb}, invalid PB {badMesh}");
                    if(all.Any(t=>t.GetComponent<Camera>()!=null || t.GetComponent<PlayerFacade>()!=null || t.GetComponent<AudioListener>()!=null)) throw new InvalidOperationException("Stage contains Core-owned objects.");
                    report.Add($"{path}: ProBuilder={pb}; MissingScript={missing}; InvalidMesh={badMesh}; Initial={registry.InitialCheckpointId}; Saved={!scene.isDirty}");
                }
                finally { EditorSceneManager.CloseScene(scene,true); }
            }
            if(previous.IsValid()) SceneManager.SetActiveScene(previous);
            Directory.CreateDirectory("output/Tech_SYM/level/evidence");
            File.WriteAllLines("output/Tech_SYM/level/evidence/asset-validation.txt",report);
        }
    }
}






