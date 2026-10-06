using FeelsGoodOnion.TechSYM.Platforms;
using UnityEngine;
namespace Cooked.Level
{
    // Solved by leaving the player's real shell on a real pressure plate and crossing the raised gate.
    public sealed class ShellPressureLatch : MonoBehaviour
    {
        [Tooltip("양파껍질의 눌림 상태를 확인할 압력 발판입니다.")]
        [SerializeField] private PressurePlatePlatform plate;
        [Tooltip("실제 껍질이 발판 위에 있는지 확인할 표면 Collider입니다. 발판 외형 대신 충돌 표면을 연결하세요.")]
        [SerializeField] private Collider plateSurface;
        [Tooltip("껍질로 발판을 누르고 통과 조건을 만족하면 비활성화할 문 오브젝트입니다.")]
        [SerializeField] private GameObject raisedGate;
        [Tooltip("퍼즐 통과로 인정할 진행 위치입니다(경로 월드 거리, 경로 없으면 스테이지 로컬 X). 작아진 플레이어의 진행값이 이 값 이상이고 껍질이 발판을 누르면 문이 열립니다.")]
        [SerializeField] private float crossingProgress;
        [Tooltip("플레이어의 경로 진행값을 계산할 소속 StageService입니다.")]
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

