using System;
namespace Cooked.Cinematics.Editor
{
    /// <summary>Retained menu/API entry only; the primary builder now owns single-artwork authoring.</summary>
    public static class SingleArtworkAuthoring
    {
        [Obsolete("Use CinematicAssetBuilder.BuildAll; no correction pass is required.")]
        public static void Apply() => CinematicAssetBuilder.BuildAll();
    }
}
