using System;
using UnityEngine;

/// <summary>플레이어 상태와 이벤트를 받아 애니메이션 재생 요청을 관리합니다.</summary>
/// <remarks>
/// PlayerFacade가 있는 루트에 추가합니다. 실제 Animator 연결은 PlaybackRequested 수신 측에서 담당합니다.<br/>
/// 재생 수신 측은 활성화 시 CurrentRequest를 조회하고, 이후 변경 이벤트를 구독해야 합니다.<br/>
/// 클립이 없거나 재생할 수 없는 일회성 요청도 TryComplete를 호출하여 기본 표현으로 복귀시켜야 합니다.
/// </remarks>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerFacade))]
[DefaultExecutionOrder(100)]
public sealed class PlayerAnimationController : MonoBehaviour
{
    [Header("Movement Animation")]
    [Tooltip("걷기 애니메이션을 사용할 이동 모드입니다. 나머지 모드는 달리기를 사용합니다.\n(실제 이동 속도와는 관련 X)")]
    [SerializeField] private PlayerMovementMode walkMode = PlayerMovementMode.Quarter;

    [Header("Debug")]
    [Tooltip("실제 클립 연결 전 재생 요청 변경을 Console에서 확인합니다.")]
    [SerializeField] private bool logPlaybackRequests;

    private PlayerFacade player;
    private CharacterMovement movement;
    private PlayerMovementModeController movementMode;
    private PlayerFormController formController;
    private readonly PlayerAnimationPlayback playback = new PlayerAnimationPlayback();
    private long publishedRequestId;

    /// <summary>현재 재생 대상으로 선택한 요청. 실제 클립 재생 여부를 뜻하지 않습니다.</summary>
    public PlayerAnimationRequest CurrentRequest => playback.CurrentRequest;
    /// <summary>플랫폼 운반·외력을 제외한 자체 수평 속력. 재생 속도 조절에 사용합니다. (단위: m/s)</summary>
    public float SelfHorizontalSpeed => movement != null ? movement.SelfHorizontalSpeed : 0f;
    /// <summary>현재 형태. 두 형태에서 동일한 애니메이션 종류를 사용합니다.</summary>
    public PlayerForm CurrentForm => formController != null ? formController.CurrentForm : PlayerForm.Normal;
    /// <summary>재생 대상이 변경되거나 일회성 표현을 다시 시작할 때 새 요청을 전달합니다.</summary>
    public event Action<PlayerAnimationRequest> PlaybackRequested;

    /// <summary>플레이어와 표현 판단에 필요한 컴포넌트를 캐싱합니다.</summary>
    private void Awake()
    {
        player = GetComponent<PlayerFacade>();
        movement = GetComponent<CharacterMovement>();
        movementMode = GetComponent<PlayerMovementModeController>();
        formController = GetComponent<PlayerFormController>();
    }

    /// <summary>일회성 표현 이벤트를 구독하고 첫 갱신에서 현재 요청을 전달하도록 준비합니다.</summary>
    private void OnEnable()
    {
        player.Landed += HandleLanded;
        player.StateChanged += HandleStateChanged;
        player.SkillAnimationRequested += HandleSkillAnimationRequested;
        publishedRequestId = 0;
    }

    /// <summary>Facade의 스킬 요청 처리 이후 최신 상태를 반영하고 변경된 재생 요청을 전달합니다.</summary>
    private void LateUpdate()
    {
        RefreshContext();
        PublishRequest();
    }

    /// <summary>현재 FSM과 이동 모드로 기본 표현 및 중단 조건을 갱신합니다.</summary>
    private void RefreshContext()
    {
        bool useWalk = movementMode != null && movementMode.CurrentMode == walkMode;
        playback.Refresh(player.CurrentState, useWalk);
    }

    /// <summary>짧은 넉백·사망도 놓치지 않도록 상태 변경 즉시 중단 조건을 반영합니다.</summary>
    /// <param name="previousState">변경 전 상태</param>
    /// <param name="currentState">변경 후 상태</param>
    private void HandleStateChanged(PlayerState previousState, PlayerState currentState)
    {
        RefreshContext();
    }

    /// <summary>실제 착지 이벤트를 일회성 표현으로 요청합니다. 이동·재점프 상태에서는 거절됩니다.</summary>
    private void HandleLanded()
    {
        RefreshContext();
        playback.TryRequest(PlayerAnimationId.Land);
    }

    /// <summary>스킬 발동 표현을 요청합니다. 스킬의 실제 효과나 조작 허용 여부는 변경하지 않습니다.</summary>
    /// <param name="skill">Facade에서 선택한 스킬 종류</param>
    private void HandleSkillAnimationRequested(PlayerSkillId skill)
    {
        RefreshContext();
        switch (skill)
        {
            case PlayerSkillId.Tear: playback.TryRequest(PlayerAnimationId.Tear); break;
            case PlayerSkillId.Shrink: playback.TryRequest(PlayerAnimationId.Shrink); break;
            case PlayerSkillId.RestoreForm: playback.TryRequest(PlayerAnimationId.RestoreForm); break;
        }
    }

    /// <summary>일회성 재생 완료를 보고합니다. 새 기본 표현은 다음 LateUpdate에서 전달됩니다.</summary>
    /// <param name="requestId">재생 시작 시 받은 요청 식별값. 현재 값을 다시 조회하지 말고 시작 시 값을 보관해야 합니다.</param>
    /// <returns><c>true</c>: 현재 일회성 요청 완료<br/><c>false</c>: 비활성 상태 또는 유효하지 않은 완료 알림</returns>
    public bool TryComplete(long requestId)
    {
        if (!isActiveAndEnabled) return false;
        RefreshContext();
        return playback.TryComplete(requestId);
    }

    /// <summary>새 요청만 전달하여 동일 기본 애니메이션을 매 프레임 다시 시작하지 않도록 합니다.</summary>
    private void PublishRequest()
    {
        PlayerAnimationRequest request = playback.CurrentRequest;
        if (publishedRequestId == request.RequestId) return;
        publishedRequestId = request.RequestId;
        if (logPlaybackRequests) Debug.Log($"[Player Animation] {request.Animation} / 요청: {request.RequestId} / 일회성: {request.IsOneShot}", this);
        PlaybackRequested?.Invoke(request);
    }

    /// <summary>이벤트 구독과 남은 재생 요청을 정리합니다.</summary>
    private void OnDisable()
    {
        player.Landed -= HandleLanded;
        player.StateChanged -= HandleStateChanged;
        player.SkillAnimationRequested -= HandleSkillAnimationRequested;
        playback.Reset();
    }

    /// <summary>클립 연결 전 Inspector 메뉴에서 현재 일회성 표현의 완료를 시험합니다.</summary>
    [ContextMenu("Debug/Complete Current One Shot")]
    private void CompleteCurrentOneShot()
    {
        if (!Application.isPlaying) return;
        TryComplete(CurrentRequest.RequestId);
    }
}
