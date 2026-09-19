using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 잔해처럼 자체 입력이 없는 Box 물체를 낙하시키고, 착지 중에는 지지면의 속도를 따라가게 한다.
/// 자유 회전과 지상 미끄러짐은 사용하지 않는다. CharacterMovement와 같은 오브젝트에 함께 붙이지 않는다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(BoxCollider), typeof(PlatformPassenger))]
public sealed class PlatformRigidbodyMovement : MonoBehaviour
{
    [Tooltip("직접 적용하는 아래쪽 중력 가속도(m/s²). Rigidbody의 Use Gravity는 꺼집니다.")]
    [SerializeField, Min(0.01f)] private float gravity = 25f;
    [Tooltip("공중에서의 최대 낙하 속도(m/s).")]
    [SerializeField, Min(0.01f)] private float maximumFallSpeed = 35f;
    [Tooltip("지지면 접촉 유지를 위한 작은 아래쪽 상대 속도(m/s).")]
    [SerializeField, Min(0f)] private float groundStickSpeed = 1f;
    [Tooltip("바닥으로 인정할 최대 각도. 평평한 Cube 테스트에서는 기본값을 유지합니다.")]
    [SerializeField, Range(0f, 60f)] private float maximumGroundAngle = 45f;
    [Tooltip("이 월드 Y 아래로 떨어지면 FellOutOfWorld 이벤트만 발생시킵니다. 자동 삭제하지 않습니다.")]
    [SerializeField] private float outOfWorldHeight = -30f;

    private readonly List<ContactPoint> contacts = new List<ContactPoint>(16);
    private Rigidbody body;
    private BoxCollider boxCollider;
    private PlatformPassenger passenger;
    private PhysicsMaterial originalMaterial;
    private PhysicsMaterial runtimeMaterial;
    private bool outOfWorldReported;
    private float originalSleepThreshold;
    private bool sleepSettingsOverridden;

    public bool IsGrounded { get; private set; }
    public Collider GroundCollider { get; private set; }
    public event Action FellOutOfWorld;

    /// <summary>
    /// 필수 컴포넌트를 캐싱하고 회전 없는 동적 Rigidbody로 설정한다.
    /// 중력은 직접 계산하며 마찰 대신 지지면 속도를 적용하므로 런타임 마찰과 반발을 0으로 설정한다.
    /// </summary>
    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        boxCollider = GetComponent<BoxCollider>();
        passenger = GetComponent<PlatformPassenger>();
        if (TryGetComponent(out CharacterMovement _))
        {
            Debug.LogError("CharacterMovement와 PlatformRigidbodyMovement는 함께 사용할 수 없습니다.", this);
            enabled = false;
            return;
        }

        body.isKinematic = false;
        body.useGravity = false;
        body.linearDamping = 0f;
        body.constraints = RigidbodyConstraints.FreezeRotation;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        boxCollider.isTrigger = false;

        originalMaterial = boxCollider.sharedMaterial;
        runtimeMaterial = new PhysicsMaterial("PlatformRigidbodyMovement Runtime Material")
        {
            staticFriction = 0f,
            dynamicFriction = 0f,
            bounciness = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };
        boxCollider.sharedMaterial = runtimeMaterial;
    }

    /// <summary>
    /// 수면으로 접촉 콜백이 끊기지 않도록 Rigidbody를 깨어 있게 유지한다. 이전 설정은 비활성화 시 복원한다.
    /// </summary>
    private void OnEnable()
    {
        originalSleepThreshold = body.sleepThreshold;
        body.sleepThreshold = 0f;
        sleepSettingsOverridden = true;
        body.WakeUp();
    }

    /// <summary>
    /// 지지 중에는 플랫폼 속도 또는 정지 바닥의 0 속도를 적용하고, 공중에서는 현재 속도에 중력을 적용한다.
    /// 발판을 잃은 순간의 속도는 낙하 중 유지되며, 다시 착지하면 지지면 기준 정지 상태가 된다.
    /// </summary>
    private void FixedUpdate()
    {
        RefreshGround();
        Vector3 velocity = body.linearVelocity;
        if (IsGrounded)
        {
            passenger.TryGetPlatformVelocity(GroundCollider, out velocity, out _);
            velocity.y -= groundStickSpeed;
        }
        else
        {
            velocity.y = Mathf.Max(velocity.y - gravity * Time.fixedDeltaTime, -maximumFallSpeed);
        }

        body.linearVelocity = velocity;
        CheckOutOfWorld();
    }

    /// <summary>
    /// 직전 스텝의 접촉에서 위쪽 지지면을 선택한다. 플랫폼의 직전 속도를 뺀 상대 상승 속도로 이륙 여부를 판단한다.
    /// 물체가 여러 바닥에 걸치면 기존 지지면을 우선 유지하며 서로 다른 플랫폼 속도를 합산하지 않는다.
    /// </summary>
    private void RefreshGround()
    {
        Collider previousGround = GroundCollider;
        IsGrounded = false;
        GroundCollider = null;
        float bestNormalY = Mathf.Cos(maximumGroundAngle * Mathf.Deg2Rad);

        foreach (ContactPoint contact in contacts)
        {
            Collider other = contact.otherCollider;
            if (other == null || !other.enabled || other.isTrigger || !other.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (Physics.GetIgnoreCollision(boxCollider, other) ||
                Physics.GetIgnoreLayerCollision(gameObject.layer, other.gameObject.layer))
            {
                continue;
            }

            passenger.TryGetPlatformVelocity(other, out _, out Vector3 previousVelocity);
            if (body.linearVelocity.y - previousVelocity.y > 0.1f || contact.normal.y < bestNormalY)
            {
                continue;
            }

            if (IsGrounded && GroundCollider == previousGround && contact.normal.y <= bestNormalY + 0.0001f)
            {
                continue;
            }

            bestNormalY = contact.normal.y;
            GroundCollider = other;
            IsGrounded = true;
        }

        contacts.Clear();
    }

    /// <summary>
    /// 충돌 시작 시 접촉 정보를 수집하여 다음 물리 스텝의 착지 판정에 사용한다.
    /// </summary>
    /// <param name="collision">이번 충돌의 Collider 및 접촉점 정보.</param>
    private void OnCollisionEnter(Collision collision)
    {
        CollectContacts(collision);
    }

    /// <summary>
    /// 충돌 유지 중에도 접촉 정보를 다시 받아 바닥 상태를 갱신한다.
    /// </summary>
    /// <param name="collision">현재 유지되는 충돌 정보.</param>
    private void OnCollisionStay(Collision collision)
    {
        CollectContacts(collision);
    }

    /// <summary>
    /// 이 물체의 BoxCollider에서 발생한 접촉만 다음 접지 검사에 사용할 목록에 저장한다.
    /// </summary>
    /// <param name="collision">접촉점을 가져올 충돌 정보.</param>
    private void CollectContacts(Collision collision)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        for (int index = 0; index < collision.contactCount; index++)
        {
            ContactPoint contact = collision.GetContact(index);
            if (contact.thisCollider == boxCollider)
            {
                contacts.Add(contact);
            }
        }
    }

    /// <summary>
    /// 기준 높이 아래로 떨어지면 한 번 이벤트를 발생시킨다. 물체를 삭제하거나 복구하지 않으며 처리 정책은 구독자가 정한다.
    /// 기준 높이 위로 돌아오면 다음 낙하를 다시 감지할 수 있게 한다.
    /// </summary>
    private void CheckOutOfWorld()
    {
        if (body.position.y >= outOfWorldHeight)
        {
            outOfWorldReported = false;
            return;
        }

        if (!outOfWorldReported)
        {
            outOfWorldReported = true;
            FellOutOfWorld?.Invoke();
        }
    }

    /// <summary>
    /// 접촉과 낙하 알림 상태를 초기화하고, 이 컴포넌트가 변경했던 Rigidbody 수면 설정을 복원한다.
    /// </summary>
    private void OnDisable()
    {
        contacts.Clear();
        IsGrounded = false;
        GroundCollider = null;
        outOfWorldReported = false;
        if (body != null && sleepSettingsOverridden)
        {
            body.sleepThreshold = originalSleepThreshold;
            sleepSettingsOverridden = false;
        }
    }

    /// <summary>
    /// 원래 물리 재질을 복원하고 이 컴포넌트가 만든 런타임 재질을 해제한다.
    /// </summary>
    private void OnDestroy()
    {
        if (runtimeMaterial == null)
        {
            return;
        }

        if (boxCollider != null && boxCollider.sharedMaterial == runtimeMaterial)
        {
            boxCollider.sharedMaterial = originalMaterial;
        }

        Destroy(runtimeMaterial);
    }
}
