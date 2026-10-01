using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 임시 키 입력으로 플레이어의 이동 모드를 전환합니다.
/// U: 사이드 모드, I: 쿼터 모드.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMovementModeController))]
public sealed class TestPlayerMovementMode : MonoBehaviour
{
    private PlayerMovementModeController movementModeController;

    private void Awake()
    {
        movementModeController = GetComponent<PlayerMovementModeController>();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        if (keyboard.uKey.wasPressedThisFrame)
        {
            TryChangeMode(PlayerMovementMode.Side);
        }
        else if (keyboard.iKey.wasPressedThisFrame)
        {
            TryChangeMode(PlayerMovementMode.Quarter);
        }
        else if (keyboard.jKey.wasPressedThisFrame)
        {
            TryChangeMode(PlayerMovementMode.Back);
        }
        else if (keyboard.kKey.wasPressedThisFrame)
        {
            TryChangeMode(PlayerMovementMode.BackFixed);
        }
    }

    private void TryChangeMode(PlayerMovementMode mode)
    {
        Debug.Log($"[이동 모드 테스트] 키 입력 수신: {mode}", this);

        if (movementModeController == null)
        {
            Debug.LogError("이동 모드 컨트롤러 참조가 없습니다.", this);
            return;
        }
        PlayerMovementMode previousMode = movementModeController.CurrentMode;
        bool accepted = movementModeController.TrySetMode(mode);
        CharacterMovement movement = GetComponent<CharacterMovement>();
        string depthMovement = movement != null ? movement.AllowDepthMovement.ToString() : "CharacterMovement 없음";

        Debug.Log($"[이동 모드 테스트] " + $"요청 성공={accepted}, " + $"초기화={movementModeController.IsInitialized}, " + $"컨트롤러 활성={movementModeController.isActiveAndEnabled}, " + $"모드={previousMode} → {movementModeController.CurrentMode}, " + $"Z축 이동 허용={depthMovement}",
            this);
    }
}