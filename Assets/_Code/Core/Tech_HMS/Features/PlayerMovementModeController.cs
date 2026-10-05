using System;
using UnityEngine;

public enum PlayerMovementMode
{
    /// <summary>월드 X축 이동.</summary>
    Side = 0,
    /// <summary>월드 X, Z축 이동.</summary>
    Quarter = 1,
    /// <summary>월드 Z축 이동, VisualRoot의 Z+ 기준으로 따라가는 백뷰.</summary>
    Back = 2,
    /// <summary>월드 Z축 이동, 월드 +Z 방향을 바라보는 고정 백뷰.</summary>
    BackFixed = 3
}

/// <summary>
/// 플레이어의 이동 모드와 모드별 이동 설정을 관리합니다.
/// </summary>
/// <remarks>
/// 점프 높이는 형태 전환 시스템에서 관리합니다.
/// 카메라와 애니메이션은 현재 모드 또는 변경 이벤트를 이용해 연결합니다.
/// </remarks>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerFacade))]
public sealed class PlayerMovementModeController : MonoBehaviour
{
    [Header("Initial Mode")]
    [SerializeField]
    private PlayerMovementMode initialMode = PlayerMovementMode.Side;

    [Header("Movement Speed")]
    [Tooltip("사이드 모드의 최대 자체 이동 속도(m/s).")]
    [SerializeField, Min(0f)]
    private float sideMoveSpeed = 5f;

    [Tooltip("쿼터 모드의 최대 자체 이동 속도(m/s). 사이드 속도 이하로 제한합니다.")]
    [SerializeField, Min(0f)]
    private float quarterMoveSpeed = 3f;
    
    [Tooltip("백뷰 모드의 최대 자체 이동 속도(m/s).")]
    [SerializeField, Min(0f)]
    private float backMoveSpeed = 5f;

    private PlayerFacade player;

    /// <summary>초기 이동 설정이 적용되었는지 여부.</summary>
    public bool IsInitialized { get; private set; }

    /// <summary>
    /// 현재 이동 모드입니다.
    /// 초기화 전에는 시작 모드를 반환합니다.
    /// </summary>
    public PlayerMovementMode CurrentMode =>
        IsInitialized ? currentMode : initialMode;

    private PlayerMovementMode currentMode;

    /// <summary>
    /// 초기 모드가 적용되거나 현재 모드가 변경된 뒤 발생합니다.
    /// </summary>
    public event Action<PlayerMovementMode> ModeChanged;

    private void Awake()
    {
        player = GetComponent<PlayerFacade>();
    }

    private void Start()
    {
        // CharacterMovement의 Awake가 완료된 이후 적용합니다.
        if (IsInitialized)
        {
            return;
        }

        ValidateSettings();
        ApplyMode(initialMode);
    }

    /// <summary>
    /// 지정한 모드로 변경을 시도합니다.
    /// 같은 모드라면 설정을 다시 적용하거나 이벤트를 발생시키지 않습니다.
    /// </summary>
    /// <param name="mode">적용할 이동 모드.</param>
    /// <returns>
    /// 요청한 모드가 적용되어 있으면 true,
    /// 비활성 또는 초기화 전이라면 false.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// 정의되지 않은 모드인 경우입니다.
    /// </exception>
    public bool TrySetMode(PlayerMovementMode mode)
    {
        if (!IsValidMode(mode))
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }

        if (!isActiveAndEnabled || !IsInitialized)
        {
            return false;
        }

        if (currentMode == mode)
        {
            return true;
        }

        ApplyMode(mode);
        return true;
    }

    private void ApplyMode(PlayerMovementMode mode)
    {
        switch (mode)
        {
            case PlayerMovementMode.Side: player.ApplyMovementModeSettings(sideMoveSpeed, allowHorizontalMovement: true, allowDepthMovement: false);
                break;
            case PlayerMovementMode.Quarter: player.ApplyMovementModeSettings( quarterMoveSpeed, allowHorizontalMovement: true, allowDepthMovement: true);
                break;
            case PlayerMovementMode.Back: player.ApplyMovementModeSettings(backMoveSpeed, allowHorizontalMovement: false, allowDepthMovement: true);
                break;
            case PlayerMovementMode.BackFixed: player.ApplyMovementModeSettings(backMoveSpeed, allowHorizontalMovement: false, allowDepthMovement: true);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mode), "존재하지 않는 이동 모드");
        }
        currentMode = mode;
        IsInitialized = true;

        ModeChanged?.Invoke(currentMode);
    }

    private void OnValidate()
    {
        ValidateSettings();
    }

    /// <summary>
    /// 속도를 유한한 음이 아닌 값으로 보정하고,
    /// 쿼터 속도가 사이드 속도를 초과하지 않도록 제한합니다.
    /// </summary>
    private void ValidateSettings()
    {
        if (!IsValidMode(initialMode)) initialMode = PlayerMovementMode.Side;
        sideMoveSpeed = SanitizeSpeed(sideMoveSpeed);
        quarterMoveSpeed = Mathf.Min(SanitizeSpeed(quarterMoveSpeed), sideMoveSpeed);
        backMoveSpeed = SanitizeSpeed(backMoveSpeed);
    }
    

    private static float SanitizeSpeed(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
        {
            return 0f;
        }

        return Mathf.Max(0f, value);
    }

    private static bool IsValidMode(PlayerMovementMode mode) => (mode == PlayerMovementMode.Side || mode == PlayerMovementMode.Quarter || mode == PlayerMovementMode.Back || mode ==  PlayerMovementMode.BackFixed);
}