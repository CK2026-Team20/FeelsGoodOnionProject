# V1.5 실행 계획

spec의 R01–R14가 동작 기준이다. 아래 목표/대상/gate가 실행 범위이며 파일 개수 감소만으로 성공을 판단하지 않는다. 현재 승인 대기인 READY이므로 이 계획의 구현 gate는 아직 통과하지 않았다.

진행 순서는 지정 기획·에이전트 지침 정비, authoring 참조 계약, 각 기능 교정, 한 작성자의 통합 연결, 씬/저장 참조 이관, 실제 소스 제거, 비 Play 검증, 최종 수동 제작 안내다. 실제 하트 리소스 연결 뒤 HudIconGraphic 삭제는 마지막 별도 조건 작업이다. 사람이 응집형 Stage를 플레이하여 전체 흐름을 확인하며 에이전트는 Play를 시작하지 않는다.

## 공유 파일 쓰기 원칙

T02는 공통 필드 표시·씬/테이블 선택 어댑터와 Scene/Dialogue/Audio authoring 계약만 만든다. SessionContracts는 T03이 단독 소유한다. T04/T05/T07/T09는 서로의 조립자/등록 파일을 수정하지 않는다. PlayerBridgeService의 실제 물리·피격 API는 T06, 이후 통합 연결은 의존성 뒤 T10이 쓴다. T08은 공통 API를 소비하고 Bridge를 수정하지 않는다.

AppComposition/CoreComposition/CoreWorldBinding/GameSessionRuntime/SessionActorHost/전역 서비스 등록·이벤트 bus 연결은 **T10 한 작성자**가 이관한다. SceneService/LoadedStageRegistry의 등록 연결도 T10에서 최종 대조한다. T11은 소유 저장 씬/설정/prefab 참조 이관을 단독으로 수행한다. T12는 T11의 참조 해소 증거를 받은 뒤 삭제한다. T15는 최종 리소스 연결/잔여 UI 소스만 다룬다.

독립 branch/worktree 실행이라면 실제 diff의 겹친 파일을 확인한다. 예측 밖 겹침은 순차 통합하거나 한 작성자로 넘긴다. 작업 순서상 API 임시 불일치는 해당 domain gate와 전체 compile gate를 구별하고 최종 연결 전에 실제 Unity 통과로 기록하지 않는다.

## T01 — 지정 기획과 에이전트 지침 정비

- 요구사항: R01, R13, R14.
- 목표: 최신 권위와 작업 경계를 다음 에이전트도 혼동하지 않도록 한다.
- 대상: `AGENTS.md`, `CLAUDE.md`, 관련 SYM/씬 지역 AGENTS; `Assets/_Docs/planning-index.md`, `standards.md`, `architecture.md`, `business-rules.md`, `engineering-notes.md`, `contracts.md`, tracking/status/findings. 동일 바이트 V1.5의 `Assets/_Docs/Cooked_시스템_기획서_V.1.5.docx` 및 자기 문서 메타데이터.
- 계약: 원본/기존 PDF/과거 기록 보존. 오래된 두값 CP/현재Stage reset/시간 고정/manager hierarchy 규칙은 현행 기준에서 제외. 간단한 Tooltip·실물참조·에이전트 Play 금지·사람 prefab 소유를 명시한다. 승인된 HMS2파일 예외를 정확히 적는다.
- gate: source hash 일치, 지정 문서 포인터 일치, 최신 규칙과 충돌하는 활성 지침0. Word 원본 내용을 고치거나 렌더 검수가 실행됐다고 기록하지 않는다.

## T02 — 편집 참조·라벨의 공통 경계

- 요구사항: R02, R11.
- 목표: 사람이 Scene/대화/소리/필드를 이해하고 실제 자산을 연결한다.
- 대상: ArchitectureInstallation, IntegrationEntrySettings, SceneContracts, DialogueContracts/DialogueTableService의 선택 계약, AudioCueCatalog/AudioCue, SYM 한정 필드 라벨 Attribute/Editor PropertyDrawer(필요한 최소 파일), BootstrapEntryRoute/IntegrationBuildPipeline의 읽기 계약.
- 계약: SceneAsset→숨은 runtimepath, Stage 식별 자동, 초기 CP는 Stage의 실제 Marker. Table TextAsset→검증된 Code 선택, Audio 용도 선택+Clip. Player 빌드 UnityEditor 참조0. enum 번호·외부 JSON의 Code/Sequence 의미 유지. 임의 global MonoBehaviour CustomEditor/패키지 설치 없음.
- gate: 누락/중복/이동한 씬/잘못된 Code·Clip가 named 작성 오류로 드러남. 내부 생성 값과 수동 key UI 구분, 이미지는 실제 asset reference. 준비 단계201개 목록을 최종199개 실제 필드/SerializedObject와 대조한다.

## T03 — CP snapshot와 해금 복원

- 요구사항: R04.
- 목표: 저장 위치·조각·마스크 한 원본과 원자 복원.
- 대상: `Architecture/Contracts/SessionContracts.cs`, `Architecture/Session/GameSessionService.cs`, 이에 대응하는 비 Play 규칙 검증 코드.
- 계약: snapshot 정확히3항목, 허용mask0/1/2/3, finiteXYZ/정수0..5. 첫 snapshot 없음, 초기 구성과 저장 완료 구분. 현재 해금은 같은 Session이 변경하고 retry는 snapshot으로 복원. Stage 출처 dictionary/ResetStageUnlocks 제거.
- gate: 공개 상태로 저장 전/성공/잘못된 입력·중단 이전 snapshot 보존/조각 소비 후 rollback/해금0→Both→저장→새 해금 rollback을 확인. 같은 계약을 구현과 똑같이 복제한 테스트 금지. 전체 Unity 연결 검증은 T13이다.

## T04 — Stage의 실물 지점과 씬 범위 조회

- 요구사항: R02, R03, R04.
- 목표: 관리자의 부모가 없어도 전체 기믹을 찾고 올바른 작성 지점으로 동작한다.
- 대상: StageService, StageTrigger, CheckpointMarker, CameraZoneTrigger, LevelCameraService/CameraZoneData/Request, SceneComponentLookup, LoadedStageRegistry의 조회 계약.
- 계약: Marker/경로/추격 시작/정렬 Transform, 실제 CameraZoneTrigger 초기 참조. CameraZoneData는 실행 값 struct이며 component/asset라고 설명하지 않는다. string value/pair 강제/Id 입력 제거. 동일 씬 비활성 root까지 소속 조회, 다른 Stage 제외. 일반CP 중복 guard/추격 전 저장 수락은 T10 Session 연결에서 완성한다. Fall의 독립Host 참조와 Player/Shell 분기 authoring도 정의한다.
- gate: 분리 root 목록과 현재 저장 authored 값이1:1로 대응; Marker pose와 실제 Actor 중심 구별; 목표가 움직이는 자식이 아님; unbound/다른 Actor/죽음/전환 요청 거절. enum StageTriggerKind0..7/Side1·Quarter3 의미 보존. 새 순차 Stage 전환 enum/Trigger를 추가하지 않는다.

## T05 — 발판·장치·수집물의 이동/수치 정합화

- 요구사항: R02, R05, R07.
- 목표: 실제 움직임에 Render/Collider가 따라가고 사람이 조절한 값이 실행된다.
- 대상: Platforms의 KinematicPlatformMotion/LinearShuttlePlatform/FloatingPlatform/PressurePlatePlatform/PressureLinkedPlatform/PlatformContacts, Features의 ShatteredPlatform/PlatformPresentation/TearFragment, OvenTrayObject, InteractionPromptAnchor.
- 계약: actual physicsBody/Contacts 한 작성자. Start/End/Open/Closed/Prompt/Interaction Point 참조, 좌표 변환은 작성 부모→world 한 경계. 목표는 고정, 조작점은 실제 움직이는 장치 아래. 압력 연결 원본은 Linked.pressurePlate 한 곳. Tear 전체 pickupRoot 이동 및 원본 복구. 순수 경고/외형 표현은 보존. 파괴 기믹의 자동복구 없음.
- gate: 현재 authored endpoint/위치/회전/기즈모와 새 지점 일치, 임의 부모 pose/회전에서 좌표 차이 없음, 자기 Tween 취소/재준비. 수집Max5에서 객체 유지와 retry Sensor활성. Contact/리소스 수명·물리 작성자 중복0. 엔진 접지/실제 아트 확인은 T13/사람 항목과 구분.

## T06 — 주기 발판·실제 몸체 분리·공통 피해 API

- 요구사항: R06, R07.
- 목표: 1/4 판정·얕은 착지·Player와 shell 분리·현재Actor 수명을 교정한다.
- 대상: PeriodicPlatform, PlayerBridgeService의 공통 피해/강제사망/분리 완료 및 현재Actor 검증 계약. 다른 조립자/씬은 수정하지 않는다.
- 계약: 조절한 시간의 합으로 주기를 계산하고 길이0인 phase를 건너뛴다. 실제 Collider 교차 여부와 몸 높이 침투 비율을 구별한다. 등장 및 이동 중 검사하되 정상 상부 착지는 제외한다. 비율<.25는 몸체 보정/정상 접지, ≥.25는 몸체 위 이동+Dead다. 이동 시간은 누락 clip의0ms와 독립이다. HMS 물리 명령을 사용하고 지속 위치 작성자를 추가하지 않는다. 껍질 소유자의 HP는 유지하며 같은 OwnedDebris의 복귀/속도 정리를 처리하고 낡은 Actor 작업을 취소한다. 공통 넉백 세기와 방향을 정의한다.
- gate: 서로 다른 가로 겹침·몸 높이·배율/Normal/Small,0.249/0.25/0.251 경계, 정상 상부/옆면/형성·이동 중, ForceDeath/Retry1회, Options 정지와 Actor 교체를 확인한다. 껍질의 객체 identity/Owner/Form/HP/IgnoreCollision/무게는 유지된다. 수치 분류 검증과 실제 접지는 별도 결과로 기록한다.

## T07 — 플레이어 실제 표현 참조와 HMS 최소 priority

- 요구사항: R02, R07, R10, R12.
- 목표: 실제 외형/카메라 지점 연결과 지정 표현 우선순위.
- 대상: PlayerDamagePresentation; `Assets/_Code/Core/Tech_HMS/Features/PlayerAnimationPlayback.cs`, `PlayerAnimationController.cs` 명시 허용 범위.
- 계약: visualRoot/cameraAnchor는 실제 ActorBody 자식 참조. PlayerForm 보호 hierarchy/Root배율/물리동거 유지. Interact만4로 분리하고 관련 설명 일치. Scene 설정 normalJump3/small2/Side·Quarter5는 기존 소유 prefab 값으로 T11 이관한다.
- gate: 실제 Playback 클래스의 Tear→Interact/Interact 중Tear거절/Dead·Hit우선/중단결과1회/기본표현복귀,잘못된body/visual연결거절. HMS diff는 허용2파일/행동범위만. 다른HMS+공용+아트 diff0. 에이전트 Play 없음.

## T08 — 일반 적 조합과 피해원 통일

- 요구사항: R02, R07, R09.
- 목표: 같은 수명 상태·접촉 전달을 흡수하고 공통 피해를 실제 소비한다.
- 대상: EnemyActor/EnemyDefinition/EnemyMotor/EnemyContactSensor/EnemyStateMachine/EnemyCombat, DamageFloorPlatform.
- 계약: 상태/남은기절 Actor, 실제Contact집합/반복피해 Sensor,지형 Motor. actor의 단순RecordContact 전달은 제거. 실제patrolStart/end,source별knockbackSpeed 삭제. normalheadtouch damage/기절noattack/끝즉시반전/다시기절누적. 보호HMS/API재구현 없음.
- gate: X/Y endpoint·대기·기절/재기절·Reset·지속접촉무적종료·한피해원1명령,현재Actor/Stage만소비. enum/5기존Definition자산의 의미 보존,기존speed10knockback10 등 실제 authored 수치 이관 설명. 삭제는 T12에서만 한다.

## T09 — UI/대화/메모/연출의 같은 수명 정리

- 요구사항: R08, R09, R10.
- 목표: Skip복원,같은수명표시래퍼흡수,MVVM보존.
- 대상: DialogueView/ViewModel/Service/Settings, CinematicService/ViewModel/View/PresentationState, CoreUiBinding/GameUiRoot, MemoModel/MemoObject/MemoView/UIScaleTween, HudView/ViewModels, TitleViewModel/ApplicationService 소비 계약.
- 계약: Skip 입력 한 번을 처리하고 Completed/Skipped는 완료, Cancelled는 미완료로 남긴다. 완료 Code를 지우는 경로를 제거한다. CoreUiBinding이 HUD/Prompt 참조와 VM 수명을 직접 소유하고, 연출 원자 발행은 Service, 메모 표시 Tween은 View가 소유한다. 메모 식별은 실제 Model 참조로 바꾼다. Quit은 주입 Action을 사용하며 Title/App 조립 변경은 T10이 한다. 하트 Sprite 연결 전 기존 표시를 유지하고 새 Image 경로와 임시 구형 소비의 제거 조건을 명시한다.
- gate: 로그/Options/문자 표시/Auto의 잔여 시간, Dispose 후 호출0, 재Bind 중복0, View의 전역 Service 검색0, 기존 VM 재사용을 확인한다. 표시 결과와 도메인 규칙은 구별하고 placeholder를 만들지 않는다. 타입 삭제는 T12/T15에서 수행한다.

## T10 — Session/App/Core/Stage 연결 단일 이관

- 요구사항: R03, R04, R06, R08, R09, R11.
- 목표: 새계약을 실제수명과초기진입/Retry/최종종료에 연결한다.
- 대상: AppComposition/CoreComposition/CoreWorldBinding/GameRuntimeService/GameSessionRuntime/SessionActorHost/GameplayInputRouter/PlayerBridgeService/SceneService/LoadedStageRegistry, EventContracts/GameEventBus/ApplicationService의 모든조립소비.
- 계약: App이 Options/Fade를 직접 연결하고 scope/Locator 수명은 유지한다. 씬 소속 조회·실제 Marker·현재 Actor의 표현 지점·주기 발판/데드존/껍질을 연결한다. 저장3항목·원자 추격 전 저장·같은 CP 억제·첫 CP 없음·해금/HUD 복원을 완성한다. 안내 완료 기록은 삭제하지 않는다. 최종 대화의 Completed/Skipped만 Ending으로 연결한다. 사망 Retry는 FlowCommand.RetryCheckpoint 명령 콜백, Stage 진입은 소유자의 좁은 이벤트를 사용한다. 독립 모드의 Reader/Router는 각 하나이며 원래 없던 통합 Retry를 추가하지 않는다.
- gate: 원본 변경→VM 표시의 한 경로, 새 판/사망/Options/대화 취소/독립 진입/부분 초기화 실패 후 Dispose/Release를 확인한다. 구형 생성자·등록·버스·이름 Find·관리자 자식 탐색의 소비를 모두 이관한다. Prototype 한 번 종료와 각 등록 Stage의 직접 선택을 확인하되 자동1→2→3은 추가하지 않는다. 전체 compile은 저장 참조 이관 후 T13에서 한다.

## T11 — 소유 씬·설정·컴포넌트 저장 참조 이관

- 요구사항: R02, R03, R05, R07, R09, R10.
- 목표: 새필드와독립월드루트를실제저장자산에연결하여소스삭제전참조를해소한다.
- 대상: `Assets/_Scenes/Tech_SYM/` 소유 씬12개/자기설정/기존자기prefab의필요컴포넌트참조. `evidence/reference-inventory.json` GUID별소비표, `scene-hierarchy-inventory.json`.
- 계약: 씬 작성은 비 Play Editor로 하며 미저장 씬을 보호한다. 모든 기존 작성 지점과 월드 pose를 새 Transform 참조로 이관한다. 정상 점프3/Small2/Quarter5를 소유 설정에서 맞춘다. 월드 루트로 옮겨도 callback 소속과 초기 재준비가 유지된다. 기존 prefab 자체의 삭제/재생성/Apply/전체 Unpack은 하지 않는다. 보호 중첩 원본은 수정하지 않고 허용된 scene override 또는 명시 삭제 대기로 남긴다. HudIcon은 여기서 강제 제거하지 않는다.
- gate: 전후 월드 pose·배율·Collider·Gizmo·부모·GUID를 대조한다. 필수 참조 누락0/잘못된 씬0/수기 목표 위치 입력0을 확인하고 삭제 타입의 소비/GUID별 이관표와 잔여표를 작성한다. 자동 저장 diff도 원인·소유·범위를 분류한다. 통과를 위해 Play를 누르지 않는다.

## T12 — 폐기 source와Editor제작기 실제 삭제

- 요구사항: R09, R11, R14.
- 목표: 이관된22개와유지기능의불필요내부코드를제거한다.
- 대상: 삭제표23개중HudIconGraphic을제외한22개 `.cs`/자기`.meta`;관련using/Configure/overload/메뉴/버스등록·미사용API. 테스트나비Play빌드에서실제필요한경계는남긴다.
- 계약: 삭제 전 코드/자산/중첩 prefab/Editor·build 소비0을 확인한다. 생성 도구를 실행하지 않으며 보호 diff0을 유지한다. 실제로 필요한 callback/검증/캐시/세대 번호/finally를 지우지 않는다. 미해소 참조는 해당 파일의 삭제 대기로 정확히 남기고 목표 미완료로 기록한다.
- gate: 삭제 source 존재0, 각 GUID 소비0, 삭제 타입 호출0, Missing Script/import 오류0을 확인한다. 파일 수만 줄이는 전달 래퍼/partial을 추가하지 않는다.22개 모두 사라졌다는 확인은 실제 파일과 참조 검색으로 한다.

## T13 — 비 Play 통합 검증과 코드 리뷰

- 요구사항: R04–R09, R12–R14.
- 목표: 실제수정source/자산의관찰가능한결과를검증하고수동확인영역을분리한다.
- 대상: 변경source/자산,이미프로젝트에있는비Play검증경계,새로필요한EditMode규칙검증,코드리뷰/검증기록.
- 계약: 정적 참조/권한/직렬화 검사, 실제 Editor import/compile, 필요한 EditMode·명시 빌드를 Play 없이 실행한다. Unity CLI 이용 시 status/pipeline/실제 query 연결 증거를 먼저 확인한다. 프로젝트 잠금이나 미저장 상태가 검증을 막으면 강제 닫기·Play 우회를 하지 않고 해당 검증을 미실행으로 기록한다. 물리는 격리 EditMode 엔진 검증을 실제 했을 때만 그 범위로 보고한다. 전체 실제 플레이는 사람이 한다.
- gate: 규칙 검증·컴파일·연결 검사의 결과, source 식별, 실패 원인, 미실행 범위를 기록한다. CLI 포트/HRESULT만으로 통과를 주장하지 않는다. 사람 확인 항목에는 CP/1/4/껍질/Root·Sensor/대화 Skip/독립 진입/프로토타입 전체 흐름이 있어야 한다. QA/FunQA 평가를 새로 작성하지 않는다.

## T14 — 최종 사람이 만드는 매뉴얼·HMS 공유 기록

- 요구사항: R02, R10, R12–R14.
- 목표: 최종코드를모르는사용자가새리소스로실제prefab을조립할수있다.
- 대상: 제작guide/Inspector표/script필요성·최종삭제/현재흐름/이전및이번HMS변경/코드리뷰·검증결과문서,필요시실제최종Inspector기록.
- 계약: 기존 READY의 “수정 후”를 실제 존재하는 필드·타입·부착 위치와 대조한다. 물리 Root 예외·비활성 Player/활성 Shell·실제 경로 지점·UI 참조·Normal/Small 이름 계약을 설명한다. 삭제 대기 리소스·부족한 아트·미실행 사람 검증을 완료로 표시하지 않는다. 별도 지시 전에 PR을 만들지 않는다.
- gate: 모든 prefab 종류에 부착 위치/필수 설정/드래그 대상/상태 원본/해제 책임/이전 소비 전환표가 있다. 사용자가 source를 읽어야만 알 수 있는 제작 단계를 남기지 않는다. 문서 링크와 추적 coverage gap은0이다.

## T15 — 사람이 제공한 하트 리소스 이후 조건부 최종 삭제

- 요구사항: R09, R10, R14.
- 목표: 실제Sprite가있는새HUD에서HudIconGraphic과임시구형표시소비를완전히제거한다.
- 대상: **외부 입력: 사람이 리소스를 병합하고 새 HUD를 조립한 상태**; HudView의 Image 참조/구형 분기, HudIconGraphic.cs/자기 meta와 모든 저장 소비.
- 계약: 에이전트가 아트/prefab을 만들거나 기존 prefab을 삭제하지 않는다. 이 조건이 없으면 T15를 완료로 처리하지 않고 리소스 대기로 인계한다. 다른22개 삭제·기능 교정·문서가 가능하면 먼저 완료한다. 자료 대기를 새 권한 요청이나 placeholder 생성으로 해소하지 않는다.
- gate: 실제 하트3개 Sprite/Image 연결, 기존 Graphic GUID/타입 소비0, source 실제 삭제, 구형 분기 제거, 비 Play import/compile을 다시 확인한다. 실제 표시·플레이 완료는 사람의 기록이 있을 때만 명시한다.

T15가 미완료이면 전체23개 삭제 완료라고 표시하지 않는다. T01–T14의 구현 작업과 T15·사람 Play 완료를 별도로 인계한다. 외부 자료가 제공된 것으로 가정하여 작업 상태를 숨기지 않는다.

## 요구사항 ↔ 작업

| 요구사항 | 작업 |
|---|---|
| R01 | T01 |
| R02 | T02, T04, T05, T07, T08, T11, T14 |
| R03 | T04, T10, T11 |
| R04 | T03, T04, T10, T13 |
| R05 | T05, T11, T13 |
| R06 | T06, T10, T13 |
| R07 | T05, T06, T07, T08, T11, T13 |
| R08 | T09, T10, T13 |
| R09 | T08, T09, T10, T11, T12, T13, T15 |
| R10 | T07, T09, T11, T14, T15 |
| R11 | T02, T10, T12 |
| R12 | T07, T13, T14 |
| R13 | T01, T13, T14 |
| R14 | T01, T12, T13, T14, T15 |

## Execution Graph

```yaml
tasks:
  - id: T01
    depends: []
    risk: NONE
  - id: T02
    depends: [T01]
    risk: RISKY
  - id: T03
    depends: [T01]
    risk: RISKY
  - id: T04
    depends: [T02, T03]
    risk: RISKY
  - id: T05
    depends: [T02]
    risk: RISKY
  - id: T06
    depends: [T03, T05, T07]
    risk: RISKY
  - id: T07
    depends: [T02]
    risk: RISKY
  - id: T08
    depends: [T06]
    risk: RISKY
  - id: T09
    depends: [T02]
    risk: RISKY
  - id: T10
    depends: [T03, T04, T05, T06, T07, T08, T09]
    risk: RISKY
  - id: T11
    depends: [T10]
    risk: RISKY
  - id: T12
    depends: [T11]
    risk: RISKY
  - id: T13
    depends: [T12]
    risk: RISKY
  - id: T14
    depends: [T13]
    risk: NONE
  - id: T15
    depends: [T14]
    risk: RISKY
regen_barriers: []
```

기존 자동 생성기를 regen barrier로 쓰지 않는다. 새 참조는 실제 편집/빌드 경계에서 검증하고 저장한다. 그래프 순서는 실제 의존을 반영하며 risk는 동작·불변식·수명·경계가 있는지로 분류했다.

## 실행 결과와 현재 인계

작업 의존·Execution Graph는 승인된 구조 그대로다. T01–T12는 실제 기능 구현·소유 참조 이관·22소스 삭제를 반영했다. T13의 최종 Source/엔진/빌드 증거와 T14의 최종 매뉴얼·독립 검토 판정은 [현재 상태](../status.md)와 [검증 기록](테스트%20및%20검증%20항목.md)에 명시한다. T15는 사람이 실제 하트·Q/E·조각 리소스와 새 HUD를 연결하고 모든 구형 소비를 이관한 뒤 진행할 외부 조건부 작업이다. 전체23삭제 또는 사람 Play 완료로 집계하지 않는다.

## 최종 실행 범위와 남은 승인

T01–T13의 기능/참조/소스 정리와 비Play검증을 반영했다. T14는 Source109/표시필드199·구체작성타입50/기존prefab61 GUID소비의 제작 매뉴얼과 현재지침을 반영했다. 최종 검토의 StageTrigger 수정 a192d2a7 이후 현재 컴파일158/158/11·Player204·Native201·6씬 빌드 오류0/경고48을 확인하고 1b7069f1까지 새 전체 diff/문서의 독립 재검토 approved/남은 차단0을 기록하여 T14 기술 인계를 완료했다. 사용자 최종 수락과 active3보관은 승인 후 진행한다. T15는 실제하트뿐아니라Q/E·조각의12소비이관과사람제작을 기다리므로완료가아니다. 위ExecutionGraph와외부자료경계는변경하지않았다. 로컬featurecommit만진행했고remotePR/main병합은없다. 최종결과승인후active3문서보관을완료한다.
