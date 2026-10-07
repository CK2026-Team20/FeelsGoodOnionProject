using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// 플레이어의 이동 모드에 따라 Cinemachine Camera의 우선순위를 변경합니다.
/// 실제 카메라 전환과 블렌딩은 CinemachineBrain이 담당합니다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera), typeof(CinemachineBrain))]
public sealed class PlayerCameraController : MonoBehaviour
{
    [Tooltip("켜면 App의 출력 Camera/Brain에서 Core의 서로 다른 Side·Quarter 카메라와 플레이어 이동 모드를 Bind로 빌려 연결합니다. 끄면 기존 네 카메라 필수 연결과 독립 동작을 유지합니다. Awake 전에 선택하세요.")]
    [SerializeField] private bool useSeparatedRig;

    [Header("References")]
    [SerializeField]
    private PlayerMovementModeController movementModeController;

    [SerializeField]
    private CinemachineCamera sideCamera;
    [SerializeField]
    private CinemachineCamera quarterCamera;
    [SerializeField]
    private CinemachineCamera backCamera;
    [SerializeField]
    private CinemachineCamera backFixedCamera;

    [Header("Priority")]
    [Tooltip("현재 모드에 해당하는 카메라의 우선순위.")]
    [SerializeField]
    private int selectedPriority = 20;

    [Tooltip("다른 모드의 카메라에 적용할 우선순위.")]
    [SerializeField]
    private int standbyPriority = 10;

    private bool referencesValid;
    private PlayerMovementModeController boundMovementMode;
    private CinemachineCamera boundSide;
    private CinemachineCamera boundQuarter;
    private bool boundSubscribed;
    private bool hasPreview;
    private PlayerMovementMode previewMode;
    private long bindingGeneration;

    /// <summary>명시적으로 분리 리그를 선택하고 유효한 borrowed 참조를 연결했는지 여부입니다.</summary>
    public bool IsBound => useSeparatedRig && boundMovementMode != null && boundSide != null && boundQuarter != null;

    /// <summary>분리 리그에서만 Side/Quarter 우선순위 소유권과 이동 모드 구독을 연결합니다. 재바인딩은 이전 연결을 먼저 해제합니다.</summary>
    /// <remarks>Awake 전 또는 이동 모드의 늦은 Start 전에도 연결할 수 있습니다. Follow/LookAt과 Brain 설정·완료 확인은 외부 조립자가 소유합니다.</remarks>
    public bool Bind(PlayerMovementModeController modes, CinemachineCamera side, CinemachineCamera quarter)
    {
        if (!useSeparatedRig || modes == null || side == null || quarter == null || side == quarter ||
            selectedPriority <= standbyPriority || !IsSeparatedMode(modes.CurrentMode)) return false;
        Unbind();
        bindingGeneration++;
        boundMovementMode = modes;
        boundSide = side;
        boundQuarter = quarter;
        if (isActiveAndEnabled && referencesValid) SubscribeBoundMode();
        ApplyCameraMode(modes.CurrentMode);
        return true;
    }

    /// <summary>자기 구독·미확정 Preview를 버리고 두 카메라를 대기 우선순위로 돌린 뒤 borrowed 참조를 비웁니다.</summary>
    /// <remarks>빌린 Camera·Brain·이동 모드 객체를 파괴하거나 Dispose하지 않습니다. 외부 소유 Follow/LookAt 참조도 조립자가 해제합니다.</remarks>
    public void Unbind()
    {
        bindingGeneration++;
        if (boundSubscribed && boundMovementMode != null) boundMovementMode.ModeChanged -= HandleModeChanged;
        boundSubscribed = false;
        hasPreview = false;
        if (boundSide != null) boundSide.Priority = standbyPriority;
        if (boundQuarter != null) boundQuarter.Priority = standbyPriority;
        boundMovementMode = null;
        boundSide = null;
        boundQuarter = null;
    }

    /// <summary>카메라 우선순위만 먼저 선택합니다. 이동축·속도·출력 Transform은 변경하지 않습니다.</summary>
    /// <remarks>새 Preview는 이전 것을 대체하고 늦은 Start의 초기 모드 통지가 Preview를 덮지 않습니다.</remarks>
    public bool Preview(PlayerMovementMode mode)
    {
        if (!isActiveAndEnabled || !referencesValid || !IsBound || !IsSeparatedMode(mode)) return false;
        previewMode = mode;
        hasPreview = true;
        bindingGeneration++;
        ApplyCameraMode(mode);
        return true;
    }

    /// <summary>외부가 Brain 블렌드·정렬 완료를 확인한 뒤 이동 모드를 확정합니다. 초기화 전 또는 다른 Preview의 오래된 Commit은 거절합니다.</summary>
    public bool Commit(PlayerMovementMode mode)
    {
        if (!isActiveAndEnabled || !referencesValid || !IsBound || !IsSeparatedMode(mode) ||
            (hasPreview && previewMode != mode)) return false;
        long generation = bindingGeneration;
        PlayerMovementModeController modes = boundMovementMode;
        CinemachineCamera side = boundSide;
        CinemachineCamera quarter = boundQuarter;
        if (!modes.TrySetMode(mode)) return false;
        // ModeChanged 관찰자가 새 연결·Preview를 선택했다면 이전 요청은 후속 쓰기를 소유하지 않습니다.
        if (generation != bindingGeneration || modes != boundMovementMode || side != boundSide ||
            quarter != boundQuarter || !isActiveAndEnabled || !IsBound) return false;
        hasPreview = false;
        ApplyCameraMode(mode);
        return true;
    }

    private void SubscribeBoundMode()
    {
        if (!IsBound || boundSubscribed) return;
        boundMovementMode.ModeChanged += HandleModeChanged;
        boundSubscribed = true;
    }

    private static bool IsSeparatedMode(PlayerMovementMode mode) => mode == PlayerMovementMode.Side || mode == PlayerMovementMode.Quarter;

    private void Awake()
    {
        referencesValid = useSeparatedRig || (
            movementModeController != null &&
            sideCamera != null &&
            quarterCamera != null &&
            backCamera != null &&
            backFixedCamera != null &&
            sideCamera != quarterCamera &&
            sideCamera != backCamera &&
            sideCamera != backFixedCamera &&
            quarterCamera != backCamera &&
            quarterCamera != backFixedCamera &&
            backCamera != backFixedCamera);

        if (!referencesValid)
        {
            Debug.LogError("플레이어 이동 모드 컨트롤러와 서로 다른 네 Cinemachine Camera를 연결해야함", this);
            enabled = false;
            return;
        }

        if (selectedPriority <= standbyPriority)
        {
            Debug.LogError("선택된 카메라의 우선순위는 대기 카메라보다 높아야 합니다.", this);
            referencesValid = false;
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (!referencesValid)
        {
            return;
        }

        if (useSeparatedRig)
        {
            SubscribeBoundMode();
            if (IsBound) ApplyCameraMode(hasPreview ? previewMode : boundMovementMode.CurrentMode);
            return;
        }

        movementModeController.ModeChanged += HandleModeChanged;

        // 초기화 이전이라면 설정된 시작 모드를 사용합니다.
        // 재활성화라면 그동안 변경된 현재 모드를 반영합니다.
        ApplyCameraMode(movementModeController.CurrentMode);
    }

    private void OnDisable()
    {
        if (useSeparatedRig)
        {
            Unbind();
            return;
        }
        if (movementModeController != null)
        {
            movementModeController.ModeChanged -= HandleModeChanged;
        }
    }

    private void HandleModeChanged(PlayerMovementMode mode)
    {
        if (useSeparatedRig && hasPreview) return;
        ApplyCameraMode(mode);
    }
    
    /// <summary>
    /// 세 카메라의 우선순위를 현재 모드에 맞게 변경
    /// </summary>
    private void ApplyCameraMode(PlayerMovementMode mode)
    {
        if (useSeparatedRig)
        {
            if (!IsBound || !IsSeparatedMode(mode)) return;
            boundSide.Priority = mode == PlayerMovementMode.Side ? selectedPriority : standbyPriority;
            boundQuarter.Priority = mode == PlayerMovementMode.Quarter ? selectedPriority : standbyPriority;
            return;
        }
        sideCamera.Priority = mode == PlayerMovementMode.Side ? selectedPriority : standbyPriority;
        quarterCamera.Priority = mode == PlayerMovementMode.Quarter ? selectedPriority : standbyPriority;
        backCamera.Priority = mode == PlayerMovementMode.Back ? selectedPriority : standbyPriority;
        backFixedCamera.Priority = mode == PlayerMovementMode.BackFixed ? selectedPriority : standbyPriority;
    }

    private void OnDestroy()
    {
        Unbind();
    }
}
