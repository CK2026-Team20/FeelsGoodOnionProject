using UnityEngine;

namespace Cooked.Chase
{
    /// <summary>Read-only engine boundary. Lifetime and actor ownership remain with the session.</summary>
    public interface IChaseActor
    {
        bool IsAlive { get; }
        Vector3 Position { get; }
    }
}
