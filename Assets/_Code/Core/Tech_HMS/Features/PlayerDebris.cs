using UnityEngine;
using FeelsGoodOnion.TechSYM.Features;

/// <summary>
/// 껍질의 소유자, 초기 배치, 충돌 제외 및 제거를 담당합니다.<br/>
/// 낙하와 플랫폼 운반은 프리팹의 기존 이동 컴포넌트가 담당합니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(BoxCollider),typeof(HeavyState))]
public sealed class PlayerDebris : MonoBehaviour
{
    private Rigidbody body;
    private BoxCollider boxCollider;
    private HeavyState heavyState;
    private bool removing;

    public PlayerFormController Owner { get; private set; }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        boxCollider = GetComponent<BoxCollider>();
        heavyState = GetComponent<HeavyState>();
    }

    private void OnEnable()
    {
        if (Owner != null)
        {
            IgnoreCollisionWith(Owner.gameObject);
        }
    }

    /// <summary>
    /// 껍질 바닥을 발 위치에 맞추고 생성 순간의 속도를 전달합니다.
    /// </summary>
    public bool Initialize(
        PlayerFormController owner,
        Vector3 footPosition,
        Vector3 initialVelocity)
    {
        if (Owner != null ||
            removing ||
            owner == null ||
            !isActiveAndEnabled ||
            body == null ||
            body.isKinematic ||
            !boxCollider.enabled ||
            boxCollider.isTrigger ||
            TryGetComponent(out CharacterMovement _))
        {
            return false;
        }

        Owner = owner;

        Vector3 localBottom =
            boxCollider.center -
            Vector3.up * (boxCollider.size.y * 0.5f);

        Vector3 worldBottomOffset =
            body.rotation *
            Vector3.Scale(localBottom, transform.lossyScale);

        body.position = footPosition - worldBottomOffset;
        body.linearVelocity = initialVelocity;

        heavyState.enabled = true;

        IgnoreCollisionWith(owner.gameObject);

        body.WakeUp();
        return true;
    }

    /// <summary>
    /// 지정한 캐릭터와 껍질 사이의 고체 충돌을 무시합니다.<br/>
    /// Trigger는 회수 감지 등에 사용할 수 있도록 제외합니다.
    /// </summary>
    public void IgnoreCollisionWith(GameObject character)
    {
        if (character == null) return;

        Collider[] characterColliders =
            character.GetComponentsInChildren<Collider>();

        Collider[] debrisColliders =
            GetComponentsInChildren<Collider>();

        foreach (Collider characterCollider in characterColliders)
        {
            if (!characterCollider.enabled ||
                characterCollider.isTrigger)
            {
                continue;
            }

            foreach (Collider debrisCollider in debrisColliders)
            {
                if (!debrisCollider.enabled ||
                    debrisCollider.isTrigger)
                {
                    continue;
                }

                Physics.IgnoreCollision(
                    characterCollider,
                    debrisCollider,
                    true);
            }
        }
    }

    /// <summary>
    /// 즉시 비활성화하고 프레임 끝에 파괴합니다.<br/>
    /// 회수 완료 처리는 소유자의 RecoverDebris에서 담당합니다.
    /// </summary>
    public void Remove()
    {
        if (removing) return;

        removing = true;

        if (heavyState != null)
        {
            heavyState.enabled = false;
        }

        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}