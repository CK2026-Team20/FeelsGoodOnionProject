using UnityEngine;

namespace Cooked.Session
{
    /// <summary>Fallback death plane for genuine platform falls; receives the serialized physical body.</summary>
    public sealed class ActorFallGuard : MonoBehaviour
    {
        [SerializeField] private PlayerFacade actor;
        [SerializeField] private float killHeight = -10f;
        private void FixedUpdate()
        {
            if (actor != null && !actor.IsDead && actor.transform.position.y < killHeight)
                actor.Damage(actor.Model.MaxHP);
        }
    }
}
