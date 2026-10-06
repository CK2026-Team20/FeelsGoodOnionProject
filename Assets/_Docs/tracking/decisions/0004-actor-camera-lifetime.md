# Actor와 Cinemachine 카메라의 수명

2026-10-06. 활성 씬 진입·HMS 조립의 승인 범위에서 적용한 구현 결정이다.

## 제약과 선택

HMS CameraController·MovementModeController는 첫 Awake/Start에 같은 Actor의 참조를 사용한다. retry는 Actor를 교체하지만 Core의 세션 서비스와 UI 모델은 유지한다. 보호 HMS 코드를 바꾸거나 활성화 이후 private 참조를 주입하는 방법은 허용하지 않는다.

비활성 PlayerRoot 안에 ActorCameraRig, 출력 Camera/Brain 및 네 고유 CinemachineCamera를 함께 조립한다. ModeController·VisualController·HMS CameraController의 serialized 참조와 Follow/LookAt을 첫 활성화 전에 연결한다. 지속 CoreWorldBinding/CoreUiBinding은 ActorRemoving에서 빌린 참조를 비우고 ActorReady에서 새 출력과 연결한다.

Core에 카메라를 영구 보관하면 재시도할 때 이미 활성화된 HMS 카메라 구성에 새 Actor 참조를 늦게 넣어야 한다. 현재 공개 초기화 계약과 원본 보호 조건에서 그 의존성을 추가하기보다 Actor와 카메라의 수명을 함께 묶었다.

## 결과와 검증

retry마다 출력/Brain/네 카메라를 새로 만들고 이전 묶음을 정리한다. 준비 완료는 HMS IsInitialized와 실제 Brain 선택 이후이며 Core UI 모델을 새로 만들지 않는다. HMS는 mode·speed·축·외형의 권위 상태를 소유하고 SYM은 priority preview·물리 평면 정렬·자기 input lease만 조정한다.

native 저장 참조 대조, 세 Stage 초기 모드, 카메라 두 게임·게임당2 retry/실제 Esc 정지, 실제 InputSystem 두 판에서 확인했다. 실제 아트 클립과 새 Windows build 검증은 이 결정의 검증 범위에 포함하지 않는다. 요구·실행 증거는 tracking/ready-editor-entry-hms-2026-10-06의 명세와 현재 결과에 보존한다.
