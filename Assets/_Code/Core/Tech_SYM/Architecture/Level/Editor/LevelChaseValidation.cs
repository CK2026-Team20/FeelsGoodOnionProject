using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cooked.Contracts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Cooked.Level.Editor
{
    /// <summary>Real Unity component/Collider contract tests. Positions are arranged fixtures, not traversal proof.</summary>
    public static class LevelChaseValidation
    {
        public static void PatchAndValidate()
        {
            LevelAssetBuilder.UpdateChaseAssets();
            LevelCheckpointValidation.ValidateSavedPoses();
            var root=UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects().Single(g=>g.GetComponent<StageService>()!=null);
            var stage=root.GetComponent<StageService>();
            var trigger=root.GetComponentsInChildren<StageTrigger>().Single(t=>t.Kind==StageTriggerKind.Chase);
            var gate=root.GetComponentInChildren<ChaseRetreatGate>(true);
            var actorObject=new GameObject("ValidationActor"); actorObject.SetActive(false);
            var capsule=actorObject.AddComponent<CapsuleCollider>(); capsule.height=2; capsule.radius=.5f; capsule.center=Vector3.zero;
            var visual=new GameObject("VisualRoot"); visual.transform.SetParent(actorObject.transform,false);
            new GameObject("NormalVisual").transform.SetParent(visual.transform,false);
            new GameObject("SmallVisual").transform.SetParent(visual.transform,false);
            var actor=actorObject.AddComponent<PlayerFacade>();
            var session=new TestSession(); session.Begin(new CheckpointSnapshot("03_3_Stage","S3_ENTRY",0));
            var bus=new TestEventBus(session);
            var results=new List<string>();
            int attempts=0, notifications=0; bool accept=false;
            try
            {
                actorObject.SetActive(true); actor.transform.position=new Vector3(83.99f,1.03f,0); Physics.SyncTransforms();
                stage.Initialize(bus,new TestFlowService(),actor,null,session);
                stage.BindChaseStart(s=>{ attempts++; Require(session.Checkpoint.CheckpointId=="S3_CHASE","checkpoint precedes Start",results); Require(!s.TryDispatch(trigger,actor),"reentrant Start dispatch is rejected",results); return accept; });
                stage.ChaseStarted+=s=>notifications++;
                Require(!stage.TryDispatch(trigger,actor) && attempts==0,"ActorBody X83.99 never starts even if collider overlaps",results);
                actor.transform.position=new Vector3(84,1.03f,0); Physics.SyncTransforms();
                Require(gate.CanClose(actor),"normal capsule at X84 has closure clearance",results);
                Require(!stage.TryDispatch(trigger,actor) && attempts==1 && !gate.IsClosed && notifications==0,"rejected Start leaves gate open and no accepted event",results);
                accept=true;
                Require(stage.TryDispatch(trigger,actor) && attempts==2 && gate.IsClosed && notifications==1,"accepted retry closes gate and publishes once",results);
                Require(stage.TryDispatch(trigger,actor) && attempts==2 && notifications==1,"duplicate dispatch never restarts chase",results);
                Require(Mathf.Abs(gate.ClosureBounds.min.x-82.1f)<.001f && Mathf.Abs(gate.ClosureBounds.max.x-82.7f)<.001f,"gate spans X82.1..82.7",results);
                Require(capsule.bounds.min.x-gate.ClosureBounds.max.x>.79f,"normal capsule has 0.8m separation at start",results);
                capsule.height=1; capsule.radius=.25f; capsule.center=Vector3.zero; Physics.SyncTransforms();
                Require(gate.CanClose(actor),"small capsule at X84 has closure clearance",results);
                actor.transform.position=new Vector3(82.4f,1.03f,0); Physics.SyncTransforms();
                Require(!gate.CanClose(actor),"overlapping actor rejects closure preflight",results);
                Directory.CreateDirectory("output/Tech_SYM/level/evidence");
                File.WriteAllLines("output/Tech_SYM/level/evidence/chase-connection-validation.txt",results);
            }
            finally
            {
                stage.Dispose(); Object.DestroyImmediate(actorObject);
                // Discard fixture poses, accepted flags, and the closed gate: saved production scene starts OPEN.
                EditorSceneManager.OpenScene(LevelAssetBuilder.StageRoot+"/03_3_Stage.unity",OpenSceneMode.Single);
            }
            Debug.Log("[Cooked.Level] Chase connection checks passed. Gameplay traversal remains unverified.");
        }
        private static void Require(bool condition,string title,List<string> results)
        { if(!condition) throw new InvalidOperationException(title); results.Add("PASS: "+title); }
        private sealed class TestEventBus : IGameEventBus
        {
            private readonly TestSession session;
            public TestEventBus(TestSession value)=>session=value;
            public IDisposable Subscribe<T>(Action<T> handler) where T:struct,IGameEvent => throw new NotSupportedException();
            public void Publish<T>(T message) where T:struct,IGameEvent
            { if(message is CheckpointReachedEvent cp) session.CaptureCheckpoint(cp.StageId,cp.CheckpointId,0); }
            public void Dispose() { }
        }
        private sealed class TestFlowService : IGameFlowService
        {
            public FlowSnapshot Snapshot=>new FlowSnapshot(FlowState.Playing,false,1,null);
            public event Action<FlowSnapshot> Changed { add { } remove { } }
            public bool Request(FlowRequest request)=>false;
            public void CancelCurrent() { }
            public void Dispose() { }
        }
        private sealed class TestSession : IGameSessionService
        {
            public bool IsActive { get; private set; }
            public CheckpointSnapshot Checkpoint { get; private set; }
            public event Action Changed { add { } remove { } }
            public void Begin(CheckpointSnapshot point) { Checkpoint=point; IsActive=true; }
            public void CaptureCheckpoint(string stageId,string checkpointId,int fragments)=>Checkpoint=new CheckpointSnapshot(stageId,checkpointId,fragments);
            public bool IsUnlocked(AbilityId ability)=>true;
            public void Unlock(AbilityId ability) { }
            public void End()=>IsActive=false;
            public void Dispose() { }
        }
    }
}

