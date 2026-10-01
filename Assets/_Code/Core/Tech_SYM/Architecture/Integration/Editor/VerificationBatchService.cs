#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cooked.Contracts;
using Cooked.Foundation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cooked.Integration.Verification.Editor
{
    /// <summary>One command starts the run only after EnteredPlayMode. Entry routing is exercised by
    /// opening each real saved scene before Play, never by assigning a runtime destination.</summary>
    [InitializeOnLoad]
    public static class VerificationBatchService
    {
        private const string Key = "Cooked.Integration.Verify.";
        static VerificationBatchService()
        {
            EditorApplication.playModeStateChanged += OnMode;
            EditorApplication.update += Tick;
        }
        [MenuItem("Cooked/Verification/Run Two Games Through Input")]
        public static void RunFullLoop() => Begin("full");
        [MenuItem("Cooked/Verification/Run All Eleven Editor Entries")]
        public static void RunAllEntries() => Begin("entries");
        private static string[] Entries()
        {
            var catalog = Integration.Editor.IntegrationSceneBuilder.Catalog;
            return new[]{ catalog.BootstrapPath, catalog.TitlePath, catalog.CorePath }
                .Concat(catalog.StagePaths).Concat(new[]{
                    "Assets/_Scenes/Tech_SYM/01_BootStrapper.unity", "Assets/_Scenes/Tech_SYM/02_Title.unity",
                    "Assets/_Scenes/Tech_SYM/03_InGame.unity", "Assets/_Scenes/Tech_SYM/01_BrokenablePlatformTest.unity",
                    "Assets/_Scenes/Tech_SYM/02_MultiFuncPlatformTest.unity" }).ToArray();
        }
        private static void Begin(string mode)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("Start batch from stable Edit Mode; no live run is interrupted.");
            if (SessionState.GetBool(Key+"Active",false)) throw new InvalidOperationException("Verification batch already active.");
            foreach(var scene in Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt))
                if(scene.isDirty) throw new InvalidOperationException("Save owned scene changes before verification.");
            foreach(var path in Entries())
                if(AssetDatabase.LoadAssetAtPath<SceneAsset>(path)==null) throw new InvalidOperationException("Missing entry scene: "+path);
            SessionState.SetString(Key+"OriginalScene",SceneManager.GetActiveScene().path);
            SessionState.SetString(Key+"Mode",mode); SessionState.SetInt(Key+"Index",0);
            SessionState.SetString(Key+"Results","[]"); SessionState.SetBool(Key+"Active",true);
            OpenNext();
        }
        private static void OpenNext()
        {
            int index=SessionState.GetInt(Key+"Index",0);
            string path=Entries()[SessionState.GetString(Key+"Mode","")=="entries" ? index : 0];
            EditorSceneManager.OpenScene(path,OpenSceneMode.Single);
            SessionState.SetString(Key+"Entry",path);
            SessionState.SetString(Key+"Phase","entering");
            SessionState.SetString(Key+"Deadline",DateTime.UtcNow.AddSeconds(60).ToString("O"));
            EditorApplication.EnterPlaymode();
        }
        private static void OnMode(PlayModeStateChange value)
        {
            if(!SessionState.GetBool(Key+"Active",false)) return;
            if(value==PlayModeStateChange.EnteredPlayMode)
            {
                SessionState.SetString(Key+"Phase","ready");
                SessionState.SetString(Key+"Deadline",DateTime.UtcNow.AddSeconds(60).ToString("O"));
            }
            if(value==PlayModeStateChange.EnteredEditMode)
            {
                string phase=SessionState.GetString(Key+"Phase","");
                if(phase!="stopping") { Fail("Play Mode stopped before verification completed."); return; }
                if(SessionState.GetBool(Key+"Failed",false)) { End(); return; }
                int index=SessionState.GetInt(Key+"Index",0)+1;
                SessionState.SetInt(Key+"Index",index);
                if(SessionState.GetString(Key+"Mode","")=="entries" && index<Entries().Length)
                    EditorApplication.delayCall+=OpenNext;
                else End();
            }
        }
        private static void Tick()
        {
            if(!SessionState.GetBool(Key+"Active",false)) return;
            string phase=SessionState.GetString(Key+"Phase","");
            if(phase=="ready" && EditorApplication.isPlaying && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                // Do not run from the EnteredPlayMode callback itself; defer until the next stable editor tick.
                try
                {
                    LoopVerificationHost.Begin(SessionState.GetString(Key+"Mode","")=="entries"?"entry":"full");
                    SessionState.SetString(Key+"Phase","running");
                    SessionState.SetString(Key+"Deadline",DateTime.UtcNow.AddMinutes(20).ToString("O"));
                }
                catch(Exception e) { Fail(e.ToString()); }
            }
            else if(phase=="running" && LoopVerificationHost.Active != null && LoopVerificationHost.Active.Finished)
            {
                var report=LoopVerificationHost.Active.Result;
                string error=report.outcome=="Passed"?null:report.failure;
                if(error==null && SessionState.GetString(Key+"Mode","")=="entries")
                {
                    var route=BootstrapEntryRoute.Resolve(SessionState.GetString(Key+"Entry",""),Integration.Editor.IntegrationSceneBuilder.Catalog);
                    var last=report.observations.Last();
                    if(last.flow!=(route.DirectGame?"Playing":"Title")) error="Entry routed to wrong flow: "+last.flow;
                    if(route.DirectGame && !last.checkpoint.StartsWith(route.StageId=="03_2_Stage"?"S2_":route.StageId=="03_3_Stage"?"S3_":"S1_"))
                        error="Entry checkpoint does not match route: "+last.checkpoint;
                }
                WriteEntry(report,error);
                SessionState.SetBool(Key+"Failed",error!=null);
                SessionState.SetString(Key+"Phase","stopping");
                EditorApplication.ExitPlaymode();
            }
            else if(DateTime.TryParse(SessionState.GetString(Key+"Deadline",""),out var deadline) && DateTime.UtcNow>deadline.ToUniversalTime())
                Fail("Batch timeout in phase "+phase);
        }
        private static void WriteEntry(LoopVerificationHost.Report report,string failure)
        {
            string dir="output/Tech_SYM/integration/evidence/sequential"; Directory.CreateDirectory(dir);
            string name=SessionState.GetString(Key+"Mode","")+"-entry-"+SessionState.GetInt(Key+"Index",0)+".json";
            if(failure!=null) {report.outcome="Failed";report.failure=failure;}
            File.WriteAllText(Path.Combine(dir,name),JsonUtility.ToJson(report,true));
        }
        private static void Fail(string reason)
        {
            SessionState.SetBool(Key+"Failed",true);
            WriteEntry(new LoopVerificationHost.Report{scenario=SessionState.GetString(Key+"Entry",""),outcome="Failed",failure=reason},reason);
            SessionState.SetString(Key+"Phase","stopping");
            if(EditorApplication.isPlayingOrWillChangePlaymode) EditorApplication.ExitPlaymode(); else End();
        }
        private static void End()
        {
            bool failed=SessionState.GetBool(Key+"Failed",false);
            SessionState.SetBool(Key+"Active",false); SessionState.SetBool(Key+"Failed",false);
            string original=SessionState.GetString(Key+"OriginalScene","");
            if(!string.IsNullOrEmpty(original)) EditorSceneManager.OpenScene(original,OpenSceneMode.Single);
            Debug.Log("Cooked input/entry verification batch finished; failed="+failed+". See JSON results, not this log, for evidence.");
        }
    }
}
#endif
