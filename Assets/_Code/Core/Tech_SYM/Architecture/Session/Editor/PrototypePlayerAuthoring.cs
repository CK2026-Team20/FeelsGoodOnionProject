using System;
using UnityEditor;
namespace Cooked.Session.Editor
{
    /// <summary>Obsolete correction entry, retained until main/T9 removes its menu/API consumers.</summary>
    public static class PrototypePlayerAuthoring
    {
        public static string AnimationPath=>SessionActorBuilder.AssetRoot+"/PrototypePlayerAnimations.asset";
        [MenuItem("Cooked/Session/Apply Prototype Player Connections")]
        [Obsolete("SessionActorBuilder.Build includes presentation and skill settings before first save.")]
        public static void ApplyOwnedActor()=>Reject();
        [Obsolete("Use primary SessionActorBuilder; existing prefab migration belongs to main/T9.")]
        public static void Apply(string prefabPath)=>Reject();
        private static void Reject()=>throw new InvalidOperationException("No correction pass is needed for fresh authoring. Existing native migration belongs to main/T9.");
    }
}
