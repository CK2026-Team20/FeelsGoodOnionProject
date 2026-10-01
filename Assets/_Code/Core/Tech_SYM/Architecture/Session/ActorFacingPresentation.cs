using UnityEngine;

namespace Cooked.Session
{
    /// <summary>Presentation only: follows HMS facing without rotating the physics body.</summary>
    public sealed class ActorFacingPresentation : MonoBehaviour
    {
        [SerializeField] private CharacterMovement movement;
        [SerializeField] private Transform visualRoot;

        private void LateUpdate()
        {
            if (movement == null || visualRoot == null) return;
            Vector3 direction = movement.FacingDirection;
            direction.y = 0;
            if (direction.sqrMagnitude > .001f)
                visualRoot.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }
    }
}
