using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class PlayerInputReader : MonoBehaviour
{
    private InputAction moveAction;
    private InputAction jumpAction;

    public event Action InputDisabled;

    public bool CanReadInput => isActiveAndEnabled &&
        moveAction != null && jumpAction != null && moveAction.enabled && jumpAction.enabled;

    public Vector2 MoveInput => CanReadInput ? moveAction.ReadValue<Vector2>() : Vector2.zero;

    public bool JumpPressedThisFrame => CanReadInput && jumpAction.WasPressedThisFrame();

    /// <summary>
    /// 이동과 점프 InputAction을 생성하고 키보드 바인딩을 구성한다.
    /// </summary>
    private void Awake()
    {
        CreateMoveAction();
        CreateJumpAction();
    }

    /// <summary>
    /// 입력 액션을 활성화하여 키보드 입력을 받을 수 있게 한다.
    /// </summary>
    private void OnEnable()
    {
        moveAction.Enable();
        jumpAction.Enable();
    }

    /// <summary>
    /// 입력 액션을 비활성화하고 구독자에게 입력 중단을 알려 남아 있는 명령을 해제하게 한다.
    /// </summary>
    private void OnDisable()
    {
        moveAction?.Disable();
        jumpAction?.Disable();

        // 입력이 꺼졌음을 전달해 남아 있는 입력을 해제한다.
        InputDisabled?.Invoke();
    }

    /// <summary>
    /// 직접 생성한 입력 액션을 해제하여 사용 중인 자원을 정리한다.
    /// </summary>
    private void OnDestroy()
    {
        moveAction?.Dispose();
        jumpAction?.Dispose();
    }

    /// <summary>
    /// WASD와 방향키를 정규화하지 않은 2차원 입력으로 구성한다. 허용 축을 제거한 뒤 이동 담당이 입력 크기를 제한한다.
    /// </summary>
    private void CreateMoveAction()
    {
        moveAction = new InputAction("Move", InputActionType.Value);

        // 정규화하지 않은 디지털 입력을 받는다.
        // 이동 가능한 축을 결정한 뒤 CharacterMovement에서 크기를 제한한다.
        moveAction.AddCompositeBinding("2DVector(mode=1)")
            .With("Up", "<Keyboard>/w")
            .With("Down", "<Keyboard>/s")
            .With("Left", "<Keyboard>/a")
            .With("Right", "<Keyboard>/d");

        moveAction.AddCompositeBinding("2DVector(mode=1)")
            .With("Up", "<Keyboard>/upArrow")
            .With("Down", "<Keyboard>/downArrow")
            .With("Left", "<Keyboard>/leftArrow")
            .With("Right", "<Keyboard>/rightArrow");
    }

    /// <summary>
    /// 스페이스바를 점프 버튼으로 등록한다. 유지 시간이 아닌 누른 프레임을 Controller에서 확인한다.
    /// </summary>
    private void CreateJumpAction()
    {
        jumpAction = new InputAction("Jump", InputActionType.Button);

        jumpAction.AddBinding("<Keyboard>/space");
    }
}
