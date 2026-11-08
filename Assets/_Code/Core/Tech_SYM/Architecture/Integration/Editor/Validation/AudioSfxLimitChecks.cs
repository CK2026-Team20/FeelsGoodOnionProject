using System;
using System.Collections.Generic;
using System.Linq;
using Cooked.Audio;
using UnityEditor;
using UnityEngine;

namespace Cooked.Validation
{
    /// <summary>정책/이벤트 경계 검사. RecordingOutput 결과는 네이티브 재생 검증으로 보고하지 않는다.</summary>
    public static class AudioSfxLimitChecks
    {
        [Serializable] public sealed class Report
        {
            public int passed, failed;
            public List<string> cases = new List<string>();
            public void Check(bool ok, string label) { if (ok) passed++; else failed++; cases.Add((ok ? "P " : "F ") + label); }
        }
        public sealed class RecordingOutput : ISfxOutput
        {
            public sealed class Voice
            {
                public AudioCue Cue;
                public bool Spatial, Paused;
                public Vector3 Position;
                public float Gain, Seek;
            }
            public readonly List<string> Starts = new List<string>();
            public readonly Dictionary<int, Voice> Voices = new Dictionary<int, Voice>();
            private int next, created;
            public int StopFailuresRemaining { get; set; }
            public int ActiveSourceCount => Voices.Count;
            public int PooledSourceCount => created - Voices.Count;
            public float ClipLength(string id) => 4;
            public int Start(AudioCue cue, bool spatial, Vector3 position, float gain, float seekSeconds, bool paused)
            {
                int token = ++next; Starts.Add(cue.Id);
                Voices.Add(token, new Voice { Cue = cue, Spatial = spatial, Position = position, Gain = gain, Seek = seekSeconds, Paused = paused });
                created = Math.Max(created, Voices.Count); return token;
            }
            public bool IsActive(int token) => Voices.ContainsKey(token);
            public void SetGain(int token, float gain) { if (Voices.TryGetValue(token, out var v)) v.Gain = gain; }
            public void SetPosition(int token, Vector3 position) { if (Voices.TryGetValue(token, out var v)) v.Position = position; }
            public void SetPaused(int token, bool paused) { if (Voices.TryGetValue(token, out var v)) v.Paused = paused; }
            public void Stop(int token)
            {
                if (Voices.ContainsKey(token) && StopFailuresRemaining > 0)
                {
                    StopFailuresRemaining--;
                    throw new InvalidOperationException("검사용 Stop 실패");
                }
                Voices.Remove(token);
            }
            public void Dispose() => Voices.Clear();
        }
        private sealed class Driver : IAudioPlaybackDriver
        {
            public int Starts;
            public readonly HashSet<int> Active = new HashSet<int>();
            public void Start(int channel, string cueId, bool loop) { Starts++; Active.Add(channel); }
            public void Stop(int channel, bool allowDestroyedSource = false) => Active.Remove(channel);
            public void SetGain(int channel, float gain) { }
            public void SetPaused(int channel, bool paused) { }
            public bool IsPlaying(int channel) => Active.Contains(channel);
            public string ResolveMusicId(AudioClip clip) => clip.name;
            public bool IsSameClip(string a, string b) => a != null && a == b;
        }
        public sealed class ImmediateFade : IMusicCrossfadeDriver
        {
            public void Start(float a, float b, float c, float d, float seconds, bool paused, Action<float,float> apply, Action completed) { apply(b,d); completed(); }
            public void SetPaused(bool paused) { }
            public void Cancel() { }
            public void Dispose() { }
        }
        public static Report Run()
        {
            var report = new Report();
            var output = new RecordingOutput(); var driver = new Driver(); double now = 0;
            var cues = new[] {
                new AudioCue(AudioCuePurpose.MonsterHit, 1), new AudioCue(AudioCuePurpose.MonsterIdle, 1),
                new AudioCue(AudioCuePurpose.Jump, 1), new AudioCue(AudioCuePurpose.Click, 1),
                new AudioCue(AudioCuePurpose.Death, 1), new AudioCue(AudioCuePurpose.Fire, 1),
                new AudioCue(AudioCuePurpose.TitleMusic, 1)
            };
            var preview = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            var listener = new GameObject("정책 검사 PC 기준점");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(listener, preview);
            try
            {
            using (var audio = new AudioService(cues, driver, new ImmediateFade(), _ => {}, realtime: () => now, sfxOutput: output))
            {
                audio.SetWorldListener(listener.transform);
                audio.PlaySfx(AudioCueIds.MonsterHit); audio.PlaySfx(AudioCueIds.MonsterHit);
                report.Check(output.Starts.Count == 1, "동일 프레임 Limited 요청은 1회");
                now = .079; audio.PlaySfx(AudioCueIds.MonsterHit);
                report.Check(output.Starts.Count == 1, "80ms 이전 drop");
                now = .08; int h = audio.PlaySfxAt(AudioCueIds.MonsterHit, Vector3.zero);
                report.Check(h != 0 && output.Starts.Count == 2, "80ms 경계에서 2D+위치 합산 두 번째 수락");
                now = .16; audio.PlaySfx(AudioCueIds.MonsterHit);
                report.Check(output.Starts.Count == 2, "동일 ID 합산 최대2, 기존 출력 유지");
                audio.StopWorldVoice(h); audio.PlaySfx(AudioCueIds.MonsterHit);
                report.Check(output.Starts.Count == 3, "거절은 예약/간격 연장 없이 빈 슬롯에 즉시 수락");
                audio.PlaySfx(AudioCueIds.MonsterIdle);
                report.Check(output.Starts.Count == 4, "다른 ID 제한 독립");
                for (int i = 0; i < 20; i++) { audio.PlaySfx(AudioCueIds.Jump); audio.PlaySfx(AudioCueIds.Click); audio.PlaySfx(AudioCueIds.Death); }
                report.Check(audio.CountActiveSfx(AudioCueIds.Jump) == 20 && audio.CountActiveSfx(AudioCueIds.Click) == 20 && audio.CountActiveSfx(AudioCueIds.Death) == 20, "Always 2D/화면/전환 동일 프레임20개 선점 없음");
                int spatial = audio.PlaySfxAt(AudioCueIds.Jump, Vector3.one);
                report.Check(spatial != 0 && audio.CountActiveSfx(AudioCueIds.Jump) == 21, "Always 위치 출력도 제한 우회");
                audio.SetPauseState(true, false);
                report.Check(output.Voices.Values.Where(v => v.Cue.Kind == AudioCueKind.World).All(v => v.Paused) && output.Voices.Values.Where(v => v.Cue.Kind != AudioCueKind.World).All(v => !v.Paused), "월드 정지는 UI/전환 유지");
                int before = output.Starts.Count; audio.PlaySfx(AudioCueIds.Jump);
                report.Check(before == output.Starts.Count, "월드 정지 중 새 요청 차단");
                audio.SetPauseState(false, true);
                report.Check(output.Voices.Values.Where(v => v.Cue.Kind == AudioCueKind.Transition || v.Spatial).All(v => v.Paused) && output.Voices.Values.Where(v => v.Cue.Kind == AudioCueKind.UI || (v.Cue.Kind == AudioCueKind.World && !v.Spatial)).All(v => !v.Paused), "presentation 단독 정지의 kind/위치 원계약");
                audio.ClearWorldSfx();
                report.Check(output.ActiveSourceCount == 40 && output.Voices.Values.All(v => v.Cue.Kind != AudioCueKind.World), "ClearWorld는 UI/전환40개 유지");
                audio.PlayMusic(AudioCueIds.Title); audio.PlayMusic(AudioCueIds.Title);
                report.Check(driver.Starts == 1 && driver.Active.SetEquals(new[] { 0 }), "SFX가 음악 채널을 사용하지 않으며 동일 BGM 유지");
                audio.StopAll(); audio.SetPauseState(false, false);
                audio.SetWorldListener(null);
                int silent = audio.PlaySfxAt(AudioCueIds.Fire, Vector3.zero);
                report.Check(silent != 0 && audio.VirtualLoopCount == 1 && output.ActiveSourceCount == 0 && audio.CountActiveSfx(AudioCueIds.Fire) == 0 && audio.CountRegisteredSfx(AudioCueIds.Fire) == 1, "PC 미연결 Virtual은 논리 등록만");
                now = 7.25; audio.PollCompletedVoices();
                report.Check(Math.Abs(audio.VirtualElapsedSeconds(silent) - 7.09) < .0001, "무음에서도 논리 시간 진행");
                audio.SetPauseState(true, true); double pausedAt = audio.VirtualElapsedSeconds(silent);
                now = 20; audio.PollCompletedVoices();
                report.Check(audio.VirtualElapsedSeconds(silent) == pausedAt, "정지 중 가상 시계 동결");
                audio.SetPauseState(false, false);
                listener.transform.position = Vector3.right * 20;
                audio.SetWorldListener(listener.transform);
                report.Check(output.ActiveSourceCount == 0 && audio.CountRegisteredSfx(AudioCueIds.Fire) == 1, "정확히20m는 휴면 등록만 유지");
                listener.transform.position = Vector3.zero; audio.PollCompletedVoices();
                report.Check(output.ActiveSourceCount == 1 && Math.Abs(output.Voices.Values.Single().Seek - (float)(pausedAt % 4)) < .0001, "재진입은 논리 경과시간 modulo Clip 길이 seek 요청");
                int secondLoop = audio.PlaySfxAt(AudioCueIds.Fire, Vector3.zero);
                int thirdLoop = audio.PlaySfxAt(AudioCueIds.Fire, Vector3.zero);
                report.Check(secondLoop != 0 && thirdLoop != 0 && audio.CountActiveSfx(AudioCueIds.Fire) == 3, "동일 ambient 근거리3개 제한 없이 출력");
                audio.SetVolumes(0, 1, 1);
                report.Check(output.ActiveSourceCount == 0 && audio.CountRegisteredSfx(AudioCueIds.Fire) == 3 && audio.CountActiveSfx(AudioCueIds.Fire) == 0, "음소거는 슬롯 반환, 가상 등록은3개 유지");
                now = 22; audio.PollCompletedVoices();
                report.Check(Math.Abs(audio.VirtualElapsedSeconds(silent) - pausedAt - 2) < .0001, "음소거 중 논리 시계 계속");
                int silentStarts = output.Starts.Count;
                audio.PlaySfx(AudioCueIds.Jump); audio.PlaySfx(AudioCueIds.Click);
                report.Check(output.Starts.Count == silentStarts && audio.LogicalSfxVoiceCount == 3, "무음 단발음은 등록/출력/예약 없음");
                audio.SetVolumes(1, 1, 1);
                report.Check(output.ActiveSourceCount == 3 && output.Starts.Count == silentStarts + 3, "음량 복구는 반복음만 재진입");
                audio.StopWorldVoice(secondLoop); audio.StopWorldVoice(thirdLoop);
                int old = audio.PlaySfxAt(AudioCueIds.Jump, Vector3.zero);
                listener.transform.position = Vector3.right * 20; audio.PollCompletedVoices();
                report.Check(!audio.IsWorldVoiceRegistered(old) && output.ActiveSourceCount == 0, "재생 중 원거리 단발음 즉시 완료/반환");
                silentStarts = output.Starts.Count;
                report.Check(audio.PlaySfxAt(AudioCueIds.Jump, Vector3.zero) == 0 && output.Starts.Count == silentStarts, "원거리 신규 단발음 source0");
                listener.transform.position = Vector3.zero; audio.PollCompletedVoices();
                report.Check(output.Starts.Count == silentStarts + 1 && audio.CountActiveSfx(AudioCueIds.Jump) == 0, "거리 복귀 후 늦은 단발음 재생 없음");
                var owner = new GameObject("추적 수명 검사");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(owner, preview);
                int followed = audio.PlaySfxAt(AudioCueIds.Fire, Vector3.zero, owner.transform);
                audio.SetPauseState(true, true); owner.SetActive(false); audio.PollCompletedVoices();
                report.Check(!audio.IsWorldVoiceRegistered(followed), "pause 중 비활성 Follow 해제");
                owner.SetActive(true); audio.SetPauseState(false, false);
                int replacementHandle = audio.PlaySfxAt(AudioCueIds.Fire, Vector3.zero, owner.transform);
                audio.StopWorldVoice(followed);
                report.Check(replacementHandle != followed && audio.IsWorldVoiceRegistered(replacementHandle), "풀 재사용 뒤 오래된 handle은 새 출력에 영향 없음");
                audio.SetPauseState(true, true); UnityEngine.Object.DestroyImmediate(owner); audio.PollCompletedVoices();
                report.Check(!audio.IsWorldVoiceRegistered(replacementHandle), "pause 중 파괴 Follow 검출/해제");
                audio.SetPauseState(false, false);
                audio.SetWorldListener(null);
                report.Check(output.ActiveSourceCount == 0 && audio.CountRegisteredSfx(AudioCueIds.Fire) == 1, "PC 연결 해제는 source 반환/가상등록 유지");
                audio.ClearWorldSfx();
                report.Check(!audio.IsWorldVoiceRegistered(silent) && audio.LogicalSfxVoiceCount == 0, "ClearWorld 가상 등록 반환");
                audio.PlaySfx(AudioCueIds.Fire);
                report.Check(audio.VirtualLoopCount == 0 && audio.RejectedRequests == 1, "Virtual 2D 호출 거절");
                audio.StopAll(); audio.PlaySfx(AudioCueIds.MonsterHit);
                report.Check(audio.CountActiveSfx(AudioCueIds.MonsterHit) == 1, "StopAll이 제한 장부 초기화");
                before = output.Starts.Count; now = 100; audio.PollCompletedVoices();
                report.Check(output.Starts.Count == before, "drop 요청 지연 재생 없음");
            }
            }
            finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(preview); }
            report.Check(output.ActiveSourceCount == 0, "Dispose 출력 반환");
            var retryOutput = new RecordingOutput();
            using (var audio = new AudioService(cues, new Driver(), new ImmediateFade(), _ => {}, realtime: () => 0, sfxOutput: retryOutput))
            {
                audio.PlaySfx(AudioCueIds.Jump); audio.PlaySfx(AudioCueIds.Jump); audio.PlaySfx(AudioCueIds.Click);
                retryOutput.StopFailuresRemaining = 1;
                bool stopFailed = false;
                try { audio.ClearWorldSfx(); }
                catch (AggregateException error)
                {
                    var failures = error.Flatten().InnerExceptions;
                    stopFailed = failures.Count == 1 && failures[0].Message == "검사용 Stop 실패";
                }
                report.Check(stopFailed && audio.CountRegisteredSfx(AudioCueIds.Jump) == 1 && audio.CountActiveSfx(AudioCueIds.Jump) == 1 && retryOutput.ActiveSourceCount == 2,
                    "ClearWorld Stop1회 실패: 예외 보고와 실패 출력의 재시도 소유권 유지, 다른 월드 출력은 정리");
                audio.ClearWorldSfx();
                report.Check(audio.CountRegisteredSfx(AudioCueIds.Jump) == 0 && audio.CountActiveSfx(AudioCueIds.Jump) == 0 && retryOutput.ActiveSourceCount == 1 && audio.CountActiveSfx(AudioCueIds.Click) == 1,
                    "ClearWorld 재시도 성공: 남은 월드 출력 반환, UI 유지");
            }
            bool Reject(Action action) { try { action(); return false; } catch (ArgumentException) { return true; } }
            report.Check(Reject(() => new AudioCue("bad", (AudioCueKind)99, 1)) && Reject(() => new AudioCue((AudioCuePurpose)99, 1)), "unknown Kind/Purpose 거절");
            report.Check(Reject(() => new AudioCue("bad", AudioCueKind.World, 1, playbackPolicy: (SfxPlaybackPolicy)99)), "unknown 정책 거절");
            report.Check(Reject(() => new AudioCue(AudioCuePurpose.Fire, 1, playbackPolicy: SfxPlaybackPolicy.LimitedOneShot)) && Reject(() => new AudioCue(AudioCuePurpose.Fire, 1, playbackPolicy: SfxPlaybackPolicy.AlwaysPlay)), "반복음의 단발 정책 거절");
            report.Check(Reject(() => new AudioCue(AudioCuePurpose.Jump, 1, playbackPolicy: SfxPlaybackPolicy.VirtualLoop)) && Reject(() => new AudioCue(AudioCuePurpose.TitleMusic, 1, playbackPolicy: SfxPlaybackPolicy.VirtualLoop)), "Virtual은 World Loop만");
            report.Check(Reject(() => new AudioCue("bad", AudioCueKind.World, float.NaN)) && Reject(() => new AudioCue("bad", AudioCueKind.World, 1, 0)) && Reject(() => new AudioCue("bad", AudioCueKind.World, 1, 2, float.NaN)), "NaN/잘못된 제한 거절");
            report.Check(Reject(() => new AudioService(new[] { cues[0], cues[0] }, new Driver(), new ImmediateFade(), _ => {})), "중복 ID 거절");
            using (var noOutput = new AudioService(cues, new Driver(), new ImmediateFade(), _ => {}))
            {
                bool rejected = false; try { noOutput.PlaySfx(AudioCueIds.Jump); } catch (InvalidOperationException) { rejected = true; }
                report.Check(rejected, "driver-only SFX fallback 금지");
            }
            var catalog = AssetDatabase.LoadAssetAtPath<AudioCueCatalog>(AudioCueCatalog.AssetPath);
            var settings = catalog.CreateSettings().Where(c => c.Kind != AudioCueKind.Music).ToArray();
            report.Check(settings.Count(c => c.PlaybackPolicy == SfxPlaybackPolicy.AlwaysPlay) == 17 && settings.Count(c => c.PlaybackPolicy == SfxPlaybackPolicy.LimitedOneShot) == 3 && settings.Count(c => c.PlaybackPolicy == SfxPlaybackPolicy.VirtualLoop) == 2, "직렬화 카탈로그 Always17/Limited3/Virtual2");
            report.Check(settings.Where(c => c.PlaybackPolicy == SfxPlaybackPolicy.LimitedOneShot).All(c => c.MaxConcurrent == 2 && c.MinimumInterval == .08f), "Limited 합산2/0.08초");
            foreach (var cue in settings) report.Check(cue.PlaybackPolicy == AudioCueIds.DefaultPolicy(cue.Id), "승인된 분류 " + cue.Id);
            return report;
        }
    }
}
