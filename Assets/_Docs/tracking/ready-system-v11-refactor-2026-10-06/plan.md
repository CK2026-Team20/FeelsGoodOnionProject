# 최신 시스템 명세 리팩터링 실행 계획

> 눈물 가림 규칙 추가: T1에서 후보 범위와 실제 Hurt Collider 직접 피격을 분리하고, T10에서 벽·사물·부분 노출·다중 Collider·원점 겹침의 기능 검증을 수행한다. 판정점/다른 적의 차단 여부는 선택 검토 후 반영한다.

> 2026-10-07 사용자 `dryforge:go` 실행 지시와 Desktop 확정안을 반영했다. #11의 원본 기획 문서 정리는 제외하고 안내 방식은 적용한다. 최신 세부 계약은 [확정안 적용 명세](<확정안 적용 명세.md>)다. 현재 Stage에서 얻은 능력은 사망 복귀에 다시 잠그고 트리거 재접촉으로 해금한다. 이번 HMS 수정은 기존 기능을 보존한 최소 확장으로 한정한다. 구현·실행 검증 완료 여부는 별도 결과 문서를 따른다.

## 진행 방식

이 계획은 `.dryforge/spec.md`의 동작을 구현할 순서와 파일 경계를 정한다. 방법은 실제 diff/검증에 따라 조정할 수 있으나 기능 규칙·두 값 저장·권한은 임의로 바꾸지 않는다. 실제 삭제는 대체 기능과 호출/직렬화 참조 이관 후 수행한다. 2026-10-07 직접 go 지시로 실행 중이다. 실제 완료와 실행 증거는 검증 문서에 따로 기록한다.

먼저 필요한 HMS 공개 경계와 세션 계약을 맞추고, 소유 카메라/기믹/적/화면 기능을 변경한다. 작성 도구는 기능 계약이 정리된 뒤 수정한다. 등록 파일과 실제 자산 연결/삭제는 한 담당자가 순서대로 수행하고 마지막에 통합 기능·새 빌드·문서를 검증한다. 공유 파일을 동시에 수정하지 않는다.

체크포인트 두 값만 보관하므로 월드 저장 DTO/Participant/Registry/객체 상태 ID 작업은 없다. App/Core/Stage와 실제 물리/표시/수명 경계는 유지한다. 전달자/중복 상태를 줄이는 과정에서 거대 GameManager나 별도 SYM HMS 복제본을 만들지 않는다.

## 작업별 계약

### T1 — 필요한 HMS 공개 경계

대응 요구: R01, R03, R04, R06, R07, R17, R20.

대상은 `Assets/_Code/Core/Tech_HMS/Features/` 아래 PlayerInputReader.cs, PlayerFacade.cs, PlayerCameraController.cs, PlayerAnimationTypes.cs, PlayerAnimationController.cs, PlayerAnimationPlayback.cs, PlayerAnimationSet.cs, PlayerAnimatorOutput.cs와 새 눈물 가림/직접 피격을 위한 TearSkill.cs의 명시한 범위뿐이다. 이번 명세 1회에 한정한 사용자 직접 허용으로 최소 additive 확장만 수정한다. 기존 기능은 보존한다. 최소 9개 기능 예외를 정리 후보 13개 전체의 변경 허가로 확대하지 않는다. TearSkill.cs는 기능 예외와 정리 후보가 겹치는 파일이며 직접 피격 경계 변경과 준비/쿨다운 정리의 범위를 구분한다.

E 입력, 공통 HP1/2초 무적과 Hit/강제 사망, 유효 표현 완료, Side/Quarter 재연결·미리 선택/완료 후 확정을 구현한다. 현재 PlayerFacade/Movement/Animation을 재사용하고 상태 원본을 추가하지 않는다. 기존 protected 독립 소비의 Back/BackFixed·입력/슬롯 계약은 무단 파손하지 않는다. 기존 HMS 공개 속성과 기능을 삭제하지 않는다.

완료 검사는 예외 diff 범위, 새 컴파일, 수락/중복 거절/무적/강제 사망/완료 취소·낡은 RequestId/clip 누락/Bind 두 번·Unbind 뒤 호출 0이다. 실제 아트 재생은 clip 연결 후 별도로 확인한다. 공용 계약·보호 메타·프리팹·ProjectSettings와 SYM 등록 파일은 쓰지 않는다.

### T2 — 세션·두 값 저장·입력·사망

대응 요구: R01, R02, R03, R04, R06, R07, R08, R16, R18, R20.

대상은 `Assets/_Code/Core/Tech_SYM/Architecture/Contracts/SessionContracts.cs`, Session의 GameSessionService/PlayerBridgeService/GameplayInputRouter/PlayerDamagePresentation/SessionActorHost/ActorFallGuard, Foundation/GameFlowService, Integration/Runtime/GameSessionRuntime 및 IntegrationEntrySettings 및 필요한 SceneContracts/SceneService/LoadedStageRegistry/GameRuntimeService 소비 계약이다. 이들 공통 계약/Host/Runtime 파일은 이 작업의 단일 작성자가 소유하며 이후 작업은 공개 결과만 소비한다.

Snapshot을 Position/Fragments 정확히 두 필드로 바꾸고 모든 생성/읽기/Retry 소비를 함께 이관한다. 현재 Stage는 Flow의 실행 구성으로 조회한다. 배치 ID/Pose는 중복 판별/초기 진입용이며 저장 payload에 남기지 않는다. Core를 최초 한 번 로드하고 등록된 Stage 씬 전부를 additive 최초 로드한 뒤 유지한다. 선택 Stage의 실행 문맥과 전체 로드 목록을 구분한다. 비활성 Stage는 Actor/입력/트리거에 연결하지 않는다. 같은 Stage 씬의 명시적 초기 재준비 후 실제 Body 위치/조각 복원·HP/Normal/껍질/속도/무적 초기화를 연결한다. 해금은 일반 세션 상태로 저장에 넣지 않으며 현재 Stage 해금/연결 안내만 retry에 재잠금/재접촉 가능하게 초기화한다. RecoverShell 능력/통지·백뷰 입력 기준·카메라 자식 탐색·Transient Scope 의존을 정리한다.

HMS Reader와 Router 각 한 개, 열린 메모 F 닫기 우선, Options 중 입력 전파 0, Hit/Dead 표현 완료와 자동 retry 단일 수락을 만든다. 장치 F Busy는 이동/점프를 막지 않는 별도 상호작용 제한을 후속 작업이 연결한다.

완료 검사는 V01..V13의 관련 경계, 실패 snapshot 원자성, 시작/끝 두 CP만 캡처·동일 CP 억제·중간 해금/추격 시작의 캡처 없음, 사망 중 화면 취소, 두 판/여러 Actor 생성·해제의 중복 없음이다. 여기서는 App/Core 서비스 등록·씬/프리팹을 수정하지 않는다.

### T3 — Side/Quarter 카메라와 구간

대응 요구: R08, R17, R18, R20.

대상은 Level/LevelCameraService.cs와 그 파일 안의 CameraZoneData/요청 데이터, CameraZoneTrigger.cs, CheckpointMarker.cs다. 별도 CameraZoneData.cs를 새로 만들지 않는다. T2의 Actor 준비/해제와 T1 공개 Bind 계약을 소비한다.

요청은 Preview→실제 Brain 블렌드/Side Z정렬→Commit 순서이며 완료 전 입력을 제한한다. 최신 구간 요청의 취소, 옵션 중 시간 보존, Actor 해제 때 구독/목표 제거를 구현한다. 새 SYM 모드는 Side=1/Quarter=3이고 저장 2의 Corridor 배치는 실제 의미를 확인해 이관한다. 초기 모드는 배치 구간에서 결정하고 checkpoint에 넣지 않는다. 로컬 XYZ 정렬과 파란 기즈모의 계산을 공유한다.

완료 검사는 V31 및 실제 Brain/물리 완료·중첩 취소·초기 복원 위치의 모드·Options/Actor 소실·옛 목표 참조 0이다. App/Core 등록·Native prefab과 ActorCameraRig 삭제는 T9에 맡긴다. 우선순위는 HMS 한 작성자다.

### T4 — 발판·공통 장치·조각

대응 요구: R04, R06, R08, R09, R10, R19, R20.

대상은 SYM Platforms, Features, OvenTray의 유지 코드와 ObjectRotation/FloatingMotion의 소비 이관이다. CharacterSlipperyEffect/OilSurface는 보호된 기존 공개 기능을 읽고 연결하며 원본을 수정하지 않는다. 소유 물리 이동자 KinematicPlatformMotion은 명시적 PhysicsBody/Contacts를 받고 단일 위치 작성자로 유지한다. Contacts는 실제 물리 자식에서 작동한다.

이동 양 종점 0.5초, 부유 한 착지 반동, 5/2/3 주기, Normal 윗면 경고 1초 종료 즉시 Collider 해제/비복구 붕괴, 실시간 압력 반전, 일반 물체/PC 운반과 공통 피해를 구현한다. 기존 OvenTrayObject를 공통 두 종점 이동/힌지 장치로 일반화하여 문/트레이/서랍을 설정으로 조립한다. 수락·완료·취소를 상호작용 서비스가 관찰할 수 있게 제공하고 입력을 읽는 두 번째 Router를 만들지 않는다. 두 TearFragment 경로의 성공/최대치 보존을 하나로 이관하고 Visual 표현을 통합한다. 소비된 조각은 비활성 객체로 보존하고 Stage retry에 활성/Collider/소비 플래그/표현을 복구하는 공개 계약을 제공한다.

World 저장/상태 등록/ID를 추가하지 않는다. 보호 HMS MovingPlatform은 좌표/운반 계약이 완전히 같지 않으므로 이름만으로 SYM을 지우지 않는다. 완료 검사는 V14..V20과 V05/06/09의 실제 물리·같은 접촉·이탈·재압력·정지·최대치 결과다. 등록/Stage 공통 파일/네이티브 자산/32개 소스 삭제는 T9에 맡긴다.

### T5 — 세 종류 일반 적

대응 요구: R03, R06, R08, R11, R19, R20.

대상은 Enemies의 EnemyDefinition/EnemyActor/EnemyCombat/EnemyMotor/EnemyStateMachine/EnemyContactSensor와 남기는 실제 상태 구현이다. 종류/상태 값은 명세의 집합을 사용하고 기존 Patrol/8방향 설정을 실제 축 설정으로 이관한다. 확정: 일반 적 추적/복귀 제거, X/Y 순찰·Idle·Stunned 유지.

X/Y 순찰·Idle·Stunned를 FSM 한 소유자로 만들고 일반 적 Chase/Return은 이관 후 제거한다. 공통 플레이어 피해 경계를 호출한다. EnemyModel HP/적 무적/처치/접촉 consumedDamage/머리 밟기/Float/사망 기름 경로를 제거한다. IState가 별도 파일로 제공하던 enum/내부 계약은 FSM에 필요한 만큼 통합한다. 물리 Body의 단순 relay는 콜백 경계로 남기고 Visual에는 스크립트를 두지 않는다.

완료 검사는 V21/22와 공통 피해/재기절/실제 지형 제한·비회전/양수 단위 배율 검증이다. Stage/등록/native 정의 자산·소스/메타 삭제는 T9 단일 연결 단계에 맡긴다. 보호 Stun VFX는 공개 IStunState를 소비한다.

### T6 — 상호작용·메모·대화·옵션/HUD

대응 요구: R04, R05, R08, R10, R12, R13, R15, R16, R19, R20.

대상은 Session/PlayerInteractionService.cs, Interaction/MemoUIController.cs, Architecture/Dialogue와 UI의 서비스/VM/View다. T2의 저장/입력/사망, T4의 장치 작동 수명 계약을 소비한다. 확정: 현재 장치 수락/Busy 방식 유지. 확정: 메모 월드 정지/대화 자체 월드 진행, 소유 정지 토큰 유지.

장치 Busy는 상호작용 선택/수락 경계 한곳에서 다루고, 실제 작동 종료/파괴/세션 취소에서 해제한다. Memo의 자기 WorldPause를 유지하고 Dialogue 자체 WorldPause만 제거하되 Closing/표시의 자기 입력 제한은 유지한다. F 한 번, 옵션 우선, 미완료 화면의 사망 취소를 연결한다. Dialogue JSON 5필드/1..N·완료 장부·행 완전 표시 로그·0.3/3초·Options/Log 표시 시간을 구현한다. 완료 장부는 snapshot과 연결하지 않는다. 현재 Stage에서 재잠근 능력에 연결된 안내 완료만 제거해 재접촉 안내가 실행되게 한다.

옵션 수동 Retry/Resume, Recover/CPID HUD, Instruction과 두 번째 Prompt의 소비를 제거한다. 기존 VM/소유형 제한·취소 가능한 표시 스케줄러를 재사용한다. DialogueDriver의 Cancel 책임은 Core 종료 소유자에 이관 요청하고 드라이버를 유지할 이유를 만들지 않는다. 완료 검사는 V07/08/19/24..27/29/30/34의 입력/월드 진행/사망/중첩/Dispose다. StageService·CoreUiBinding·CoreComposition·Audio 등록 파일은 T9에 맡긴다.

### T7 — 단일 원화 컷 연출

대응 요구: R07, R14, R15, R18, R19, R20.

대상은 Cinematics/CinematicCatalog/Service/PresentationState/Host/ViewModel/View 및 PresentationContracts의 실제 연출 소비다. 순서 있는 Cut 목록과 서비스 시계로 원화/문구/기본 4초/전체 Skip/옵션 남은 시간을 구현한다. View는 표시/입력만, Host는 런타임 tick/수명, 서비스는 컷 진행과 결과를 소유한다.

단일 원화 요구에 Timeline Track/Clip/Bridge·4패널/Scale 호환을 남기지 않는다. Catalog/VM/View의 실제 책임을 유지하고 새 Playable/Track 래퍼를 만들지 않는다. 완료 검사는 V28과 준비 실패·Skip 한 번·옵션 재개·입력 전파 0·재진입/취소/원화 없음의 명확한 오류다. 기존 Timeline 자산/컴포넌트 소비 이관과 네 소스 삭제, 공통 등록은 T9에 맡긴다. Packages/다른 Timeline 자산은 쓰지 않는다.

### T8 — 최초 작성 도구 통합

대응 요구: R09, R10, R11, R13, R14, R16, R17, R18, R19, R20.

대상은 SYM의 기존 Audio/Chase/Cinematic/Dialogue/Integration/Level/Session/UI Builder, EnemyPrefabAuthoring, PrototypeStageAuthoring 및 중복 보정 메뉴의 이관이다. 기능 계약과 실제 필드가 나온 뒤 최초 작성 경로가 새 플레이어/분리 리그/장치/화면 구조를 바로 만들게 한다.

HmsActorCameraAuthoring/PrototypePlayerAuthoring/PrototypeUiAuthoring/SingleArtworkAuthoring의 필요한 처리만 기본 Builder로 흡수하고 폐기 Back/출력 교대/퍼즐/Timeline/자동 복구 생성 분기를 제거한다. BootstrapPlayModeHook와 IntegrationBuildPipeline, 실제 작성 메뉴는 유지한다. 누군가의 전체 자산을 자동 재생성하거나 기존 prefab을 일괄 제거하지 않는다.

완료 검사는 소유 임시 자산에서 한 번의 작성/동일 설정 재작성에 중복 객체/참조가 없고, 정확한 계층·필수 값·한국어 Tooltip을 만드는지다. 실제 공유 자산 쓰기/삭제는 T9에서 순서대로 실행한다.

### T9 — 등록·Stage·추격·Native 연결·실제 삭제

대응 요구: R01..R20.

단일 작성자 대상은 Integration/Runtime/AppComposition/CoreComposition/CoreWorldBinding/CoreUiBinding 및 종료/오디오 연결, Level/StageService의 배치 데이터와 StageTrigger/추격·Poppo 트리거, Chase의 모델/런타임/표현, 기존 소유 씬/설치/작성 자산이다. 새 StageDefinition 파일을 분리하지 않는다. T1..T8 결과가 준비된 뒤 연결한다. 전체 Stage 씬을 로드한 상태에서 활성 Stage 하나만 CoreWorldBinding에 연결하고 비활성 Stage의 외부 Tween/타이머/추격을 중단한다. CoreComposition은 정상 세션 종료 시 world/runtime/연출/Host 참조를 해제하는 명시적 경로를 제공하며 같은 Core 씬에서 다음 판을 초기화한다. SessionActorHost의 세션 해제는 앱 영구 종료와 구분한다.

App 출력 Camera/Brain/Listener 한 개와 Core 가상 카메라 두 개를 연결하고 Actor 자식 카메라/대기 출력 교대/두 번째 우선순위 경로를 제거한다. Core가 Dialogue 취소와 Actor 수명/모드 연결을 맡는다. 전체 Collider 시작선·새 캡처 없는 추격 시작 수락 후 문·gap 0.8 강제 사망·Poppo 최종 대화 완료 Ending을 연결한다. 구출/껍질 래치/게이트·옵션 수동 Retry/회수 소리·Transient Scope 등록/자산 참조를 제거한다.

먼저 소비를 새 책임에 이관하고 compile/기능을 확인한다. 그 뒤 `스크립트 필요성 및 정리 명세.md`의 32개를 정확한 path+GUID로 재조사한다. 코드 호출·문자열/반사/메뉴·native 직접/재귀 의존·소유 저장 자산의 참조가 모두 이관되면 소스/소유 메타를 삭제한다. 임시 호환도 여기서 제거한다. 폐기 Timeline 등 소유 자산은 다른 소비가 없는 경우에만 정리한다. 보호 자산의 필요한 참조가 발견되면 권한/이관 없이 삭제하지 않는다.

기존 prefab 일괄 제거는 사용자 몫이다. 사용자가 제거한 뒤의 실제 자산 목록을 다시 확인하고 남은 소유 씬 참조를 필요한 새 prefab에 연결한다. 미래 가이드에 맞는 필요한 자산만 만들며 원본 Apply는 없다. 남은 비대상 소유 씬에도 삭제 GUID의 Missing을 남기지 않는다. 빈 필수 참조를 정상으로 통과시키지 않는다.

Native Editor API로 계층/필드/메타/저장/재로드를 확인하고 사전 scene setup/isDirty를 보존한다. 이미 미저장이면 임의 저장/폐기하지 않는다. 완료 검사는 V23/24/31..34, GUID/필수 null/Missing SYM 0, 입력/이동자/출력 각 단일성, 32개 실제 삭제·내부 폐기 경로 없음·보호 diff 허가 범위다.

### T10 — 기능 통합·새 빌드·문서/QA 인계

대응 요구: R01..R20.

대상은 구현된 사용자 기능, 일회성 검증, 새 Windows 빌드, 기능 케이스 Excel과 `Assets/_Docs/` 문서다. 실제 입력과 물리 시간을 포함한 V01..V34를 실행하고 NewGame→Opening→Stage/메모/적/기믹→여러 retry→Poppo/Ending→Title→다음 판을 검증한다. 서비스 직접 호출·teleport 결과는 실제 입력 전체 동선과 별도로 기록한다.

실행 전 `unity status --project-path <checkout> --format json`, `unity pipeline list --format json`와 실제 Editor 조회를 성공시킨다. 일회성 검증 소스는 `output/Tech_SYM/verification/system-v11-refactor-2026-10-06/<실행시각>/`에 두고 live run_script의 진단 후 실행한다. 취소/finally 복원과 현재 소스/명세 hash를 기록한다. 검증 전/후 scene setup/dirty·컴파일/Console·보호 hash·자동 저장 diff와 실제 원인을 분리한다.

필요한 기능 검사가 통과하면 `Cooked/Integration/Build Windows Player` 또는 `Cooked.Integration.Editor.IntegrationBuildPipeline.BuildWindowsPlayer()`로 정확한 여섯 씬의 새 exe/BuildReport/hash를 만든다. 이전 exe/검증 결과를 새 성공으로 쓰지 않는다. 증거 경로는 기존 `output/Tech_SYM/integration/build/Windows/`를 따른다.

기능 Excel은 실제 판정만 갱신하고 QA FunQA DOCX 원본 양식은 보존한다. 팀 실제 빌드 플레이·아트/clip 재생·FunQA가 대기면 해당 상태를 유지한다. 문서의 계획 API/필드·필요성·실제 삭제 수·자산 연결·잔여 보호 정리 후보를 현재 구현과 다시 맞춘다. Go의 해당 Git 승인/범위 규칙을 확인한 후에만 저장소 작업을 수행하며 이 계획 자체는 Git 실행 승인이 아니다.

## 공유 작성과 이관 규칙

T2가 SessionContracts/Host/Bridge/Router/Flow와 GameSessionRuntime을 단독 작성한다. T3..T7는 그 계약을 소비하며 공통 등록/배치에 쓰지 않는다. T4의 장치 수명 계약이 나온 뒤 T6가 Busy를 연결한다. T6의 표시 정지 계약 이후 T7이 컷신에 연결한다. T8은 기능 결과를 바탕으로 작성 도구만 변경한다. T9만 App/Core/Stage/추격 통합 파일과 실제 자산을 순서대로 수정한다.

코드 이관 중 compile 유지가 필요한 임시 연결은 이유와 T9 제거 조건을 남긴다. 32개를 먼저 삭제하고 compile 오류로 필요성을 추측하지 않는다. 예상 밖 공유 diff는 실제 파일 경계로 확인해 순서대로 처리한다. 버전관리 reset/clean으로 기존 작업을 없애지 않는다.

## 요구사항 대응

| 요구 | 구현 작업 | 기능 검증 |
|---|---|---|
| R01 | T1,T2,T9,T10 | V01,V02 |
| R02 | T2,T9,T10 | V03,V04 |
| R03 | T1,T2,T5,T9,T10 | V05,V06 |
| R04 | T1,T2,T4,T6,T9,T10 | V07,V19 |
| R05 | T6,T9,T10 | V08,V27 |
| R06 | T1,T2,T4,T5,T9,T10 | V09 |
| R07 | T1,T2,T7,T9,T10 | V10,V11,V34 |
| R08 | T2,T3,T4,T5,T6,T9,T10 | V12,V13 |
| R09 | T4,T8,T9,T10 | V14..V18,V20 |
| R10 | T4,T6,T8,T9,T10 | V15,V19 |
| R11 | T5,T8,T9,T10 | V21,V22 |
| R12 | T6,T9,T10 | V23,V24 |
| R13 | T6,T8,T9,T10 | V25..V27 |
| R14 | T7,T8,T9,T10 | V28 |
| R15 | T6,T7,T9,T10 | V29 |
| R16 | T2,T6,T8,T9,T10 | V30 |
| R17 | T1,T3,T8,T9,T10 | V31 |
| R18 | T2,T3,T7,T8,T9,T10 | V32 |
| R19 | T4,T5,T6,T7,T8,T9,T10 | V33 |
| R20 | T1,T2,T3,T4,T5,T6,T7,T8,T9,T10 | V33,V34・문서/필드 검사 |

## Execution Graph

```yaml
{
  "tasks": [
    { "id": "T1", "depends": [], "risk": "RISKY" },
    { "id": "T2", "depends": ["T1"], "risk": "RISKY" },
    { "id": "T3", "depends": ["T2"], "risk": "RISKY" },
    { "id": "T4", "depends": ["T2"], "risk": "RISKY" },
    { "id": "T5", "depends": ["T2"], "risk": "RISKY" },
    { "id": "T6", "depends": ["T2", "T4"], "risk": "RISKY" },
    { "id": "T7", "depends": ["T2", "T6"], "risk": "RISKY" },
    { "id": "T8", "depends": ["T3", "T4", "T5", "T6", "T7"], "risk": "RISKY" },
    { "id": "T9", "depends": ["T8"], "risk": "RISKY" },
    { "id": "T10", "depends": ["T9"], "risk": "RISKY" }
  ],
  "regen_barriers": []
}
```

자산 전체를 자동 재생성하는 단계는 두지 않는다. 기존 작성 도구는 T8에서 수정하고 필요한 소유 자산만 T9에서 순서대로 저장한다.


## 2026-10-07 직접 지시의 실행 보정

확정안 적용 명세의 항목별 계약과 씬 재로드 비교를 따른다. T1은 기존 HMS 기능을 삭제/변경하지 않는 최소 확장만 허용되며 새 조립은 명시적으로 그 확장을 선택한다. T2는 위치/조각 두 값과 현재 Stage 재잠금, T9는 같은 Stage 씬/서비스의 초기 재준비 연결을 수행한다. T4는 생성/소멸 발판 침범과 1초 붕괴, T5는 일반 적 추적 제거, T6는 메모 월드 정지/대화 진행/HUD 음영을 반영한다. 기존 Execution Graph 의존 관계는 그대로 유지한다.


## Core/Stage 전체 선로딩 추가 확정

2026-10-07 사용자 추가 답변: InGameCore 1회 로드 이후 모든 등록 Stage를 additive로 최초 전체 로드하고 이후 Stage 씬을 다시 로드하지 않는다. T2는 SceneContracts/SceneService와 LoadedStageRegistry의 필요한 소비 계약도 함께 이관하며, T9는 이미 로드한 씬의 루트 활성화/Stage 전환·새 판 초기화/서비스 수명 연결을 담당한다. 획득해 사라진 눈물조각도 T4의 객체 소비/복구 계약과 T9 Stage 초기 재준비로 다시 등장한다. Stage 시작/끝 CP 두 곳만 저장하며 중간 해금/추격 시작은 새 저장 지점이 아니다. 기존 그래프의 의존 관계는 유지한다.
