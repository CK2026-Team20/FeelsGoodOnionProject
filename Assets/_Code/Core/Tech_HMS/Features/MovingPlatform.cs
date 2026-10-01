using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 회전 없는 Box 플랫폼을 두 위치 사이에서 왕복시키고, 직전 물리 스텝에서 위에 있던 대상에게 속도를 제공한다.
/// 이동 거리는 월드 기준이며 Cube의 스케일에 영향을 받지 않는다.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-200)]
[RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
public sealed class MovingPlatform : MonoBehaviour
{
    [Tooltip("시작 위치에서 끝 위치까지의 월드 이동량. Cube 크기와 무관합니다.")]
    [SerializeField] private Vector3 travelOffset = new Vector3(5f, 0f, 0f);
    [Tooltip("플랫폼 이동 속도(m/s). 0이면 현재 위치에서 정지합니다.")]
    [SerializeField, Min(0f)] private float moveSpeed = 2f;
    [Tooltip("각 끝점에 도착한 뒤 기다릴 시간(초). 시간은 물리 스텝 단위로 처리됩니다.")]
    [SerializeField, Min(0f)] private float endpointWaitTime = 0.5f;
    [Tooltip("위쪽 지지 접촉으로 인정할 최대 각도. 평평한 Cube에서는 기본값을 유지합니다.")]
    [SerializeField, Range(0f, 60f)] private float maximumSupportAngle = 45f;

    private readonly Dictionary<Collider, PlatformPassenger> passengers = new Dictionary<Collider, PlatformPassenger>();
    private Rigidbody body;
    private BoxCollider supportCollider;
    private PhysicsMaterial originalMaterial;
    private PhysicsMaterial runtimeMaterial;
    private Vector3 startPosition;
    private bool movingToEnd = true;
    private float waitRemaining;

    public Vector3 Velocity { get; private set; }
    public Vector3 PreviousVelocity { get; private set; }

    /// <summary>
    /// 필수 컴포넌트를 캐싱하고 Kinematic 이동 및 마찰 없는 지지면을 설정한다. 시작 위치는 생성 시점에 고정된다.
    /// </summary>
    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        supportCollider = GetComponent<BoxCollider>();
        startPosition = body.position;
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        body.constraints = RigidbodyConstraints.FreezeRotation;
        supportCollider.isTrigger = false;

        originalMaterial = supportCollider.sharedMaterial;
        runtimeMaterial = new PhysicsMaterial("MovingPlatform Runtime Material")
        {
            staticFriction = 0f,
            dynamicFriction = 0f,
            bounciness = 0f,
            frictionCombine = PhysicsMaterialCombine.Minimum,
            bounceCombine = PhysicsMaterialCombine.Minimum
        };
        supportCollider.sharedMaterial = runtimeMaterial;
    }

    /// <summary>
    /// 다음 위치와 속도를 먼저 계산하고 탑승자에게 전달한 뒤 MovePosition으로 이동을 예약한다.
    /// 접촉 목록은 비워서 다음 시뮬레이션에서 실제로 지지한 대상만 다시 받는다.
    /// </summary>
    private void FixedUpdate()
    {
        float deltaTime = Time.fixedDeltaTime;
        PreviousVelocity = Velocity;
        Vector3 nextPosition = CalculateNextPosition(deltaTime);
        Velocity = (nextPosition - body.position) / deltaTime;

        if (supportCollider.enabled && !supportCollider.isTrigger)
        {
            foreach (KeyValuePair<Collider, PlatformPassenger> entry in passengers)
            {
                Collider actorCollider = entry.Key;
                PlatformPassenger passenger = entry.Value;
                if (actorCollider == null || passenger == null || !passenger.isActiveAndEnabled)
                {
                    continue;
                }

                passenger.ReceivePlatformVelocity(supportCollider, actorCollider, Velocity, PreviousVelocity);
            }
        }

        passengers.Clear();
        body.MovePosition(nextPosition);
    }

    /// <summary>
    /// 끝점까지 일정 속도로 이동하고, 도달하면 지정 시간 대기한 뒤 반대 방향으로 전환한다.
    /// </summary>
    /// <param name="deltaTime">이번 물리 스텝의 시간(초). 이동 거리와 대기 시간 계산에 사용한다.</param>
    /// <returns>이번 스텝의 목표 월드 위치.</returns>
    private Vector3 CalculateNextPosition(float deltaTime)
    {
        if (waitRemaining > 0f)
        {
            waitRemaining = Mathf.Max(0f, waitRemaining - deltaTime);
            return body.position;
        }

        Vector3 destination = movingToEnd ? startPosition + travelOffset : startPosition;
        Vector3 nextPosition = Vector3.MoveTowards(body.position, destination, moveSpeed * deltaTime);
        if ((nextPosition - destination).sqrMagnitude <= 0.000001f)
        {
            movingToEnd = !movingToEnd;
            waitRemaining = endpointWaitTime;
        }

        return nextPosition;
    }

    /// <summary>
    /// 새 충돌의 접촉 방향을 검사하여 위에 놓인 탑승자를 다음 전달 목록에 등록한다.
    /// </summary>
    /// <param name="collision">플랫폼과 충돌한 대상 및 접촉점 정보.</param>
    private void OnCollisionEnter(Collision collision)
    {
        CollectPassengers(collision);
    }

    /// <summary>
    /// 계속 유지되는 충돌에서도 지지 여부를 다시 검사한다. 옆면과 아랫면 접촉은 운반하지 않는다.
    /// </summary>
    /// <param name="collision">현재 유지되는 충돌 정보.</param>
    private void OnCollisionStay(Collision collision)
    {
        CollectPassengers(collision);
    }

    /// <summary>
    /// 플랫폼 관점의 접촉 법선을 반전하여 위쪽 지지면을 찾고, 상대 Rigidbody 루트의 수신 컴포넌트를 조회한다.
    /// 플랫폼과 대상은 각각 하나의 Rigidbody가 담당하며, 지지용 Collider는 이 오브젝트의 BoxCollider이다.
    /// </summary>
    /// <param name="collision">지지 접촉을 검색할 충돌.</param>
    private void CollectPassengers(Collision collision)
    {
        if (!isActiveAndEnabled)
        {
            return;
        }

        float minimumNormalY = Mathf.Cos(maximumSupportAngle * Mathf.Deg2Rad);
        for (int index = 0; index < collision.contactCount; index++)
        {
            ContactPoint contact = collision.GetContact(index);
            if (contact.thisCollider != supportCollider || -contact.normal.y < minimumNormalY)
            {
                continue;
            }

            Collider actorCollider = contact.otherCollider;
            Rigidbody actorBody = actorCollider.attachedRigidbody;
            if (actorBody == null || actorBody == body || actorBody.isKinematic)
            {
                continue;
            }

            if (actorBody.TryGetComponent(out PlatformPassenger passenger) && passenger.isActiveAndEnabled)
            {
                passengers[actorCollider] = passenger;
            }
        }
    }

    /// <summary>
    /// 전달 목록과 공개 속도를 초기화한다. 재활성화하면 현재 위치에서 남은 경로를 계속 진행한다.
    /// </summary>
    private void OnDisable()
    {
        passengers.Clear();
        Velocity = Vector3.zero;
        PreviousVelocity = Vector3.zero;
    }

    /// <summary>
    /// 원래 물리 재질을 복원하고 이 컴포넌트가 생성한 런타임 재질만 해제한다.
    /// </summary>
    private void OnDestroy()
    {
        if (supportCollider != null && supportCollider.sharedMaterial == runtimeMaterial)
        {
            supportCollider.sharedMaterial = originalMaterial;
        }

        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
        }
    }
}
