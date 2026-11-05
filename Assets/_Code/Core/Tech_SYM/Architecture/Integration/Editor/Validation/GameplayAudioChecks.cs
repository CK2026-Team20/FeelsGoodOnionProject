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
            var output = new WorldAudioOutput(root.transform, catalog);
            var warnings = new List<string>();
            double audioTime = 0;
            var audio = new AudioService(catalog.CreateSettings(), driver, new Fade(), warnings.Add, worldOutput: output, realtime: () => audioTime);
            PlayerAudioBinding playerBinding = null; WorldAudioBinding world = null;
            try
            {
                var playerNode = new GameObject("Player"); playerNode.SetActive(false); playerNode.transform.SetParent(root.transform);
                var actor = playerNode.AddComponent<PlayerFacade>();
                var model = new PlayerModel(3, 3, 0, 5); Set(actor, "model", model); Set(bridge, "actor", actor); Set(bridge, "model", model);
                playerBinding = new PlayerAudioBinding(audio, bridge, control, flow, catalog.Gameplay);
                Event<Action>(actor, "Jumped")?.Invoke(); Event<Action>(actor, "Landed")?.Invoke();
                Check(driver.Starts.SequenceEqual(new[]{ AudioCueIds.Jump, AudioCueIds.Land }), "실제 Actor 이벤트 → 점프/착지 호출");
                Event<Action<AbilityId>>(bridge, "AbilitySucceeded")?.Invoke(AbilityId.Tear);
                Check(driver.Starts.Last() == AudioCueIds.Tear, "눈물 성공 이벤트 연결");
                Event<Action<PlayerSnapshot>>(bridge, "Changed")?.Invoke(new PlayerSnapshot(3,3,4,5));
                Event<Action<PlayerSnapshot>>(bridge, "Changed")?.Invoke(new PlayerSnapshot(3,3,5,5));
                Check(driver.Starts.TakeLast(2).SequenceEqual(new[]{AudioCueIds.Pickup,AudioCueIds.Unlock}), "조각 획득/준비음 공유 바인딩");
                int before = driver.Starts.Count;
                using (control.PauseWorld("test")) Event<Action>(actor, "Jumped")?.Invoke();
                Check(driver.Starts.Count == before, "월드 정지 중 성공 통지 재생 억제");
                playerBinding.Dispose(); playerBinding = null;
                Event<Action>(actor, "Jumped")?.Invoke();
                Check(driver.Starts.Count == before, "Dispose 후 Actor 구독 해제");
                audioTime += .1;
                playerBinding = new PlayerAudioBinding(audio, bridge, control, flow, catalog.Gameplay);
                Event<Action>(actor, "Jumped")?.Invoke();
                Check(driver.Starts.Count == before + 1, "재바인딩 후 중복 없이 한 번");

                var enemyNode = new GameObject("Enemy"); enemyNode.SetActive(false); enemyNode.transform.SetParent(root.transform);
                var enemy = enemyNode.AddComponent<EnemyActor>();
                var bodyNode = new GameObject("Body"); bodyNode.transform.SetParent(enemyNode.transform);
                var body = bodyNode.AddComponent<BoxCollider>(); body.isTrigger = true;
                var sensor = bodyNode.AddComponent<EnemyContactSensor>(); Set(sensor,"owner",enemy);
                Set(enemy,"definition",AssetDatabase.LoadAssetAtPath<EnemyDefinition>("Assets/_Data/Tech_SYM/Enemies/Stationary.asset")); Set(enemy,"body",body);
                enemyNode.SetActive(true); enemy.ResetForRetry();
                var damage = (PlayerBridgeService.AcceptedDamage)typeof(PlayerBridgeService.AcceptedDamage).GetConstructors(BindingFlags.Instance|BindingFlags.NonPublic).Single().Invoke(new object[]{actor,0L,null,1,1,false,enemy,true});
                Event<Action<PlayerBridgeService.AcceptedDamage>>(bridge,"DamageAccepted")?.Invoke(damage);
                Check(driver.Starts.Last()==AudioCueIds.Hit && warnings.Count==2,"수락된 피해 → 피격/적 접촉/넉백 용도 분기");
                var plateNode = new GameObject("Plate"); plateNode.SetActive(false); plateNode.transform.SetParent(root.transform);
                var plate = plateNode.AddComponent<FeelsGoodOnion.TechSYM.Platforms.PressurePlatePlatform>();
                var breakNode = new GameObject("Break"); breakNode.SetActive(false); breakNode.transform.SetParent(root.transform);
                var platform = breakNode.AddComponent<FeelsGoodOnion.TechSYM.Features.ShatteredPlatform>();
                var deviceNode = new GameObject("Device"); deviceNode.SetActive(false); deviceNode.transform.SetParent(root.transform);
                var device = deviceNode.AddComponent<FeelsGoodOnion.TechSYM.OvenTray.OvenTrayObject>();
                world = new WorldAudioBinding(audio, bridge, scene, control, flow, catalog.Gameplay);
                Event<Action<bool>>(plate,"PressedChanged")?.Invoke(true);
                Event<Action<bool>>(plate,"PressedChanged")?.Invoke(false);
                Event<Action>(platform,"CollapseStarted")?.Invoke();
                Event<Action>(device,"OperationStarted")?.Invoke();
                var played = root.GetComponentsInChildren<AudioSource>(true).Where(s=>s.clip!=null).Select(s=>s.clip.name).ToArray();
                Check(new[]{"Sfx_Weight_On","Sfx_Weight_Off","Sfx_Platform_Break","Sfx_Interaction"}.All(played.Contains),"압력/붕괴/작동 성공 이벤트의 실제 Clip 연결");
                world.Tick(1); world.Tick(catalog.Gameplay.IdleMaximum + 1);
                Check(root.GetComponentsInChildren<AudioSource>(true).Count(s=>s.clip!=null && s.clip.name=="Sfx_Monster_Idle")==1,"범위 안 정상 적 기본음 타이머");
                int Sources() => root.GetComponentsInChildren<AudioSource>(true).Count(s=>s.clip != null && s.clip.name == "Sfx_Monster_Hit");
                int baseline = Sources(); enemy.TryStun(1000); enemy.TryStun(1000);
                Check(Sources() == baseline + 1, "같은 시각 TryStun 재적중 효과음 억제");
                audioTime += .1; enemy.TryStun(1000);
                Check(Sources() == baseline + 2, "최소 간격 후 TryStun 재적중 두 번째 효과음 허용");
                world.Tick(catalog.Gameplay.IdleMaximum + 1);
                Check(root.GetComponentsInChildren<AudioSource>(true).Count(s=>s.clip!=null && s.clip.name=="Sfx_Monster_Idle")==1,"기절 중 기본음 중단");
                enemy.Tick(3);
                world.Tick(.1f);
                Check(root.GetComponentsInChildren<AudioSource>(true).Count(s=>s.clip!=null && s.clip.name=="Sfx_Monster_Idle")==1,"기절 해제 시 대기 간격 재설정");
                enemy.TryStun(0);
                Check(Sources() == baseline + 2, "거절된 기절은 소리 없음");
                Check(root.GetComponentsInChildren<AudioSource>(true).All(s=>s.spatialBlend == 1), "위치 출력은3D");
                world.Dispose(); world = null; enemy.TryStun(1000);
                Check(Event<Action<bool>>(plate,"PressedChanged")==null && Event<Action>(platform,"CollapseStarted")==null && Event<Action>(device,"OperationStarted")==null,"장치 구독 해제");
                Check(Sources() == baseline + 2, "월드 해제 후 적중 구독 해제");

                audio.ClearWorldSfx(); audio.SetWorldListener(playerNode.transform); audio.SetVolumes(.5f,.7f,.5f);
                int fire = audio.PlaySfxAt(AudioCueIds.Fire, playerNode.transform.position, bodyNode.transform);
                int pot = audio.PlaySfxAt(AudioCueIds.Pot, playerNode.transform.position);
                var sources = root.GetComponentsInChildren<AudioSource>(true);
                Check(sources.Count(s=>s.clip != null && s.loop) == 2, "환경 Loop 두 개 독립 출력");
                audio.StopWorldVoice(fire);
                Check(sources.Count(s=>s.clip != null && s.loop) == 1, "개별 Loop 정지는 다른 Loop 유지");
                var potSource = sources.Single(s=>s.clip != null);
                var potGain = catalog.CreateSettings().Single(c=>c.Id==AudioCueIds.Pot).Gain;
                Check(Mathf.Abs(potSource.volume - .25f * potGain) < .001f, "출력 전에 Master×SFX×Cue 게인 적용");
                playerNode.transform.position = Vector3.right * (catalog.Gameplay.FarDistance + 1);
                audio.SetWorldListener(playerNode.transform);
                Check(potSource.volume == 0, "PC 최대 청취 거리 밖은 무음");
                playerNode.transform.position = Vector3.zero; audio.SetWorldListener(playerNode.transform);
                Check(potSource.volume > 0, "PC가 복귀하면 Loop 게인 복원");
                audio.SetPauseState(true,true); before = sources.Count(s=>s.clip != null);
                Check(audio.PlaySfxAt(AudioCueIds.MonsterHit, Vector3.zero) == 0 && sources.Count(s=>s.clip != null)==before, "정지 중 새 위치 음성 없음");
                audio.SetPauseState(false,false); audio.ClearWorldSfx();
                Check(sources.All(s=>s.clip == null), "Retry 월드 정리로 모든 위치 출력 종료");
                foreach (var id in new[]{AudioCueIds.HitMonster, AudioCueIds.PlatformSpawn, AudioCueIds.Knockback}) { audio.PlaySfx(id); audio.PlaySfx(id); }
                Check(warnings.Count == 3, "미확보3종은 대체 재생 없이 용도별 한 번 경고");
                audio.PlayMusic(AudioCueIds.Stage1);
                Event<Action>(bridge,"Dying")?.Invoke();
                Check(audio.CurrentMusicId==AudioCueIds.Stage1 && driver.Voices.TryGetValue(AudioService.TransitionChannel,out var death) && death==AudioCueIds.Death,"사망 이벤트 → BGM 유지와 전환 채널 사망음");
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

