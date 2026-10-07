using System;
using Unity.Cinemachine;
using UnityEngine;
namespace Cooked.Session.Editor
{
    /// <summary>Temporary API only until T9 removes this helper. Primary builders never depend on it.</summary>
    public static class HmsActorCameraAuthoring
    {
        public const string ReferenceScene=SessionActorBuilder.HmsReferenceScene;
        [Obsolete("Use SessionActorBuilder.AssembleIndependentHmsCamera for non-six independent consumers.")]
        public static GameObject Assemble(GameObject owner,PlayerFacade facade,bool waitForSession)
        {
            if(waitForSession) throw new InvalidOperationException("Integrated authoring uses App output + Core Side/Quarter; no Actor rig is authored.");
            return SessionActorBuilder.AssembleIndependentHmsCamera(owner,facade);
        }
        [Obsolete("Use IntegrationSceneBuilder.ConfigureAppOutput.")]
        public static void ConfigureAppOutput(GameObject output)=>Cooked.Integration.Editor.IntegrationSceneBuilder.ConfigureAppOutput(output);
        [Obsolete("Use LevelAssetBuilder.CreateCoreCameras.")]
        public static CinemachineCamera[] CreateCoreCameras(Transform parent)=>Cooked.Level.Editor.LevelAssetBuilder.CreateCoreCameras(parent);
    }
}
