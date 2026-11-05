using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cooked.Contracts;
using Cooked.DebugTesting;
using Cooked.Level;
using Cooked.Session;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Cooked.Validation
{
    public static class DebugPanelContextChecks
    {
        [Serializable] public sealed class Report { public int passed,failed;public List<string> cases=new List<string>(); }
        private sealed class Context : IDebugPanelContext
        {
            public PlayerBridgeService Player { get; set; }
            public GameSessionService Session { get; set; }
            public IGameplayControlService Control { get; set; }
            public IGameFlowService Flow { get; set; }
            public Scene WorldScene { get; set; }
            public Transform StartPoint { get; set; }
            public IEnumerable<Transform> Points => new[]{StartPoint};
            public bool PointsUseFeet => true;
            public bool IsReady { get; set; }=true;
            public bool CanSelectCamera => false;
            public CameraMovementMode CurrentView => CameraMovementMode.Side;
            public bool SelectCamera(CameraMovementMode mode)=>false;
            public int respawns;
            public bool Respawn(){respawns++;return true;}
            public void RefreshBinding(){}
            public event Action ActorReady;
            public event Action ActorRemoving;
            public void Ready()=>ActorReady?.Invoke();
            public void Remove()=>ActorRemoving?.Invoke();
        }
        private static void Set(object target,string name,object value)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value);
        public static Report Run()
        {
            var r=new Report();void Check(bool ok,string name){if(ok)r.passed++;else r.failed++;r.cases.Add((ok?"P ":"F ")+name);}
            var scene=EditorSceneManager.NewPreviewScene();
            GameObject Make(string name){var go=new GameObject(name);SceneManager.MoveGameObjectToScene(go,scene);return go;}
            var control=new GameplayControlService();var session=new GameSessionService();
            session.Begin(new CheckpointSnapshot(Vector3.zero,0,AbilityMask.None));
            var bridgeControl=new GameplayControlService();
            var player=new PlayerBridgeService(bridgeControl,session,null);var flow=new DirectSceneTestFlow(()=>{});
            var logs=new DebugLogBuffer(20);DebugPanelViewModel vm=null;
            try
            {
                var actorNode=Make("Existing actor");actorNode.SetActive(false);
                var actor=actorNode.AddComponent<PlayerFacade>();var state=new PlayerModel(1,3,0,5);
                Set(actor,"model",state);Set(player,"actor",actor);Set(player,"model",state);
                var body=actorNode.GetComponent<Rigidbody>() ?? actorNode.AddComponent<Rigidbody>();body.isKinematic=true;
                var shape=actorNode.GetComponent<CapsuleCollider>() ?? actorNode.AddComponent<CapsuleCollider>();shape.height=2;shape.radius=.4f;
                actorNode.SetActive(true);
                var point=Make("Authored checkpoint").transform;point.position=new Vector3(3,2,4);
                var context=new Context {Player=player,Session=session,Control=control,Flow=flow,WorldScene=scene,StartPoint=point};
                vm=new DebugPanelViewModel(context,logs);
                int roots=scene.GetRootGameObjects().Length;
                vm.ToggleVisible();Check(vm.Visible&&!control.State.WorldPaused&&!control.State.GameplayBlocked,"showing panel keeps gameplay running");
                Check(vm.Heal()&&state.CurrentHP==3,"heal uses existing player");
                Check(vm.FillFragments()&&state.CurrentSkillFragment==5,"fragments use existing player");
                Check(vm.UnlockAll()&&session.UnlockedAbilities==AbilityMask.Both,"unlock uses existing session");
                Check(vm.MoveToStart()&&Vector3.Distance(SpawnPlacement.Feet(shape),point.position)<.001f,"checkpoint teleport uses foot position");
                Check(vm.Respawn()&&context.respawns==1,"respawn delegates once to context");
                Check(scene.GetRootGameObjects().Length==roots,"commands create no extra session or actor");
                using(var otherPause=control.PauseWorld("Other owner"))
                {vm.TogglePause();vm.TogglePause();Check(control.State.WorldPaused,"debug resume preserves other pause owner");}
                vm.TogglePause();context.Remove();Check(!control.State.WorldPaused,"actor removal releases own pause");
                vm.ToggleInvincible();Check(vm.DebugInvincible,"invincibility enabled");context.Remove();
                using(var so=new SerializedObject(actor))Check(!so.FindProperty("invincible").boolValue,"actor removal restores invincibility");
                context.IsReady=false;Check(!vm.Heal()&&!vm.Respawn(),"loading context rejects gameplay commands");
                var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/_Art/UI/Fonts/IM_Hyemin-Bold SDF.asset");
                int fonts=Resources.FindObjectsOfTypeAll<TMP_FontAsset>().Length;
                var format=new DebugLogText(font);var text=format.ForDisplay("로그 ABC\n"+char.ConvertFromUtf32(0x10FFFF));
                Check(text.Contains("ABC")&&text.Contains("[U+10FFFF]"),"unsupported log glyph escaped without recursive TMP warning");
                var ui=Make("Panel");var view=ui.AddComponent<DebugPanelView>();view.Bind(vm,font,format);
                Check(view.GetComponentsInChildren<TMP_Text>(true).All(t=>t.font==font),"all panel and log text reuse main-game font");
                Check(Resources.FindObjectsOfTypeAll<TMP_FontAsset>().Length==fonts,"no temporary font asset created");
                view.Unbind();vm.Dispose();vm=null;Check(font!=null,"disposing panel preserves borrowed font");
                using(var main=new GameDebugPanelContext(()=>null,control,flow))
                using(var titleVm=new DebugPanelViewModel(main,logs))
                {main.RefreshBinding();titleVm.ToggleVisible();titleVm.Refresh();Check(titleVm.Visible&&!titleVm.CanCommand&&!main.WorldScene.IsValid(),"main-game title or loading supports logs without direct-test settings");}
            }
            finally{vm?.Dispose();logs.Dispose();Set(player,"actor",null);Set(player,"model",null);player.Dispose();bridgeControl.Dispose();session.Dispose();control.Dispose();flow.Dispose();EditorSceneManager.ClosePreviewScene(scene);}
            return r;
        }
    }
}
