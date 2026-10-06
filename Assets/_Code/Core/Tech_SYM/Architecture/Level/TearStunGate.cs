using FeelsGoodOnion.TechSYM.Enemies;
using UnityEngine;
namespace Cooked.Level
{
    // Serialization compatibility for preserved historical scenes only.
    // Remove after those scenes are explicitly migrated; new prototypes never author this component.
    public sealed class TearStunGate : MonoBehaviour
    {
        [Tooltip("폐기된 눈물 문 연결의 이전 적 참조입니다. 현재 초기화에서 이 기믹을 종료하며 적 기절로 문을 열지 않습니다.")]
        [SerializeField] private EnemyActor enemy;
        [Tooltip("폐기된 눈물 문 연결의 이전 장애물 참조입니다. 현재 초기화에서 장애물을 비활성화하며 새 프로토타입에 배치하지 않습니다.")]
        [SerializeField] private GameObject barrier;
        private PlayerFacade actor;
        private int lastFragments;
        private float expenditureAt = float.NegativeInfinity;
        public bool IsSolved { get; private set; }
        public void RetireLegacyGate()
        { if (barrier != null) barrier.SetActive(false); actor = null; enabled = false; }
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
