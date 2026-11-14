#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cooked.Contracts;
using Cooked.Level;
using Cooked.Session;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEditor;
namespace Cooked.DebugTesting
{
    // Editor-only integration fixture; remove only when replaced by equivalent Play tests.
    public sealed class CheckpointOilPlayChecks : MonoBehaviour
    {
        [Serializable] public sealed class Report { public int passed,failed; public int startFrame,endFrame; public List<string> cases=new List<string>(); }
        public Report Result=new Report();
        public bool Done;
        private PlayerFacade actor;
        private Rigidbody body;
        private PlayerBridgeService bridge;
        private GameplayControlService control;
        private GameSessionService session;
        private Keyboard keyboard;
        private GameObject player;
        private readonly List<GameObject> owned=new List<GameObject>();
        private sealed class Flow : IGameFlowService
        {
            public FlowSnapshot Snapshot=>new FlowSnapshot(FlowState.Playing,false,1,null);
            public event Action<FlowSnapshot> Changed { add{} remove{} }
            public bool Request(FlowRequest r)=>false;
            public void CancelCurrent(){}
            public void Dispose(){}
        }
        private static void Set(object o,string n,object v)=>o.GetType().GetField(n,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(o,v);
        private void Check(bool ok,string name){if(ok)Result.passed++;else Result.failed++;Result.cases.Add((ok?"P ":"F ")+name);}
        public void Begin()=>StartCoroutine(Guard());
        private IEnumerator Guard()
        {
            Result.startFrame=Time.frameCount;
            var stack=new Stack<IEnumerator>();stack.Push(Run());
            while(stack.Count>0)
            {
                var current=stack.Peek();bool next=false;Exception failure=null;
                try{next=current.MoveNext();}catch(Exception e){failure=e;}
                if(failure!=null){Check(false,failure.ToString());break;}
                if(!next){stack.Pop();continue;}
                if(current.Current is IEnumerator nested){stack.Push(nested);continue;}
                yield return current.Current;
            }
            Result.endFrame=Time.frameCount;Done=true;
            System.IO.Directory.CreateDirectory("output/Tech_SYM/checkpoint-oil-2026-11-06");
            System.IO.File.WriteAllText("output/Tech_SYM/checkpoint-oil-2026-11-06/play-results.json",JsonUtility.ToJson(Result,true));
            Debug.Log("Checkpoint/oil Play checks: "+Result.passed+" passed, "+Result.failed+" failed");
        }
        private IEnumerator Wait(float seconds){float end=Time.time+seconds;while(Time.time<end)yield return null;}
        private void Move(Vector3 center){body.position=center;if(!body.isKinematic)body.linearVelocity=Vector3.zero;Physics.SyncTransforms();}
        private IEnumerator Run()
        {
            keyboard=InputSystem.AddDevice<Keyboard>();
            control=new GameplayControlService();session=new GameSessionService();session.Begin(new CheckpointSnapshot(Vector3.zero,0,AbilityMask.Both));
            player=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Characters/Players/Main/PlayerRoot.prefab"));
            player.SetActive(true);actor=player.GetComponentInChildren<PlayerFacade>();
            yield return null;
            actor.GetComponent<PlayerController>().enabled=false;
            actor.GetComponent<CharacterMovement>().enabled=false;
            body=actor.GetComponent<Rigidbody>();body.isKinematic=true;
            bridge=new PlayerBridgeService(control,session,null);
            Set(bridge,"actor",actor);Set(bridge,"model",actor.Model);
            Move(new Vector3(50,10,50));
            foreach(bool quarter in new[]{false,true})
                foreach(int overlap in new[]{0,1,2})
                {
                    yield return Boundary(quarter,overlap);
                    yield return null;
                }
            yield return Oil();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());
        }
        private IEnumerator Boundary(bool quarter,int overlap)
        {
            Move(new Vector3(50,10,50));
            var owner=new GameObject("Stage fixture");owned.Add(owner);
            var stage=owner.AddComponent<StageService>();
            var roots=new List<GameObject>();
            StageTrigger Point(string name,Vector3 position)
            {
                var root=Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/StageControl/Trigger/CP_Variant.prefab"));
                root.name=name;roots.Add(root);owned.Add(root);
                var p=root.GetComponentInChildren<StageTrigger>();Set(p,"stage",stage);
                p.transform.position=position;p.transform.rotation=Quaternion.identity;p.transform.localScale=Vector3.one;
                p.GetComponent<BoxCollider>().size=new Vector3(2,4,2);
                p.SpawnPoint.transform.position=position;
                return p;
            }
            Vector3 axis=quarter?Vector3.forward:Vector3.right;
            float gap=overlap==0?4:overlap==1?1:0;
            var start=Point("Initial",axis*-10);
            var end=Point("End1",Vector3.zero);
            var next=Point("Start2",axis*gap+Vector3.up*(overlap==2?0:1));
            var final=Point("End2",axis*10);
            var a=new StageService.Section();Set(a,"start",start);Set(a,"end",end);
            var b=new StageService.Section();Set(b,"start",next);Set(b,"end",final);Set(b,"previousEnd",end);Set(b,"view",quarter?CameraMovementMode.Quarter:CameraMovementMode.Side);
            Set(stage,"sections",new[]{a,b});Set(stage,"firstSectionStart",start);
            Set(stage,"actor",actor);Set(stage,"bridge",bridge);Set(stage,"boundRound",session);Set(stage,"bound",true);Set(stage,"flow",new Flow());Set(stage,"control",control);
            stage.BindSectionPresentation((s,restore)=>{});
            stage.ValidateSections();
            int events=0,sounds=0;var order=new List<string>();
            stage.CheckpointCaptured+=p=>{events++;order.Add(p==end?"end":"start");if(stage.ShouldPlayCheckpointSound(p))sounds++;};
            stage.RegisterInitialCheckpoint(session.InitialConfiguration);
            yield return null;
            var firstView=start.GetComponentInParent<CheckpointPresentation>();
            var nextView=next.GetComponentInParent<CheckpointPresentation>();
            Check(firstView.ActivationEffectCount==0&&stage.IsActivated(start)&&firstView.GetComponentInChildren<MeshRenderer>().sharedMaterial.name=="bg_checkpoint","initial silent/red "+quarter+"/"+overlap);
            Check(stage.IsBoundaryAligned(b),"axis alignment allows Y "+quarter+"/"+overlap);
            if(overlap==0){Move(Vector3.zero);yield return Wait(.12f);Move(next.transform.position);}
            else Move((end.transform.position+next.transform.position)*.5f);
            yield return Wait(.2f);
            Check(stage.IsActivated(end)&&stage.IsActivated(next),"physics contact both accepted "+quarter+"/"+overlap);
            Check(stage.CurrentSection==b&&stage.ActiveCheckpoint==next&&stage.Checkpoint.Position==next.SpawnPoint.FeetPose.position,"final save/start "+quarter+"/"+overlap);
            Check(order.SequenceEqual(new[]{"end","start"})&&events==2&&sounds==1,"two captures/one sound policy "+quarter+"/"+overlap);
            Check(nextView.ActivationEffectCount==1,"one checkpoint VFX "+quarter+"/"+overlap);
            Check(!end.GetComponentInParent<CheckpointPresentation>().GetComponentInChildren<MeshRenderer>().enabled,"hidden end renderer "+quarter+"/"+overlap);
            Move(Vector3.zero);yield return Wait(.1f);
            Check(stage.ActiveCheckpoint==next&&events==2,"reverse recontact no regression "+quarter+"/"+overlap);
            stage.Dispose();
            Check(stage.IsActivated(next)&&stage.ResolveRestore(session.InitialConfiguration).Position==next.SpawnPoint.FeetPose.position,"retry ledger preserved "+quarter+"/"+overlap);
            Move(new Vector3(50,10,50));
            foreach(var root in roots)Destroy(root);Destroy(owner);yield return null;
        }
        private IEnumerator Oil()
        {
            var oil=actor.GetComponent<CharacterSlipperyEffect>();var input=actor.GetComponent<PlayerInputReader>();var presenter=actor.GetComponent<OilSplashPresenter>();
            body.isKinematic=false;body.useGravity=false;body.linearDamping=0;body.constraints=RigidbodyConstraints.FreezeRotation;body.interpolation=RigidbodyInterpolation.None;actor.transform.position=Vector3.zero;body.position=Vector3.zero;body.linearVelocity=Vector3.zero;Physics.SyncTransforms();
            var surface=new GameObject("Oil physical trigger");owned.Add(surface);
            var box=surface.AddComponent<BoxCollider>();box.isTrigger=true;box.size=Vector3.one*5;
            var source=surface.AddComponent<OilSurface>();Set(source,"oilTrigger",box);Set(source,"effectDuration",20f);
            Physics.SyncTransforms();yield return Wait(.15f);
            Check(oil.IsActive,"actual OilSurface trigger applies slippery; actor="+actor.transform.position+" velocity="+body.linearVelocity);
            surface.SetActive(false);
            foreach(float interval in new[]{.1f,.3f})
            {
                Set(presenter,"reactivationSeconds",interval);
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.RightArrow));
                body.linearVelocity=Vector3.right;
                yield return Wait(.4f);
                var times=new List<float>();int last=presenter.BurstCount;float until=Time.time+1.1f;
                while(Time.time<until){yield return null;if(presenter.BurstCount!=last){times.Add((float)presenter.LastBurstTime);last=presenter.BurstCount;}}
                Check(times.Count>=2&&times.Zip(times.Skip(1),(x,y)=>y-x).All(d=>d>=interval-.0001f),"oil interval "+interval+" observed="+string.Join(",",times.Zip(times.Skip(1),(x,y)=>(y-x).ToString("F3"))));
                InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;
                int before=presenter.BurstCount;yield return Wait(.4f);Check(presenter.BurstCount>before,"no key inertia continues "+interval);
                body.linearVelocity=Vector3.zero;yield return null;
                int stopped=presenter.BurstCount;yield return Wait(.35f);Check(stopped==presenter.BurstCount,"stationary stops "+interval);
            }
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(Key.LeftArrow,Key.RightArrow));
            yield return null;int opposite=presenter.BurstCount;yield return Wait(.35f);Check(opposite==presenter.BurstCount,"opposite keys at zero velocity do not emit");
            body.linearVelocity=Vector3.right;yield return Wait(.35f);
            input.enabled=false;int disabled=presenter.BurstCount;yield return Wait(.4f);Check(disabled==presenter.BurstCount,"disabled input stops despite velocity");
            input.enabled=true;yield return Wait(.35f);
            actor.SetInputBlocked(true);int blocked=presenter.BurstCount;yield return Wait(.35f);Check(blocked==presenter.BurstCount,"session input restriction stops");actor.SetInputBlocked(false);yield return Wait(.35f);
            int paused=presenter.BurstCount;Time.timeScale=0;yield return new WaitForSecondsRealtime(.25f);Check(paused==presenter.BurstCount,"pause stops");Time.timeScale=1;
            oil.ClearEffect();int expired=presenter.BurstCount;yield return Wait(.4f);Check(expired==presenter.BurstCount,"expired slippery stops");
            oil.Apply(2f);actor.Damage(999);yield return null;int dead=presenter.BurstCount;yield return Wait(.35f);Check(actor.IsDead&&dead==presenter.BurstCount,"dead actor stops");
        }
        private void OnDestroy()
        {
            if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);
            if(player!=null)Destroy(player);
            foreach(var root in owned)if(root!=null)Destroy(root);
            // Fixture bridge uses borrowed state and no subscriptions to the Actor.
            control?.Dispose();session?.Dispose();
        }
    }
}
#endif
