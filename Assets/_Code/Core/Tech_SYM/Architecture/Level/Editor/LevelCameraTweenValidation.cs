using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Cooked.Contracts;
using DG.Tweening;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Cooked.Level.Editor
{
    /// <summary>Run via the integration Play Mode coroutine host after PM grants an Editor slot.
    /// Uses actual scaled DOTween updates; creates temporary objects without saving or opening scenes.</summary>
    public static class LevelCameraTweenValidation
    {
        public static IEnumerator RunInPlayMode()
        {
            if(!Application.isPlaying) throw new InvalidOperationException("This validation requires real Play Mode.");
            float previousScale=Time.timeScale;
            var root=new GameObject("LevelCameraTweenValidationFixture");
            var target=new GameObject("Target").transform; target.SetParent(root.transform);
            var rig=new GameObject("Rig").transform; rig.SetParent(root.transform);
            var service=root.AddComponent<LevelCameraService>();
            var bridge=new TestBridge(target);
            var zones=new[]
            {
                new CameraZoneData { Id="SIDE",MinimumProgress=-100,MaximumProgress=10,Offset=new Vector3(0,2,-10),LookOffset=Vector3.up },
                new CameraZoneData { Id="QUARTER",MinimumProgress=10,MaximumProgress=100,AllowDepth=true,Offset=new Vector3(-4,8,-8),LookOffset=Vector3.up*2 }
            };
            var results=new List<string>();
            Set(service,"cameraRig",rig); Set(service,"zones",zones); Set(service,"blendSeconds",.6f);
            try
            {
                Time.timeScale=1;
                service.Bind(bridge);
                Require(Get<Sequence>(service,"zoneBlend")==null && (rig.position-target.position-zones[0].Offset).sqrMagnitude<.000001f,"initial bind snaps without blend",results);
                target.position=new Vector3(12,0,0); Tick(service);
                var first=Get<Sequence>(service,"zoneBlend");
                Require(first!=null && first.IsActive(),"boundary creates finite blend",results);
                for(int i=0;i<10;i++) Tick(service);
                Require(ReferenceEquals(first,Get<Sequence>(service,"zoneBlend")) && bridge.DepthChanges==2,"same-zone frames retain one handle and one depth command",results);
                yield return new WaitForSecondsRealtime(.1f);
                Time.timeScale=0;
                float pausedAt=first.Elapsed(); var pausedOffset=Get<Vector3>(service,"offset");
                yield return new WaitForSecondsRealtime(.15f);
                Require(Mathf.Abs(first.Elapsed()-pausedAt)<.00001f && (Get<Vector3>(service,"offset")-pausedOffset).sqrMagnitude<.000001f,"options timeScale0 freezes scaled blend",results);
                Time.timeScale=1;
                yield return null;
                Require(first.Elapsed()>=pausedAt && first.Elapsed()>0,"options resume preserves elapsed progress",results);
                service.enabled=false;
                float disabledAt=first.Elapsed();
                yield return new WaitForSecondsRealtime(.1f);
                Require(Mathf.Abs(first.Elapsed()-disabledAt)<.00001f,"disable pauses owned handle",results);
                service.enabled=true;
                Require(ReferenceEquals(first,Get<Sequence>(service,"zoneBlend")),"enable resumes same handle",results);
                var beforeReverse=Get<Vector3>(service,"offset");
                target.position=Vector3.zero; Tick(service);
                var reverse=Get<Sequence>(service,"zoneBlend");
                Require(!first.IsActive() && reverse!=null && !ReferenceEquals(first,reverse) && (Get<Vector3>(service,"offset")-beforeReverse).sqrMagnitude<.000001f,"reversal cancels old blend without snapping current offset",results);
                yield return new WaitForSecondsRealtime(.75f);
                Tick(service);
                Require((Get<Vector3>(service,"offset")-zones[0].Offset).sqrMagnitude<.000001f && (Get<Vector3>(service,"lookOffset")-zones[0].LookOffset).sqrMagnitude<.000001f,"finite blend reaches both endpoints",results);
                target.position=new Vector3(1,1,0); Tick(service);
                Require((rig.position-target.position-zones[0].Offset).sqrMagnitude<.000001f,"LateUpdate keeps tracking moving target after blend",results);
                target.position=new Vector3(12,0,0); Tick(service);
                var replaced=Get<Sequence>(service,"zoneBlend");
                service.Bind(bridge);
                Require(!replaced.IsActive() && Get<Sequence>(service,"zoneBlend")==null,"rebind cancels only its previous tween and snaps",results);
                yield return new WaitForSecondsRealtime(.1f);
                Require((Get<Vector3>(service,"offset")-zones[1].Offset).sqrMagnitude<.000001f,"cancelled tween cannot write old offset after rebind",results);
                target.position=Vector3.zero; Tick(service);
                var detached=Get<Sequence>(service,"zoneBlend"); bridge.Detach(); Tick(service);
                Require(!detached.IsActive() && service.CurrentZoneId==null,"actor detach cancels blend",results);
                bridge.HasActor=true; service.Bind(bridge); target.position=new Vector3(12,0,0); Tick(service);
                var disposed=Get<Sequence>(service,"zoneBlend"); service.Dispose(); service.Dispose();
                Require(!disposed.IsActive() && Get<Sequence>(service,"zoneBlend")==null,"Dispose is repeat-safe and releases owned handle",results);
                yield return new WaitForSecondsRealtime(.1f);
                Directory.CreateDirectory("output/Tech_SYM/level/evidence");
                File.WriteAllLines("output/Tech_SYM/level/evidence/camera-tween-play-validation.txt",results);
            }
            finally
            {
                service.Dispose(); Time.timeScale=previousScale; Object.Destroy(root);
            }
        }
        private static void Require(bool passed,string title,List<string> results)
        { if(!passed) throw new InvalidOperationException(title); results.Add("PASS: "+title); }
        private static void Tick(LevelCameraService service) => typeof(LevelCameraService).GetMethod("LateUpdate",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(service,null);
        private static T Get<T>(LevelCameraService service,string field) => (T)typeof(LevelCameraService).GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(service);
        private static void Set(LevelCameraService service,string field,object value) => typeof(LevelCameraService).GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(service,value);
        private sealed class TestBridge : IPlayerBridgeService
        {
            public TestBridge(Transform target) { CameraTarget=target; HasActor=true; }
            public bool HasActor { get; set; }
            public int DepthChanges { get; private set; }
            public PlayerSnapshot Snapshot => new PlayerSnapshot(1,1,0,1);
            public Transform CameraTarget { get; }
            public event Action<PlayerSnapshot> Changed { add { } remove { } }
            public void Attach(PlayerFacade actor) => throw new NotSupportedException();
            public void Detach() => HasActor=false;
            public void RestoreFragments(int fragments) => throw new NotSupportedException();
            public void SetDepthMovementAllowed(bool allowed) => DepthChanges++;
            public void Dispose() { }
        }
    }
}
