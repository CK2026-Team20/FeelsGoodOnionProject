using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 플랫폼이 전달한 속도를 스텝별로 보관하고 이동 담당에게 조회 기능을 제공한다.
/// 특정 캐릭터나 플랫폼 구현을 참조하지 않으며, 실제 이동과 Rigidbody 설정은 이동 담당의 책임이다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody))]
public sealed class PlatformPassenger : MonoBehaviour
{
    private struct PlatformMotion
    {
        public Collider PlatformCollider;
        public Collider PassengerCollider;
        public Vector3 WorldVelocity;
        public Vector3 PreviousWorldVelocity;
    }

    private readonly List<PlatformMotion> motions = new List<PlatformMotion>(4);
    private Rigidbody body;
    private double receivedTime = double.NegativeInfinity;

    /// <summary>
    /// 접촉한 Collider가 이 수신기의 물체에 속하는지 검증하기 위해 같은 오브젝트의 Rigidbody를 캐싱한다.
    /// </summary>
    private void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    /// <summary>
    /// 비활성화되면 저장된 정보를 비워 재활성화 시 이전 플랫폼 속도를 사용하지 않도록 한다.
    /// </summary>
    private void OnDisable()
    {
        motions.Clear();
        receivedTime = double.NegativeInfinity;
    }

    /// <summary>
    /// 이번 물리 스텝의 플랫폼 정보를 저장한다. 같은 Collider 쌍이 다시 전달되면 최신 값으로 교체한다.
    /// 플랫폼은 이동 담당의 FixedUpdate보다 먼저 호출해야 하며, 정지 중에도 속도 0을 전달해야 한다.
    /// 지지를 잃으면 전달을 중단한다. 이 함수는 접촉이나 접지를 새로 검사하지 않고 속도도 직접 적용하지 않는다.
    /// </summary>
    /// <param name="platformCollider">플랫폼의 실제 지지면 Collider.</param>
    /// <param name="passengerCollider">위에서 지지받는 대상의 Collider. 이 수신기와 같은 Rigidbody에 속해야 한다.</param>
    /// <param name="worldVelocity">이번 물리 스텝에 적용할 플랫폼의 월드 속도(m/s).</param>
    /// <param name="previousWorldVelocity">직전 물리 스텝에 적용했던 플랫폼의 월드 속도. 이동 담당의 상대 속도 판정에 사용한다.</param>
    public void ReceivePlatformVelocity(Collider platformCollider, Collider passengerCollider,
        Vector3 worldVelocity, Vector3 previousWorldVelocity)
    {
        if (!isActiveAndEnabled || body == null)
        {
            return;
        }

        if (receivedTime != Time.fixedTimeAsDouble)
        {
            motions.Clear();
            receivedTime = Time.fixedTimeAsDouble;
        }

        PlatformMotion motion = new PlatformMotion
        {
            PlatformCollider = platformCollider,
            PassengerCollider = passengerCollider,
            WorldVelocity = worldVelocity,
            PreviousWorldVelocity = previousWorldVelocity
        };

        if (!IsValid(motion))
        {
            return;
        }

        for (int index = 0; index < motions.Count; index++)
        {
            PlatformMotion existing = motions[index];
            if (existing.PlatformCollider == platformCollider && existing.PassengerCollider == passengerCollider)
            {
                motions[index] = motion;
                return;
            }
        }

        motions.Add(motion);
    }

    /// <summary>
    /// 이동 담당이 선택한 지지면의 이번 물리 스텝 정보를 반환한다. 서로 다른 플랫폼의 속도는 합산하지 않는다.
    /// 전달이 없는 다음 스텝이나 무효한 Collider에 대해서는 false와 0 속도를 반환한다.
    /// 물리 스텝 중 플랫폼 전달 이후에 호출하며, 점프 및 최종 접지 여부는 이동 담당이 결정한다.
    /// </summary>
    /// <param name="supportCollider">현재 지지면으로 선택한 Collider.</param>
    /// <param name="velocity">조회 성공 시 이번 스텝의 월드 속도, 실패 시 Vector3.zero.</param>
    /// <param name="previousVelocity">조회 성공 시 직전 스텝의 월드 속도, 실패 시 Vector3.zero.</param>
    /// <returns>해당 지지면에 대한 유효한 전달 정보가 있으면 true.</returns>
    public bool TryGetPlatformVelocity(Collider supportCollider, out Vector3 velocity, out Vector3 previousVelocity)
    {
        velocity = Vector3.zero;
        previousVelocity = Vector3.zero;
        if (!isActiveAndEnabled || receivedTime != Time.fixedTimeAsDouble || supportCollider == null)
        {
            return false;
        }

        // 같은 플랫폼에 여러 Collider가 닿았으면 뒤에서부터 유효한 정보를 하나 선택한다.
        for (int index = motions.Count - 1; index >= 0; index--)
        {
            PlatformMotion motion = motions[index];
            if (motion.PlatformCollider != supportCollider || !IsValid(motion))
            {
                continue;
            }

            velocity = motion.WorldVelocity;
            previousVelocity = motion.PreviousWorldVelocity;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Collider의 활성 상태, 대상 Rigidbody 소속 및 충돌 무시 여부를 검사한다.
    /// Rigidbody의 속도, 수면 설정이나 캐릭터 상태는 변경하지 않는다.
    /// </summary>
    /// <param name="motion">검사할 플랫폼 전달 정보.</param>
    /// <returns>현재 설정에서 사용할 수 있는 정보이면 true.</returns>
    private bool IsValid(PlatformMotion motion)
    {
        Collider platform = motion.PlatformCollider;
        Collider passenger = motion.PassengerCollider;
        if (body == null || platform == null || passenger == null || platform == passenger)
        {
            return false;
        }

        if (passenger.attachedRigidbody != body || platform.attachedRigidbody == body)
        {
            return false;
        }

        if (!platform.enabled || !passenger.enabled || platform.isTrigger || passenger.isTrigger ||
            !platform.gameObject.activeInHierarchy || !passenger.gameObject.activeInHierarchy)
        {
            return false;
        }

        return !Physics.GetIgnoreCollision(platform, passenger) &&
            !Physics.GetIgnoreLayerCollision(platform.gameObject.layer, passenger.gameObject.layer);
    }
}
