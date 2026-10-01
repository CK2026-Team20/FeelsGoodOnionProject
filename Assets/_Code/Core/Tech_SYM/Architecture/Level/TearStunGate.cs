using FeelsGoodOnion.TechSYM.Enemies;
using UnityEngine;
namespace Cooked.Level
{
    // A real HMS tear expenditure and actual enemy stun must coincide. No public solve/test setter.
    public sealed class TearStunGate : MonoBehaviour
    {
        [SerializeField] private EnemyActor enemy;
        [SerializeField] private GameObject barrier;
        private PlayerFacade actor;
        private int lastFragments;
        private float expenditureAt = float.NegativeInfinity;
        public bool IsSolved { get; private set; }
        public void Bind(PlayerFacade value)
        {
            actor = value;
            lastFragments = actor != null ? actor.Model.CurrentSkillFragment : 0;
            expenditureAt = float.NegativeInfinity;
        }
        private void Update()
        {
            if (IsSolved || actor == null || actor.IsDead || enemy == null) return;
            var count = actor.Model.CurrentSkillFragment;
            if (count < lastFragments) expenditureAt = Time.time;
            lastFragments = count;
            if (!enemy.IsStunned || Time.time - expenditureAt > .25f) return;
            IsSolved = true;
            barrier.SetActive(false);
        }
    }
}
