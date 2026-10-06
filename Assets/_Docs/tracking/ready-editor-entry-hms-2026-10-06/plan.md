# 활성 씬 Play 및 HMS 조립 실행 계획

2026-10-06. 승인 전에는 조사와 문서 작성만 완료한다.

## 단계와 실행 원칙

아래 실행 계획은 `기능 명세서.md`의 동작을 구현하기 위한 변경 가능한 방법이다. 먼저 진입 설정과 Actor/HMS 조립 수명을 수정하고, 그 위에 Cinemachine 구간 연결과 독립 씬의 누락을 해결한다. 실제 구현과 저장 자산을 확인한 뒤 지침·프로그래머 가이드·검증 및 인계 자료를 확정한다.

씬·프리팹 변경은 Unity Editor API로 수행한다. 미저장 사용자 작업을 자동 저장하거나 버리지 않는다. 실제 Unity 인스턴스의 씬 교체·자산 저장·Play·빌드는 한 작업자가 순차 실행한다. 코드 파일이 분리되어 있어도 하나의 Editor를 동시에 제어하지 않는다. 전체 Builder로 자산을 일괄 재생성하지 않고 대상별 수정/마이그레이션과 필요한 생성 코드의 동작 보존을 확인한다.

### T1. 활성 씬 Play 정책 및 설정 복원 — R1, R2, R6

**목표:** 정확한 여섯 통합 씬의 Bootstrap 경유와 비대상 씬의 독립 진입을 모두 보장한다. Stage2/3 매핑을 보존하고 잔존 Bootstrap startScene·취소·설정 복원 문제를 해결한다.

**대상:** `Assets/_Code/Core/Tech_SYM/Architecture/Foundation/BootstrapEntryRoute.cs`, `Foundation/Editor/BootstrapPlayModeHook.cs`, `BootstrapEntryRouteRegressionTests.cs`, 진입 검증을 담당하는 `Integration/Editor/VerificationBatchService.cs`와 필요한 소비 검증 코드. 여기서 Foundation/Integration 경로는 모두 같은 Architecture 코드 디렉터리 아래다. 설치 자산은 읽기 대조하며 현재 여섯 목록이 맞으면 불필요하게 쓰지 않는다.

**동작 계약:** ExitingEditMode 직전 실제 sourcePath를 기록한다. 대상만 유효한 route를 생성하고 비대상은 route를 제거하고 startScene=null을 소유한다. 임시 설정의 기준값과 소유 값을 구분하며 현재 소유 값일 때만 복원한다. recompile/domain reload, Stop, abort와 외부 설정 변경에서도 stale route·강제 첫 Stage·외부 값 덮어쓰기가 없어야 한다. 설치 불일치는 통합 진입 실패로 정리되고 독립 씬은 설치 자산을 요구하지 않는다.

**검증:** 순수 경로 검증을 실행하고 실제 Editor 진입 시나리오를 T6에서 재실행한다. 회귀 검증이 startScene의 원래 값을 항상 null이라고 가정하지 않도록 바꾼다. Hook의 조건문을 그대로 복제하는 테스트 대신 로드된 실제 씬·flow·checkpoint 및 종료 후 설정을 비교한다. diff와 실행 JSON을 남긴다.

### T2. Actor·카메라 프리팹의 HMS 조립 및 수명 — R3, R4, R6

**목표:** HMS 모드·외형·카메라 필수 연결을 첫 Awake 전에 완성하고 재시도 시 Actor와 카메라 참조가 함께 교체되도록 한다.

**대상:** `Assets/_Scenes/Tech_SYM/Architecture/Session/PlayerRoot.prefab`, `SessionActorHost.prefab`, `Level/Prefabs/CoreLevelCamera.prefab`; `Assets/_Code/Core/Tech_SYM/Architecture/Session/SessionActorHost.cs`, `PlayerBridgeService.cs`, `ActorFacingPresentation.cs`, `Session/Editor/SessionActorBuilder.cs`; 필요한 새 SYM 소비 어댑터. 보호 HMS 코드와 공유 PlayerCharacter 원본은 수정하지 않는다.

**동작 계약:** ActorBody의 물리/센서 배치를 보존하고 그 Facade에 HMS MovementModeController와 VisualController를 연결한다. 외형 회전의 중복 작성자를 정리한다. 한 InputReader/GameplayInputRouter 계약을 유지한다. Actor와 HMS CameraController가 교체 때도 사전 직렬화 참조를 가질 수 있는 비활성 소유 조립을 사용한다.

**우선 검토할 구성:** Actor의 비활성 PlayerRoot 안에 소유 카메라 묶음을 구성하거나 중첩하여 ModeController·출력 Brain/Camera·네 CinemachineCamera가 같은 생성 인스턴스의 내부 참조를 갖게 한다. 지속 Core의 서비스는 새 묶음에 연결한다. Awake 이후 private 필드 주입이나 보호 원본 수정으로 수명 제약을 우회하지 않는다. 이 방법을 변경해도 명세의 수명·화면·입력 계약은 유지한다.

**검증:** 첫 활성화 전에 모든 serialized reference와 네 카메라의 고유성을 Unity API로 검사한다. 저장 비활성 Actor, Start 후 IsInitialized 및 TrySetMode 성공, initial stage mode, CharacterMovement 제약·speed, VisualController 배치, 중복 입력 컴포넌트 부재를 확인한다. 실제 retry 교체의 검증은 T6에서 수행한다. 저장 후 디스크 자산과 변경 GUID를 대조한다.

### T3. Cinemachine 구간 전환과 Core/UI 재연결 — R3, R4, R6

**목표:** 출력은 Brain이 쓰고 SYM은 Stage 요청·평면 정렬·차단·수명만 조정하도록 한다. 초기 진입/체크포인트/정지/재시도가 한 기능으로 동작해야 한다.

**대상:** `Assets/_Code/Core/Tech_SYM/Architecture/Level/LevelCameraService.cs`, `CameraZoneTrigger.cs`, `Level/Editor/LevelAssetBuilder.cs`; `Integration/Runtime/CoreComposition.cs`, `CoreWorldBinding.cs`, `CoreUiBinding.cs`, `GameSessionRuntime.cs`, `GameRuntimeService.cs`; `Integration/Editor/IntegrationSceneBuilder.cs`, `PrototypeStageAuthoring.cs`, `PrototypeRegression.cs`, `PrototypeAxisRegression.cs`; 관련 소유 카메라·Core·세 Stage의 serialized 연결. 전체 Stage 배치 재생성은 하지 않는다.

**동작 계약:** Side/Quarter/Corridor/Legacy를 명세의 HMS 모드로 매핑한다. 기존 CurrentZoneId 중복 억제와 평면 Origin을 보존한다. 일반 카메라 Transform을 갱신하는 기존 LateUpdate와 offset/look tween의 경쟁을 없앤다. Cinemachine의 실제 완료와 물리 정렬 완료를 확인한 뒤 HMS 모드의 축을 확정하고 자기 lease만 해제한다. 같은 구간·연속 다른 구간·옵션 timeScale=0·취소·OnDisable·Destroy·retry를 각각 처리한다.

**전환 순서의 구현 검토:** HMS TrySetMode는 즉시 이동 설정과 우선순위를 변경한다. 축을 블렌드 이후 적용하려면 소유 어댑터가 공개 Cinemachine priority API로 요청 카메라를 먼저 블렌드하고, 입력 차단/정렬/실제 Brain 완료 후 TrySetMode로 권위 상태를 확정하는 순서 등을 사용한다. 요청 중 상태를 두 번째 권위 있는 HMS 모드로 저장하지 않는다. 완료 시 HMS CameraController의 동일 우선순위 적용이 추가 블렌드를 만들지 않는지 확인한다.

CoreUiBinding은 현재 session 생성 시 카메라를 필수로 요구한다. Actor 수명의 카메라를 택하면 session의 상태 바인딩과 ActorReady의 카메라 바인딩을 분리하여 최초 Actor 전에도 예외가 나지 않게 한다. ActorRemoving에서 참조를 비우고 새 ActorReady에서 월드 프롬프트 카메라를 갱신한다. 기존 ViewModel 명령/상태를 재사용하고 중복 구독을 만들지 않는다. GameSessionRuntime은 HMS Start 준비와 initial mode/camera 적용 완료 전에 Playing/게임플레이 표시를 확정하지 않는다. 현재 동기식 Actor 생성의 준비 대기는 소비 측 GameRuntimeService.SpawnActorAtCheckpoint의 기존 코루틴 경계에 연결한다. 취소·실패를 기존 cleanup으로 전달하고 실제 준비 전에는 OperationResult를 성공으로 완료하지 않는다. 공유 인터페이스나 HMS Start 구현을 변경하지 않는다.

**검증:** 정적 연결, 실제 Brain 활성 카메라/블렌드, 각 checkpoint 모드·초기 화면, 기존 트리거 동선과 물리 축, transition input lease, UI 카메라, 두 번 retry와 정지/종료를 확인한다. private DOTween field만 probe하는 기존 검증은 관찰 가능한 새 동작 검증으로 수정한다. 생성 코드도 같은 구성으로 만드는지 범위를 좁혀 확인한다.

### T4. 독립 SYM 씬·프리팹의 누락 및 카메라 수정 — R3, R4, R6

**목표:** 통합 외의 소유 게임플레이 배치도 역할에 맞게 HMS 기능을 사용할 수 있게 한다. 상속 연결 누락과 소유 Missing Script를 해결하고 보호 결함은 인계한다.

**대상:** `Assets/_Scenes/Tech_SYM/Prefabs/MultiFuncPlatforms.prefab`, `01_BrokenablePlatformTest.unity`, `02_MultiFuncPlatformTest.unity`, `03_InGame.unity`, `Architecture/Stages/03_1_Stage.unity` 및 감사에서 실제 소비/override가 확인된 소유 자산. 적·기름·껍질 소유 프리팹은 기존 정상 연결을 유지하고 기능 검증한다.

**동작 계약:** MultiFunc Player_HMS와 Brokenable Player의 세 skill definition·visualRoot·debrisPrefab를 올바른 계약으로 연결한다. 상속 사용 씬에서 null override·누락·중복 입력을 확인한다. 실제 플레이어가 있는 소유 배치에 기준 HMS 카메라를 연결한다. 기존 두 카메라 Missing Script의 GUID/역할을 확인하고 필요한 구성에서 해결한다. old Stage1의 레벨/이력을 일괄 재작성하지 않는다. 기능이 없는 Sample/Art/Title 객체에 카메라/플레이어 기능을 무차별 삽입하지 않는다.

**검증:** 저장 파일과 native prefab instance의 결과를 함께 비교한다. 16씬/65프리팹 감사 모집단과 추가 자산을 다시 검사하여 소유 Missing Script·필수 null/잘못된 target을 0으로 한다. HMS PlatformManager 결함은 별도 인계 항목으로 유지한다. 독립 입력 및 플레이는 T6의 실제 시나리오로 확인한다.

### T5. 참조 권한과 프로그래머 가이드 확정 — R2, R5

**목표:** 사용자 설명대로 공개 기능/컴포넌트 조립은 허용하고 타인 원본 수정은 금지하는 의미를 모든 관련 지침에 일치시킨다. 진입 대상을 바꾸는 안내는 최종 구현을 반영한다.

**대상:** 루트 `AGENTS.md`, `CLAUDE.md`; `Assets/_Docs/security.md`, `standards.md`, `contracts.md`, `operations.md`, `engineering-notes.md`의 관련 절; `Assets/_Scenes/Tech_SYM/AGENTS.md`, `Assets/_Code/Core/Tech_SYM/AGENTS.md`와 변경된 Architecture Foundation/Session/Integration/Level 지역 지침; 본 폴더의 `프로그래머 수정 가이드.md`. 기존 기획/원본과 무관한 문서 전체 재작성은 하지 않는다.

**동작 계약:** 명세의 참조 정의를 일관되게 적용하고 원본 Apply/.meta/import 수정 금지를 명시한다. 최신 사용자 지시와 예전 “참조” 문구가 충돌하지 않도록 한다. 대상 whitelist·Stage catalog/ID·direct entry·회귀/빌드 목록을 실제 소스와 교차 설명한다. 루트 두 진입 문서의 내용은 일치시킨다.

**검증:** 수정 문서 diff, 실제 경로 존재, 코드/API/Inspector 슬롯과 안내의 교차 검토. 튜닝 기본값을 사용자 답변 완료나 절대 고정값으로 기록하지 않는다. 문서가 구조 감사 또는 미실행 Play를 기능 PASS로 확장하지 않는지 확인한다.

### T6. 기능 회귀 및 보호 경계 검증 — R1, R3, R4, R6

**목표:** 현재 선택 씬 진입과 HMS의 실제 결합 동작을 증명한다. 사용자 작업과 보호 원본을 보존하고 미실행 항목을 분리한다.

**대상:** 기존 `BootstrapEntryRouteRegressionTests.Run`, 세션/카메라 회귀 코드, `VerificationBatchService.RunAllEntries/RunFullLoop`, 실제 기존 씬의 Actor와 카메라/UI; `output/Tech_SYM/verification/editor-entry-hms-2026-10-06/`의 신규 증거. 본 폴더 `테스트 및 검증 항목.md`가 시나리오의 기준이다.

**검증:** Unity 컴파일과 outer/inner tool result를 확인한다. 실행 전 dirty/setup/start/EnterPlayMode, 소유·보호 baseline을 기록한다. 실제 Play를 통해 여섯 대상·대표 비대상 네 개·잔존 Bootstrap·외부 start·cancel·Domain/Scene Reload 조합을 검사한다. 세 Stage, trigger route, 축, single F/E/Q, oil·stun·form·shell, retry·pause·two games를 관찰 가능한 결과로 확인한다. 저장 뒤 native null/missing/disk diff와 protected hash를 검사한다. 에디터 자동 기능 검증과 서비스 호출 검사를 구분해 보고한다.

**빌드 경계:** 이번 핵심 완료 대상은 Editor Play다. Windows 빌드는 현재 명시 여섯 씬 `IntegrationBuildPipeline.BuildWindowsPlayer`와 설정 감사 절차를 따르되 보호 ProjectSettings를 변경할 권한을 이 계획에서 확대하지 않는다. 기존 엔진 정규화 부수 변경을 안전하게 통제할 수 없으면 빌드는 미실행 사유와 함께 별도 인계하고 빌드/실행 PASS를 주장하지 않는다. Computer Use 직접 플레이는 필수로 추가하지 않는다.

### T7. 리뷰·실행 결과 및 QA 인계 정리 — R2, R3, R4, R5, R6

**목표:** 실제 변경과 검증의 한계를 사용자가 추적할 수 있게 한다. 구현/자동 검증과 사용자·QA의 빌드 검증을 구분한다.

**대상:** 본 폴더 `코드 리뷰.md`, `테스트 및 검증 항목.md`, `프로그래머 수정 가이드.md`; `Assets/_Docs/tracking/status.md`, `findings.md`의 관련 항목; 기존 `2026-10-05-functional-qa-spec.md`/cases가 지정하는 기능 테스트 엑셀 및 FunQA 원본. 기타 산출물은 `output/Tech_SYM/` 아래 기능별 경로다.

**동작 계약:** 각 요구사항별 수정 파일·실행 시각·증거·실제 판정·잔여 보호 결함을 기록한다. 기존 QA 결과를 덮지 않고 변경 기능 케이스와 현재 빌드 환경을 갱신한다. FunQA 서식을 보존하고 QA가 실행하지 않은 재미 평가를 작성하지 않는다. 안내·명세·소스의 불일치와 필요 검증 누락을 마지막 리뷰에서 해소한다.

**검증:** R1~R6 대응과 누락 0, 보호 변경 0, 사용자 기존 수정 보존, 문서 링크/숫자/실행 판정 일치. 빌드 미실행이나 보호 HMS 결함은 명시적 한계로 남긴다. 최종 diff를 사용자에게 제공하며 승인 없는 Git 작업은 수행하지 않는다.

## 공유 파일과 Unity 작업 순서

T2/T3의 Actor/카메라/생성 코드, T3/T4의 소유 카메라 배치는 의존 순서대로 저장한다. T5만 권한 지침·공통 문서 본문을 일괄 갱신하고 T7이 검증 결과를 추가한다. T1의 진입 검증과 T6 실행에서 같은 VerificationBatchService를 동시에 쓰지 않는다. 발견한 새 공유 쓰기 경로는 직렬 실행하거나 한 조립 단계로 모은다. Unity import/컴파일을 기다린 후 다음 native 작업을 실행한다.

## 요구사항 대응

| 요구사항 | 구현·검증 작업 |
|---|---|
| R1 | T1, T6 |
| R2 | T1, T5, T7 |
| R3 | T2, T3, T4, T6, T7 |
| R4 | T2, T3, T4, T6, T7 |
| R5 | T5, T7 |
| R6 | T1, T2, T3, T4, T6, T7 |

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
    depends: [T2, T3]
    risk: RISKY
  - id: T5
    depends: [T1, T4]
    risk: NONE
  - id: T6
    depends: [T1, T3, T4, T5]
    risk: RISKY
  - id: T7
    depends: [T6]
    risk: NONE
regen_barriers: []
```

추가 일괄 regeneration은 없다. 각 작업의 허용 범위에서 생성 코드와 저장 자산을 함께 수정하고 컴파일/저장을 확인한다. 그래프의 독립성은 동일 Editor를 병렬 조작할 권한을 뜻하지 않는다.
