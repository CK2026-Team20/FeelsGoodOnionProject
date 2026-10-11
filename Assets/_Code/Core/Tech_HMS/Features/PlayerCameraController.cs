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

    private void Awake()
    {
        referencesValid =
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
            backCamera != backFixedCamera;

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

        movementModeController.ModeChanged += HandleModeChanged;

        // 초기화 이전이라면 설정된 시작 모드를 사용합니다.
        // 재활성화라면 그동안 변경된 현재 모드를 반영합니다.
        ApplyCameraMode(movementModeController.CurrentMode);
    }

    private void OnDisable()
    {
        if (movementModeController != null)
        {
            movementModeController.ModeChanged -= HandleModeChanged;
        }
    }

    private void HandleModeChanged(PlayerMovementMode mode)
    {
        ApplyCameraMode(mode);
    }
    
    /// <summary>
    /// 세 카메라의 우선순위를 현재 모드에 맞게 변경
    /// </summary>
    private void ApplyCameraMode(PlayerMovementMode mode)
    {
        sideCamera.Priority = mode == PlayerMovementMode.Side ? selectedPriority : standbyPriority;
        quarterCamera.Priority = mode == PlayerMovementMode.Quarter ? selectedPriority : standbyPriority;
        backCamera.Priority = mode == PlayerMovementMode.Back ? selectedPriority : standbyPriority;
        backFixedCamera.Priority = mode == PlayerMovementMode.BackFixed ? selectedPriority : standbyPriority;
    }
}