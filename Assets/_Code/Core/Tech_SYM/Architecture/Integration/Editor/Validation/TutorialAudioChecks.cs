using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cooked.Audio;
using Cooked.Contracts;
using Cooked.Level;
using Cooked.Session;
using Cooked.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Cooked.Validation
{
    /// <summary>22~24 요구사항의 관찰 결과 검사. 전체 플레이/청취 검증을 대신하지 않는다.</summary>
    public static class TutorialAudioChecks
    {
        [Serializable] public sealed class Report { public int passed; public int failed; public List<string> cases = new List<string>(); }
        private sealed class Driver : IAudioPlaybackDriver
        {
            public readonly List<string> starts = new List<string>();
            public readonly Dictionary<int,string> voices = new Dictionary<int,string>();
            public readonly Dictionary<int,bool> loops = new Dictionary<int,bool>(), pauses = new Dictionary<int,bool>();
            public void Start(int c,string id,bool loop) { starts.Add(id); voices[c]=id; loops[c]=loop; }
            public void Stop(int c,bool destroyed=false) { voices.Remove(c); }
            public void SetGain(int c,float gain) { }
            public void SetPaused(int c,bool pause) { pauses[c]=pause; }
            public bool IsPlaying(int c) => voices.ContainsKey(c);
            public string ResolveMusicId(AudioClip clip) => "stage:"+clip.name;
            public bool IsSameClip(string a,string b) => a != null && a==b;
        }
        private sealed class Fade : IMusicCrossfadeDriver
        {
            public Action Complete;
            public bool Paused;
            public void Start(float a,float b,float c,float d,float seconds,bool paused,Action<float,float> apply,Action completed)
            { Paused=paused; Complete=()=>{apply(b,d);completed();}; }
            public void SetPaused(bool p) { Paused=p; }
            public void Cancel() { Complete=null; }
            public void Dispose() { Cancel(); }
            public void Finish() { var c=Complete;Complete=null;c?.Invoke(); }
        }
        private sealed class Flow : IGameFlowService
        {
            public FlowSnapshot Snapshot {get;private set;} = new FlowSnapshot(FlowState.Title,false,0,null);
            public event Action<FlowSnapshot> Changed;
            public void Emit(FlowState state) { Snapshot=new FlowSnapshot(state,false,Snapshot.Revision+1,null);Changed?.Invoke(Snapshot); }
            public bool Request(FlowRequest r)=>false;
            public void CancelCurrent(){}
            public void Dispose(){Changed=null;}
        }
        private static void Set(object target,string field,object value)
        { target.GetType().GetField(field,BindingFlags.Instance|BindingFlags.NonPublic).SetValue(target,value); }
        public static Report Run()
        {
            var report=new Report();
            Action<bool,string> check=(ok,name)=>{if(ok)report.passed++;else report.failed++;report.cases.Add((ok?"P ":"F ")+name);};
            var catalog=AssetDatabase.LoadAssetAtPath<AudioCueCatalog>(AudioCueCatalog.AssetPath);
            var driver=new Driver();var fade=new Fade();
            using(var audio=new AudioService(catalog.CreateSettings(),driver,fade,_=>{}))
            {
                audio.PlayMusic(AudioCueIds.Title);
                check(driver.loops[0],"타이틀 Loop");
                audio.PlayMusic(AudioCueIds.Title);
                check(driver.starts.Count==1,"동일 음악 요청 재시작 없음");
                audio.PlayMusic(AudioCueIds.Opening);
                check(driver.starts.Count==1,"이전 곡 종료 전 새 곡 시작 없음");
                fade.Finish();
                check(driver.starts.Last()==AudioCueIds.Opening&&!driver.loops[0]&&driver.voices.Count==1,"오프닝 One Shot 및 단일 BGM");
                audio.SetPauseState(true,true);
                check(driver.pauses[0]&&fade.Paused,"옵션 음악/전환 정지");
                audio.SetPauseState(false,false);
                check(!driver.pauses[0]&&!fade.Paused,"옵션 음악/전환 재개");
                audio.PlayMusic(AudioCueIds.Chase);
                var stale=fade.Complete;
                audio.PlayMusic(AudioCueIds.Ending);
                int n=driver.starts.Count;stale?.Invoke();
                check(driver.starts.Count==n,"취소된 추격 전환 콜백 무효");fade.Finish(); n=driver.starts.Count;
                check(driver.starts.Count==n&&driver.starts.Last()==AudioCueIds.Ending&&!driver.loops[0],"엔딩 단독 One Shot·낡은 전환 무효");
                audio.FadeOutMusic();fade.Finish();
                check(driver.voices.Count==0,"명시적 페이드아웃 요청 후 음악 종료");
            }
            driver=new Driver();fade=new Fade();
            using(var audio=new AudioService(catalog.CreateSettings(),driver,fade,_=>{}))
            using(var control=new GameplayControlService(()=>1,_=>{}))
            using(var flow=new Flow())
            using(var binding=new AudioFlowBinding(audio,flow,control,catalog))
            {
                flow.Emit(FlowState.LoadingGame);fade.Finish();
                binding.SetStage("01_Stage_Tutorial");
                flow.Emit(FlowState.Playing);fade.Finish();
                check(driver.starts.Count(x=>x==AudioCueIds.Respawn)==0,"최초 스폰 리스폰음 없음");
                var first=driver.starts.Count;
                binding.SetStage("01_Stage_Tutorial");flow.Emit(FlowState.Playing);
                check(driver.starts.Count==first,"스테이지 재알림 BGM 재시작 없음");
                flow.Emit(FlowState.Retrying);fade.Finish();flow.Emit(FlowState.Playing);fade.Finish();
                check(driver.starts.Count(x=>x==AudioCueIds.Respawn)==1,"재시도 성공 리스폰음 1회");
                check(driver.starts.Count(x=>x.StartsWith("stage:"))==1,"재시도 일반 BGM 기존 재생 유지");
                flow.Emit(FlowState.Playing);
                check(driver.starts.Count(x=>x==AudioCueIds.Respawn)==1,"Playing 중복 알림 리스폰음 중복 없음");
                binding.SetChaseActive(true);
                check(driver.starts.Last()!=AudioCueIds.Chase,"추격은 이전 음악 페이드 완료 대기");
                fade.Finish();check(driver.starts.Last()==AudioCueIds.Chase&&driver.loops[0],"추격 Loop");
                flow.Emit(FlowState.Retrying);fade.Finish();flow.Emit(FlowState.Playing);fade.Finish();
                check(driver.starts.Count(x=>x==AudioCueIds.Chase)==1,"재시도 추격 BGM 자동 재시작 없음");
                binding.Dispose();int count=driver.starts.Count;flow.Emit(FlowState.Ending);
                check(driver.starts.Count==count,"오디오 바인딩 폐기 후 호출 없음");
            }
            using(var control=new GameplayControlService(()=>1,_=>{}))
            {
                var progress=new TutorialProgress();var id=new object();
                var vm=new TutorialPromptViewModel(progress,id,control);int events=0;vm.Changed+=()=>events++;
                check(!vm.Visible&&vm.TryReveal()&&vm.Visible,"튜토리얼 최초 활성화");
                check(!vm.TryReveal()&&events==1,"재접촉 활성화 1회");
                using(var pause=control.PausePresentation("검사"))check(vm.Paused,"튜토리얼 옵션 정지");
                check(!vm.Paused,"튜토리얼 옵션 복귀");
                vm.Dispose();int before=events;
                using(var pause=control.PausePresentation("폐기 검사")){}
                check(events==before&&!vm.TryReveal(),"튜토리얼 폐기 후 통지/활성화 없음");
                using(var restored=new TutorialPromptViewModel(progress,id,control))
                    check(restored.Visible&&!restored.TryReveal(),"재시도 재바인딩 활성화 기록 유지");
                using(var fresh=new TutorialPromptViewModel(new TutorialProgress(),id,control))
                    check(!fresh.Visible&&fresh.TryReveal(),"새 게임 기록 초기화");
            }
            var preview=EditorSceneManager.NewPreviewScene();
            try
            {
                var root=new GameObject("CheckpointCheck");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,preview);
                var stage=root.AddComponent<StageService>();
                var a=new GameObject("Start");a.transform.SetParent(root.transform);a.AddComponent<BoxCollider>().isTrigger=true;
                var start=a.AddComponent<StageTrigger>();Set(start,"stage",stage);
                var b=new GameObject("End");b.transform.SetParent(root.transform);b.AddComponent<BoxCollider>().isTrigger=true;
                var end=b.AddComponent<StageTrigger>();Set(end,"stage",stage);
                var floor=new GameObject("Ground");floor.transform.SetParent(root.transform);floor.transform.position=new Vector3(0,-.5f,0);floor.AddComponent<BoxCollider>().size=new Vector3(10,1,10);
                var shapeObject=new GameObject("Normal shape");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(shapeObject,preview);shapeObject.SetActive(false);
                var shape=shapeObject.AddComponent<CapsuleCollider>();shape.height=2;shape.radius=.5f;
                foreach(var point in new[]{start,end})
                {
                    var anchor=new GameObject("SpawnAnchor");anchor.transform.SetParent(root.transform);
                    var spawn=anchor.AddComponent<CheckpointSpawnPoint>();Set(spawn,"normalCapsule",shape);Set(point,"spawnPoint",spawn);
                }
                var section=new StageService.Section();Set(section,"start",start);Set(section,"end",end);
                Set(stage,"sections",new[]{section});Set(stage,"firstSectionStart",start);
                int captured=0;stage.CheckpointCaptured+=_=>captured++;
                var initial=new CheckpointSnapshot(Vector3.zero,3,AbilityMask.FormChange);
                stage.RegisterInitialCheckpoint(initial);stage.RegisterInitialCheckpoint(initial);
                check(stage.HasCheckpoint&&stage.IsActivated(start)&&stage.ActivatedCheckpointCount==1,"시작 체크포인트 초기 장부 단일 등록");
                check(captured==0&&!stage.HasReachedCheckpoint,"초기 등록 도달 이벤트/도달 판정 없음");
                var restored=stage.ResolveRestore(initial);
                check(restored.Fragments==3&&restored.UnlockedAbilities==AbilityMask.FormChange,"초기 스냅샷 상태 보존");
                check(stage.ShouldPlayCheckpointSound(start)&&!stage.ShouldPlayCheckpointSound(end),"구간 시작 재생·종료 무음 기본 정책");
                Set(start,"checkpointSound",CheckpointSoundPolicy.Silent);
                check(!stage.ShouldPlayCheckpointSound(start),"명시적 무음 정책");
                stage.Dispose();
                check(stage.IsActivated(start)&&stage.HasCheckpoint,"Actor 연결 해제 후 체크포인트 기록 유지");
            }
            finally {EditorSceneManager.ClosePreviewScene(preview);}
            foreach(var pair in new[]{("Move",4),("Jump",2),("Tear",8),("Form",16)})
            {
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Animations/Tech_SYM/Tutorial/"+pair.Item1+".anim");
                var keys=AnimationUtility.GetObjectReferenceCurve(clip,AnimationUtility.GetObjectReferenceCurveBindings(clip)[0]);
                check(keys.Take(pair.Item2).Select((k,i)=>Mathf.Abs(k.time*60-i*40)<.01f).All(x=>x)&&
                    Mathf.Abs(clip.length-pair.Item2*40f/60)<.001f, pair.Item1+" 40프레임 교체 및 마지막 유지");
                check(keys.All(k=>k.value is Sprite && AssetDatabase.GetAssetPath(k.value).StartsWith("Assets/_Art/UI/Tutorials/")),pair.Item1+" 원본 Sprite 직접 참조");
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/UI/Tutorial/Tutorial_"+pair.Item1+".prefab");
                check(prefab.GetComponentsInChildren<Transform>(true).All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0)&&
                    prefab.GetComponentInChildren<Canvas>(true).renderMode==RenderMode.WorldSpace,pair.Item1+" 프리팹 참조/월드 Canvas");
            }
            return report;
        }
    }
}
