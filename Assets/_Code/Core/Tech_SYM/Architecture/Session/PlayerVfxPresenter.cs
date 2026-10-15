using Cooked.Authoring;
using UnityEngine;

namespace Cooked.Session
{
    /// <summary>HMS가 확정한 이동·점프·폼 상태를 플레이어 소유 파티클에 표시한다.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(250)]
    public sealed class PlayerVfxPresenter : MonoBehaviour
    {
        [SerializeField, InspectorLabel("플레이어 상태"), Tooltip("같은 실제 몸체의 PlayerFacade를 연결하세요. 점프·착지·사망 통지를 표시합니다.")]
        private PlayerFacade actor;
        [SerializeField, InspectorLabel("플레이어 이동"), Tooltip("같은 실제 몸체의 CharacterMovement를 연결하세요. 지상에서 직접 이동할 때만 먼지를 표시합니다.")]
        private CharacterMovement movement;
        [SerializeField, InspectorLabel("플레이어 폼"), Tooltip("같은 실제 몸체의 PlayerFormController를 연결하세요. 기본 폼으로 돌아올 때 등장 효과를 표시합니다.")]
        private PlayerFormController forms;
        [SerializeField, InspectorLabel("발 충돌체"), Tooltip("실제 몸체의 세로 CapsuleCollider를 연결하세요. 현재 폼의 발 위치를 구합니다.")]
        private CapsuleCollider bodyCollider;
        [SerializeField, InspectorLabel("발 효과 지점"), Tooltip("몸체 VFX 안의 빈 지점을 연결하세요. 이동·점프 먼지가 현재 발 위치를 따라갑니다.")]
        private Transform footAnchor;
        [SerializeField, InspectorLabel("이동 먼지"), Tooltip("이 플레이어의 VFX 자식에 배치한 이동 먼지 파티클을 연결하세요. Project 프리팹 원본을 직접 연결하지 않습니다.")]
        private ParticleSystem walkDust;
        [SerializeField, InspectorLabel("점프·착지 먼지"), Tooltip("이 플레이어의 VFX 자식에 배치한 점프 먼지 파티클을 연결하세요. 점프와 착지에 한 번 재생합니다.")]
        private ParticleSystem jumpDust;
        [SerializeField, InspectorLabel("등장·회수 효과"), Tooltip("이 플레이어의 VFX 자식에 배치한 등장 파티클을 연결하세요. 생성과 기본 폼 복귀에 한 번 재생합니다.")]
        private ParticleSystem spawnBurst;
        [SerializeField, InspectorLabel("먼지 시작 속도"), Tooltip("이동 먼지를 표시할 최소 자체 이동 속도입니다(m/s). 0이면 직접 이동을 시작할 때 표시하며 발판에 실려 가는 속도는 포함하지 않습니다."), Min(0f)]
        private float minimumMoveSpeed = .1f;

        private bool bound;
        private bool walking;

        private void Awake()
        {
            if (actor == null || movement == null || forms == null || bodyCollider == null || footAnchor == null ||
                walkDust == null || jumpDust == null || spawnBurst == null ||
                actor.gameObject != gameObject || movement.gameObject != gameObject || forms.gameObject != gameObject ||
                bodyCollider.gameObject != gameObject || bodyCollider.direction != 1 ||
                !footAnchor.IsChildOf(transform) || !walkDust.transform.IsChildOf(transform) ||
                !jumpDust.transform.IsChildOf(transform) || !spawnBurst.transform.IsChildOf(transform))
            {
                Debug.LogError("플레이어 몸체·발 지점·소유 VFX 자식 연결을 확인하세요.", this);
                enabled = false;
            }
        }

        private void OnEnable()
        {
            if (actor == null || movement == null || forms == null || bound) return;
            actor.Jumped += OnJumped;
            actor.Landed += OnLanded;
            actor.StateChanged += OnStateChanged;
            forms.FormChanged += OnFormChanged;
            bound = true;
            UpdateFootPosition();
            StopOwnedParticles();
            if (!actor.IsDead) PlayBurst(spawnBurst);
        }

        private void LateUpdate()
        {
            if (!bound) return;
            UpdateFootPosition();
            float speed = movement.SelfHorizontalSpeed;
            bool show = !actor.IsDead && movement.IsGrounded && speed > 0f && speed >= minimumMoveSpeed;
            if (show == walking) return;
            walking = show;
            if (show) walkDust.Play(true);
            else walkDust.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void UpdateFootPosition()
        {
            if (bodyCollider != null && footAnchor != null)
                footAnchor.position = bodyCollider.transform.TransformPoint(bodyCollider.center - Vector3.up * (bodyCollider.height * .5f));
        }

        private void OnJumped()
        {
            walkDust.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            walking = false;
            UpdateFootPosition();
            PlayBurst(jumpDust);
        }

        private void OnLanded()
        {
            if (actor.IsDead) return;
            UpdateFootPosition();
            PlayBurst(jumpDust);
        }

        private void OnFormChanged(PlayerForm form)
        {
            UpdateFootPosition();
            if (form == PlayerForm.Normal && !actor.IsDead) PlayBurst(spawnBurst);
        }

        private void OnStateChanged(PlayerState previous, PlayerState current)
        {
            if (actor.IsDead) StopOwnedParticles();
        }

        private static void PlayBurst(ParticleSystem particles)
        {
            if (particles == null) return;
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            particles.Play(true);
        }

        private void StopOwnedParticles()
        {
            walking = false;
            if (walkDust != null) walkDust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (jumpDust != null) jumpDust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (spawnBurst != null) spawnBurst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void OnDisable()
        {
            if (bound)
            {
                if (actor != null) { actor.Jumped -= OnJumped; actor.Landed -= OnLanded; actor.StateChanged -= OnStateChanged; }
                if (forms != null) forms.FormChanged -= OnFormChanged;
                bound = false;
            }
            StopOwnedParticles();
        }

        private void OnValidate() => minimumMoveSpeed = float.IsFinite(minimumMoveSpeed) ? Mathf.Max(0f, minimumMoveSpeed) : .1f;
    }
}
