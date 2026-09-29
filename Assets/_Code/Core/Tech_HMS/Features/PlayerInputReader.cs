using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class PlayerInputReader : MonoBehaviour
{
    private InputAction moveAction;
    private InputAction jumpAction;
    private InputAction recoverDebrisAction;
    private InputAction formChangeAction;

    public event Action InputDisabled;
    public bool CanReadInput => isActiveAndEnabled && moveAction != null && jumpAction != null && moveAction.enabled && jumpAction.enabled;
    public Vector2 MoveInput => CanReadInput ? moveAction.ReadValue<Vector2>() : Vector2.zero;
    public bool JumpPressedThisFrame => CanReadInput && jumpAction.WasPressedThisFrame();
    private InputAction tearSkillAction;
    /// <summary>눈물 스킬 버튼을 이번 프레임에 새로 눌렀는지 확인</summary>
    public bool TearSkillPressedThisFrame => CanReadInput && tearSkillAction != null && tearSkillAction.enabled && tearSkillAction.WasPressedThisFrame();
    /// <summary>껍질 회수 버튼을 이번 프레임에 새로 눌렀는지 확인</summary>
    public bool RecoverDebrisPressedThisFrame => CanReadInput && recoverDebrisAction != null && recoverDebrisAction.enabled && recoverDebrisAction.WasPressedThisFrame();
    /// <summary>형태 전환 버튼을 이번 프레임에 새로 눌렀는지 확인</summary>
    public bool FormChangePressedThisFrame => CanReadInput && formChangeAction != null && formChangeAction.enabled && formChangeAction.WasPressedThisFrame();

    /// <summary>
    /// 이동과 점프 InputAction을 생성하고 키보드 바인딩을 구성
    /// </summary>
    private void Awake()
    {
        CreateMoveAction();
        CreateJumpAction();
        CreateTearSkillAction();
        CreateRecoverDebrisAction();
        CreateFormChangeAction();
    }

    /// <summary>
    /// 입력 액션을 활성화하여 키보드 입력을 받을 수 있도록 함
    /// </summary>
    private void OnEnable()
    {
        moveAction.Enable();
        jumpAction.Enable();
        tearSkillAction.Enable();
        recoverDebrisAction.Enable();
        formChangeAction.Enable();
    }

    /// <summary>
    /// 입력 액션을 비활성화하고 구독자에게 입력 중단을 알려 남아 있는 명령을 해제
    /// </summary>
    private void OnDisable()
    {
        moveAction?.Disable();
        jumpAction?.Disable();
        tearSkillAction.Disable();
        recoverDebrisAction.Disable();
        formChangeAction.Disable();
        
        // 입력이 꺼졌음을 전달해 남아 있는 입력을 해제
        InputDisabled?.Invoke();
    }

    /// <summary>
    /// 직접 생성한 입력 액션을 해제하여 사용 중인 자원을 정리
    /// </summary>
    private void OnDestroy()
    {
        moveAction?.Dispose();
        jumpAction?.Dispose();
        tearSkillAction?.Dispose();
        recoverDebrisAction?.Dispose();
        formChangeAction?.Dispose();
    }

    /// <summary>
    /// WASD와 방향키를 정규화하지 않은 2차원 입력으로 구성하고, 허용 축을 제거한 뒤 이동 담당이 입력 크기를 제한
    /// </summary>
    private void CreateMoveAction()
    {
        moveAction = new InputAction("Move", InputActionType.Value);

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
    
    private void CreateJumpAction()
    {
        jumpAction = new InputAction("Jump", InputActionType.Button);
        jumpAction.AddBinding("<Keyboard>/space");
    }
    
    private void CreateTearSkillAction()
    {
        tearSkillAction = new InputAction( "TearSkill", InputActionType.Button);
        tearSkillAction.AddBinding("<Keyboard>/e");
    }
    
    private void CreateRecoverDebrisAction()
    {
        recoverDebrisAction = new InputAction( "RecoverDebris", InputActionType.Button);
        recoverDebrisAction.AddBinding("<Keyboard>/q");
    }
    
    private void CreateFormChangeAction()
    {
        formChangeAction = new InputAction( "FormChange", InputActionType.Button);
        formChangeAction.AddBinding("<Keyboard>/r");
    }
}
