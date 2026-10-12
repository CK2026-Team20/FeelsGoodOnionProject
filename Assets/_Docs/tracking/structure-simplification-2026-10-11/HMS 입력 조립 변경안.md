# HMS 기본 입력 조립 변경안

2026-10-11 사용자 요청: HMS PlayerController를 기본으로 조립하고 GameplayInputRouter를 제거한다. 모달·F·사망 진행은 기존 Host와 Bridge에 연결한다. 보호된 HMS 수정 파일과 내용을 먼저 제시한다.

현재 상태: **사용자가 HMS 한 파일 변경을 승인하여 소스·자산 이관과 Router 삭제를 적용했다.** 아래 설계는 승인된 변경 근거다. 실제 검증 결과는 이 폴더의 HMS 입력 조립 검증 기록을 따른다.

## 목표와 참고 자료

- 입력 실행: HMS PlayerInputReader → PlayerController → PlayerFacade → 이동·스킬 구현.
- 조립/수명/F: 기존 SessionActorHost와 PlayerInteractionService.
- 세션 정책 연결: 기존 PlayerBridgeService, GameSessionService, GameplayControlService.
- 근거: 위 실제 소스, PlayerSkillController/SkillSlot/PlayerSkillTypes, ShrinkSkill.asset/RestoreSkill.asset, CoreWorldBinding, 현행 planning-index와 V1.5 실행 명세. 최신 사용자 지시가 우선이다.
- 제약: HMS 원본은 파일별 허용 전 수정하지 않는다. 새 Router 대체 클래스·프리팹 재생성·Apply·Play 실행을 추가하지 않는다. ActorBody의 물리 배치를 보존한다.

## 먼저 확인받을 HMS 변경: 정확히 한 파일

`Assets/_Code/Core/Tech_HMS/Features/PlayerSkillController.cs`

후보 전체 소스와 unified diff:

- `output/Tech_SYM/hms-input-assembly/PlayerSkillController.proposed.cs`
- `output/Tech_SYM/hms-input-assembly/hms-input-hooks.patch`

1. `BindAdditionalCondition(Func<int, PlayerSkillBlockReason>)` / `UnbindAdditionalCondition(...)` 추가. 미연결이면 기존 동작 그대로다. 중복 소유자의 덮어쓰기는 오류로 거절하고, 해제는 자신이 연결한 함수와 일치할 때만 한다.
2. `GetBlockReason(slotIndex)`에서 기존 초기화·인덱스·공통 조건 검사 다음, 기존 슬롯 조건 검사 전에 추가 조건을 평가한다. Bridge가 해금 여부와 폼 공통 쿨다운을 조회해 결과만 반환한다. HMS에 세션 참조·해금 마스크·별도 쿨다운 상태를 넣지 않는다.
3. `UseRejected(int slotIndex, PlayerSkillBlockReason reason)` 이벤트 추가. `TryUse`의 거절 경로에서 한 번만 전달한다. 단순 조건 조회, 성공 요청, 수락 후 준비 취소에는 발생시키지 않는다. 피드백 구독자 예외는 로그로 남기고 다른 구독자와 요청 결과를 보존한다.
4. 파괴 시 추가 조건과 자신이 소유한 이벤트 참조를 정리한다. Bridge는 Actor Detach에서도 자기 조건/구독을 먼저 해제한다.

PlayerController, PlayerInputReader, PlayerFacade, SkillSlot, PlayerSkillTypes 및 HMS meta에는 이 안을 위한 변경이 필요하지 않다. 아직 해금되지 않은 능력은 기존 Unavailable 사유로 차단하고 별도 실패음을 추가하지 않는다. 새로운 잠금 전용 UI 문구가 필요하다면 별도 요구사항으로 판단한다.

## SYM 이관 내용

| 대상 | 변경 내용 |
|---|---|
| SessionActorHost | PlayerController를 금지하던 검사를 제거하고 Reader/Controller 각 1개 및 Facade와 동일 GameObject 연결을 검증한다. 기존 E 바인딩을 유지한다. F 닫기/상호작용/대상 Refresh를 기존 Interaction에 전달한다. |
| Host·Bridge 모달 연결 | Host 입력 처리가 HMS Controller Update보다 먼저 실행되도록 순서를 명시한다. Bridge가 기존 SetInputBlocked에 모달 상태와 이번 프레임 억제를 합쳐 적용한다. 해제 프레임에도 F/Q/E/Space가 플레이어 명령으로 새지 않게 한다. 세션 비활성·Actor 교체 중 입력도 차단한다. |
| PlayerBridgeService | Attach에서 현재 Actor 세대를 캡처한 추가 조건과 거절 이벤트를 연결하고 Detach에서 해제한다. GameSessionService의 해금 원본을 조회한다. 기존 nextFormChangeTime은 실제 FormChanged 후 갱신한다. |
| 성공/실패 피드백 | HMS 기존 실제 발동/폼 변경 이벤트를 사용한다. Router 경유 요청에만 세워지던 pending 플래그에 성공 알림을 의존시키지 않는다. 자동 껍질 반환과 능력 사용을 구분한다. 기존 CoreWorldBinding의 폼 NotEnoughSpace 실패음과 성공음 경로를 보존한다. |
| 사망 진행 | 이미 존재하는 Host.Update의 TickDeathPresentation 한 곳을 유지한다. Router의 중복 Tick을 없애며 Bridge의 Actor 세대·사망 완료·재시도 1회 검사를 보존한다. |
| 불필요한 전달 메서드 | 현재 SYM C#에서 Bridge.Move/Jump/Tear/ChangeForm 호출자는 Router뿐이다. 최종 전체 참조 검사 후 Router와 함께 제거한다. 새 전달용 래퍼로 옮기지 않는다. |

폼 슬롯은 Equip 시 기존 슬롯의 쿨다운을 지우고 새 스킬의 initCooldown으로 시작한다. 현재 Shrink/Restore 설정은 cooldown=1, initCooldown=0이다. 기존 Bridge의 공통 간격을 제거하면 역방향 즉시 전환이 가능하므로 추가 조건으로 보존한다. SkillSlot의 일반 장착 계약은 변경하지 않는다.

HMS 점프 경로는 Update에서 요청하고 물리 처리에서 접지를 검사한다. 기존 Bridge.Jump는 Update 시점에도 접지를 검사했다. 이 차이는 HMS 기본 경로 채택에 따라 함께 검증해야 하며, 동일 동작을 이미 확인했다고 주장하지 않는다.

## 자산 이관과 삭제 순서

Router GUID `f16143e385b04575aa7151fbcf0a28d2` 직접 직렬화 참조는 다음 세 자산이다.

1. `Assets/_Scenes/Tech_SYM/Architecture/Session/PlayerRoot.prefab`
2. `Assets/_Scenes/Tech_SYM/Prefabs/MultiFuncPlatforms.prefab`
3. `Assets/_Scenes/Tech_SYM/01_BrokenablePlatformTest.unity`

기존 SYM 소유 객체의 컴포넌트 연결을 이관한다. Router가 있던 관리 Root에 Controller를 그대로 붙이지 않고 **실제 Reader와 Facade가 있는 ActorBody**에 연결한다. 프리팹을 새로 만들거나 보호 원본에 Apply하지 않는다. 기존 중첩 인스턴스·추가/제거 override를 함께 확인한다. 코드와 자산 이관 후 Router.cs/.meta를 제거하고 현행 지침/가이드를 갱신한다. 과거 실행 기록은 보존한다.

## 코드 리뷰·검증 기준과 현재 결과

| 항목 | 현재 상태 / 완료 기준 |
|---|---|
| 소스·직렬화 참조 조사 | 완료: 위 3개 직접 참조 확인. 상세 `output/Tech_SYM/hms-input-assembly/reference-audit.json`. 중첩 override 최종 검증은 이관 시 수행. |
| HMS 변경 범위 | 한 파일 후보 diff 작성. 원본 미수정. `git apply --check --ignore-space-change` 통과(적용하지 않음), 기록한 원본 SHA-256과 현재 파일 일치. 컴파일/실행 검증을 뜻하지 않는다. |
| 기본 HMS 독립 사용 | 추가 조건 미연결 시 기존 스킬 조건/결과 유지. 적용 후 검증 예정. |
| 해금/쿨다운/피드백 | 미해금 E/Q만 거절, 이동/점프 유지. 전환 성공 후 1초 동안 역전환 거절. 공간 부족 실패음 1회. 취소 요청에 성공음 없음. 적용 후 검증 예정. |
| 입력·수명 | F 한 번 한 처리, 모달 해제 프레임 전파 없음, 세션 종료 차단, Actor 교체/재바인딩 중복 구독 없음, 해제 후 낡은 콜백 없음. 적용 후 검증 예정. |
| 사망 | World/Presentation 정지와 Actor 세대를 유지하며 사망 완료 후 Retry 한 번. 적용 후 검증 예정. |
| 자산/컴파일 | 이관 후 GUID/타입/누락 참조 검사 및 Unity 비 Play 컴파일. 현재 미실행. |
| 실제 플레이 | 에이전트 미실행. HMS 점프 접지 타이밍, 모달 동시 키, 스킬 발동/음향은 사람 플레이 검증 대상. |

기존 HMS 연결 검토에서 모달/F/사망을 PlayerController 우회의 필수 근거처럼 설명한 부분은 이 검토로 정정한다. 기존 공개 API와 Host를 이용할 수 있으며, 필요한 HMS 확장은 슬롯 정책 연결과 요청 거절 통지다.
