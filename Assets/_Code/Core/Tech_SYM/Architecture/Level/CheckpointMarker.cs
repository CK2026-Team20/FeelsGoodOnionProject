using UnityEngine;
namespace Cooked.Level
{
    public sealed class CheckpointMarker : MonoBehaviour
    {
        [SerializeField] private string checkpointId;
        [Tooltip("World-space offset from authored floor marker to ActorBody centre. Level owns this conversion; callers must not add another offset.")]
        [SerializeField] private Vector3 bodyOffset = new Vector3(0f, 1.03f, 0f);
        public string CheckpointId => checkpointId;
        // Marker/beacon stay at the surface. Spawn uses centre=(0,0,0), height=2 normal capsule.
        public Pose Pose => new Pose(transform.position + bodyOffset, transform.rotation);
    }
}
