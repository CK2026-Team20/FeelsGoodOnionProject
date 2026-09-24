using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider), typeof(PlatformPassenger))]
public sealed class CharacterMovement : MonoBehaviour
{
    [FormerlySerializedAs("_moveSpeed")]
    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 5f;
    [FormerlySerializedAs("_groundAcceleration")] [SerializeField, Min(0f)] private float groundAcceleration = 40f;
    [FormerlySerializedAs("_groundDeceleration")] [SerializeField, Min(0f)] private float groundDeceleration = 50f;
    [FormerlySerializedAs("_airAcceleration")] [SerializeField, Min(0f)] private float airAcceleration = 30f;
    [FormerlySerializedAs("_airDeceleration")] [SerializeField, Min(0f)] private float airDeceleration = 20f;
    /// <summary>
    /// Z축 이동 허용 여부.<br/>
    /// 해당 값에 따라 FreezePositionZ가 제어되어, Z축으로의 이동을 막거나 허용함.
    /// </summary>
    [FormerlySerializedAs("_allowDepthMovement")] [SerializeField] private bool allowDepthMovement;

    [FormerlySerializedAs("_jumpHeight")]
    [Header("Jump")]
    [SerializeField, Min(0f)] private float jumpHeight = 2f;
    [FormerlySerializedAs("_gravity")] [SerializeField, Min(0.01f)] private float gravity = 25f;
    [FormerlySerializedAs("_fallGravityMultiplier")] [SerializeField, Min(1f)] private float fallGravityMultiplier = 1.5f;
    [FormerlySerializedAs("_maximumFallSpeed")] [SerializeField, Min(0.01f)] private float maximumFallSpeed = 35f;

    [FormerlySerializedAs("_maximumGroundAngle")]
    [Header("Ground")]
    [SerializeField, Range(0f, 85f)]
    private float maximumGroundAngle = 50f;
    [FormerlySerializedAs("_groundStickSpeed")] [SerializeField, Min(0f)] private float groundStickSpeed = 1f;

    [FormerlySerializedAs("_externalDeceleration")]
    [Header("External Movement")]
    [SerializeField, Min(0f)]
    private float externalDeceleration = 8f;

    private Rigidbody body;
    private CapsuleCollider capsuleCollider;
    private PlatformPassenger platformPassenger;

    private readonly List<ContactPoint> contacts = new List<ContactPoint>(16);

    private Vector3 moveInput;
    private Vector3 selfVelocity;
    private Vector3 externalHorizontalVelocity;
    private Vector3 pendingImpulse;
    private Vector3 pendingKnockbackVelocity;
    private bool hasPendingKnockback;

    private float accelerationMultiplier = 1f;
    private float decelerationMultiplier = 1f;
    private float movementLockRemaining;

    private bool jumpRequested;
    private float originalSleepThreshold;
    private bool sleepSettingsOverridden;

    public bool IsGrounded { get; private set; }
    public Collider GroundCollider { get; private set; }
    public Vector3 GroundNormal { get; private set; } = Vector3.up;
    public Vector3 FacingDirection { get; private set; } = Vector3.right;

    public bool IsMovementLocked => movementLockRemaining > 0f;
    public bool AllowDepthMovement => allowDepthMovement;

    public Vector3 Velocity => body != null ? body.linearVelocity : Vector3.zero;
    public Vector3 SelfVelocity => selfVelocity;
    public Vector3 ExternalHorizontalVelocity => externalHorizontalVelocity;

    public event Action Jumped;

    /// <summary>
    /// 같은 오브젝트의 Rigidbody와 캡슐을 캐싱하고, 직접 계산한 속도로 움직이도록 물리 설정과 축 제약을 초기화한다.
    /// </summary>
    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        capsuleCollider = GetComponent<CapsuleCollider>();
        platformPassenger = GetComponent<PlatformPassenger>();

        body.isKinematic = false;
        body.useGravity = false;
        body.linearDamping = 0f;
        body.interpolation = RigidbodyInterpolation.Extrapolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        capsuleCollider.isTrigger = false;
        ApplyConstraints();
    }

    /// <summary>
    /// 매 물리 스텝의 지지 접촉을 계속 받도록 수면을 방지한다. 비활성화 시 복원할 원래 값을 저장한다.
    /// </summary>
    private void OnEnable()
    {
        originalSleepThreshold = body.sleepThreshold;
        body.sleepThreshold = 0f;
        sleepSettingsOverridden = true;
        body.WakeUp();
    }

    /// <summary>
    /// 입력, 외부 충격과 접촉 기록을 비우고 이동을 정지하며 원래 Rigidbody 수면 설정을 복원한다.
    /// </summary>
    private void OnDisable()
    {
        moveInput = Vector3.zero;
        selfVelocity = Vector3.zero;
        externalHorizontalVelocity = Vector3.zero;
        pendingImpulse = Vector3.zero;

        jumpRequested = false;
        movementLockRemaining = 0f;
        hasPendingKnockback = false;
        pendingKnockbackVelocity = Vector3.zero;

        contacts.Clear();
        IsGrounded = false;
        GroundCollider = null;

        if (body != null && sleepSettingsOverridden)
        {
            body.sleepThreshold = originalSleepThreshold;
            sleepSettingsOverridden = false;
        }

        if (body != null && !body.isKinematic)
        {
            body.linearVelocity = Vector3.zero;
        }
    }

    /// <summary>
    /// 이전 물리 스텝의 접촉으로 접지를 갱신한 뒤 입력, 점프, 중력과 플랫폼 속도를 합성해 최종 속도를 적용한다.
    /// 접지 판정 이후 수신기에서 선택된 지지면의 이번 스텝 속도를 직접 조회한다.
    /// </summary>
    private void FixedUpdate()
    {
        float deltaTime = Time.fixedDeltaTime;

        if (hasPendingKnockback)
        {
            ApplyPendingKnockback(deltaTime);
            return;
        }

        RefreshGround();

        Vector3 platformVelocity = Vector3.zero;
        if (IsGrounded)
        {
            platformPassenger.TryGetPlatformVelocity(GroundCollider, out platformVelocity, out _);
            platformVelocity = ConstrainDepth(platformVelocity);
        }

        bool movementLocked = IsMovementLocked;

        UpdateSelfVelocity(deltaTime, movementLocked);

        externalHorizontalVelocity += ConstrainDepth(new Vector3(pendingImpulse.x, 0f, pendingImpulse.z));

        float verticalVelocity = body.linearVelocity.y;

        // 위쪽 충격은 접지를 해제한다.
        if (pendingImpulse.y > 0f)
        {
            if (IsGrounded)
            {
                verticalVelocity = platformVelocity.y;
            }

            IsGrounded = false;
            GroundCollider = null;
        }
        else if (IsGrounded)
        {
            verticalVelocity = platformVelocity.y - groundStickSpeed;
        }

        verticalVelocity += pendingImpulse.y;
        pendingImpulse = Vector3.zero;

        if (jumpRequested && IsGrounded && !movementLocked)
        {
            verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight) + platformVelocity.y;

            IsGrounded = false;
            GroundCollider = null;

            // 이륙 순간의 수평 발판 속도는 한 번만 상속한다.
            externalHorizontalVelocity += new Vector3(platformVelocity.x, 0f, platformVelocity.z);

            platformVelocity = Vector3.zero;
            Jumped?.Invoke();
        }

        jumpRequested = false;

        if (!IsGrounded)
        {
            float gravityMultiplier = verticalVelocity < 0f ? fallGravityMultiplier : 1f;

            verticalVelocity -= gravity * gravityMultiplier * deltaTime;
        }

        verticalVelocity = Mathf.Max(verticalVelocity, -maximumFallSpeed);

        Vector3 velocity = selfVelocity + externalHorizontalVelocity;

        velocity.x += platformVelocity.x;
        velocity.z += platformVelocity.z;
        velocity.y = verticalVelocity;

        // 일반 이동 스텝의 최종 속도를 적용한다. 넉백 예약 스텝은 별도 처리한다.
        body.linearVelocity = ConstrainDepth(velocity);

        externalHorizontalVelocity = Vector3.MoveTowards(
            externalHorizontalVelocity, Vector3.zero, externalDeceleration * deltaTime);

        movementLockRemaining = Mathf.Max(0f, movementLockRemaining - deltaTime);
    }

    /// <summary>
    /// 목표 이동 속도를 향해 자체 속도를 변경한다. 입력이 있으면 가속도를, 없으면 감속도를 적용한다.
    /// </summary>
    /// <param name="deltaTime">이번 물리 스텝의 시간(초). 가속도와 감속도를 속도 변화량으로 환산한다.</param>
    /// <param name="movementLocked">true이면 이동 입력을 무시하고 자체 속도를 감속한다.</param>
    private void UpdateSelfVelocity(float deltaTime, bool movementLocked)
    {
        Vector3 input = movementLocked ? Vector3.zero : ConstrainDepth(moveInput);

        bool hasInput = input.sqrMagnitude > 0.0001f;

        float acceleration = IsGrounded ? groundAcceleration : airAcceleration;

        float deceleration = IsGrounded ? groundDeceleration : airDeceleration;

        float rate = hasInput ? acceleration * accelerationMultiplier : deceleration * decelerationMultiplier;

        selfVelocity = Vector3.MoveTowards(selfVelocity, input * moveSpeed, rate * deltaTime);

        if (hasInput)
        {
            FacingDirection = input.normalized;
        }
    }

    /// <summary>
    /// 직전 접촉에서 유효한 지지면을 선택하고 해당 플랫폼의 직전 속도를 뺀 상대 상승 속도를 검사한다.
    /// 상승 발판이 밀어 올린 속도를 점프로 오인하지 않으며, 실제 점프 중에는 남은 접촉으로 재접지하지 않는다.
    /// 여러 바닥에 걸친 경우 기존 지지면을 우선 유지한다. 판정 후 다음 스텝을 위해 접촉 목록을 비운다.
    /// </summary>
    private void RefreshGround()
    {
        Collider previousGround = GroundCollider;
        IsGrounded = false;
        GroundCollider = null;
        GroundNormal = Vector3.up;
        float bestNormalY = Mathf.Cos(maximumGroundAngle * Mathf.Deg2Rad);

        foreach (ContactPoint contact in contacts)
        {
            Collider other = contact.otherCollider;
            if (other == null || !other.enabled || !other.gameObject.activeInHierarchy || other.isTrigger)
            {
                continue;
            }

            if (Physics.GetIgnoreCollision(capsuleCollider, other) ||
                Physics.GetIgnoreLayerCollision(gameObject.layer, other.gameObject.layer))
            {
                continue;
            }

            platformPassenger.TryGetPlatformVelocity(other, out _, out Vector3 supportPreviousVelocity);

            float relativeVerticalVelocity = body.linearVelocity.y - supportPreviousVelocity.y;
            if (relativeVerticalVelocity > 0.1f || contact.normal.y < bestNormalY)
            {
                continue;
            }

            if (IsGrounded && GroundCollider == previousGround && contact.normal.y <= bestNormalY + 0.0001f)
            {
                continue;
            }

            bestNormalY = contact.normal.y;
            GroundCollider = other;
            GroundNormal = contact.normal;
            IsGrounded = true;
        }

        contacts.Clear();
    }

    /// <summary>
    /// 충돌이 시작되면 다음 접지 판정에 사용할 접촉 정보를 수집한다.
    /// </summary>
    /// <param name="collision">이번 충돌의 접촉점과 Collider 정보.</param>
    private void OnCollisionEnter(Collision collision)
    {
        CollectContacts(collision);
    }

    /// <summary>
    /// 충돌이 유지되는 동안 접촉 정보를 다시 수집하여 다음 접지 판정에 사용한다.
    /// </summary>
    /// <param name="collision">유지 중인 충돌의 접촉점과 Collider 정보.</param>
    private void OnCollisionStay(Collision collision)
    {
        CollectContacts(collision);
    }

    /// <summary>
    /// 관리 중인 캡슐의 접촉을 기록하고, 수직 벽 안쪽을 향하는 자체 속도와 외부 수평 속도 성분을 제거한다.
    /// </summary>
    /// <param name="collision">접촉점을 읽을 충돌 정보.</param>
    private void CollectContacts(Collision collision)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        for (int index = 0; index < collision.contactCount; index++)
        {
            ContactPoint contact = collision.GetContact(index);

            // 이 컴포넌트가 관리하는 캡슐의 접촉만 사용.
            if (contact.thisCollider != capsuleCollider)
            {
                continue;
            }

            contacts.Add(contact);

            // 수직 벽 방향으로 저장된 속도가 쌓이지 않게 한다.
            if (Mathf.Abs(contact.normal.y) < 0.01f)
            {
                selfVelocity = RemoveIntoSurface(selfVelocity, contact.normal);

                externalHorizontalVelocity = RemoveIntoSurface(externalHorizontalVelocity, contact.normal);
            }
        }
    }

    /// <summary>
    /// 월드 기준 이동 입력에서 수직 성분을 제거하고 크기를 1 이하로 제한해 저장한다. 입력은 다음 호출까지 유지된다.
    /// </summary>
    /// <param name="worldDirection">월드 XZ 평면의 이동 방향과 입력 강도. Y는 무시한다.</param>
    public void SetMoveInput(Vector3 worldDirection)
    {
        worldDirection.y = 0f;
        moveInput = Vector3.ClampMagnitude(worldDirection, 1f);
    }

    /// <summary>
    /// 이동이 잠기지 않았다면 다음 물리 스텝에 점프를 시도하도록 요청한다.
    /// 접지 중일 때만 실행하며, 키 유지 시간과 관계없이 jumpHeight로 계산한 초기 상승 속도를 사용한다.
    /// 공중에서 소비된 요청은 착지할 때까지 보관하지 않는다.
    /// </summary>
    public void RequestJump()
    {
        if (!IsMovementLocked)
        {
            jumpRequested = true;
        }
    }

    /// <summary>
    /// 아직 실행되지 않은 점프 요청을 취소한다. 이미 시작한 점프의 상승 속도는 바꾸지 않는다.
    /// </summary>
    public void CancelJumpRequest()
    {
        jumpRequested = false;
    }

    /// <summary>
    /// 외부 충격을 누적하여 다음 물리 스텝에 속도 변화량으로 적용한다. 양의 Y 충격은 접지를 해제한다.
    /// </summary>
    /// <param name="velocityChange">월드 기준 속도 변화량(m/s). 힘이나 질량 기반 충격량이 아니며, 깊이 이동이 금지되면 Z는 무시한다.</param>
    public void AddImpulse(Vector3 velocityChange)
    {
        pendingImpulse += ConstrainDepth(velocityChange);
    }

    /// <summary>
    /// PC의 기존 운동을 교체할 넉백을 다음 물리 스텝에 예약합니다. 같은 스텝의 여러 요청은 마지막 속도를 사용합니다.
    /// 자체 이동과 점프를 즉시 제한하며 남은 제한 시간은 짧아지지 않습니다.
    /// 예약 적용 스텝에서는 다른 AddImpulse 요청보다 넉백 교체가 우선합니다.
    /// </summary>
    /// <param name="knockbackVelocity">월드 기준 초기 속도(m/s). 금지된 Z축은 제거됩니다.</param>
    /// <param name="controlLockSeconds">내부 이동 API의 초 단위 제한 시간. Facade가 공용 인터페이스의 ms를 변환합니다.</param>
    public void ApplyKnockback(Vector3 knockbackVelocity, float controlLockSeconds)
    {
        if (!isActiveAndEnabled || !float.IsFinite(knockbackVelocity.x) || !float.IsFinite(knockbackVelocity.y) ||
            !float.IsFinite(knockbackVelocity.z) || !float.IsFinite(controlLockSeconds))
        {
            return;
        }
        pendingKnockbackVelocity = ConstrainDepth(knockbackVelocity);
        hasPendingKnockback = true;
        selfVelocity = Vector3.zero;
        externalHorizontalVelocity = Vector3.zero;
        pendingImpulse = Vector3.zero;
        jumpRequested = false;
        LockMovement(controlLockSeconds, true);
        if (body != null)
        {
            body.WakeUp();
        }
    }

    /// <summary>
    /// 기존 운동과 접촉 기록을 지우고 넉백 속도를 적용합니다. 첫 스텝은 자체 가속과 플랫폼 속도를 더하지 않습니다.
    /// 중력과 낙하 속도 제한은 즉시 적용하고, 다음 스텝부터 새로운 접촉으로 플랫폼 운반을 다시 판단합니다.
    /// </summary>
    /// <param name="deltaTime">물리 스텝 시간(초).</param>
    private void ApplyPendingKnockback(float deltaTime)
    {
        Vector3 velocity = ConstrainDepth(pendingKnockbackVelocity);
        hasPendingKnockback = false;
        pendingKnockbackVelocity = Vector3.zero;
        selfVelocity = Vector3.zero;
        pendingImpulse = Vector3.zero;
        jumpRequested = false;
        contacts.Clear();
        IsGrounded = false;
        GroundCollider = null;
        GroundNormal = Vector3.up;
        externalHorizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
        float multiplier = velocity.y < 0f ? fallGravityMultiplier : 1f;
        velocity.y = Mathf.Max(velocity.y - gravity * multiplier * deltaTime, -maximumFallSpeed);
        body.linearVelocity = velocity;
        externalHorizontalVelocity = Vector3.MoveTowards(externalHorizontalVelocity, Vector3.zero, externalDeceleration * deltaTime);
        movementLockRemaining = Mathf.Max(0f, movementLockRemaining - deltaTime);
    }

    /// <summary>
    /// 지정 시간 동안 자체 이동 입력과 점프를 제한한다. 중력, 플랫폼 운반과 외부 충격은 계속 적용된다.
    /// </summary>
    /// <param name="duration">잠금 시간(초). 남은 시간보다 짧은 요청은 기존 잠금을 단축하지 않는다.</param>
    /// <param name="clearSelfVelocity">true이면 자체 이동 속도를 즉시 0으로 만든다. 외부 속도는 지우지 않는다.</param>
    public void LockMovement(float duration, bool clearSelfVelocity = true)
    {
        if (duration <= 0f)
        {
            return;
        }

        movementLockRemaining = Mathf.Max(movementLockRemaining, duration);

        jumpRequested = false;

        if (clearSelfVelocity)
        {
            selfVelocity = Vector3.zero;
        }
    }

    /// <summary>
    /// 자체 이동의 가속 및 감속 배율을 설정한다. 지상과 공중 모두에 적용하며 최대 이동 속도는 바꾸지 않는다.
    /// </summary>
    /// <param name="accelerationMultiplier">입력 중 가속도에 곱할 0 이상의 배율. 작을수록 방향 전환도 느려진다.</param>
    /// <param name="decelerationMultiplier">입력을 놓았을 때 감속도에 곱할 0 이상의 배율. 작을수록 오래 미끄러진다.</param>
    public void SetTractionMultipliers(float accelerationMultiplier, float decelerationMultiplier)
    {
        this.accelerationMultiplier = Mathf.Max(0f, accelerationMultiplier);

        this.decelerationMultiplier = Mathf.Max(0f, decelerationMultiplier);
    }

    /// <summary>
    /// 최대 자체 이동 속도와 점프 초기 속도 계산에 사용할 높이를 갱신한다.
    /// </summary>
    /// <param name="moveSpeed">최대 자체 이동 속도(m/s). 음수는 0으로 제한한다.</param>
    /// <param name="jumpHeight">정지한 지면 기준 목표 점프 높이(m). 물리 스텝 오차, 천장, 플랫폼 속도나 외부 충격으로 실제 높이는 달라질 수 있다.</param>
    public void SetMovementStats(float moveSpeed, float jumpHeight)
    {
        this.moveSpeed = Mathf.Max(0f, moveSpeed);
        this.jumpHeight = Mathf.Max(0f, jumpHeight);
    }

    /// <summary>
    /// 깊이 이동 허용 여부와 Rigidbody 제약을 갱신하고, 금지 시 저장된 이동 속도의 Z 성분을 제거한다.
    /// 현재 Z 위치를 고정할 뿐 지정된 레인 위치로 정렬하지는 않는다.
    /// </summary>
    /// <param name="allowed">true이면 XZ 이동을 허용하고, false이면 Z 이동을 제한한다.</param>
    public void SetDepthMovementAllowed(bool allowed)
    {
        allowDepthMovement = allowed;

        selfVelocity = ConstrainDepth(selfVelocity);
        externalHorizontalVelocity = ConstrainDepth(externalHorizontalVelocity);
        pendingImpulse = ConstrainDepth(pendingImpulse);
        pendingKnockbackVelocity = ConstrainDepth(pendingKnockbackVelocity);

        body.linearVelocity = ConstrainDepth(body.linearVelocity);

        ApplyConstraints();
    }

    /// <summary>
    /// Rigidbody의 회전을 모든 축에서 고정하고, 사이드 이동 모드일 때 Z 위치도 고정한다. 기존 제약 설정을 대체한다.
    /// </summary>
    private void ApplyConstraints()
    {
        // 이 클래스가 캐릭터의 위치/회전 제약을 소유한다.
        body.constraints = RigidbodyConstraints.FreezeRotation;

        if (!allowDepthMovement)
        {
            body.constraints |= RigidbodyConstraints.FreezePositionZ;
        }
    }

    /// <summary>
    /// 깊이 이동이 금지되어 있으면 벡터의 Z 성분을 제거한다.
    /// </summary>
    /// <param name="value">축 제약을 적용할 월드 기준 벡터.</param>
    /// <returns>현재 이동 모드의 축 제약이 반영된 벡터.</returns>
    private Vector3 ConstrainDepth(Vector3 value)
    {
        if (!allowDepthMovement)
        {
            value.z = 0f;
        }

        return value;
    }

    /// <summary>
    /// 속도의 표면 법선 성분이 안쪽을 향할 때만 그 성분을 제거하여 벽을 향하는 저장 속도를 없앤다.
    /// </summary>
    /// <param name="velocity">보정할 속도 벡터.</param>
    /// <param name="normal">표면에서 바깥쪽으로 향하는 단위 법선.</param>
    /// <returns>표면 안쪽으로 향하는 성분을 제거한 속도.</returns>
    private static Vector3 RemoveIntoSurface(Vector3 velocity, Vector3 normal)
    {
        float inwardSpeed = Vector3.Dot(velocity, normal);

        return inwardSpeed < 0f ? velocity - normal * inwardSpeed : velocity;
    }
}
