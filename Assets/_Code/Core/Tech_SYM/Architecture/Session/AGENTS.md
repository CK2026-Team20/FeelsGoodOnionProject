# 세션과 플레이어 연결

## 책임과 경계

GameSessionService는 체크포인트·해금을, PlayerBridgeService는 HMS 공개 API 연결을, ActorHost는 플레이어 생성·반환을 소유한다. HMS 원본의 이동·폼·스킬 규칙을 복제하지 않는다.

## 유지할 계약

PlayerRoot는 비활성 프리팹으로 보관하고 의존성이 갖춰진 뒤 활성화한다. PlayerInputReader·GameplayInputRouter는 하나씩이며 기존 PlayerController·CheckInteract·테스트 입력을 함께 넣지 않는다.

빈 PlayerRoot 아래 ActorBody에 Facade·물리 컴포넌트를 함께 두는 현행 경계를 유지한다. Scene과 Actor의 임시 참조는 제거 통지 후 정리한다. 자기 차단 토큰만 해제한다.

HMS Mode/Visual과 Actor 소유 CameraRig의 출력 Brain·HMS CameraController·네 카메라 참조는 첫 Awake 전에 직렬화로 조립한다. TrySetMode는 Start의 IsInitialized 이후 사용하고 Bridge는 CharacterMovement의 공개 축 상태를 소비하며 별도 축 원본을 두지 않는다. 보호 HMS 원본 수정이나 런타임 private 필드 주입으로 초기화 순서를 우회하지 않는다. 자기 소유 Inspector·배치 override 조립은 참조 허용에 포함한다.

## 검증

같은 체크포인트 재진입, 능력 해금 유지, 조각 복원, 준비 취소, 재진입 중 종료 오류, 중복 Dispose, Actor와 임시 객체 잔존 여부를 검증한다.
