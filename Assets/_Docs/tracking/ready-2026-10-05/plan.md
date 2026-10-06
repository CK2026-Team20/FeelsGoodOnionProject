# 실행 계획 — Cooked 통합 프로토타입

작성일: 2026-10-05. 구현 전 검토용. 동작은 [spec.md](spec.md), 실행 제약은 [handoff.md](handoff.md)를 따른다.

## 진행 순서와 공통 경계

먼저 병합 PC 연결, 화면 정지, 적, Editor 진입 정책을 각각 정리한다. 화면 정지를 토대로 대화·UI와 컷신을 연결하고, 레벨 규칙을 새 프로토타입의 실제 플레이 흐름으로 조립한다. 끝으로 지정 여섯 씬과 독립 씬을 구분하여 실행하고 두 판 연속·빌드까지 검증한다. 기존 첫 스테이지를 보존하는 이유는 상세 레벨 기획이 아직 확정되지 않았고 새 기능 응집형 씬을 별도로 선택했기 때문이다.

아래 작업 구획은 구현 순서이지 추가 에이전트 생성 승인이 아니다. 실행 단계의 도구·스킬과 승인 범위에 따라 담당자를 정하며, 연결 파일과 Unity Editor 변경은 주 작업자 한 명이 직렬 처리한다. 모든 대상은 프로젝트 루트 상대 경로다. 축약된 코드 경로의 기본은 `Assets/_Code/Core/Tech_SYM/Architecture/`다.

공유 조립/등록 지점인 `Integration/Composition`, `Integration/Editor/IntegrationSceneBuilder.cs`, `Integration/Runtime/IntegrationEntrySettings.cs`, `Assets/_Scenes/Tech_SYM/Architecture/Configuration/ArchitectureInstallation.asset`는 개별 기능 작업자가 동시에 수정하지 않는다. 필요한 공개 계약과 연결 목록을 넘기고 주 작업자가 해당 선행 작업 완료 후 한 번씩 연결한다. 실제 씬/프리팹 수정·저장도 단일 Editor에서 순차 수행한다. 예상 밖 파일 중복은 병합 전에 직렬화하며 Git 자동 병합 성공만으로 Unity 직렬화 충돌이 해결됐다고 판단하지 않는다.

각 작업의 검증은 아래 관찰 가능한 결과와 해당 R 항목의 예외를 포함한다. 현재 작성 중인 계획 자체를 게임 검증 결과로 기록하지 않는다.

## T1. PC 입력·형태·병합 기능 연결

- 목표: 한 입력 경로에서 방향키/Space/Q/E/F를 실행하고 Q 하나로 축소 및 회수·복귀를 처리한다. 기존 FSM·이벤트·애니메이션 상태 소유권을 재사용한다.
- 대상: `Session/GameplayInputRouter.cs`, `Session/PlayerBridgeService.cs`, `Session/SessionActorHost.cs`와 소유 PC 표현 연결; 예외 허용된 `Assets/_Code/Core/Tech_HMS/Features/PlayerInputReader.cs`, `PlayerFormController.cs` 두 파일. `Assets/_Scenes/Tech_SYM/Architecture/Session/PlayerRoot.prefab`의 실제 연결은 단일 Editor 작업으로 수행한다. HMS의 나머지 파일과 공유 PlayerCharacter 프리팹은 참조만 한다.
- 계약: 복귀 공간 검사를 통과할 때만 껍질 회수와 정상 형태 복귀를 함께 확정한다. 실패 후 공간 확보만으로 자동 복귀하지 않는다. 공개 입력 속성의 소비자를 읽어 컴파일 호환을 지키되 독립 회수 명령은 발생시키지 않는다. 눈물은 조각 5개 용량/소비와 일치하고 애니메이션 누락은 발동을 막지 않는다. PlayerController/CheckInteract를 통합 객체에 중복 추가하지 않는다.
- 표현: 기존 애니메이션 요청 출력 경로와 점프·착지·피격·스킬 이벤트를 연결한다. 실제 모델/클립 없이 누락 대체를 확인하고 PC 빨간 피격 효과는 개체별로 복원한다. 실제 음원은 기존 카탈로그만 사용한다.
- 검증: Q 연타/먼 껍질/막힌 공간/재입력, F 4개 거절·5개 소비·대상 없음·반복 기절, E 단일 실행, 사망 뒤 Heal 불부활/TryRevive, 우선순위·오래된 요청 완료 무시·비활성 구독 해제, 실제 피해만 빨간 표시, 공유 머테리얼 불변.
- 요구사항: R02~R05, R16~R18. 의존 없음.

## T2. 모달 정지 수명과 메모

- 목표: 월드·게임 입력·UI 표시 진행의 정지 소유자를 분리하고 메모/옵션 중첩에서도 올바르게 복원한다.
- 대상: `Session/GameplayControlService.cs`, `UI/Runtime`의 옵션 정지 수명, `Assets/_Code/Core/Tech_SYM/Interaction`의 메모/프롬프트와 대응 Tech_SYM 자산.
- 계약: View는 ViewModel 명령만 호출한다. 메모 E/닫기 버튼은 동일 명령을 실행한다. 월드 프롬프트와 화면 본문 Canvas를 분리한다. 마지막 소유 토큰 해제 때 이전 시간 배율을 복원하며 자기 화면만 정리한다. 옵션은 대화/컷신 표시도 정지시킬 수 있지만 해당 화면의 자기 월드 정지가 자기 진행을 막지 않는다.
- 검증: 메모→옵션→옵션 닫기→메모 닫기, 역순 파괴/세션 종료, 원래 timeScale이 1이 아닌 경우, 중복 Bind/Unbind, E 닫은 같은 프레임에 다시 열리지 않음. HUD는 정지 토큰이 없다.
- 요구사항: R02, R12, R13. 의존 없음.

## T3. 대화와 UI 기획 레이아웃

- 목표: PDF의 타이틀·옵션·대화·로그 배치와 명령을 실제 화면에 반영하고 대화 시퀀스를 단일 입력 소비 정책으로 실행한다.
- 대상: `Dialogue/`, `UI/Views`, `UI/Runtime`, `UI/Composition`의 관련 파일과 Tech_SYM UI 자산. T2의 UI 수명 변경 뒤 진행한다. 전체 Integration 조립은 주 작업자에게 넘긴다.
- 계약: R07의 JSON Rows/필드·ID·순서 검증을 유지하고 문자 출력 완료와 다음 문장 진입을 한 입력으로 중복하지 않는다. 클릭/아무 키, 로그·자동·전체 스킵은 최상위 UI에서 소비한다. 설정 저장·씬 이동은 ViewModel이 서비스에 위임한다. 옵션 열림/닫힘 때 유입 입력을 재생하지 않는다.
- 검증: 잘못된 표 거절, 마지막 줄 완료/전체 취소 각각 한 번, 자동 진행의 unscaled 시간, 로그 동안 진행 제어, 옵션 중 자동 멈춤/재개, 리바인딩·파괴 후 호출 없음. PDF 7페이지와 실제 해상도별 배치/클릭 영역을 비교한다.
- 요구사항: R02, R07, R12, R13. 의존 T2.

## T4. 단일 원화 컷신

- 목표: 오프닝/엔딩을 지정 순서의 원화 한 장씩 전체 화면으로 제공한다.
- 대상: `Cinematics/`의 데이터 검증·진행·View·검증 코드와 Tech_SYM 컷신 자산. 기존 4칸 동시 표시와 스케일 트윈 경로를 제거한다.
- 계약: 동시에 한 장, 기본 장당 3초(조정 가능), 컷신 UI에는 트윈 없음. 자체 월드 정지와 독립적으로 unscaled 진행하며 옵션은 표시 진행도 정지한다. 스킵은 정상 완료, 씬 종료/파괴는 취소로 구분하고 콜백은 한 번만 보낸다.
- 검증: 원화 4장 순차 표시, 두 장 겹침 없음, 자동/스킵/옵션/취소, 자원 누락 오류, 완료 후 입력 누수 없음. 모델/새 일러스트 제작은 하지 않는다.
- 요구사항: R02, R06, R08, R13. 의존 T2.

## T5. 일반 적 동작과 실제 스턴 프리팹

- 목표: 일반 적에서 추격·밟기 처치를 제거하고 실제 세 적 프리팹에 스턴 시각 효과를 연결한다.
- 대상: `Assets/_Code/Core/Tech_SYM/Enemies/`와 `Assets/_Scenes/Tech_SYM/Enemies/Prefabs/{StationaryEnemy,PatrolEnemy,OilPatrolEnemy}.prefab`. `IStunState`, HMS `EnemyStunVisual`, 공용 더미·텍스처는 참조/재사용만 한다.
- 계약: 머리 Collider와 필수 참조 검사·공중 하강 처치 분기를 함께 없앤다. 몸통 접촉 피해와 순찰/고정형은 유지한다. 기절 중 피해 없음, 재기절 시간 연장, 사망 시 해제 통지와 VFX 정리를 유지한다. 머리 위 효과는 전투용 머리 Collider와 분리한다.
- 검증: 위에서 접촉해도 적 생존, 양수 Damage만 접촉 소비, 스턴 중 무피해/해제 후 접촉 규칙, 활성 당시 이미 스턴, 재활성/사망/반환, 효과·텍스처 복원, 다른 적 표시 불변. 원본 세 프리팹을 저장 후 다시 읽는다.
- 요구사항: R05, R14, R19. 의존 없음.

## T6. 레벨 장치·카메라·체크포인트·추격

- 목표: 단일 레벨에서 기능 해금과 장치, 트리거 카메라와 마지막 추격이 같은 체크포인트 수명을 따른다.
- 대상: `Level/`, `Chase/`, 관련 `Assets/_Code/Core/Tech_SYM/{Platforms,Features,OvenTray}`와 소유 설정. 기존 Physics/Facade 계약을 보존한다.
- 계약: 가까운 구간 자동 선택 대신 트리거 진입/이탈로 카메라를 전환한다. 통로 이동은 월드 Z만, 사이드 복귀는 깊이 보간 후 축을 확정한다. stageId 문자열 2/3에만 묶인 대화/추격/해금 조건을 명시적 스테이지 설정으로 대체한다. 진행 거리는 통로의 Z 구간을 포함하는 경로로 계산한다. 눈물 길 열기는 제거하며 기존 압력/껍질·구출 장치를 유지한다. 붕괴는 현재 시도에서 자동 복구하지 않는다.
- 추격: 앞 Trigger 접촉을 포획의 단일 판정으로 사용하고 기존 거리 포획 분기를 제거한다. 시작 수락 전 문을 닫지 않는다. 리스폰/제목 복귀/옵션 정지에 따라 시도 수명을 정리한다.
- 검증: 카메라 정·역방향, 전환 중 정지/사망/재진입, 체크포인트 해금·복원, 7종 발판 실제 물리, 서랍 E 열기/닫기, 다중 Collider 포획 단 한 번, 간격만 좁아지고 미접촉이면 미포획, 추격 전 체크포인트로 재시도.
- 요구사항: R03, R09~R11, R14, R15. 의존 T1, T5.

## T7. Editor 지정 씬 진입 범위

- 목표: spec R20의 여섯 정확한 경로만 부트 진입하고 나머지는 현재 씬에서 Play한다.
- 대상: `Foundation/Editor/BootstrapPlayModeHook.cs`, `Foundation/BootstrapEntryRoute.cs`, 필요한 소유 route 계약, `Integration/Editor/VerificationBatchService.cs`의 진입 검사. 설치 목록 변경은 주 작업자 연결로 제한한다.
- 계약: Resolve 결과의 Title 기본값을 비대상 판정으로 오용하지 않는다. 사전 Arm과 ExitingEditMode 양쪽에서 적용 여부를 판정하며 대상에서 벗어난 뒤 hook 소유 임시 startScene/pending route를 해제한다. 활성 경로만 비교하고 기존 사용자 설정/씬 setup을 보존한다. 통합 강제 11개 검사 메뉴를 대상 6개와 비대상 검사로 교체한다.
- 검증: 경로 판정의 여섯 positive/같은 이름 다른 경로/옛 Stage1/테스트씬/빈 경로 negative, 대상→비대상 이동, Play 취소/Stop/도메인 reload 설정별 잔류 없음. 실제 새 씬 대상 Play는 T8 후 T9에서 수행한다. 미저장 씬 자동 저장/폐기 금지.
- 요구사항: R06, R20. 의존 없음.

## T8. 새 프로토타입 콘텐츠와 세션 흐름

- 목표: 실제 `Assets/_Scenes/Tech_SYM/Architecture/Prototype/1_Stage.unity`에 기능을 응집하고 단일 정상 게임/직접 진입의 콘텐츠 수명을 구현한다. 단순 등록만이 아니라 비선택 스테이지가 함께 활성화되는 기존 동작을 바로잡는 작업이다.
- 대상: 새 소유 씬/설정; `Foundation/GameFlowService.cs`, `Integration/Runtime/LoadedStageRegistry.cs`, `Session/GameSessionRuntime.cs`, 단일 조립 담당자가 관리하는 설치/빌더/EntrySettings/Composition 지점.
- 계약: 정상 새 게임은 오프닝→새 프로토타입→엔딩→Title이다. Stage2/3 직접 진입은 해당 설정으로 플레이하며 다른 스테이지의 물리·트리거·등록이 간섭하지 않는다. 기본 파편 상한 1 검사를 5개 계약과 맞추고 Stage2 고정 깊이 허용 대신 현재 카메라 상태를 적용한다. 설치 첫 항목과 빌더 카탈로그를 새 경로로 맞춘다. 기존 `Stages/03_1_Stage.unity`는 보존한다.
- 배치: side→corridor→quarter→corridor→quarter 순서를 고정하고 체크포인트별 형태/눈물 체험과 플랫폼, 메모, 대화, 적 스턴, 마지막 추격/탈출을 지나게 한다. 구체 치수는 테스트 가능한 배치값으로 조정하되 추후 본편 레벨 문서로 확정했다고 기록하지 않는다.
- 검증: 한 번의 플레이로 모든 필수 기능 접근, 초기 HP3/파편0/해금, 같은 stage/checkpoint ID 중복 없음, stage2/3 직접 진입 분리, 정상 새 게임에 stage2/3 동시 콘텐츠 없음. Missing Scripts/필수 참조/GUID/직렬화 저장 재검사.
- 요구사항: R01~R20의 실제 연결, 특히 R06, R09, R16, R19, R20. 의존 T1, T3, T4, T5, T6, T7.

## T9. 실행 검증·독립 구현 검토·현재 문서 갱신

- 목표: 새 실제 씬과 저장 자산, Editor 분기, Windows 실행 파일의 결과를 증거로 판정한다.
- 대상: 변경 코드/자산 전부와 `Assets/_Docs/tracking/코드 리뷰.md`, `테스트 및 검증 항목.md`, `status.md`, `findings.md`; 동작이 바뀐 공통/지역 지침. 보조 로그는 `output/Tech_SYM/ready-2026-10-05/` 아래 종류별로 둔다.
- 순서: 컴파일 → 순수/연결 검사 → Missing Script/참조 → 실제 Play → 소유 자산 저장 재조회 → 명시적 Windows 빌드 → 실행 파일 → 독립 구현 diff 검토. 실패/차단을 고치거나 명확히 보고하며 검사하지 않은 결과를 성공으로 바꾸지 않는다.
- 발견된 실행 진입: `Cooked/Dialogue/Run Pure Regression Tests`, `Cooked/UI/Verify logic`, `Cooked/Session/Verify Domain and Authored Prefabs`, `Cooked/Verification/Run Two Games Through Input`, 수정한 Editor 진입 검사, `Cooked/Integration/Build Windows Player`. 메뉴/실제 메서드와 필요한 상태는 실행 직전 소스에서 재확인한다. 구버전 검사 기대값은 변경 계약에 맞추고 무조건 PASS로 완화하지 않는다.
- 완료: spec의 필수 검증 6항목을 전부 분리 보고한다. 실행 당시 기존 미저장 씬을 보호할 수 없으면 해당 Play/빌드는 차단으로 남기고 안전한 저장 상태를 사용자에게 확인한다. 최초 발견된 미저장 씬을 임의로 저장하지 않는다. 코드 리뷰는 입력/취소·구독·참조·제약 위반을 별도 검토한다.
- 요구사항: R01~R20. 의존 T8.

## 요구사항 추적

| 요구사항 | 구현 및 검증 작업 |
|---|---|
| R01 | T8, T9 |
| R02 | T1, T2, T3, T4, T8, T9 |
| R03 | T1, T6, T8, T9 |
| R04 | T1, T8, T9 |
| R05 | T1, T5, T8, T9 |
| R06 | T4, T7, T8, T9 |
| R07 | T3, T8, T9 |
| R08 | T4, T8, T9 |
| R09 | T6, T8, T9 |
| R10 | T6, T8, T9 |
| R11 | T6, T8, T9 |
| R12 | T2, T3, T8, T9 |
| R13 | T2, T3, T4, T8, T9 |
| R14 | T5, T6, T8, T9 |
| R15 | T6, T8, T9 |
| R16 | T1, T8, T9 |
| R17 | T1, T8, T9 |
| R18 | T1, T8, T9 |
| R19 | T5, T8, T9 |
| R20 | T7, T8, T9 |

## Execution Graph

```yaml
tasks:
  - id: T1
    depends: []
    risk: RISKY
  - id: T2
    depends: []
    risk: RISKY
  - id: T3
    depends: [T2]
    risk: RISKY
  - id: T4
    depends: [T2]
    risk: RISKY
  - id: T5
    depends: []
    risk: RISKY
  - id: T6
    depends: [T1, T5]
    risk: RISKY
  - id: T7
    depends: []
    risk: RISKY
  - id: T8
    depends: [T1, T3, T4, T5, T6, T7]
    risk: RISKY
  - id: T9
    depends: [T8]
    risk: RISKY
regen_barriers: []
```

별도의 생성 코드 재생성 단계는 발견되지 않았다. Unity 에셋 가져오기/컴파일 완료 대기는 각 자산 변경 및 검증 단계의 필수 조건으로 다룬다.
