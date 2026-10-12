using System;
using UnityEngine;
using FeelsGoodOnion.TechSYM.Features;

/// <summary>플레이어의 형태 정의</summary>
public enum PlayerForm
{
    /// <summary>기본 형태</summary>
    Normal = 0,
    /// <summary>작은 형태</summary>
    Small = 1
}

/// <summary>
/// 형태별 외형 전환, Collider 크기 변경,<br/>
/// 껍질 생성·회수 및 복귀 공간 검사를 담당합니다.
/// </summary>
/// <remarks>
/// 루트의 위치와 scale, Rigidbody 속도는 변경하지 않습니다.<br/>
/// 준비 시간과 쿨다운은 스킬 시스템에서 관리합니다.
/// </remarks>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
[RequireComponent(typeof(CharacterMovement), typeof(HeavyState))]
public sealed class PlayerFormController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("비워두면 VisualRoot라는 직계 자식을 찾습니다.")]
    [SerializeField] private Transform visualRoot;

    [Tooltip("PlayerDebris와 플랫폼 이동 처리가 붙은 활성 프리팹입니다.")]
    [SerializeField] private PlayerDebris debrisPrefab;

    [Header("Small Form Collider")]
    [Tooltip("작은 형태의 캡슐 반경입니다. 루트 로컬 단위입니다.")]
    [SerializeField, Min(0.001f)]
    private float smallRadius = 0.25f;

    [Tooltip("작은 형태의 캡슐 전체 높이입니다. 지름 이상이어야 합니다.")]
    [SerializeField, Min(0.002f)]
    private float smallHeight = 1f;

    [Header("Restore Space")]
    [Tooltip("바닥과의 미세한 겹침 허용 거리입니다. 월드 단위입니다.")]
    [SerializeField, Range(0f, 0.02f)]
    private float restoreCheckTolerance = 0.005f;
    
    [Header("Form Movement")]
    [SerializeField, Min(0f)]
    private float normalJumpHeight = 2f;

    [SerializeField, Min(0f)]
    private float smallJumpHeight = 3f;

    [Header("Debris Recovery")]
    [Tooltip("플레이어와 껍질의 루트 위치 사이 회수 가능 거리입니다. 월드 단위입니다.")]
    [SerializeField, Min(0f)]
    private float debrisRecoveryDistance = 2f;

    private CharacterMovement movement;
    private HeavyState heavyState;

    private Rigidbody body;
    private CapsuleCollider capsule;

    private GameObject normalVisual;
    private GameObject smallVisual;
    private Animator normalAnimator;
    private Animator smallAnimator;

    private Vector3 normalCenter;
    private Vector3 localFoot;
    private float normalRadius;
    private float normalHeight;
    private float worldScale;

    private bool initialized;
    private bool debrisRecovered = true;

    /// <summary>현재 형태</summary>
    public PlayerForm CurrentForm { get; private set; } = PlayerForm.Normal;

    public bool IsSmall => CurrentForm == PlayerForm.Small;

    /// <summary>
    /// 현재 소유한 껍질입니다. 없으면 null입니다.
    /// </summary>
    public PlayerDebris OwnedDebris { get; private set; }

    /// <summary>
    /// 생성한 껍질을 회수했는지 확인합니다.<br/>
    /// 단순 파괴는 회수로 취급하지 않습니다.
    /// </summary>
    public bool IsDebrisRecovered => debrisRecovered;

    /// <summary>
    /// 현재 형태의 Animator입니다.<br/>
    /// 해당 외형에 Animator가 없다면 null입니다.
    /// </summary>
    public Animator CurrentAnimator
    {
        get
        {
            EnsureInitialized();
            return IsSmall ? smallAnimator : normalAnimator;
        }
    }

    /// <summary>
    /// 외형과 Collider 전환 완료 후 변경된 형태를 전달합니다.
    /// </summary>
    public event Action<PlayerForm> FormChanged;

    private void Awake()
    {
        EnsureInitialized();
    }

    private void OnEnable()
    {
        if (initialized && OwnedDebris != null)
        {
            OwnedDebris.IgnoreCollisionWith(gameObject);
        }
    }

    /// <summary>
    /// 기본형 Collider와 외형 참조를 초기화합니다.
    /// </summary>
    private void EnsureInitialized()
    {
        if (initialized) return;

        body = GetComponent<Rigidbody>();
        capsule = GetComponent<CapsuleCollider>();
        movement = GetComponent<CharacterMovement>();
        heavyState = GetComponent<HeavyState>();

        if (visualRoot == null)
        {
            visualRoot = transform.Find("VisualRoot");
        }

        if (visualRoot == null || visualRoot.parent != transform)
        {
            throw new InvalidOperationException(
                "플레이어의 직계 자식 VisualRoot가 필요합니다.");
        }

        Transform normal = visualRoot.Find("NormalVisual");
        Transform small = visualRoot.Find("SmallVisual");

        if (normal == null || small == null)
        {
            throw new InvalidOperationException(
                "VisualRoot 아래에 NormalVisual과 SmallVisual이 필요합니다.");
        }

        if (visualRoot.GetComponentsInChildren<Collider>(true).Length > 0 ||
            visualRoot.GetComponentsInChildren<Rigidbody>(true).Length > 0)
        {
            throw new InvalidOperationException(
                "VisualRoot에는 Collider와 Rigidbody를 배치하지 않습니다.");
        }

        if (capsule.direction != 1 ||
            capsule.isTrigger ||
            !capsule.enabled)
        {
            throw new InvalidOperationException(
                "활성화된 Y 방향의 일반 CapsuleCollider가 필요합니다.");
        }

        Vector3 scale = transform.lossyScale;

        if (!IsPositiveFinite(scale.x) ||
            !IsPositiveFinite(scale.y) ||
            !IsPositiveFinite(scale.z) ||
            !Mathf.Approximately(scale.x, scale.y) ||
            !Mathf.Approximately(scale.y, scale.z) ||
            Vector3.Dot(transform.up, Vector3.up) < 0.9999f)
        {
            throw new InvalidOperationException(
                "플레이어 루트는 양의 균일 배율과 월드 Y축 위쪽 방향을 유지해야 합니다.");
        }

        worldScale = scale.x;

        normalRadius = capsule.radius;
        normalHeight = Mathf.Max(capsule.height, normalRadius * 2f);
        normalCenter = capsule.center;

        localFoot =
            normalCenter - Vector3.up * (normalHeight * 0.5f);

        normalVisual = normal.gameObject;
        smallVisual = small.gameObject;

        normalAnimator = normal.GetComponentInChildren<Animator>(true);
        smallAnimator = small.GetComponentInChildren<Animator>(true);

        initialized = true;

        // 초기 형태는 기본형입니다.
        ApplyForm(PlayerForm.Normal);
    }

    /// <summary>
    /// 축소할 수 없는 사유를 반환합니다.<br/>
    /// 플레이어 공통 제한은 스킬 컨트롤러에서 검사합니다.
    /// </summary>
    public PlayerSkillBlockReason GetShrinkBlockReason()
    {
        EnsureInitialized();

        if (!isActiveAndEnabled ||
            debrisPrefab == null ||
            !debrisPrefab.gameObject.activeSelf ||
            !debrisPrefab.enabled)
        {
            return PlayerSkillBlockReason.Unavailable;
        }

        if (IsSmall)
        {
            return PlayerSkillBlockReason.InvalidForm;
        }

        if (OwnedDebris != null)
        {
            return PlayerSkillBlockReason.DebrisNotRecovered;
        }

        return PlayerSkillBlockReason.None;
    }

    /// <summary>
    /// 껍질을 생성한 뒤 작은 형태로 전환합니다.
    /// </summary>
    /// <returns>실제로 전환에 성공했는지 여부</returns>
    public bool Shrink()
    {
        if (GetShrinkBlockReason() != PlayerSkillBlockReason.None)
        {
            return false;
        }

        // 보간된 외형 위치가 아닌 Rigidbody의 물리 위치를 기준으로 합니다.
        Vector3 footPosition =
            body.position + body.rotation * (localFoot * worldScale);

        PlayerDebris debris = Instantiate(
            debrisPrefab,
            footPosition,
            body.rotation);

        if (!debris.Initialize(this, footPosition, body.linearVelocity))
        {
            debris.Remove();

            Debug.LogError(
                "껍질은 활성화된 동적 Rigidbody와 일반 BoxCollider가 필요합니다.",
                this);

            return false;
        }

        OwnedDebris = debris;
        debrisRecovered = false;

        ApplyForm(PlayerForm.Small);
        FormChanged?.Invoke(CurrentForm);

        return true;
    }

    /// <summary>
    /// 현재 소유한 껍질이 회수 가능한 거리 안에 있는지 확인합니다.
    /// </summary>
    public bool CanRecoverNearbyDebris()
    {
        EnsureInitialized();

        if (!isActiveAndEnabled ||
            !IsSmall ||
            debrisRecovered ||
            OwnedDebris == null ||
            !OwnedDebris.isActiveAndEnabled)
        {
            return false;
        }

        Vector3 difference =
            OwnedDebris.transform.position - body.position;

        return difference.sqrMagnitude <=
               debrisRecoveryDistance * debrisRecoveryDistance;
    }

    /// <summary>
    /// 거리 조건을 만족하면 껍질을 회수합니다.<br/>
    /// Q키 입력 등 거리 기반 회수에서 사용합니다.
    /// </summary>
    public bool TryRecoverNearbyDebris()
    {
        if (!CanRecoverNearbyDebris())
        {
            return false;
        }

        return RecoverDebris();
    }

    /// <summary>
    /// 현재 소유한 껍질을 실제로 회수합니다.<br/>
    /// 형태는 변경하지 않습니다.
    /// </summary>
    /// <remarks>
    /// 거리와 입력 조건은 검사하지 않습니다.<br/>
    /// 접촉 회수에서도 접촉 대상이 OwnedDebris인지 확인한 뒤 이 함수를 재사용할 수 있습니다.
    /// </remarks>
    public bool RecoverDebris()
    {
        EnsureInitialized();

        if (!isActiveAndEnabled ||
            !IsSmall ||
            debrisRecovered ||
            OwnedDebris == null ||
            !OwnedDebris.isActiveAndEnabled)
        {
            return false;
        }

        OwnedDebris.Remove();
        OwnedDebris = null;
        debrisRecovered = true;

        return true;
    }

    /// <summary>
    /// 기본 형태로 복귀할 수 없는 사유를 반환합니다.
    /// </summary>
    public PlayerSkillBlockReason GetRestoreBlockReason()
    {
        EnsureInitialized();

        if (!isActiveAndEnabled)
        {
            return PlayerSkillBlockReason.Unavailable;
        }

        if (!IsSmall)
        {
            return PlayerSkillBlockReason.InvalidForm;
        }

        if (!HasRestoreSpace())
        {
            return PlayerSkillBlockReason.NotEnoughSpace;
        }

        return PlayerSkillBlockReason.None;
    }

    /// <summary>
    /// 공간 확인 성공 후 거리와 무관하게 소유 껍질 회수와 기본 형태 복귀를 함께 처리합니다.
    /// </summary>
    public bool RestoreForm()
    {
        if (GetRestoreBlockReason() != PlayerSkillBlockReason.None)
        {
            return false;
        }

        // 실패 시 껍질을 보존합니다. 공간이 생겨도 새 요청 없이는 자동 복귀하지 않습니다.
        if (OwnedDebris != null) OwnedDebris.Remove();
        OwnedDebris = null;
        debrisRecovered = true;
        ApplyForm(PlayerForm.Normal);
        FormChanged?.Invoke(CurrentForm);

        return true;
    }

    /// <summary>
    /// 현재 발점을 유지한 기본형 캡슐의 공간을 검사합니다.
    /// </summary>
    public bool HasRestoreSpace()
    {
        EnsureInitialized();

        Physics.SyncTransforms();

        Vector3 center =
            body.position + body.rotation * (normalCenter * worldScale);

        Vector3 up = body.rotation * Vector3.up;

        float radius = normalRadius * worldScale;
        float height = normalHeight * worldScale;
        float halfSegment = Mathf.Max(0f, height * 0.5f - radius);

        float tolerance = Mathf.Clamp(
            restoreCheckTolerance,
            0f,
            radius * 0.1f);

        Collider[] overlaps = Physics.OverlapCapsule(
            center - up * halfSegment,
            center + up * halfSegment,
            radius - tolerance,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);

        foreach (Collider other in overlaps)
        {
            if (other == null) continue;

            // 자기 자신의 Collider 제외
            if (other.attachedRigidbody == body ||
                other.transform.IsChildOf(transform))
            {
                continue;
            }

            // 소유한 껍질 제외
            if (OwnedDebris != null &&
                other.transform.IsChildOf(OwnedDebris.transform))
            {
                continue;
            }

            // 실제로 충돌하지 않는 대상 제외
            if (Physics.GetIgnoreCollision(capsule, other) ||
                Physics.GetIgnoreLayerCollision(
                    capsule.gameObject.layer,
                    other.gameObject.layer))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    /// <summary>
    /// 형태에 맞춰 Collider, 외형, 점프 높이와 무거움을 변경합니다.
    /// </summary>
    /// <remarks>
    /// 루트 위치와 속도는 유지합니다.<br/>
    /// 이미 진행 중인 점프의 속도는 바꾸지 않습니다.
    /// </remarks>
    private void ApplyForm(PlayerForm form)
    {
        bool useSmall = form == PlayerForm.Small;

        float radius = useSmall ? smallRadius : normalRadius;
        float height = useSmall
            ? Mathf.Max(smallHeight, radius * 2f)
            : normalHeight;

        capsule.radius = radius;
        capsule.height = height;
        capsule.center = useSmall
            ? localFoot + Vector3.up * (height * 0.5f)
            : normalCenter;

        CurrentForm = form;

        movement.SetJumpHeight(
            useSmall ? smallJumpHeight : normalJumpHeight);

        // 기본 형태만 무거운 상태입니다.
        heavyState.enabled = !useSmall;

        if (useSmall)
        {
            normalVisual.SetActive(false);
            smallVisual.SetActive(true);
        }
        else
        {
            smallVisual.SetActive(false);
            normalVisual.SetActive(true);
        }

        body.WakeUp();
    }

    private void OnValidate()
    {
        smallRadius = IsPositiveFinite(smallRadius)
            ? Mathf.Max(0.001f, smallRadius)
            : 0.25f;

        smallHeight = IsPositiveFinite(smallHeight)
            ? Mathf.Max(smallHeight, smallRadius * 2f)
            : smallRadius * 2f;

        restoreCheckTolerance =
            float.IsNaN(restoreCheckTolerance) ||
            float.IsInfinity(restoreCheckTolerance)
                ? 0.005f
                : Mathf.Clamp(restoreCheckTolerance, 0f, 0.02f);
        
        normalJumpHeight = ValidateNonNegative(normalJumpHeight, 2f);
        smallJumpHeight = ValidateNonNegative(smallJumpHeight, 3f);
        debrisRecoveryDistance = ValidateNonNegative(debrisRecoveryDistance, 2f);
    }
    
    private static float ValidateNonNegative(float value, float fallback)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return fallback;
        }

        return Mathf.Max(0f, value);
    }

    private static bool IsPositiveFinite(float value)
    {
        return !float.IsNaN(value) &&
               !float.IsInfinity(value) &&
               value > 0f;
    }

    private void OnDestroy()
    {
        if (OwnedDebris != null)
        {
            OwnedDebris.Remove();
        }
    }
}