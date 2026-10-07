# 구현 사실과 새 명세 적용 시 주의점

2026-10-07 최종 선택 반영. 변경 전 구현 설명과 현재 목표를 구분한다. 목표는 business-rules.md와 확정안 적용 명세, 실제 실행 상태는 tracking/status.md를 따른다.

## 입력과 상태

변경 전 HMS Reader는 F 눈물, SYM Router/상호작용은 E였다. T1/T2 통합 경로는 E 눈물/F 상호작용이며 HMS의 독립 기본 F는 선택적 공개 설정으로 보존한다. 안내·메모 열기/닫기·독립 씬 소비자를 함께 바꿔야 한다. 이미 열린 메모의 F닫기는 gameplay CanAct 검사보다 먼저 처리하되 위에 Options가 있으면 아래 메모로 입력을 흘리지 않는다. 하나의 Reader/Router만 허용한다.

메모는 자기 WorldPause를 유지하고, 대화/로그 자체의 WorldPause만 제거한다. 플레이어 입력/표시 제한은 남으며 옵션 중첩 해제는 자기 토큰만 처리한다. 메모 Opening에서도 F닫기를 수락하고 Closing까지 입력을 막는다. 대화 중 월드가 진행하므로 피해/사망 취소가 실제로 작동해야 한다.

EnemyCombat의 consumedDamage와 피해 발판 자체1초 타이머는 공통2초 무적과 다르다. ActorFallGuard의 Damage(MaxHP)는 무적이면 실패할 수 있다. 데드존/포획은 강제 사망 계약을 사용하고 일반 피해와 합치지 않는다. 클립 완료 RequestId와 실제 일회성 우선순위를 확인하며 고정 초를 실제 재생 완료로 기록하지 않는다.

## 카메라/Play 진입

PlayerRoot CameraRig를 소유 native에서 제거하고 App 출력1/Core 분리 리그를 연결했다. T1 Bind/Unbind/Preview/Commit과 실제 움직이는 Anchor를 사용하며 HMS Start 전 실패를 준비 완료로 처리하지 않는다. Phase2 initial Quarter 보정 후 Options 사망 두 경로의 각10 GREEN이 저장 두 값·씬 유지·Quarter 준비·3적 setup을 확인했다. 최종4소스 actual compile과6route+3direct/Stop, Stage2·3 능력 보존 각6GREEN은 확인했고 Windows 최종 수락/수동 동선은 별도다.

새 Side/Quarter 전환은 실제 Brain 완료와 Side Z정렬 후 물리 축을 확정한다. 요청 중 다른 구간은 이전 작업을 취소하고 최신 요청을 처리하며 옵션은 블렌드/정렬/확정을 정지한다. 반복 요청이 새 토큰/명령을 만들지 않게 한다. DOTween을 강제 종료한 검사는 실제 Brain 완료 증거가 아니다.

ArchitectureInstallation의 정확한 여섯 씬만 부트 보정하고 비대상은 직접 Play한다. 통합 중 활성 씬은 Core이며 Core 1회 로드 뒤 모든 등록 Stage를 최초 additive 로드한다. 이후 Core/Stage를 재로드하지 않고 활성 Stage 루트와 Actor 연결만 선택한다. Build Settings 일반 목록과 설치의 여섯 빌드 경로가 다르므로 기존 IntegrationBuildPipeline의 명시적 목록/전후 설정 검사를 사용한다. 씬 경로/StageId를 새 원본의 본편 장 구분만으로 재작성하지 않는다.

## GUID·월드 복원·물리

현재 TearFragment는 AddSkillFragments(1)의 실제 추가량이1일 때 동일 객체를 비활성 소비한다. 최대5에서 거절되면 남고, Stage reset은 비활성 자식까지 조회해 Visual/Collider/소비 플래그를 복구한다. 새 객체나 저장 ID는 없다. fragment-retry9/native-world8 GREEN이 동일 객체 복구를 확인했다. LevelTearFragment 등 지정32개 소스/meta는 실제 삭제됐고 삭제 후 native 참조는0이다.

새 checkpoint는 **현재 ActorBody 월드 Position(Vector3)과 Fragments(int0..5) 두 값만** 저장한다. 다른 저장 항목을 추가하지 않는다. 시작/끝 두 CP만 같은 Actor 출처와 활성 Stage를 확인해 캡처한다. 같은 CP 재접촉은 덮어쓰지 않고 중간 해금/추격 시작은 캡처하지 않는다. 비유한 위치/잘못된 조각 수는 거절하고 이전 두 값을 지킨다. 배치 ID/씬 설정은 입력의 출처/중복 판별용 실행 문맥으로만 사용하고 payload에 저장하지 않는다. retry는 Stage를 초기 재준비해 조각의 실제 객체/Collider/소비 플래그도 복구하고 occupancy/새 Actor 참조를 다시 수집한다. 현재 Stage에서 얻은 능력과 해당 안내 완료만 재잠그며 이전 Stage 해금은 유지한다. 기믹 상태 등록자/DTO/범용 저장 계층은 만들지 않는다.

물리 접촉은 attachedRigidbody에서 Facade를 읽는 현재 계약을 확인한다. HMS ActorBody 동거 예외를 유지한다. 소유 SYM 발판은 Root 로직/Body 참조 주입으로 바꿀 수 있으나 현재 RequireComponent/GetComponent와 실제 콜백이 옮겨지는지 함께 확인한다. 동일 Rigidbody에 HMS/SYM 이동자를 둘 다 붙이지 않는다.

로컬 XYZ 종점/정렬 오프셋을 필드 이름만 바꾸면 기존 override를 잃는다. 기존 보존 자산은 변경 전 월드 기준점/GUID/enum 저장값을 캡처하고 native API로 이관한다. 사용자가 삭제한 prefab과 재작성할 새 prefab을 혼동하지 않는다. 자동 전체 빌더로 사용자 씬을 덮어쓰지 않는다.

## 표시와 종료 검증

Dialogue Tick/취소와 Title/사망 수명은 Core runtime이 소유한다. Cancel-only DialogueDriver 등 지정32개 소스/meta는 Phase2 메인에서 삭제했고 삭제 후 gate로 참조0을 확인했다. ViewModel은 observer 예외에도 자기 구독을 해제하고 완전히 표시된 행만 log에 넣는다. memo10/dialogue14 GREEN을 확인했으며 Phase3 Flow의 취소 사전 검사와 disposed Publish guard가 종료 후 Disposed/false를 유지한다. ServiceLocator.IsDisposed는 기존 단조 disposed flag의 읽기 전용 공개값이다. Bootstrapper가 App Cleanup 전에 app scope를 Dispose하고 Core가 먼저 Unity-null이 된 Stop 순서에서 App은 cleaning 또는 자기 scope disposing일 때만 누락 Core의 해제를 건너뛴다. 정상 실행 중 Core 누락·실제 owner 해제 오류는 계속 보고한다. Flow의 사전 취소/Disposed Publish guard와 EnemyActor exactPhase2는 유지한다. `T10-route-stop-console-product-RED-R1.json`은 보존하고 R2 Stop console 새 오류0과 연결한다.

Timeline의 Artwork Track/Clip/FrameBridge와4패널/scale 호환은 단일 Cut catalog/시계로 이관 후 제거한다. 실제 원화/하단 문구·4초/Skip/정지를 확인하고 패키지나 타인 연출 자산을 삭제하지 않는다. 빈 Animator Controller/요청 이벤트가 실제 clip 재생 증거가 되지 않는다. 누락 clip 대체의 입력 복구와 실제 아트/블렌딩 검증은 별도 상태다.

Destroy 요청 직후 같은 프레임에는 Actor가 남을 수 있다. 종료 완료와 저장된 isDirty=false를 확인하고 이전 세션 참조로 다음 판을 관찰하지 않는다. 검증 결과의 중첩 케이스/관찰 목록을 실제 JSON에서 확인한다. 마지막 scalar 완료값만으로 성공을 주장하지 않는다.

최종 Stage1 baseline8/movement10/unlock6/fragments9/enemies8/options5/nativeworld8, OptionsDeath 두경로 각10/next-round11/chase8/POPO priority2·cancel2·completed4/startTitle6은 actual GREEN이다. `IntegratedPlayChecks.cs`의 PositionPrepare·서비스 준비를 사용한 자동 반복이며 전체 수동 동선 QA가 아니다. `PhysicalPlayChecks.cs`의 최종10시나리오17/15/21/19/25/29/32/20/31/29와 `SelectorAndHazardPlayChecks.cs`의 selector54/damage-floor35/shallow-oil26/deep-oil28/force-death32는 최신 실제 PhysX/InputSystem fixture GREEN이다. deep-oil은 OilSurface+fall-plane fixture, force-death는 직접 service setup, hazard retry는 격리 요청이므로 원본 위험 배치·app fade/respawn 전체 수락으로 확대하지 않는다.

최종 presentation Memo10/Dialogue14/Opening15/Ending13 GREEN과 21:14 UTC의 Opening/Ending8 PNG 재촬영을 확인했다. `PresentationPlayChecks.cs`에서 render frame을650ms 기다린 뒤 새 캡처를 확인해 승인 임시 caption 전체 표시를 기록했다. 과거 partial-caption raw 캡처는 보존하며 제품 UI를 바꾼 결과가 아니다. 원화8장·각4컷×4초/16초·임시 문구를 유지하고 최종 스토리 원고/실제 animation clip 재생으로 확대하지 않는다.
## 빌드 중 입력 자산의 자동 사전 로드

첫 새 빌드는 엔진 보고서상 성공했지만 InputSystem_Actions의 preloadedAssets 한 개가 디스크에 추가되어 설정 보존 명령이 실패했다. 현행 Input System의 ProjectWideActionsBuildProvider는 preprocess에서 이 자산을 추가하고 postprocess에서는 제거하는 API를 사용한다. 파일에 제거가 반영되지 않은 이유는 확인하지 못했다. 관찰된 변경과 추정 원인을 구분하며 URP의 별도 줄바꿈 자동 변화와 혼동하지 않는다. 정확한 diff를 보존·분류한 뒤 재빌드에서는 설정 변화가 없었다. 보호 설정을 수동 복원하지 않는다.

TMP 동적 폰트는 기존 `clearDynamicDataOnBuild=1`에 따라 빌드 전처리에서 캐시를 지운다. 설치된 TMP_PreBuildProcessor가 ClearCharacterAndGlyphTablesInternal을 호출하고, 이 함수가 glyph/character/atlas를 비운 뒤 변경을 영속 갱신에 등록한다. CookedKorean SDF의 이전 두 캐시 문자(8230/95)와 텍스처가 비워진 diff는 이 자동 동작이다. 원본 Font 참조·GUID·Dynamic 설정·2048×2048 atlas 설정은 같고 빈 텍스처는 1×1이다. 이를 폰트 원본 손상이나 새 글자 확장으로 잘못 분류하지 않는다. 실제 화면 캡처는 빌드 이전 렌더 결과이며 빌드 UI/UX 수락은 별도다. 근거는 `output/Tech_SYM/verification/system-v11-refactor-2026-10-07/T10-automatic-assets-audit.json`이다.

## 겹친 카메라 구간의 접촉 수명

CameraZoneTrigger는 같은 Actor의 유효 Collider를 집합으로 추적하고 첫 접촉에만 전환을 제출한다. Stay는 Bind 전 접촉/구간 내부 생성 보완이며 매프레임 우선순위를 다시 쓰지 않는다. 마지막 유효 Collider 이탈 후 새 진입을 허용하고 Retry·비활성·파괴는 자기 접촉과 빌린 참조를 해제한다. 같은 Actor/service의 반복 Bind는 이 체류 상태를 지우지 않는다. 실제 원본 겹침의 반복 취소·입력 차단 RED와 수정 후 회귀·두 판 원본 입력 증거는 현재 tracking 검증을 따른다.
