#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Cooked.Contracts;
using Cooked.Audio;
using Cooked.Cinematics;
using Cooked.Dialogue;
using UnityEditor;
using UnityEngine;

namespace Cooked.UI.Editor
{
    // Real prefab/domain/Tween/Director/audio execution in one isolated content fixture.
    // No Stage gameplay or full game LOOP claim is made by this fixture.
    public static class ContentPlayVerification
    {
        public static string State { get; private set; } = "Idle";
        public static AudioRuntimeHost Audio;
        public static CinematicHost Cinema;
        public static DialogueService Dialogue;
        private static DialogueViewModel dialogueVm;
        private static GameObject dialogueRoot, backdrop, lightRoot;
        private static readonly Stack<IEnumerator> pending = new Stack<IEnumerator>();
        private static readonly List<string> log = new List<string>();
        private static double deadline;
        private const string Output = "output/Tech_SYM/Content/evidence/native-content.txt";

        public static void Begin()
        {
            if (!Application.isPlaying) throw new InvalidOperationException("Play Mode required");
            Dispose(); UiPlayVerification.Begin(); log.Clear();
            Audio = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Cooked.Audio.Editor.AudioPrefabBuilder.PrefabPath)).GetComponent<AudioRuntimeHost>();
            Audio.CreateService();
            var camera=Camera.main;if(camera.GetComponent<AudioListener>()==null)camera.gameObject.AddComponent<AudioListener>();
            UiPlayVerification.Options.Dispose();UiPlayVerification.Settings.Dispose();
            UiPlayVerification.Settings=new SettingsService(new MemoryStore(),Audio.Service);
            UiPlayVerification.Options=new OptionsService(UiPlayVerification.Flow,UiPlayVerification.Control,UiPlayVerification.Settings);
            UiPlayVerification.Global.Bind(UiPlayVerification.Options,UiPlayVerification.Settings,UiPlayVerification.Fade);
            UiPlayVerification.Title.Bind(UiPlayVerification.Flow,UiPlayVerification.Options);
            Audio.BindFlow(UiPlayVerification.Flow,UiPlayVerification.Control,new Dictionary<string,string>{{"03_1_Stage",AudioCueIds.Stage1},{"03_2_Stage",AudioCueIds.Stage2},{"03_3_Stage",AudioCueIds.Stage3}});
            Cinema=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Cooked.Cinematics.Editor.CinematicAssetBuilder.PrefabPath),UiPlayVerification.Game.CinematicMount,false).GetComponent<CinematicHost>();
            Cinema.Initialize(UiPlayVerification.Control);
            var table=new DialogueTableService("shipped DialogueTable.json");
            table.LoadJson(File.ReadAllText("Assets/_Scenes/Tech_SYM/Architecture/Dialogue/Data/DialogueTable.json"));
            Dialogue=new DialogueService(table,UiPlayVerification.Control,new DOTweenDialogueDelayScheduler());dialogueVm=new DialogueViewModel(Dialogue);
            dialogueRoot=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Cooked.Dialogue.Editor.DialoguePrefabBuilder.PrefabPath),UiPlayVerification.Game.DialogueMount,false);
            dialogueRoot.GetComponent<DialogueView>().Bind(dialogueVm);
            backdrop=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Cooked.Background.Editor.KitchenContentBackdropBuilder.PrefabPath));
            lightRoot=new GameObject("ContentPreviewLight");var light=lightRoot.AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.transform.rotation=Quaternion.Euler(45,-30,0);
            CameraForStage(1);
            State="Running";deadline=EditorApplication.timeSinceStartup+150;pending.Push(Scenario());EditorApplication.update+=Step;
        }
        private static void Step()
        {
            try
            {
                if(EditorApplication.timeSinceStartup>deadline)throw new TimeoutException("Content native tests exceeded 150 seconds");
                if(pending.Count==0){EditorApplication.update-=Step;State="Passed";Write();return;}
                var top=pending.Peek();
                if(!top.MoveNext()){(top as IDisposable)?.Dispose();pending.Pop();return;}
                if(top.Current is IEnumerator nested)pending.Push(nested);
            }
            catch(Exception e){EditorApplication.update-=Step;State="Failed";log.Add(e.ToString());while(pending.Count>0)(pending.Pop() as IDisposable)?.Dispose();Write();Debug.LogException(e);}
        }
        private static IEnumerator Scenario()
        {
            yield return new WaitForSecondsRealtime(.5f);
            Check(UiPlayVerification.Fade.Alpha==0,"Production Fade reveals real content fixture");
            Cooked.Audio.Editor.AudioTweenVerification.Run();
            Check(Audio.Service.CaptureVoices().Any(v=>v.CueId==AudioCueIds.Title),"Title flow selects real music clip");
            float rms=0;float audioDeadline=Time.realtimeSinceStartup+5;while(Time.realtimeSinceStartup<audioDeadline && rms<=.00001f){rms=Mathf.Max(rms,Audio.MeasureOutputRms(0),Audio.MeasureOutputRms(1));yield return null;}
            Check(rms>0.00001f,"AudioSource native output RMS="+rms);
            UiPlayVerification.Options.Open();
            Check(UiPlayVerification.Options.IsOpen && UiPlayVerification.Control.Count==0,"Title Options never pauses world or presentation");
            UiPlayVerification.Settings.SetVolumes(1,.35f,1);yield return new WaitForSecondsRealtime(.25f);
            Check(Audio.Service.CaptureVoices().Where(v=>v.Channel<2&&v.CueId!=null).All(v=>!v.Paused),"Title music continues during live volume adjustment");
            UiPlayVerification.Options.Close();
            var cinema=(CinematicService)Cinema.Service;
            foreach(var id in new[]{CinematicId.Opening,CinematicId.Ending})
            {
                UiPlayVerification.Flow.Emit(id==CinematicId.Opening?FlowState.Opening:FlowState.Ending,true);
                var prepare=new OperationResult();yield return cinema.Prepare(id,CancellationToken.None,prepare);
                Check(prepare.Status==OperationStatus.Succeeded && cinema.Time==0,id+" first frame prepared at time zero");
                CaptureComic(id,1);
                var result=new OperationResult();var play=cinema.Play(CancellationToken.None,result);play.MoveNext();
                yield return new WaitForSecondsRealtime(.4f);double pausedAt=cinema.Time;
                UiPlayVerification.Options.Open();yield return new WaitForSecondsRealtime(.4f);
                Check(UiPlayVerification.Options.IsOpen&&Math.Abs(cinema.Time-pausedAt)<.00001,id+" actual Options pauses native Timeline");
                Check(Audio.Service.CaptureVoices().Where(v=>v.Channel<2&&v.CueId!=null).All(v=>!v.Paused),"Options keeps real music voices audible");
                UiPlayVerification.Options.Close();
                int captured=1;
                while(play.MoveNext())
                {
                    int count=cinema.Presentation.Frame.RevealedCount;
                    if(count>captured && cinema.Presentation.Frame[count-1].Opacity>=.99f)
                    { CaptureComic(id,count); captured=count; }
                    yield return null;
                }
                Check(captured==4,id+" four cumulative native playback screenshots");
                Check(result.Status==OperationStatus.Succeeded&&cinema.Outcome==CinematicOutcome.Completed,id+" native 11.1s playback completes and holds last cut");
                cinema.Hide();
            }
            var ready=new OperationResult();yield return cinema.Prepare(CinematicId.Opening,CancellationToken.None,ready);
            var skip=new OperationResult();var skipping=cinema.Play(CancellationToken.None,skip);skipping.MoveNext();
            Cinema.GetComponentInChildren<UnityEngine.UI.Button>().onClick.Invoke();yield return skipping;
            Check(skip.Status==OperationStatus.Succeeded&&cinema.Outcome==CinematicOutcome.Skipped,"Cinematic VM-bound SKIP reaches distinct Skipped outcome");cinema.Hide();
            UiPlayVerification.Playing();Audio.Binding.SetStage("03_1_Stage");
            Check(Dialogue.TryStart("EXPLORE_OVEN_01"),"Shipped dialogue JSON starts by Dialogue_Code");
            yield return new WaitForSecondsRealtime(.4f);
            var text=dialogueRoot.GetComponentsInChildren<TMPro.TMP_Text>(true).Single(t=>t.name=="Context");
            Check(text.text.Length>0&&text.text!=dialogueVm.Context,"Real TMP DOText shows partial Korean line");
            UiPlayVerification.Options.Open();string frozen=text.text;yield return new WaitForSecondsRealtime(.4f);
            Check(text.text==frozen,"Real Options pauses Dialogue typing");UiPlayVerification.Options.Close();
            Dialogue.Advance(Time.frameCount);Check(text.text==dialogueVm.Context,"Panel command completes current full line");
            Dialogue.SetAuto(true);Dialogue.OpenLog();Check(!Dialogue.AutoEnabled&&Dialogue.LogOpen,"Log disables automatic progression");Dialogue.CloseLog();Dialogue.SkipAll();
            Check(!Dialogue.IsActive&&UiPlayVerification.Control.Count==0,"Whole-trigger SKIP releases only Dialogue input lease");
            yield return Cooked.Dialogue.Tests.DialogueTweenRegressionTests.Run(AssetDatabase.LoadAssetAtPath<GameObject>(Cooked.Dialogue.Editor.DialoguePrefabBuilder.PrefabPath),UiPlayVerification.Game.DialogueMount,s=>{log.Add(s);Write();});
            Audio.Service.PlaySfx(AudioCueIds.Pickup);yield return new WaitForSecondsRealtime(.1f);
            Check(Audio.Service.CaptureVoices().Any(v=>v.CueId==AudioCueIds.Pickup),"Real pickup SFX occupies a world voice");
            ShowHud();Check(true,"Content native checks completed; full Stage LOOP belongs to Core integration");
        }
        private static void CaptureComic(CinematicId id,int count)
        {
            var frame=((CinematicService)Cinema.Service).Presentation.Frame;
            Check(frame.RevealedCount==count,id+" cumulative panel count "+count);
            CaptureScreen(id+"-comic-"+count);
        }
        public static void CaptureScreen(string name)
        {
            var type=typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            var window=EditorWindow.GetWindow(type);window.position=new Rect(0,0,1920,1101);
            var render=type.GetMethod("RenderView",System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            Canvas.ForceUpdateCanvases();render.Invoke(window,new object[]{new Vector2(1920,1080),false});
            Canvas.ForceUpdateCanvases();render.Invoke(window,new object[]{new Vector2(1920,1080),false});
            var assembly=AppDomain.CurrentDomain.GetAssemblies().First(a=>a.GetType("Unity.Pipeline.Editor.Commands.Capture.CaptureCommands")!=null);
            var capture=assembly.GetType("Unity.Pipeline.Editor.Commands.Capture.CaptureCommands").GetMethod("CaptureGameView");
            var value=capture.Invoke(null,new object[]{1920,1080,null,null,true,0,"screen"});
            File.WriteAllBytes("output/Tech_SYM/Content/evidence/"+name+".png",Convert.FromBase64String((string)value.GetType().GetProperty("Base64").GetValue(value)));
        }
        public static void ShowHud(){Dialogue?.Cancel();((CinematicService)Cinema.Service).Hide();UiPlayVerification.Options.Close();UiPlayVerification.Playing();}
        public static void ShowDialogue(){ShowHud();Dialogue.TryStart("EXPLORE_OVEN_01");Dialogue.Advance(Time.frameCount);}
        public static void CameraForStage(int stage)
        {
            var camera=Camera.main;camera.orthographic=false;camera.fieldOfView=50;camera.farClipPlane=200;
            // Read-only match to Core LevelAssetBuilder camera zones (actor camera anchor at y1).
            Vector3 target=stage==1?new Vector3(18,1,0):stage==2?new Vector3(36,1,15):new Vector3(14,1,30);
            Vector3 offset=stage==1?new Vector3(1,3,-12):stage==2?new Vector3(-10,12,-7):new Vector3(-1,3,12);
            camera.transform.position=target+offset;
            camera.transform.LookAt(target+new Vector3(stage==3?-2:2,0,0));camera.backgroundColor=new Color(.38f,.29f,.21f);
        }
        private static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);log.Add("PASS: "+message);Write();}
        private static void Write(){Directory.CreateDirectory(Path.GetDirectoryName(Output));File.WriteAllLines(Output,new[]{State}.Concat(log));}
        public static void Dispose()
        {
            EditorApplication.update-=Step;while(pending.Count>0)(pending.Pop() as IDisposable)?.Dispose();
            if(dialogueRoot!=null)dialogueRoot.GetComponent<DialogueView>().Unbind();dialogueVm?.Dispose();Dialogue?.Dispose();Dialogue=null;dialogueVm=null;
            Cinema?.Shutdown();Audio?.Shutdown();
            foreach(var obj in new[]{dialogueRoot,backdrop,lightRoot,Cinema==null?null:Cinema.gameObject,Audio==null?null:Audio.gameObject})if(obj!=null)UnityEngine.Object.Destroy(obj);
            Cinema=null;Audio=null;UiPlayVerification.Dispose();State="Idle";
        }
        private sealed class MemoryStore:ISettingsStore{public float Read(string key,float fallback)=>fallback;public void Write(string key,float value){}public void Save(){}}
    }
}
#endif
