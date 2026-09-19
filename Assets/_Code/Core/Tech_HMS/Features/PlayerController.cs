using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInputReader))]
[RequireComponent(typeof(CharacterMovement))]
public sealed class PlayerController : MonoBehaviour
{
    private PlayerInputReader inputReader;
    private CharacterMovement movement;

    /// <summary>
    /// 같은 오브젝트의 입력 판독기와 이동 컴포넌트를 캐싱한다.
    /// </summary>
    private void Awake()
    {
        inputReader = GetComponent<PlayerInputReader>();
        movement = GetComponent<CharacterMovement>();
    }

    /// <summary>
    /// 입력 중단 이벤트를 구독하여 입력 컴포넌트가 꺼질 때 이동과 점프 요청을 해제한다.
    /// </summary>
    private void OnEnable()
    {
        inputReader.InputDisabled += ResetInput;
    }

    /// <summary>
    /// 입력 중단 이벤트 구독을 해제하고 남은 이동 입력과 실행 전 점프 요청을 지운다.
    /// </summary>
    private void OnDisable()
    {
        inputReader.InputDisabled -= ResetInput;
        ResetInput();
    }

    /// <summary>
    /// 입력과 이동 컴포넌트가 사용 가능하면 이동과 점프 명령을 전달하고, 그렇지 않으면 남은 명령을 해제한다.
    /// </summary>
    private void Update()
    {
        if (!inputReader.CanReadInput || !movement.isActiveAndEnabled)
        {
            ResetInput();
            return;
        }

        UpdateMovementInput();
        UpdateJumpInput();
    }

    /// <summary>
    /// 입력의 가로와 세로를 월드 X와 Z로 변환한다. 사이드 모드에서는 Z를 먼저 제거하여 대각 입력에 의한 좌우 감속을 방지한다.
    /// </summary>
    private void UpdateMovementInput()
    {
        Vector2 input = inputReader.MoveInput;

        // 현재 테스트 씬의 월드 기준:
        // 좌우 입력 → X, 위아래 입력 → Z.
        Vector3 worldDirection = new Vector3(input.x, 0f, input.y);

        // 정규화 전에 허용하지 않는 축을 제거한다.
        // 사이드뷰에서 W+D를 눌러도 좌우 속도가 줄지 않게 한다.
        if (!movement.AllowDepthMovement)
        {
            worldDirection.z = 0f;
        }

        movement.SetMoveInput(worldDirection);
    }

    /// <summary>
    /// 스페이스바를 새로 누른 프레임에만 점프를 요청한다. 키 유지나 해제는 점프 높이에 영향을 주지 않는다.
    /// </summary>
    private void UpdateJumpInput()
    {
        if (inputReader.JumpPressedThisFrame)
        {
            movement.RequestJump();
        }
    }

    /// <summary>
    /// 이동 입력을 0으로 설정하고 실행 전 점프 요청을 취소한다. 이미 시작한 점프는 중단하지 않는다.
    /// </summary>
    private void ResetInput()
    {
        if (movement == null)
        {
            return;
        }

        movement.SetMoveInput(Vector3.zero);
        movement.CancelJumpRequest();
    }
}
