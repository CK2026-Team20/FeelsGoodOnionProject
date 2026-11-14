using Unity.Profiling;

namespace Cooked.Boids
{
    internal static class BoidProfiler
    {
        internal static readonly ProfilerMarker Animation = new ProfilerMarker("Boids.Animation");
        internal static readonly ProfilerMarker PairDistances = new ProfilerMarker("Boids.PairDistances");
        internal static readonly Unity.Profiling.ProfilerMarker Discovery = new Unity.Profiling.ProfilerMarker("Boids.Discovery");
        internal static readonly ProfilerMarker Flock = new ProfilerMarker("Boids.Flock");
        internal static readonly ProfilerMarker Arc = new ProfilerMarker("Boids.Arc");
        internal static readonly ProfilerMarker Choose = new ProfilerMarker("Boids.Choose");
        internal static readonly ProfilerMarker StaticCast = new ProfilerMarker("Boids.StaticCast");
        internal static readonly ProfilerMarker MovingBox = new ProfilerMarker("Boids.MovingBox");
        internal static readonly ProfilerMarker PathContains = new ProfilerMarker("Boids.PathContains");
        internal static readonly ProfilerMarker Tick = new ProfilerMarker("Boids.Tick");
    }
}
