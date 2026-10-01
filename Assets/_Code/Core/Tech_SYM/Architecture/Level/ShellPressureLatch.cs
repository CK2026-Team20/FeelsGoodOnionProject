using FeelsGoodOnion.TechSYM.Platforms;
using UnityEngine;
namespace Cooked.Level
{
    // Solved by leaving the player's real shell on a real pressure plate and crossing the raised gate.
    public sealed class ShellPressureLatch : MonoBehaviour
    {
        [SerializeField] private PressurePlatePlatform plate;
        [SerializeField] private Collider plateSurface;
        [SerializeField] private GameObject raisedGate;
        [SerializeField] private float crossingProgress;
        [SerializeField] private StageService stage;
        private PlayerFormController form;
        public bool IsSolved { get; private set; }
        public float CrossingProgress => crossingProgress;
        public void Bind(PlayerFacade actor) => form = actor != null ? actor.GetComponent<PlayerFormController>() : null;
        private void FixedUpdate()
        {
            if (IsSolved || form == null || !form.IsSmall || form.OwnedDebris == null || !plate.IsPressed) return;
            var debris = form.OwnedDebris.GetComponent<Collider>();
            var surface = plateSurface.bounds;
            surface.Expand(new Vector3(.1f, .5f, .1f));
            if (debris == null || !surface.Intersects(debris.bounds) || stage.ProgressAt(form.transform.position) < crossingProgress) return;
            IsSolved = true;
            // Door stays open for backtracking to the shell. Plate/puzzle remain stage-scoped.
            raisedGate.SetActive(false);
        }
    }
}

