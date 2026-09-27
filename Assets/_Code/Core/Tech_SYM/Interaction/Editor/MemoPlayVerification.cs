using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using FeelsGoodOnion.TechSYM.Interaction;
using FeelsGoodOnion.TechSYM.Testing;

namespace FeelsGoodOnion.TechSYM.EditorTools
{
    /// <summary>현재 Play 세션에서 가상 키보드로 실제 Update 입력 경로를 검증한다.</summary>
    public static class MemoPlayVerification
    {
        private struct Step { public string Name; public Action Run; public double Wait; }
        private static readonly List<Step> steps = new List<Step>();
        private static readonly List<string> results = new List<string>();
        private static readonly List<GameObject> temporary = new List<GameObject>();
        private static int index;
        private static double nextTime;
        private static Keyboard keyboard;
        private static Keyboard previousKeyboard;
        private static Key[] keys = Array.Empty<Key>();
        private static InputSettings.BackgroundBehavior previousBackground;
        private static InputSettings.EditorInputBehaviorInPlayMode previousEditorInput;
        private static bool previousRunInBackground;
        private static CheckInteract check;
        private static MemoUIController controller;
        private static PlayerFacade player;
        private static MemoObject first, second;
        private static Transform world;
        private static GameObject bubble, wall;
        private static InteractionTestTarget generic;
        private static Vector3 playerPosition, firstPosition, secondPosition;
        private static Quaternion playerRotation;
        public static string Status { get; private set; } = "Not started";

        public static string Begin()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Play Mode에서 실행하세요.");
            if (Status == "Running") throw new InvalidOperationException("이미 실행 중입니다.");
            check = UnityEngine.Object.FindFirstObjectByType<CheckInteract>();
            controller = UnityEngine.Object.FindFirstObjectByType<MemoUIController>();
            player = UnityEngine.Object.FindFirstObjectByType<PlayerFacade>();
            var memos = UnityEngine.Object.FindObjectsByType<MemoObject>(FindObjectsSortMode.None).OrderBy(x=>x.Data.MemoID).ToArray();
            first=memos[0]; second=memos[1];
            world=UnityEngine.Object.FindFirstObjectByType<InteractionPromptView>().transform;
            bubble=world.GetChild(0).gameObject;
            playerPosition=player.transform.position;
            playerRotation=player.transform.rotation;
            firstPosition=first.transform.position;
            secondPosition=second.transform.position;
            Align(first);
            previousKeyboard=Keyboard.current;
            previousBackground=InputSystem.settings.backgroundBehavior;
            previousRunInBackground=Application.runInBackground;
            previousEditorInput=InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            Application.runInBackground=true;
            keyboard=InputSystem.AddDevice<Keyboard>("MemoVerificationKeyboard");
            InputSystem.onBeforeUpdate+=FeedInput;
            results.Clear(); steps.Clear(); temporary.Clear(); index=0;
            Add("initial root collider / shared prompt",()=> {
                Expect(check.CurrentTarget==first.GetComponent<InteractionPromptAnchor>(),"initial target");
                Expect(bubble.activeSelf,"prompt visible");
                Expect(UnityEngine.Object.FindObjectsByType<InteractionPromptView>(FindObjectsSortMode.None).Length==1,"one world prompt");
                Keys(Key.E);
            });
            Add("E opens one memo",()=> {
                Expect(controller.IsOpen && Views()==1,"one view");
                Expect(!bubble.activeSelf,"prompt hidden on interaction");
            },.55);
            Add("held E does not close/reopen",()=> {
                Expect(controller.IsOpen && Views()==1,"held key is single-shot");
                Expect(player.CanReceiveInput,"player input remains enabled");
                Keys();
                player.transform.position += Vector3.right*4;
                Physics.SyncTransforms();
            });
            Add("close without current ray target",()=>Keys(Key.E));
            Add("close destroys view",()=> {Expect(!controller.IsOpen && Views()==0,"view destroyed"); Keys(); player.transform.position=playerPosition; Align(first);});
            Add("repeat interaction",()=>Keys(Key.E));
            Add("repeat open creates fresh view",()=> {Expect(controller.IsOpen && Views()==1,"repeat open"); Keys();});
            Add("close second open",()=>Keys(Key.E));
            Add("second memo target",()=> {
                Expect(!controller.IsOpen,"closed"); Keys();
                first.gameObject.SetActive(false);
                Align(second);
            });
            Add("second memo opens",()=> {Expect(check.CurrentTarget==second.GetComponent<InteractionPromptAnchor>(),"target switch"); Keys(Key.E);});
            Add("second memo has its own image",()=> {
                var view=UnityEngine.Object.FindFirstObjectByType<MemoView>();
                Expect(view.GetComponent<UnityEngine.UI.Image>().sprite==second.Data.MemoImage,"per-ID sprite");
                Keys();
            });
            Add("close second memo",()=>Keys(Key.E));
            Add("generic target and player self collider",()=> {
                Keys(); second.gameObject.SetActive(false);
                var root=Temp("GenericInteractable"); root.transform.position=player.transform.position+player.transform.forward*1.8f;
                root.AddComponent<InteractionPromptAnchor>(); generic=root.AddComponent<InteractionTestTarget>();
                var child=GameObject.CreatePrimitive(PrimitiveType.Cube); temporary.Add(child); child.transform.SetParent(root.transform,false); child.transform.localScale=Vector3.one*.6f;
                var self=Temp("SelfCollider"); self.transform.SetParent(player.transform,false); self.transform.localPosition=Vector3.forward*.6f;
                self.AddComponent<BoxCollider>().size=Vector3.one*.2f;
                Physics.SyncTransforms();
            });
            Add("generic reuse and self exclusion",()=> {
                Expect(check.CurrentTarget==generic.GetComponent<InteractionPromptAnchor>(),"generic root found beyond self");
                Keys(Key.E);
            },.5);
            Add("generic held input exactly once",()=> {Expect(generic.InvocationCount==1,"one invocation"); Keys();});
            Add("wall blocks first hit",()=> {
                wall=GameObject.CreatePrimitive(PrimitiveType.Cube); temporary.Add(wall);
                wall.transform.position=player.transform.position+player.transform.forward*.9f;
                wall.transform.localScale=Vector3.one*.4f; Physics.SyncTransforms();
            });
            Add("wall hides prompt and blocks E",()=> {Expect(check.CurrentTarget==null && !bubble.activeSelf,"wall blocks"); Keys(Key.E);});
            Add("wall collision layer can be excluded",()=> {
                Expect(generic.InvocationCount==1,"no through-wall invocation");
                Keys(); wall.layer=2; Physics.SyncTransforms();
            });
            Add("LayerMask respected",()=> {
                Expect(check.CurrentTarget==generic.GetComponent<InteractionPromptAnchor>(),"ignore-raycast layer");
                wall.SetActive(false);
                generic.transform.position=player.transform.position+player.transform.forward*10;
                Physics.SyncTransforms();
            });
            Add("finite range hides prompt",()=> {
                Expect(check.CurrentTarget==null && !bubble.activeSelf,"finite range");
                player.transform.rotation=Quaternion.Euler(0,90,0);
                generic.transform.position=player.transform.position+player.transform.forward*1.8f;
                generic.transform.rotation=Quaternion.Euler(0,0,35);
                Physics.SyncTransforms();
            });
            Add("forward / local offset / billboard",()=> {
                Expect(check.CurrentTarget==generic.GetComponent<InteractionPromptAnchor>(),"player forward");
                Expect(Vector3.Distance(world.position,generic.GetComponent<InteractionPromptAnchor>().WorldPosition)<.01f,"local offset");
                var camera=new SerializedObject(check).FindProperty("viewCamera").objectReferenceValue as Camera;
                Expect(Quaternion.Angle(world.rotation,camera.transform.rotation)<.01f,"billboard");
                generic.gameObject.SetActive(false);
            });
            Add("disabled target hides reusable prompt",()=> {
                Expect(!bubble.activeSelf && world!=null,"disable hides not destroys");
                first.gameObject.SetActive(true); Align(first);
            });
            Add("open for forced cleanup",()=>Keys(Key.E));
            Add("controller disable cleans up",()=> {Expect(controller.IsOpen,"open before disable"); Keys(); controller.enabled=false;});
            Add("no view after forced cleanup",()=> {
                Expect(!controller.IsOpen && Views()==0,"forced cleanup");
                controller.enabled=true;
                VerifyBinding();
                VerifyDuplicateOwner();
            });
            Add("CheckInteract re-enable keeps one binding",()=> {check.enabled=false; check.enabled=true; Keys(Key.E);});
            Add("re-enabled input opens once",()=> {Expect(controller.IsOpen && Views()==1,"rebind one command"); Keys();});
            Add("request final close",()=>Keys(Key.E));
            Add("final destroyed view",()=> {Expect(!controller.IsOpen && Views()==0,"final closed"); Keys();});
            nextTime=Time.timeAsDouble+.5;
            Status="Running";
            EditorApplication.update+=Tick;
            return "Started "+steps.Count+" Play steps";
        }

        private static void VerifyBinding()
        {
            var prefab=(MemoView)new SerializedObject(controller).FindProperty("viewPrefab").objectReferenceValue;
            var view=UnityEngine.Object.Instantiate(prefab);
            temporary.Add(view.gameObject);
            var oldModel=new MemoViewModel(first.Data);
            var newModel=new MemoViewModel(second.Data);
            int unrelated=0;
            oldModel.StateChanged += _=>unrelated++;
            view.Bind(oldModel); view.Bind(newModel);
            oldModel.RequestClose();
            Expect(unrelated==1,"unrelated subscriber survives rebind");
            Expect(newModel.State==MemoDisplayState.Opening,"old binding cannot close new VM");
            view.Unbind();
            newModel.RequestClose();
            Expect(newModel.State==MemoDisplayState.Closing,"unbound VM stays independent");
            UnityEngine.Object.DestroyImmediate(view.gameObject);
        }

        private static void VerifyDuplicateOwner()
        {
            var duplicate=Temp("DuplicateOwner");
            duplicate.SetActive(false);
            var owner=duplicate.AddComponent<MemoObject>();
            MemoSceneSetup.Set(owner,"memoModel",first.Data);
            bool rejected=false;
            try { MemoValidation.Validate(); } catch(InvalidOperationException) {rejected=true;}
            Expect(rejected,"inactive duplicate model owner rejected");
            UnityEngine.Object.DestroyImmediate(duplicate);
            MemoValidation.Validate();
        }

        private static void Align(MemoObject memo)
        {
            memo.transform.position=player.transform.position+player.transform.forward*1.8f;
            Physics.SyncTransforms();
        }
        private static GameObject Temp(string name) {var g=new GameObject(name); temporary.Add(g); return g;}
        private static int Views()=>UnityEngine.Object.FindObjectsByType<MemoView>(FindObjectsSortMode.None).Length;
        private static void Keys(params Key[] value)=>keys=value;
        private static void FeedInput()
        {
            if(InputState.currentUpdateType!=InputUpdateType.Dynamic || keyboard==null) return;
            keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
        }
        private static void Add(string name,Action action,double wait=.3)=>steps.Add(new Step{Name=name,Run=action,Wait=wait});
        private static void Expect(bool valid,string message) {if(!valid) throw new InvalidOperationException(message);}
        private static void Tick()
        {
            if(!Application.isPlaying) {Finish("FAIL: Play stopped during verification");return;}
            if(Time.timeAsDouble<nextTime) return;
            try
            {
                var step=steps[index];
                step.Run();
                results.Add("PASS "+step.Name);
                nextTime=Time.timeAsDouble+step.Wait;
                index++;
                if(index==steps.Count) Finish("PASS");
            }
            catch(Exception ex) {Finish("FAIL "+steps[index].Name+": "+ex.Message);}
        }
        private static void Finish(string outcome)
        {
            EditorApplication.update-=Tick;
            InputSystem.onBeforeUpdate-=FeedInput;
            if(keyboard!=null) InputSystem.RemoveDevice(keyboard);
            keyboard=null;
            if(previousKeyboard!=null && previousKeyboard.added) previousKeyboard.MakeCurrent();
            InputSystem.settings.editorInputBehaviorInPlayMode=previousEditorInput;
            InputSystem.settings.backgroundBehavior=previousBackground;
            Application.runInBackground=previousRunInBackground;
            if(controller!=null) {controller.enabled=false;controller.enabled=true;}
            foreach(var g in temporary) if(g!=null) UnityEngine.Object.DestroyImmediate(g);
            if(player!=null) {player.transform.position=playerPosition;player.transform.rotation=playerRotation;}
            if(first!=null) {first.transform.position=firstPosition;first.gameObject.SetActive(true);}
            if(second!=null) {second.transform.position=secondPosition;second.gameObject.SetActive(true);}
            Physics.SyncTransforms();
            Status=outcome+" ("+results.Count+"/"+steps.Count+")";
            Directory.CreateDirectory("output/Tech_SYM/Memo");
            File.WriteAllText("output/Tech_SYM/Memo/PlayVerification.txt",Status+"\n"+string.Join("\n",results));
        }
    }
}
