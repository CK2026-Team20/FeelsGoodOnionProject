using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;
namespace Cooked.Level.Editor
{
    /// <summary>Saved registry + actual physics geometry checks; not SessionActorHost Play Mode proof.</summary>
    public static class LevelCheckpointValidation
    {
        public static void ValidateSavedPoses()
        {
            if(Application.isPlaying) throw new InvalidOperationException("Exit Play Mode before validation.");
            for(int i=0;i<SceneManager.sceneCount;i++)
                if(SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save dirty scenes before validation.");
            var feet=new Dictionary<string,Vector3>
            {
                {"S1_START",new Vector3(2,0,0)}, {"S1_SKILL",new Vector3(20,2.3f,0)},
                {"S2_ENTRY",new Vector3(36,0,2)}, {"S2_OVEN",new Vector3(36,0,15)},
                {"S3_ENTRY",new Vector3(34,0,30)}, {"S3_CHASE",new Vector3(20,0,30)}
            };
            var report=new List<string> { "Saved checkpoint registry/normal capsule fixture validation. Actual Session spawn remains integration Play verification." };
            int count=0;
            for(int number=1;number<=3;number++)
            {
                var scene=EditorSceneManager.OpenScene(LevelAssetBuilder.StageRoot+"/03_"+number+"_Stage.unity",OpenSceneMode.Single);
                var roots=scene.GetRootGameObjects();
                var stage=roots.SelectMany(r=>r.GetComponentsInChildren<StageService>()).Single();
                var markers=roots.SelectMany(r=>r.GetComponentsInChildren<CheckpointMarker>(true)).ToArray();
                var obstacles=roots.SelectMany(r=>r.GetComponentsInChildren<Collider>())
                    .Where(c=>c.enabled && !c.isTrigger && c.gameObject.activeInHierarchy).ToArray();
                stage.RebuildRegistry();
                var fixture=new GameObject("CheckpointNormalCapsuleFixture");
                var capsule=fixture.AddComponent<CapsuleCollider>();
                capsule.center=Vector3.zero; capsule.height=2f; capsule.radius=.5f;
                try
                {
                    foreach(var marker in markers)
                    {
                        if(!feet.TryGetValue(marker.CheckpointId,out var foot) || (marker.transform.position-foot).sqrMagnitude>.000001f)
                            throw new InvalidOperationException("Authored foot position changed: "+marker.CheckpointId);
                        if(!stage.TryGetCheckpointPose(marker.CheckpointId,out var pose) || (pose.position-(foot+Vector3.up*1.03f)).sqrMagnitude>.000001f)
                            throw new InvalidOperationException("ActorBody centre mismatch: "+marker.CheckpointId);
                        fixture.transform.SetPositionAndRotation(pose.position,pose.rotation);
                        Physics.SyncTransforms();
                        int penetrations=0;
                        foreach(var obstacle in obstacles)
                        {
                            if(Physics.ComputePenetration(capsule,pose.position,pose.rotation,obstacle,obstacle.transform.position,obstacle.transform.rotation,out _,out float distance) && distance>0f)
                            {
                                penetrations++;
                                report.Add($"FAIL {marker.CheckpointId}: {obstacle.name}, penetration={distance}");
                            }
                        }
                        if(penetrations!=0)
                        {
                            WriteReport(report);
                            throw new InvalidOperationException("Checkpoint capsule penetration: "+marker.CheckpointId);
                        }
                        report.Add($"PASS {marker.CheckpointId}: authored={foot:F3}; returned={pose.position:F3}; surface+1.03; capsule centre0 height2; penetrations=0");
                        count++;
                    }
                }
                finally { Object.DestroyImmediate(fixture); }
                // Discard fixture creation/destruction dirty state, preserving only the saved authoring patch.
                EditorSceneManager.OpenScene(scene.path,OpenSceneMode.Single);
            }
            if(count!=6) throw new InvalidOperationException("Expected six checkpoint checks, found "+count);
            WriteReport(report);
            Debug.Log("[Cooked.Level] Six saved checkpoint poses/capsule fixtures passed; actual Session spawn remains unverified.");
        }
        private static void WriteReport(List<string> report)
        {
            Directory.CreateDirectory("output/Tech_SYM/level/evidence");
            File.WriteAllLines("output/Tech_SYM/level/evidence/checkpoint-pose-validation.txt",report);
        }
    }
}
