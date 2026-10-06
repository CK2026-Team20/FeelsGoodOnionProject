using UnityEngine;

namespace Cooked.Session
{
    /// <summary>Fallback death plane for genuine platform falls; receives the serialized physical body.</summary>
    public sealed class ActorFallGuard : MonoBehaviour
    {
        [Tooltip("아래 낙사 높이를 검사하고 사망시킬 플레이어입니다.")]
        [SerializeField] private PlayerFacade actor;
        [Tooltip("이 값보다 월드 Y 위치가 낮아지면 사망합니다(월드 단위). 값을 높이면 더 높은 위치에서 낙사합니다. 0은 비활성화가 아니라 월드 Y=0 경계입니다.")]
        [SerializeField] private float killHeight = -10f;
        private void FixedUpdate()
        {
            if (actor != null && !actor.IsDead && actor.transform.position.y < killHeight)
                actor.Damage(actor.Model.MaxHP);
        }
    }
}
