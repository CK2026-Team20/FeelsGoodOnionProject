using System;
using System.Collections.Generic;
using Cooked.Audio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cooked.Validation
{
    public static class AudioSfxLimitChecks
    {
        [Serializable] public sealed class Report { public int passed, failed; public List<string> cases = new List<string>(); }
        private sealed class Driver : IAudioPlaybackDriver
        {
            public int starts;
            public readonly HashSet<int> active = new HashSet<int>();
            public void Start(int channel, string cueId, bool loop) { starts++; active.Add(channel); }
            public void Stop(int channel, bool allowDestroyedSource = false) => active.Remove(channel);
            public void SetGain(int channel, float gain) { }
            public void SetPaused(int channel, bool paused) { }
            public bool IsPlaying(int channel) => active.Contains(channel);
            public string ResolveMusicId(AudioClip clip) => clip.name;
            public bool IsSameClip(string a, string b) => a != null && a == b;
        }
        private sealed class Fade : IMusicCrossfadeDriver
        {
            public void Start(float a,float b,float c,float d,float seconds,bool paused,Action<float,float> apply,Action completed) { apply(b,d);completed(); }
            public void SetPaused(bool paused) { }
            public void Cancel() { }
            public void Dispose() { }
        }
        public static Report Run()
        {
            var report=new Report();
            void Check(bool ok,string label) { if(ok) report.passed++;else report.failed++;report.cases.Add((ok?"P ":"F ")+label); }
            var driver=new Driver(); double now=0;
            var a=new AudioCue("test.a",AudioCueKind.World,1);
            var b=new AudioCue("test.b",AudioCueKind.World,1,1,0);
            using(var audio=new AudioService(new[]{a,b,new AudioCue("test.music",AudioCueKind.Music,1)},driver,new Fade(),_=>{},realtime:()=>now))
            {
                audio.PlaySfx(a.Id);audio.PlaySfx(a.Id);audio.PlaySfx(a.Id);
                Check(driver.starts==1,"same-frame burst accepts only one");
                now=.079;audio.PlaySfx(a.Id);Check(driver.starts==1,"before 80ms ignored");
                now=.08;audio.PlaySfx(a.Id);Check(driver.starts==2,"at 80ms second voice accepted; rejection did not extend interval");
                now=.16;audio.PlaySfx(a.Id);Check(driver.starts==2,"two active voices reject third after interval");
                driver.active.Remove(AudioService.FirstWorldChannel);audio.PlaySfx(a.Id);
                Check(driver.starts==3,"completed voice frees capacity without cooldown from rejected request");
                audio.PlaySfx(b.Id);Check(driver.starts==4,"different ID has independent limit");
                audio.PlaySfx(b.Id);Check(driver.starts==4,"per-cue maximum one enforced with zero interval");
                now=.3;audio.SetPauseState(true,false);audio.PlaySfx(a.Id);
                Check(driver.starts==4,"world pause still blocks requests");
                audio.SetPauseState(false,false);audio.ClearWorldSfx();audio.PlaySfx(a.Id);
                Check(driver.starts==5,"clear releases active capacity");
                now=10;audio.PollCompletedVoices();Check(driver.starts==5,"ignored requests never replay later");
                audio.PlayMusic("test.music");audio.PlayMusic("test.music");
                Check(driver.starts==6&&audio.CurrentMusicId=="test.music","BGM identity preservation unchanged");
                audio.StopAll();audio.PlaySfx(a.Id);Check(driver.starts==7,"StopAll resets playback limits");
            }
            bool badCount=false,badInterval=false;
            try {new AudioCue("bad",AudioCueKind.World,1,0,0);}catch(ArgumentOutOfRangeException){badCount=true;}
            try {new AudioCue("bad",AudioCueKind.World,1,2,float.NaN);}catch(ArgumentOutOfRangeException){badInterval=true;}
            Check(badCount&&badInterval,"invalid limit settings rejected");
            var catalog=AssetDatabase.LoadAssetAtPath<AudioCueCatalog>(AudioCueCatalog.AssetPath);
            foreach(var cue in catalog.CreateSettings())
                if(cue.Kind!=AudioCueKind.Music) Check(cue.MaxConcurrent==2&&cue.MinimumInterval==.08f,"catalog defaults "+cue.Id);
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                var root=new GameObject("Sfx limit output test");SceneManager.MoveGameObjectToScene(root,scene);
                var spatial=new WorldAudioOutput(root.transform,catalog);
                // Paused native voices retain their slots even though AudioSource.isPlaying is false.
                spatial.SetState(1,true);
                driver=new Driver();now=0;
                using(var audio=new AudioService(catalog.CreateSettings(),driver,new Fade(),_=>{},worldOutput:spatial,realtime:()=>now))
                {
                    int first=audio.PlaySfxAt(AudioCueIds.MonsterHit,Vector3.zero);
                    Check(first!=0&&audio.PlaySfxAt(AudioCueIds.MonsterHit,Vector3.right)==0,"spatial burst suppressed globally across positions");
                    now=.08;audio.PlaySfx(AudioCueIds.MonsterHit);
                    Check(driver.starts==1,"2D and spatial paths share interval and capacity");
                    now=.16;Check(audio.PlaySfxAt(AudioCueIds.MonsterHit,Vector3.left)==0,"mixed paths enforce maximum two including paused native voice");
                    audio.StopWorldVoice(first);
                    int next=audio.PlaySfxAt(AudioCueIds.MonsterHit,Vector3.left);
                    Check(next!=0,"explicit stop releases spatial slot immediately");
                    audio.ClearWorldSfx();now=.24;
                    Check(audio.PlaySfxAt(AudioCueIds.MonsterHit,Vector3.zero)!=0,"world clear releases both output paths");
                }
            }
            finally {EditorSceneManager.ClosePreviewScene(scene);}
            return report;
        }
    }
}
