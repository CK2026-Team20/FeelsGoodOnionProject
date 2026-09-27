using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using FeelsGoodOnion.TechSYM.Interaction;
using FeelsGoodOnion.TechSYM.OvenTray;
using FeelsGoodOnion.TechSYM.Platforms;
using FeelsGoodOnion.TechSYM.Testing;
namespace FeelsGoodOnion.TechSYM.EditorTools
{
    /// <summary>가상 키보드와 실제 물리 스텝으로 검사한다. 실행 중 씬 저장은 하지 않는다.</summary>
    public static class OvenTrayVerification
    {
        private struct Step { public string Name; public Action Run; public double Wait; }
        private static readonly List<Step> steps=new List<Step>();
        private static readonly List<string> results=new List<string>();
        private static readonly List<GameObject> temporary=new List<GameObject>();
        private static int index;
        private static double nextTime;
        private static Keyboard keyboard,previousKeyboard;
        private static Key[] keys=Array.Empty<Key>();
        private static InputSettings.BackgroundBehavior previousBackground;
        private static InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;
        private static bool previousRun;
        private static PlayerFacade player;
        private static CheckInteract check;
        private static OvenTrayObject[] trays;
        private static OvenTrayObject isolated;
        private static KinematicPlatformMotion motor;
        private static Vector3 playerStart;
        private static Quaternion playerRotation;
        private static Vector3 pausedPosition,rideStart,platformStart,jumpStart;
        private static bool sample;
        private static readonly List<float> speeds=new List<float>();
        private static GameObject wall,target;
        private static InteractionPromptAnchor targetAnchor;
        private static KinematicPlatformMotion shuttle,floatMotion,linked;
        private static Vector3 shuttleStart,floatStart,linkedStart;
        private static float floatTravel;
        private static PressurePlatePlatform plate;
        public static string Status { get; private set; }="Not started";
        public static string Begin()
        {
            if(!Application.isPlaying || Status=="Running") throw new InvalidOperationException("Play 상태/중복 실행 확인");
            player=UnityEngine.Object.FindFirstObjectByType<PlayerFacade>();
            check=UnityEngine.Object.FindFirstObjectByType<CheckInteract>();
            trays=UnityEngine.Object.FindObjectsByType<OvenTrayObject>(FindObjectsSortMode.None).OrderBy(t=>t.name).ToArray();
            playerStart=player.transform.position;playerRotation=player.transform.rotation;
            previousKeyboard=Keyboard.current;previousBackground=InputSystem.settings.backgroundBehavior;previousRun=Application.runInBackground;
            previousEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;Application.runInBackground=true;
            keyboard=InputSystem.AddDevice<Keyboard>("OvenVerificationKeyboard");
            InputSystem.onBeforeUpdate+=Feed;
            steps.Clear();results.Clear();temporary.Clear();speeds.Clear();index=0;
            var oven=trays[0].transform.parent;
            Teleport(oven.TransformPoint(new Vector3(0,.5f,-1.15f)));player.GetComponent<Rigidbody>().rotation=oven.rotation;player.transform.rotation=oven.rotation;
            Add("closed tray prompt selected",()=>{Expect(check.CurrentTarget==trays[0].GetComponent<InteractionPromptAnchor>(),"lowest target: actual="+check.CurrentTarget+" position="+player.transform.position+" forward="+player.transform.forward); Keys(Key.E);},.15);
            Add("moving tray rejects interaction",()=> {
                Expect(trays[0].State==OvenTrayState.Opening,"opening");
                Expect(!trays[0].Interact(),"reject moving input");
                Expect(!trays[0].GetComponent<InteractionPromptAnchor>().enabled,"anchor hidden");
                motor=trays[0].GetComponent<KinematicPlatformMotion>();sample=true;
            },.9);
            Add("held E opens only one tray / OutSine slows",()=>{
                sample=false;
                Expect(trays[0].State==OvenTrayState.Open,"held does not close");
                Expect(trays[1].State==OvenTrayState.Closed,"held does not open another");
                Expect(speeds.Count>2 && speeds[0]>speeds[speeds.Count-1],"OutSine deceleration");
                Expect(Vector3.Distance(trays[0].transform.localPosition,trays[0].OpenLocalPosition)<.002f,"open endpoint");
                Keys();
                Teleport(oven.TransformPoint(new Vector3(0,1f,-1.15f)));
            });
            Add("next independent tray",()=>{Expect(check.CurrentTarget==trays[1].GetComponent<InteractionPromptAnchor>(),"middle target");Keys(Key.E);},1);
            Add("middle opened",()=>{Expect(trays[1].State==OvenTrayState.Open,"middle");Keys();Teleport(oven.TransformPoint(new Vector3(0,1.5f,-1.15f)));});
            Add("top independent tray",()=>{Expect(check.CurrentTarget==trays[2].GetComponent<InteractionPromptAnchor>(),"top target");Keys(Key.E);},1);
            Add("three stair endpoints",()=>{
                Expect(trays.All(t=>t.State==OvenTrayState.Open),"all open");
                Expect(trays.All(t=>Vector3.Distance(t.transform.localPosition,t.OpenLocalPosition)<.002f),"all endpoints");
                Keys();
                foreach(var t in trays) Expect(t.Interact(),"close each");
            },1);
            Add("closed without accumulated offset",()=>{
                Expect(trays.All(t=>t.State==OvenTrayState.Closed),"all closed");
                Expect(trays.All(t=>Vector3.Distance(t.transform.localPosition,t.ClosedLocalPosition)<.002f),"closed origins");
                trays[0].Interact();
            },.25);
            Add("component pause",()=>{
                trays[0].enabled=false;pausedPosition=trays[0].GetComponent<KinematicPlatformMotion>().Position;
            },.4);
            Add("pause keeps position and no callback",()=>{
                Expect(Vector3.Distance(pausedPosition,trays[0].GetComponent<KinematicPlatformMotion>().Position)<.002f,"no hidden movement");
                Expect(trays[0].State==OvenTrayState.Opening,"no canceled completion");
                trays[0].enabled=true;
            },.9);
            Add("resume completes original target",()=> {
                Expect(trays[0].State==OvenTrayState.Open,"resumed open");
                Expect(Vector3.Distance(trays[0].transform.localPosition,trays[0].OpenLocalPosition)<.002f,"resumed endpoint");
                trays[0].Interact();
            },1);
            Add("rotated parent fixture",()=>{
                var parent=Temp("RotatedOvenTest");parent.transform.SetPositionAndRotation(new Vector3(-5,1,0),Quaternion.Euler(0,90,0));
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(OvenTrayAuthoring.Folder+"/OvenTray.prefab");
                var tray=UnityEngine.Object.Instantiate(prefab,parent.transform.position,parent.transform.rotation,parent.transform);
                isolated=tray.GetComponent<OvenTrayObject>();motor=isolated.GetComponent<KinematicPlatformMotion>();
                Teleport(parent.transform.position+Vector3.up*.575f);
            },.6);
            Add("stationary tray supports player",()=>{
                Expect(Mathf.Abs(player.transform.position.y-(isolated.transform.position.y+.575f))<.12f,"standing height");
                rideStart=player.transform.position;platformStart=isolated.transform.position;
                Expect(isolated.Interact(),"start carry");
            },1);
            Add("rotated local offset carries player",()=>{
                Expect(isolated.State==OvenTrayState.Open,"rotated open");
                Expect(Vector3.Distance(isolated.transform.localPosition,isolated.OpenLocalPosition)<.005f,"rotated local endpoint");
                Vector3 expected=isolated.transform.position-platformStart;
                Vector3 actual=player.transform.position-rideStart;
                Expect(Mathf.Abs(actual.x-expected.x)<.3f && Mathf.Abs(actual.y)<.2f,"passenger carried");
                Expect(motor.Speed<.001f,"zero speed at endpoint");
                isolated.Interact();
            },1);
            Add("return carry",()=>{
                Expect(isolated.State==OvenTrayState.Closed,"return closed");
                Expect(Mathf.Abs(player.transform.position.x-rideStart.x)<.35f,"passenger returned");
                jumpStart=player.transform.position;Keys(Key.Space);
            },.25);
            Add("jump leaves platform",()=>{
                Expect(player.transform.position.y>jumpStart.y+.2f,"jump");
                Keys();Teleport(new Vector3(-5,.5f,-2));isolated.Interact();
            },1);
            Add("detached player is not carried",()=>{
                Expect(Mathf.Abs(player.transform.position.x+5)<.15f,"no stale passenger");
                motor.enabled=false;motor.enabled=true;
                isolated.Interact();
            },1);
            Add("motion re-enable / probe setup",()=>{
                Expect(isolated.State==OvenTrayState.Closed,"motion still usable");
                isolated.transform.parent.gameObject.SetActive(false);
                Teleport(new Vector3(-5,.5f,-1));
                player.GetComponent<Rigidbody>().rotation=Quaternion.identity;player.transform.rotation=Quaternion.identity;
                target=GameObject.CreatePrimitive(PrimitiveType.Cube);temporary.Add(target);
                target.transform.position=player.transform.position+new Vector3(0,1,1.4f);
                target.transform.localScale=Vector3.one*.2f;
                targetAnchor=target.AddComponent<InteractionPromptAnchor>();target.AddComponent<InteractionTestTarget>();
                Physics.SyncTransforms();
            });
            Add("higher front handle selectable",()=>{
                Expect(check.CurrentTarget==targetAnchor,"above");
                target.transform.position=player.transform.position+new Vector3(0,-.2f,1.4f);Physics.SyncTransforms();
            });
            Add("lower front handle selectable",()=>{
                Expect(check.CurrentTarget==targetAnchor,"below");
                target.transform.position=player.transform.position+Vector3.back;Physics.SyncTransforms();
            });
            Add("behind player rejected",()=>{
                Expect(check.CurrentTarget!=targetAnchor,"behind");
                target.transform.position=player.transform.position+Vector3.forward*3;Physics.SyncTransforms();
            });
            Add("outside distance rejected",()=>{
                Expect(check.CurrentTarget!=targetAnchor,"distance");
                target.transform.position=player.transform.position+Vector3.forward*1.4f;
                wall=GameObject.CreatePrimitive(PrimitiveType.Cube);temporary.Add(wall);
                wall.transform.position=player.transform.position+Vector3.forward*.7f;
                wall.transform.localScale=new Vector3(.7f,2,.2f);Physics.SyncTransforms();
            });
            Add("occluded handle rejected",()=>{
                Expect(check.CurrentTarget!=targetAnchor,"wall");
                wall.SetActive(false);Physics.SyncTransforms();
            });
            Add("visible again and one command across child colliders",()=>{
                Expect(check.CurrentTarget==targetAnchor,"visible");
                var child=new GameObject("ExtraCollider");child.transform.SetParent(target.transform,false);child.AddComponent<BoxCollider>();
                Keys(Key.E);
            },.5);
            Add("generic E remains single-shot",()=>{
                Expect(target.GetComponent<InteractionTestTarget>().InvocationCount==1,"one command");
                Keys();target.SetActive(false);
                shuttle=UnityEngine.Object.FindFirstObjectByType<LinearShuttlePlatform>().GetComponent<KinematicPlatformMotion>();
                floatMotion=UnityEngine.Object.FindObjectsByType<FloatingMotion>(FindObjectsSortMode.None).Select(f=>f.GetComponent<KinematicPlatformMotion>()).First(m=>m!=null);
                linked=UnityEngine.Object.FindFirstObjectByType<PressureLinkedPlatform>().GetComponent<KinematicPlatformMotion>();
                plate=UnityEngine.Object.FindFirstObjectByType<PressurePlatePlatform>();
                shuttleStart=shuttle.Position;floatStart=floatMotion.Position;linkedStart=linked.Position;
                floatTravel=0f;
                var heavy=Temp("PressureTestOccupant");heavy.transform.position=plate.transform.position+Vector3.up*1.5f;
                heavy.AddComponent<FeelsGoodOnion.TechSYM.Features.HeavyState>();
                heavy.AddComponent<BoxCollider>().size=Vector3.one*.4f;
                heavy.AddComponent<Rigidbody>().constraints=RigidbodyConstraints.FreezeRotation;
            },1.6);
            Add("existing shuttle float pressure regression",()=>{
                Expect(Vector3.Distance(shuttleStart,shuttle.Position)>.05f,"shuttle moving");
                Expect(floatTravel>.005f,"float moving");
                Expect(plate.IsPressed && Vector3.Distance(linkedStart,linked.Position)>.01f,"pressure linked moving");
                foreach(var g in temporary.Where(g=>g!=null&&g.name=="PressureTestOccupant")) g.SetActive(false);
            },1.2);
            Add("pressure releases",()=>Expect(!plate.IsPressed,"plate released"));
            nextTime=Time.timeAsDouble+.7;Status="Running";EditorApplication.update+=Tick;
            return "Started "+steps.Count+" steps";
        }
        private static void Teleport(Vector3 position)
        {
            player.ClearInput();
            var body=player.GetComponent<Rigidbody>();body.position=position;body.linearVelocity=Vector3.zero;
            player.transform.position=position;Physics.SyncTransforms();
        }
        private static GameObject Temp(string name){var g=new GameObject(name);temporary.Add(g);return g;}
        private static void Keys(params Key[] input)=>keys=input;
        private static void Feed()
        {
            if(InputState.currentUpdateType!=InputUpdateType.Dynamic || keyboard==null)return;
            keyboard.MakeCurrent();InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
        }
        private static void Add(string name,Action action,double wait=.3)=>steps.Add(new Step{Name=name,Run=action,Wait=wait});
        private static void Expect(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
        private static void Tick()
        {
            if(!Application.isPlaying){Finish("FAIL Play stopped");return;}
            if(sample && motor!=null && motor.Speed>.001f)speeds.Add(motor.Speed);
            if(floatMotion!=null)floatTravel=Mathf.Max(floatTravel,Vector3.Distance(floatStart,floatMotion.Position));
            if(Time.timeAsDouble<nextTime)return;
            try{
                var step=steps[index];step.Run();results.Add("PASS "+step.Name);
                nextTime=Time.timeAsDouble+step.Wait;
                if(++index==steps.Count)Finish("PASS");
            }catch(Exception e){Finish("FAIL "+steps[index].Name+": "+e.Message);}
        }
        private static void Finish(string outcome)
        {
            EditorApplication.update-=Tick;InputSystem.onBeforeUpdate-=Feed;sample=false;keys=Array.Empty<Key>();
            if(keyboard!=null)InputSystem.RemoveDevice(keyboard);keyboard=null;
            if(previousKeyboard!=null&&previousKeyboard.added)previousKeyboard.MakeCurrent();
            InputSystem.settings.editorInputBehaviorInPlayMode=previousEditorInput;
            InputSystem.settings.backgroundBehavior=previousBackground;Application.runInBackground=previousRun;
            foreach(var g in temporary)if(g!=null)UnityEngine.Object.DestroyImmediate(g);
            if(player!=null){Teleport(playerStart);player.transform.rotation=playerRotation;}
            Status=outcome+" ("+results.Count+"/"+steps.Count+")";
            Directory.CreateDirectory("output/Tech_SYM/OvenTray");
            File.WriteAllText("output/Tech_SYM/OvenTray/PlayVerification.txt",Status+"\n"+string.Join("\n",results));
        }
    }
}
