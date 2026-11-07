using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cooked.Audio;
using Cooked.Contracts;
using Cooked.Session;
using FeelsGoodOnion.TechSYM.Enemies;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cooked.Validation
{
    public static class GameplayAudioChecks
    {
        [Serializable] public sealed class Report { public int passed, failed; public List<string> cases = new List<string>(); }
        private sealed class Driver : IAudioPlaybackDriver
        {
            public readonly List<string> Starts = new List<string>();
            public readonly Dictionary<int, string> Voices = new Dictionary<int, string>();
            public void Start(int channel, string cue, bool loop) { Starts.Add(cue); Voices[channel] = cue; }
            public void Stop(int channel, bool allowDestroyedSource = false) => Voices.Remove(channel);
            public void SetGain(int channel, float gain) { }
            public void SetPaused(int channel, bool paused) { }
            public bool IsPlaying(int channel) => Voices.ContainsKey(channel);
            public string ResolveMusicId(AudioClip clip) => clip.name;
            public bool IsSameClip(string a, string b) => a != null && a == b;
        }
        private sealed class Fade : IMusicCrossfadeDriver
        {
            public void Start(float a, float b, float c, float d, float seconds, bool paused, Action<float,float> apply, Action completed) { apply(b,d); completed(); }
            public void SetPaused(bool paused) { }
            public void Cancel() { }
            public void Dispose() { }
        }
        private sealed class Flow : IGameFlowService
        {
            public FlowSnapshot Snapshot { get; private set; } = new FlowSnapshot(FlowState.Playing, false, 0, null);
            public event Action<FlowSnapshot> Changed;
            public bool Request(FlowRequest value) { Snapshot = new FlowSnapshot(FlowState.LoadingGame, true, 1, null); Changed?.Invoke(Snapshot); return true; }
            public void Set(FlowState state) { Snapshot = new FlowSnapshot(state, false, Snapshot.Revision + 1, null); Changed?.Invoke(Snapshot); }
            public void CancelCurrent() { }
            public void Dispose() { Changed = null; }
        }
        private static void Set(object owner, string field, object value)
            => owner.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(owner, value);
        private static T Event<T>(object owner, string field) where T : Delegate
            => owner.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(owner) as T;
        public static Report Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("Edit Mode 검사입니다.");
            var report = new Report();
            void Check(bool ok, string name) { if (ok) report.passed++; else report.failed++; report.cases.Add((ok ? "P " : "F ") + name); }
            var cadence = new FootstepCadence();
            Check(cadence.Tick(true,.01f,.5f), "이동 시작 즉시 발소리");
            Check(!cadence.Tick(true,.1f,.5f), "반복 간격 이전 무음");
            cadence.Tick(false,.1f,.5f);
            Check(cadence.Tick(true,.01f,.5f), "정지 후 이동 재시작 즉시 발소리");
            cadence.Land(.5f);
            Check(!cadence.Tick(true,.1f,.5f) && cadence.Tick(true,.4f,.5f), "기름 착지 뒤0.5초 중복 방지");
            Check(!cadence.Tick(true,0,.5f), "정지 시간에는 타이머 미진행");
            var randomState = UnityEngine.Random.state;
            var scene = EditorSceneManager.NewPreviewScene();
            var root = new GameObject("AudioValidation"); SceneManager.MoveGameObjectToScene(root, scene);
            var catalog = AssetDatabase.LoadAssetAtPath<AudioCueCatalog>(AudioCueCatalog.AssetPath);
            var driver = new Driver(); var flow = new Flow(); var control = new GameplayControlService(); var session = new GameSessionService();
            var bridgeControl = new GameplayControlService();
            var bridge = new PlayerBridgeService(bridgeControl, session, null);
            var output = new AudioSfxLimitChecks.RecordingOutput();
            var warnings = new List<string>();
            double audioTime = 0;
            var audio = new AudioService(catalog.CreateSettings(), driver, new Fade(), warnings.Add, sfxOutput: output, realtime: () => audioTime);
            PlayerAudioBinding playerBinding = null; WorldAudioBinding world = null;
            try
            {
                var playerNode = new GameObject("Player"); playerNode.SetActive(false); playerNode.transform.SetParent(root.transform);
                var actor = playerNode.AddComponent<PlayerFacade>();
                var model = new PlayerModel(3, 3, 0, 5); Set(actor, "model", model); Set(bridge, "actor", actor); Set(bridge, "model", model);
                playerBinding = new PlayerAudioBinding(audio, bridge, control, flow, catalog.Gameplay);
                Event<Action>(actor, "Jumped")?.Invoke(); Event<Action>(actor, "Landed")?.Invoke();
                Check(output.Starts.SequenceEqual(new[]{ AudioCueIds.Jump, AudioCueIds.Land }), "실제 Actor 이벤트 → 점프/착지 호출");
                Event<Action<AbilityId>>(bridge, "AbilitySucceeded")?.Invoke(AbilityId.Tear);
                Check(output.Starts.Last() == AudioCueIds.Tear, "눈물 성공 이벤트 연결");
                Event<Action<PlayerSnapshot>>(bridge, "Changed")?.Invoke(new PlayerSnapshot(3,3,4,5));
                Event<Action<PlayerSnapshot>>(bridge, "Changed")?.Invoke(new PlayerSnapshot(3,3,5,5));
                Check(output.Starts.TakeLast(2).SequenceEqual(new[]{AudioCueIds.Pickup,AudioCueIds.Unlock}), "조각 획득/준비음 공유 바인딩");
                int before = output.Starts.Count;
                using (control.PauseWorld("test")) Event<Action>(actor, "Jumped")?.Invoke();
                Check(output.Starts.Count == before, "월드 정지 중 성공 통지 재생 억제");
                playerBinding.Dispose(); playerBinding = null;
                Event<Action>(actor, "Jumped")?.Invoke();
                Check(output.Starts.Count == before, "Dispose 후 Actor 구독 해제");
                audioTime += .1;
                playerBinding = new PlayerAudioBinding(audio, bridge, control, flow, catalog.Gameplay);
                Event<Action>(actor, "Jumped")?.Invoke();
                Check(output.Starts.Count == before + 1, "재바인딩 후 중복 없이 한 번");

                var enemyNode = new GameObject("Enemy"); enemyNode.SetActive(false); enemyNode.transform.SetParent(root.transform);
                var enemy = enemyNode.AddComponent<EnemyActor>();
                var bodyNode = new GameObject("Body"); bodyNode.transform.SetParent(enemyNode.transform);
                var body = bodyNode.AddComponent<BoxCollider>(); body.isTrigger = true;
                var sensor = bodyNode.AddComponent<EnemyContactSensor>(); Set(sensor,"owner",enemy);
                Set(enemy,"definition",AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/_Data/Tech_SYM/Enemies/Stationary.asset")); Set(enemy,"body",body);
                enemyNode.SetActive(true); enemy.ResetForRetry();
                var damage = (PlayerBridgeService.AcceptedDamage)typeof(PlayerBridgeService.AcceptedDamage).GetConstructors(BindingFlags.Instance|BindingFlags.NonPublic).Single().Invoke(new object[]{actor,0L,null,1,1,false,enemy,true});
                Event<Action<PlayerBridgeService.AcceptedDamage>>(bridge,"DamageAccepted")?.Invoke(damage);
                Check(output.Starts.Last()==AudioCueIds.Hit && warnings.Count==2,"수락된 피해 → 피격/적 접촉/넉백 용도 분기");
                var plateNode = new GameObject("Plate"); plateNode.SetActive(false); plateNode.transform.SetParent(root.transform);
                var plate = plateNode.AddComponent<FeelsGoodOnion.TechSYM.Platforms.PressurePlatePlatform>();
                var breakNode = new GameObject("Break"); breakNode.SetActive(false); breakNode.transform.SetParent(root.transform);
                var platform = breakNode.AddComponent<FeelsGoodOnion.TechSYM.Features.ShatteredPlatform>();
                var deviceNode = new GameObject("Device"); deviceNode.SetActive(false); deviceNode.transform.SetParent(root.transform);
                var device = deviceNode.AddComponent<FeelsGoodOnion.TechSYM.OvenTray.OvenTrayObject>();
                // Actor는 이벤트 검사 전용 비활성 객체다. 가청 위치는 별도의 활성 기준점으로 주입한다.
                var listener = new GameObject("검사 PC 기준점"); listener.transform.SetParent(root.transform);
                audio.SetWorldListener(listener.transform);
                var burners = new FeelsGoodOnion.TechSYM.Platforms.BurnerDamagePlatform[3];
                for (int i = 0; i < burners.Length; i++)
                {
                    var burner = new GameObject("ambient 수명 검사 " + i); burner.transform.SetParent(root.transform);
                    burners[i] = burner.AddComponent<FeelsGoodOnion.TechSYM.Platforms.BurnerDamagePlatform>();
                }
                world = new WorldAudioBinding(audio, bridge, scene, control, flow, catalog.Gameplay);
                Event<Action<bool>>(plate,"PressedChanged")?.Invoke(true);
                Event<Action<bool>>(plate,"PressedChanged")?.Invoke(false);
                Event<Action>(platform,"CollapseStarted")?.Invoke();
                Event<Action>(device,"OperationStarted")?.Invoke();
                var played = output.Voices.Values.Select(v=>catalog.CreateClipMap()[v.Cue.Id].name).ToArray();
                Check(new[]{"Sfx_Weight_On","Sfx_Weight_Off","Sfx_Platform_Break","Sfx_Interaction"}.All(played.Contains),"압력/붕괴/작동 성공 이벤트의 카탈로그 Clip 연결(네이티브 재생 제외)");
                world.Tick(1); world.Tick(catalog.Gameplay.IdleMaximum + 1);
                Check(output.Voices.Values.Count(v=>v.Cue.Id==AudioCueIds.MonsterIdle)==1,"범위 안 정상 적 기본음 타이머");
                int Sources() => output.Voices.Values.Count(v=>v.Cue.Id==AudioCueIds.MonsterHit);
                int baseline = Sources(); enemy.TryStun(1000); enemy.TryStun(1000);
                Check(Sources() == baseline + 1, "같은 시각 TryStun 재적중 효과음 억제");
                audioTime += .1; enemy.TryStun(1000);
                Check(Sources() == baseline + 2, "최소 간격 후 TryStun 재적중 두 번째 효과음 허용");
                world.Tick(catalog.Gameplay.IdleMaximum + 1);
                Check(output.Voices.Values.Count(v=>v.Cue.Id==AudioCueIds.MonsterIdle)==1,"기절 중 기본음 중단");
                enemy.Tick(3);
                world.Tick(.1f);
                Check(output.Voices.Values.Count(v=>v.Cue.Id==AudioCueIds.MonsterIdle)==1,"기절 해제 시 대기 간격 재설정");
                enemy.TryStun(0);
                Check(Sources() == baseline + 2, "거절된 기절은 소리 없음");
                Check(output.Voices.Values.Where(v=>v.Cue.Id==AudioCueIds.MonsterHit).All(v=>v.Spatial), "위치 출력은3D");
                Check(audio.CountRegisteredSfx(AudioCueIds.Fire)==3, "바인딩은 동일 환경음3개를 독립 등록");
                using (control.PauseWorld("ambient 정리 검사"))
                {
                    audio.SetPauseState(true,true);
                    burners[0].enabled = false;
                    UnityEngine.Object.DestroyImmediate(burners[1].gameObject);
                    world.Tick(0);
                    Check(audio.CountRegisteredSfx(AudioCueIds.Fire)==1, "pause/delta0에서도 disabled Owner와 destroyed Owner 해제");
                }
                audio.SetPauseState(false,false);
                world.Dispose(); world = null; enemy.TryStun(1000);
                Check(audio.CountRegisteredSfx(AudioCueIds.Fire)==0, "바인딩 Dispose는 자기 환경음 등록 반환");
                Check(Event<Action<bool>>(plate,"PressedChanged")==null && Event<Action>(platform,"CollapseStarted")==null && Event<Action>(device,"OperationStarted")==null,"장치 구독 해제");
                Check(Sources() == baseline + 2, "월드 해제 후 적중 구독 해제");

                using (var rebound = new WorldAudioBinding(audio, bridge, scene, control, flow, catalog.Gameplay))
                {
                    rebound.Tick(0);
                    audio.ClearWorldSfx();
                    rebound.Tick(0);
                    Check(audio.CountRegisteredSfx(AudioCueIds.Fire)==1, "Retry/ClearWorld 후 낡은 handle 버리고 한 번 재등록");
                }
                audio.ClearWorldSfx(); audio.SetWorldListener(listener.transform); audio.SetVolumes(.5f,.7f,.5f);
                int fire = audio.PlaySfxAt(AudioCueIds.Fire, listener.transform.position, bodyNode.transform);
                int pot = audio.PlaySfxAt(AudioCueIds.Pot, listener.transform.position);
                Check(audio.CountActiveSfx(AudioCueIds.Fire)==1 && audio.CountActiveSfx(AudioCueIds.Pot)==1, "환경 Loop 독립 출력 요청");
                audio.StopWorldVoice(fire);
                Check(audio.CountActiveSfx(AudioCueIds.Fire)==0 && audio.CountActiveSfx(AudioCueIds.Pot)==1, "개별 Loop 정지는 다른 Loop 유지");
                var potGain = catalog.CreateSettings().Single(c=>c.Id==AudioCueIds.Pot).Gain;
                Check(Mathf.Abs(output.Voices.Values.Single().Gain - .25f * potGain) < .001f, "출력 전에 Master×SFX×Cue 게인 전달");
                listener.transform.position = Vector3.right * 20;
                audio.PollCompletedVoices();
                Check(output.ActiveSourceCount==0 && audio.CountRegisteredSfx(AudioCueIds.Pot)==1, "20m에서 슬롯 반환, 논리 등록 유지");
                listener.transform.position = Vector3.zero; audio.PollCompletedVoices();
                Check(output.ActiveSourceCount==1, "PC 복귀 시 Loop 출력 재획득");
                audio.SetPauseState(true,true); before = output.ActiveSourceCount;
                Check(audio.PlaySfxAt(AudioCueIds.MonsterHit, Vector3.zero) == 0 && output.ActiveSourceCount==before, "정지 중 새 위치 음성 없음");
                audio.SetPauseState(false,false); audio.ClearWorldSfx();
                Check(output.ActiveSourceCount==0 && audio.LogicalSfxVoiceCount==0, "Retry 월드 정리로 논리 등록/출력 종료");
                foreach (var id in new[]{AudioCueIds.HitMonster, AudioCueIds.PlatformSpawn, AudioCueIds.Knockback}) { audio.PlaySfx(id); audio.PlaySfx(id); }
                Check(warnings.Count == 3, "미확보3종은 대체 재생 없이 용도별 한 번 경고");
                audio.PlayMusic(AudioCueIds.Stage1);
                Event<Action>(bridge,"Dying")?.Invoke();
                Check(audio.CurrentMusicId==AudioCueIds.Stage1 && output.Voices.Values.Any(v=>v.Cue.Id==AudioCueIds.Death),"사망 이벤트 → BGM 유지와 전환 종류 사망음");
                int click = 0;
                using (var settings = new Cooked.UI.SettingsService(new Store(),audio))
                using (var options = new Cooked.UI.OptionsService(flow,control,settings))
                {
                    flow.Set(FlowState.Title);
                    using (var title = new Cooked.UI.TitleViewModel(flow,options,()=>{},()=>click++))
                    { title.NewGame(true); title.NewGame(true); Check(click==1,"수락한 UI Command만 클릭음1회"); }
                }
            }
            finally
            {
                world?.Dispose(); playerBinding?.Dispose(); audio.Dispose();
                Set(bridge,"actor",null); Set(bridge,"model",null); bridge.Dispose(); bridgeControl.Dispose(); session.Dispose(); control.Dispose(); flow.Dispose();
                EditorSceneManager.ClosePreviewScene(scene); UnityEngine.Random.state = randomState;
            }
            return report;
        }
        private sealed class Store : Cooked.UI.ISettingsStore
        {
            public float Read(string key,float fallback)=>fallback;
            public void Write(string key,float value) { }
            public void Save() { }
        }
    }
}

