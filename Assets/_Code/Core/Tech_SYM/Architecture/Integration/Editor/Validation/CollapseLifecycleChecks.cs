using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FeelsGoodOnion.TechSYM.Features;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cooked.Validation
{
    public static class CollapseLifecycleChecks
    {
        [Serializable] public sealed class Report { public int passed,failed;public List<string> cases=new List<string>(); }
        static void Set(object obj,string name,object value)=>obj.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(obj,value);
        static object Call(object obj,string name,params object[] args)=>obj.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(obj,args);
        public static Report Run()
        {
            var report=new Report();
            void Check(bool ok,string label){if(ok)report.passed++;else report.failed++;report.cases.Add((ok?"P ":"F ")+label);}
            var scene=EditorSceneManager.NewPreviewScene();
            var controller=new AnimatorController(); controller.AddLayer("Base Layer");
            var machine=controller.layers[0].stateMachine;
            var ready=machine.AddState("Ready");var collapse=machine.AddState("Collapse");machine.AddState("Completed");machine.defaultState=ready;
            controller.AddParameter("Collapse",AnimatorControllerParameterType.Trigger);
            var clip=new AnimationClip();clip.SetCurve("Part",typeof(Transform),"localPosition.y",AnimationCurve.Linear(0,0,.2f,-1));collapse.motion=clip;
            var transition=ready.AddTransition(collapse);transition.hasExitTime=false;transition.duration=0;transition.AddCondition(AnimatorConditionMode.If,0,"Collapse");
            try
            {
                var root=new GameObject("Collapse check");SceneManager.MoveGameObjectToScene(root,scene);
                var owner=root.AddComponent<ShatteredPlatform>();
                var physics=new GameObject("Physics");physics.transform.SetParent(root.transform);var support=physics.AddComponent<BoxCollider>();
                var visual=new GameObject("Visual");visual.transform.SetParent(root.transform);var part=new GameObject("Part");part.transform.SetParent(visual.transform);
                var animator=visual.AddComponent<Animator>();animator.runtimeAnimatorController=controller;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;animator.Rebind();animator.Update(0);
                var presentation=new PlatformPresentation(visual.transform,animator);
                Set(owner,"presentation",presentation);Set(owner,"supportCollider",support);Set(owner,"supportWasEnabled",true);
                void Phase(ShatteredPlatform.CollapsePhase phase)=>Set(owner,"<Phase>k__BackingField",phase);
                IEnumerator Playback(){Phase(ShatteredPlatform.CollapsePhase.Animating);support.enabled=false;return (IEnumerator)Call(owner,"PlayCollapse",0);}
                var run=Playback();bool more=true;int steps=0;while(more&&steps++<60){more=run.MoveNext();if(more)animator.Update(.02f);}
                Check(!more&&owner.Phase==ShatteredPlatform.CollapsePhase.Collapsed,"실제 비반복 클립 완료 판정");
                Check(!visual.activeSelf&&root.activeSelf&&!support.enabled,"완료 시 외형만 Active false·Root 유지·지지 해제");
                root.SetActive(false);owner.ResetForRetry();
                Check(visual.activeSelf&&!visual.activeInHierarchy&&support.enabled&&owner.Phase==ShatteredPlatform.CollapsePhase.Ready,"비활성 Stage에서도 재시도 외형·지지 복구");
                root.SetActive(true);Call(owner,"OnEnable");
                Check(animator.enabled&&animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Ready")&&part.transform.localPosition==Vector3.zero,"재활성화 후 Ready와 원래 자세 복구");
                Set(owner,"version",0);run=Playback();run.MoveNext();Call(owner,"CancelOwnedWork");
                Check(owner.Phase==ShatteredPlatform.CollapsePhase.Interrupted&&visual.activeSelf,"중간 취소는 완료·외형 숨김으로 처리하지 않음");
                Check(!run.MoveNext(),"취소된 낡은 코루틴은 후처리하지 않음");
                owner.ResetForRetry();Set(owner,"version",0);animator.runtimeAnimatorController=null;
                run=Playback();bool expectedError=false;
                Application.LogCallback listener=(message,stack,type)=>{if(type==LogType.Error&&message.Contains("재생 실패"))expectedError=true;};
                Application.logMessageReceived+=listener;
                try{run.MoveNext();}finally{Application.logMessageReceived-=listener;}
                Check(owner.Phase==ShatteredPlatform.CollapsePhase.Failed&&visual.activeSelf&&!support.enabled,"연결 실패는 Failed·외형 유지·지지 복구 없음");
                Check(expectedError,"재생 실패 원인 로그 출력");
                owner.ResetForRetry();Check(visual.activeSelf&&support.enabled&&owner.Phase==ShatteredPlatform.CollapsePhase.Ready,"실패 후 명시적 Retry로 복구");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
                foreach(var state in machine.states) UnityEngine.Object.DestroyImmediate(state.state);
                UnityEngine.Object.DestroyImmediate(machine);UnityEngine.Object.DestroyImmediate(controller);UnityEngine.Object.DestroyImmediate(clip);
            }
            return report;
        }
    }
}
