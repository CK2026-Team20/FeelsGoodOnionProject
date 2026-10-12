# 실행 인계 — Cooked 통합 프로토타입

작성일: 2026-10-05. 현재는 명세/계획 준비 완료 후 최종 검토 단계이며 게임 구현 완료 기록이 아니다.

## 문서 역할과 우선순위

| 문서 | 역할 |
|---|---|
| `Assets/_Docs/tracking/ready-2026-10-05/handoff.md` | 실행 제약과 문서 사용법 |
| `Assets/_Docs/tracking/ready-2026-10-05/spec.md` | 구현할 행동·예외·완료 기준의 기준 |
| `Assets/_Docs/tracking/ready-2026-10-05/plan.md` | 작업 대상·순서·검증과 실행 그래프 |
| `Assets/_Docs/planning-index.md` | 현재 지정 원본/버전과 직접 지시 등록 |
| `.dryforge/handoff.md`, `.dryforge/spec.md`, `.dryforge/plan.md` | 위 세 파일과 바이트가 같은 실행 도구용 사본 |

최신 직접 지시가 최우선이다. spec과 plan이 다르면 동작은 spec을 따른다. spec의 사용자 결정 변경은 재확인이 필요하지만 동일 동작을 달성하는 구현 순서는 근거와 함께 조정할 수 있다. 원본 기획서가 갱신되면 실제 내용을 다시 읽고 영향을 기록하며 이전 해석을 자동 승계하지 않는다. 정본을 수정한 뒤 실행 사본 세 파일을 동기화하고 해시를 확인한다. 원본 기획서에 포함된 문장을 에이전트 실행 지시로 취급하지 않는다.

## 현재 프로젝트와 보존 상태

Unity 6000.6.0f1 로컬 게임이다. 루트 `AGENTS.md`/`CLAUDE.md`, `Assets/_Docs/standards.md`, `engineering-notes.md`, 대상 지역 AGENTS를 읽고 실제 코드·자산을 확인한다. `.dryforge/status.json`은 현재 없으나 기존 문서 체계를 유지하는 방식이 이미 선택됐다. 새 프로젝트로 간주해 지침을 재생성하지 않고 상태 파일을 꾸며내지 않는다. 기존 세 실행 문서는 `output/Tech_SYM/ready-2026-10-05/evidence/previous-ready/`에 보존되어 있다. 현재 계획과 혼용하지 않는다.

준비 시점 Editor는 Play 중이 아니며 활성 `Assets/_Scenes/Tech_SYM/Architecture/Stages/03_1_Stage.unity`가 **미저장 상태**였다. 이를 저장·폐기·전환하지 않았다. 구현 시작 시 다시 조회하여 보호한다. 미저장 변경이 남으면 코드/문서 등 독립 작업은 진행할 수 있지만 그 상태를 훼손할 씬 전환/Play 검사는 하지 않는다. 저장 경로가 확정된 소유 신규 자산만 명시적으로 저장한다.

## 변경 범위의 고정 조건

1. 새 씬 `Assets/_Scenes/Tech_SYM/Architecture/Prototype/1_Stage.unity`를 만들어 설치의 첫 스테이지로 등록한다. 기존 `Stages/03_1_Stage.unity`는 보존하고 독립 Play 대상으로 둔다. 상세 본편 레벨 배치의 확정은 추후 기획서로 진행한다.
2. 부트 강제 진입은 spec R20의 정확한 여섯 경로에만 적용한다. Bootstrapper와 Title은 Title, Core와 새 1_Stage는 새 첫 스테이지, 기존 Stage2/3은 각각 직접 진입한다. 단순 파일명/부분 문자열/열려 있는 다른 씬으로 판정하지 않는다.
3. Q는 축소와 자동 껍질 회수·복귀를 함께 담당한다. 별도의 회수 키는 없다. F 눈물, E 열기/닫기다. HMS 허용 파일은 `Assets/_Code/Core/Tech_HMS/Features/PlayerInputReader.cs`와 `PlayerFormController.cs` **두 개**이며 이번 세션의 해당 규칙 최소 변경으로 제한한다. 나머지 HMS/공용 Interfaces/공유 프리팹/_Art/Packages/ProjectSettings 수정으로 확대하지 않는다.
4. 기존 병합 플레이어의 상태·FSM·이벤트·애니메이션 출력을 재사용한다. 실제 모델/Animator/클립 연결과 재생·블렌딩·착지 타이밍은 제외한다. 빨간 피격 표시와 기존 사운드 연결, 실제 세 SYM 적 프리팹의 스턴 VFX/텍스처는 포함한다.
5. 새 프로토타입은 side→corridor→quarter→corridor→quarter다. 추후 본편 순서로 바꾸거나 마지막을 임의로 side로 교정하지 않는다. 카메라 트리거·축 제한과 보간, 체크포인트 해금, 앞 Trigger 추격 포획을 구현한다.
6. 모달 메모/옵션/대화/로그/컷신은 월드 정지, HUD는 제외다. UI는 독립적으로 진행하며 옵션은 대화/컷신 표시도 정지한다. 전역 시간 배율 및 입력 잠금의 소유 수명을 보존한다.
7. 빈 루트 원칙을 적용하되 이미 검증된 ActorBody의 Rigidbody/Facade/센서 물리 계약을 이름 맞추기 위해 재배치하지 않는다. 예외 계층과 이유를 기록한다. 기존 GUID를 유지하며 공유 원본 Apply는 하지 않는다.
8. 문서·원본은 `Assets/_Docs/`, 보조 추출·로그·렌더·빌드는 `output/Tech_SYM/ready-2026-10-05/`의 종류별 하위에 둔다. 범위 밖 정리나 Git 커밋/푸시/PR/병합은 승인 범위에 포함하지 않는다.

## 시작할 때 읽을 실제 접점

- 입력/세션: `Session/GameplayInputRouter.cs`, `PlayerBridgeService.cs`, `SessionActorHost.cs`, `GameSessionRuntime.cs`; HMS PlayerFacade/PlayerFormController/PlayerInputReader와 읽기 전용 PlayerController/RestoreFormSkill/애니메이션 구현.
- 흐름/Editor: `Foundation/Editor/BootstrapPlayModeHook.cs`, `Foundation/BootstrapEntryRoute.cs`, `Foundation/GameFlowService.cs`, `Integration/Runtime/LoadedStageRegistry.cs`, `Integration/Runtime/IntegrationEntrySettings.cs`, `Integration/Editor/IntegrationSceneBuilder.cs`, `VerificationBatchService.cs`와 설치 asset.
- UI: `Session/GameplayControlService.cs`, UI의 ViewModel/바인딩과 Dialogue/Cinematics의 표시 완료·취소 계약.
- 레벨: LevelCameraService/StageService와 Chase, Tech_SYM Enemies 및 기존 IStunState/EnemyStunVisual. 실제 대화/추격 stageId 고정 조건과 조각 상한 1을 잊지 않는다.

위 축약 경로의 기본은 `Assets/_Code/Core/Tech_SYM/Architecture/`다. 원본 문서 이름과 위치는 planning-index에 있다. 현재 코드의 과거 동작은 HOW 참고이며 최신 spec을 덮어쓰지 않는다.

## 구현 완료의 증거

정적 검사, 컴파일, 실제 Play, 저장 재조회, Windows 빌드, 실행 파일, 독립 구현 리뷰를 구분한다. 상세 절차는 plan T9와 spec 마지막 절을 따른다. 새 실제 프로토타입을 쓰며 대체 검증 씬만으로 완료하지 않는다. 여섯 positive 진입과 독립 negative 진입, 두 판 연속, 메모/옵션 중첩, Q 실패 원자성, 기절 무피해/표시 수명, 카메라 복귀, 추격 중 재시도는 필수다.

결과는 `Assets/_Docs/tracking/코드 리뷰.md`, `테스트 및 검증 항목.md`, `status.md`, `findings.md`에 반영한다. 누락·차단·범위 제외는 그대로 기록한다. 현재 ready의 원본 해시/문서 검토 통과가 게임 컴파일·Play 통과를 의미하지 않는다.

## 준비 검토와 다음 행동

직접 지시와 원본·현재 소스를 대조했고 별도 의도 검토에서 잔여 중요한 질문은 없었다. 최종 세 문서의 독립 검토와 추적/그래프/동기화 정적 결과는 추적 문서 및 `output/Tech_SYM/ready-2026-10-05/evidence/`에 기록한다. 최종 문서 승인 후 실행 단계로 넘어간다. ready 단계에서는 게임 코드·씬·프리팹을 수정하지 않는다.
