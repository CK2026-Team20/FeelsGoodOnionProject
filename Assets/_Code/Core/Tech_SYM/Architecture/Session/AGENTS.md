# 세션과 플레이어 연결

## 책임과 경계

GameSessionService는 체크포인트·해금을, PlayerBridgeService는 HMS 공개 API 연결을, ActorHost는 플레이어 생성·반환을 소유한다. HMS 원본의 이동·폼·스킬 규칙을 복제하지 않는다.

## 유지할 계약

PlayerRoot는 비활성 프리팹으로 보관하고 의존성이 갖춰진 뒤 활성화한다. PlayerInputReader·GameplayInputRouter는 하나씩이며 기존 PlayerController·CheckInteract·테스트 입력을 함께 넣지 않는다.

빈 PlayerRoot 아래 ActorBody에 Facade·물리 컴포넌트를 함께 두는 현행 경계를 유지한다. Scene과 Actor의 임시 참조는 제거 통지 후 정리한다. 자기 차단 토큰만 해제한다.

현재 작업자는 Tech_SYM이며 변경은 승인된 Tech_SYM 범위에 한정한다. Tech_HMS 등 다른 작업자 폴더, 공용 Interfaces, _Art 원본과 그 .meta는 참조 전용이다. 같은 직군의 별도 명시적 파일 허용이 없는 한 수정하지 않는다.

## 검증

같은 체크포인트 재진입, 능력 해금 유지, 조각 복원, 준비 취소, 재진입 중 종료 오류, 중복 Dispose, Actor와 임시 객체 잔존 여부를 검증한다.

실행하지 않은 검증을 통과로 기록하지 않는다. Editor의 자산 생성 메뉴와 읽기·검증 메뉴를 구분하며 다른 기능의 씬을 재생성하지 않는다.
