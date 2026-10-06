using System;
using Unity.Cinemachine;
using UnityEngine;

namespace Cooked.Level
{
    /// <summary>Actor-owned camera references authored before the first HMS Awake.</summary>
    [DisallowMultipleComponent, DefaultExecutionOrder(-200)]
    public sealed class ActorCameraRig : MonoBehaviour
    {
        [Tooltip("이 플레이어의 이동 방식·속도·이동축을 관리하는 HMS 컴포넌트입니다. 같은 PlayerRoot 아래 ActorBody의 PlayerMovementModeController를 연결하세요.")]
        [SerializeField] private PlayerMovementModeController movementMode;
        [Tooltip("플레이어 화면을 실제로 출력하는 Camera입니다. 이 Camera에 아래 Brain과 HMS 카메라 제어기가 함께 있어야 합니다. 재시도할 때 새 플레이어의 Camera로 교체됩니다.")]
        [SerializeField] private Camera outputCamera;
        [Tooltip("위의 출력 Camera에 있는 CinemachineBrain입니다. 선택한 카메라의 화면과 전환을 처리하며 실제 블렌드가 끝나야 이동 모드가 확정됩니다.")]
        [SerializeField] private CinemachineBrain brain;
        [Tooltip("위의 출력 Camera에 있는 HMS PlayerCameraController입니다. 같은 플레이어의 이동 모드와 서로 다른 네 CinemachineCamera를 연결하세요.")]
        [SerializeField] private PlayerCameraController hmsController;
        [Tooltip("사이드뷰에서 사용할 CinemachineCamera입니다. 같은 플레이어를 따라가도록 설정하세요. 다른 모드의 Camera와 같은 객체를 연결하면 안 됩니다.")]
        [SerializeField] private CinemachineCamera side;
        [Tooltip("X/Z 두 방향으로 움직이는 쿼터뷰의 CinemachineCamera입니다. 같은 플레이어를 따라가며 다른 세 Camera와 구분해 연결하세요.")]
        [SerializeField] private CinemachineCamera quarter;
        [Tooltip("플레이어의 바라보는 방향을 따르는 백뷰 CinemachineCamera입니다. HMS 기준 설정처럼 VisualRoot를 따라가며 고정 통로뷰 Camera와 구분하세요.")]
        [SerializeField] private CinemachineCamera back;
        [Tooltip("월드 Z축을 따라 움직이는 통로의 고정 백뷰 CinemachineCamera입니다. 같은 플레이어를 따라가며 다른 세 Camera와 구분해 연결하세요.")]
        [SerializeField] private CinemachineCamera backFixed;
        [Tooltip("켜면 세션과 첫 카메라가 준비될 때까지 화면·소리를 숨깁니다. 통합 PlayerRoot에서는 켜고, 세션 없이 직접 실행하는 독립 배치에서는 끄세요.")]
        [SerializeField] private bool waitForSession = true;
        private CinemachineCamera requestedCamera;
        private AudioListener listener;
        public Camera OutputCamera => outputCamera;
        public CinemachineBrain Brain => brain;
        public PlayerMovementModeController MovementMode => movementMode;
        public bool IsReady => movementMode != null && movementMode.isActiveAndEnabled && movementMode.IsInitialized;
        public bool SelectionComplete => requestedCamera != null && brain != null && brain.isActiveAndEnabled &&
            brain.ActiveVirtualCamera == requestedCamera && !brain.IsBlending;

        private void Awake()
        {
            ValidateConfiguration();
            // The physics body may start with a rotated checkpoint pose; fixed camera modes
            // retain the reference scene's world orientation independently of that pose.
            transform.rotation = Quaternion.identity;
            listener = outputCamera.GetComponent<AudioListener>();
            SetOutputVisible(!waitForSession);
        }
        private void OnEnable()
        {
            if (!waitForSession && outputCamera != null) SetOutputVisible(true);
        }
        public void ValidateConfiguration()
        {
            if (movementMode == null || outputCamera == null || brain == null || hmsController == null ||
                side == null || quarter == null || back == null || backFixed == null)
                throw new InvalidOperationException("Actor camera requires HMS mode/controller, Camera/Brain and four cameras.");
            if (side == quarter || side == back || side == backFixed || quarter == back || quarter == backFixed || back == backFixed)
                throw new InvalidOperationException("Actor camera modes require distinct Cinemachine cameras.");
            if (brain.gameObject != outputCamera.gameObject || hmsController.gameObject != outputCamera.gameObject)
                throw new InvalidOperationException("HMS camera controller and Brain must share the output Camera.");
        }
        public CinemachineCamera CameraFor(PlayerMovementMode mode)
        {
            return mode switch
            {
                PlayerMovementMode.Side => side,
                PlayerMovementMode.Quarter => quarter,
                PlayerMovementMode.Back => back,
                PlayerMovementMode.BackFixed => backFixed,
                _ => throw new ArgumentOutOfRangeException(nameof(mode))
            };
        }
        /// <summary>Preview a requested view while input is blocked. HMS remains the movement-mode owner.</summary>
        public void SelectCamera(PlayerMovementMode mode, bool snap, float seconds)
        {
            ValidateConfiguration();
            requestedCamera = CameraFor(mode);
            brain.DefaultBlend = new CinemachineBlendDefinition(snap ? CinemachineBlendDefinition.Styles.Cut :
                CinemachineBlendDefinition.Styles.EaseInOut, snap ? 0 : seconds);
            side.Priority = side == requestedCamera ? 20 : 10;
            quarter.Priority = quarter == requestedCamera ? 20 : 10;
            back.Priority = back == requestedCamera ? 20 : 10;
            backFixed.Priority = backFixed == requestedCamera ? 20 : 10;
            if (snap) requestedCamera.PreviousStateIsValid = false;
        }
        public bool CommitMode(PlayerMovementMode mode) => IsReady && movementMode.TrySetMode(mode);
        public void CancelSelection()
        {
            if (movementMode != null && brain != null) SelectCamera(movementMode.CurrentMode, true, 0);
            requestedCamera = null;
        }
        public void SetOutputVisible(bool visible)
        {
            if (outputCamera == null) return;
            outputCamera.enabled = visible;
            if (listener == null) listener = outputCamera.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = visible;
        }
        private void OnDisable() { SetOutputVisible(false); requestedCamera = null; }
    }
}
