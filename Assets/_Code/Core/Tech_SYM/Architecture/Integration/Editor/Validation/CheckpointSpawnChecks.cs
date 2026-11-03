using System;
using System.Collections.Generic;
using System.Reflection;
using Cooked.Contracts;
using Cooked.Level;
using Cooked.Session;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cooked.Validation
{
    public static class CheckpointSpawnChecks
    {
        [Serializable] public sealed class Report { public int passed, failed; public List<string> cases = new List<string>(); }
        private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner,value);
        public static Report Run()
        {
            var result = new Report();
            void Check(bool ok,string name) { if(ok) result.passed++; else result.failed++; result.cases.Add((ok ? "P " : "F ") + name); }
            var scene = EditorSceneManager.NewPreviewScene();
            GameObject Make(string name) { var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, scene); return go; }
            try
            {
                var body=Make("Player test shape"); body.SetActive(false);
                var rb=body.AddComponent<Rigidbody>();rb.isKinematic=true;
                var capsule=body.AddComponent<CapsuleCollider>();capsule.height=2;capsule.radius=.4f;capsule.center=new Vector3(.2f,.8f,.1f);
                var floor=Make("Ground");floor.transform.position=new Vector3(0,-.5f,0);floor.AddComponent<BoxCollider>().size=new Vector3(20,1,20);
                var anchor=Make("SpawnAnchor").AddComponent<CheckpointSpawnPoint>();Set(anchor,"normalCapsule",capsule);
                Check(anchor.CanPlace(out _),"지면 위 Normal Capsule 배치 가능");
                var feet=new Pose(Vector3.zero,Quaternion.Euler(0,90,0));
                var center=SpawnPlacement.Center(capsule,feet);
                typeof(SessionActorHost).GetMethod("PlaceColliderCenter",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{capsule,new Pose(center,feet.rotation)});
                Check(Vector3.Distance(SpawnPlacement.Feet(capsule),feet.position)<.0001f,"비영점 Collider center·회전에서도 발바닥 정렬");
                body.transform.localScale=new Vector3(2,2,2);
                Check(Mathf.Abs(SpawnPlacement.Center(capsule,feet).y-2)<.0001f,"실제 Capsule 배율 반영");body.transform.localScale=Vector3.one;
                capsule.height=1;capsule.radius=.2f;
                Check(Mathf.Abs(SpawnPlacement.Center(capsule,feet).y-.5f)<.0001f,"폼 크기에 따라 중심 높이 변경");capsule.height=2;capsule.radius=.4f;
                var blocker=Make("Wall");blocker.transform.position=Vector3.up;var obstacle=blocker.AddComponent<BoxCollider>();
                Check(!anchor.CanPlace(out var reason)&&reason.Contains("Wall"),"벽 겹침 거절 및 원인 이름");
                obstacle.isTrigger=true;Check(anchor.CanPlace(out _),"감지 Trigger는 스폰 공간을 막지 않음");
                obstacle.isTrigger=false;blocker.SetActive(false);Check(anchor.CanPlace(out _),"비활성 장애물 제외");
                anchor.transform.position=Vector3.up;Check(!anchor.CanPlace(out _),"공중 발바닥 거절");
                anchor.transform.position=Vector3.down*.1f;Check(!anchor.CanPlace(out _),"지면 침투 거절");
                anchor.transform.position=Vector3.zero;floor.SetActive(false);Check(!anchor.CanPlace(out _),"지면 없음 거절");floor.SetActive(true);
                anchor.transform.rotation=Quaternion.Euler(30,0,0);Check(!anchor.CanPlace(out _),"기울어진 스폰 회전 거절");anchor.transform.rotation=Quaternion.identity;
                Set(anchor,"normalCapsule",null);Check(!anchor.CanPlace(out _),"검사 Capsule 미연결은 빨강/불가");Set(anchor,"normalCapsule",capsule);
                var stage=Make("Stage").AddComponent<StageService>();
                StageTrigger Point(string name) { var go=Make(name);go.AddComponent<BoxCollider>().isTrigger=true;var p=go.AddComponent<StageTrigger>();Set(p,"stage",stage);Set(p,"spawnPoint",anchor);return p; }
                var start=Point("Start");var end=Point("End");start.transform.position=new Vector3(9,9,9);
                var section=new StageService.Section();Set(section,"start",start);Set(section,"end",end);Set(stage,"sections",new[]{section});Set(stage,"firstSectionStart",start);
                Check(start.CheckpointPose.position==anchor.transform.position,"Trigger 위치와 스폰 위치 분리");
                var initial=new CheckpointSnapshot(new Vector3(5,5,5),2,AbilityMask.Both);
                floor.SetActive(false);blocker.SetActive(true);stage.RegisterInitialCheckpoint(initial);
                Check(stage.HasCheckpoint&&stage.IsActivated(start),"런타임 초기 등록은 지면 없음·Collider 겹침에도 공간 검사 없이 수락");
                floor.SetActive(true);int captured=0;stage.CheckpointCaptured+=_=>captured++;
                stage.RegisterInitialCheckpoint(initial);stage.RegisterInitialCheckpoint(initial);
                var saved=stage.ResolveRestore(initial);
                Check(saved.Position==anchor.transform.position&&saved.Fragments==2&&saved.UnlockedAbilities==AbilityMask.Both,"발바닥 좌표와 기존 상태3값 저장");
                Check(captured==0&&stage.ActivatedCheckpointCount==1,"초기 무음·중복 활성화 억제 유지");
                blocker.SetActive(true);var before=anchor.transform.position;
                Check(!anchor.CanPlace(out _)&&anchor.transform.position==before&&stage.ResolveRestore(initial).Position==saved.Position,"에디터는 겹침 표시·런타임 복귀는 저장 위치 유지·대체 위치 없음");
                stage.Dispose();
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
            return result;
        }
    }
}
