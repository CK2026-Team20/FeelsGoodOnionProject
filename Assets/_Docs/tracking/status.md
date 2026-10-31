# 현재 요구와 검증 상태

## 2026-10-31 디버그 원본 구도 재사용

사용자 최신 지시로 디버그의 Y180 강제 회전·롤 초기화·수직각 제한을 제거했다. 선택된 StageService 구도를 그대로 복사하며 입력 로직과 씬은 변경하지 않았다. StageService가 없는 씬에서는 기존 Inspector 테스트 구도를 사용한다. 실제 프로토타입 저장 자산의 세 구도/거리/시야각 일치 등 Native 검사9건 통과, 컴파일 상태 오류0·경고0. 현재 Stage_quarter의 미저장 상태 유지. 실제 Play 입력 검증은 미실행이다. 아래 날짜별 고정 방향 설명은 당시 이력이다.

## 2026-10-30 본편 구도·타이틀 출력 분리

등록된 01_Stage_Prototype의 StageService 세 구도와 신규 기본값을 대상 +Z 쪽 배치(Y180)로 변경했다. Title 씬에는 별도 Camera/AudioListener를 추가하고, TitleScenePresentation이 게임 출력을 일시 비활성화한 뒤 타이틀 출력을 사용한다. Title 해제 시 게임 활성 상태를 복원하며 게임 카메라 Transform·시야각은 변경하지 않는다. 컴파일 오류0·경고0, 저장 자산을 사용한 PreviewScene 출력/복원 검사21건 통과. 실제 게임 흐름 Play·렌더 검증은 미실행. 현재 Stage_quarter 미저장 씬은 저장하거나 재로드하지 않았다. [명세·검증](camera-output-separation-2026-10-30/테스트%20및%20검증%20항목.md).

## 2026-10-30 직접 테스트 카메라·붕괴 발판 복원

후속 정정: 입력은 HMS 월드 X/Z 그대로 유지하고 카메라 복사본의 수평 방향을 월드 +Z로 통일했다(원본 각도 보존). 메모·오븐 트레이의 기존 F 입력 경로에 월드 안내를 연결하고 빈 메모 화면 참조를 실행 중에만 보완한다. 최신 Native 검사는 카메라17건·상호작용 구성11건 통과이며 실제 Play는 미실행이다. Stage_quarter의 현재 미저장 상태(dirty=true·루트86개)를 유지했다.

직접 테스트에 본편 CinemachineCamera·PositionComposer·Brain과 구도/이동축 설정 코드를 재사용하는 세 시점을 추가했다. F1 패널에서 측면·통로·쿼터뷰를 선택하며 리스폰 후에도 유지한다. 임시 Settings 추적 코드는 대체했고 같은 씬의 기존 화면 출력은 테스트 중에만 비활성화·종료 시 복원한다. 붕괴 발판도 사망/버튼 재생성 시 기존 ResetForRetry를 사용한다. 컴파일 오류0·경고0, 비 Play PreviewScene 카메라 구성/수명 검사14건 통과. 실제 시점 클릭·추적·리스폰 Play 검증은 미실행이며 작업 씬 저장/전환 없이 확인했다. [사용 절차](ready-debug-authoring-2026-10-29/디버깅%20모드%20사용%20방법.md), [검증](ready-debug-authoring-2026-10-29/테스트%20및%20검증%20항목.md).

## 2026-10-30 프리팹 공통 설정 반영

피해 묶음의 빈 루트 컴포넌트 제거, Pot/Burner 구체 타입과 묶음 내부 Gratan 피해 연결, StageTriggerPoint 기본 체크포인트, LinearPlatforms 기본속도0.6을 원본에 반영했다. 묶음의 빈 Collider/Rigidbody 작성 슬롯과 씬 참조는 그대로 두었다. 새 인스턴스 구조·참조 및 보호 파일 검사 통과. 프로토타입 기존 override는 유지했고 저장본은 바이트 동일하다. 실제 Play 검증은 수행하지 않았다. [명세·검증](prefab-default-sync-2026-10-30/테스트%20및%20검증%20항목.md).

## 2026-10-29 직접 씬 테스트·적/피해 작성 개선 — 구현·빌드 검증

최소 현재 씬 진입, F1 로그/조작 패널, Body XYZ 앵커 이동, Inspector 피해량과 구체 플랫폼 타입, 빈 Title 월드/구도, 컷신 옵션 복귀, 플레이어 Inspector 원본 동기화를 구현했다. 직접 Play25항목·적 API12항목, 실제 오프닝 ESC 복귀9항목×2회, Playing ESC 복귀7항목, 서비스 취소8시나리오 및 직접 테스트 종료 정리를 통과했다. Windows 비개발 빌드 성공(오류0·경고52), Player 테스트 전용 타입 제외 확인.

새 게임 네이티브 충돌은 Core/Stage 준비 후 Title 해제로 순서를 변경한 뒤 위 경로에서 재발하지 않았다. Unity6000.6.0f1·GPU Resident Drawer 원본 설정을 유지한다. 보호 파일1,218개와 Prototype 변환을 보존했다. 실제 엔딩 화면·실패 주입·전체 자연 플레이 QA는 남는다. 기능별 로컬 커밋만 수행하며 push/PR은 하지 않는다. [검증](ready-debug-authoring-2026-10-29/테스트%20및%20검증%20항목.md), [사용 방법](ready-debug-authoring-2026-10-29/디버깅%20모드%20사용%20방법.md).


## 2026-10-29 시스템 V1.8 문서 교체

현행 시스템 원본을 V1.8로 교체하고 V1.6 DOCX·메타를 제거했다. V1.8 활성 본문이 이전 사용자 기능 명세보다 우선한다. 공통·지역 지침 갱신은 게임 구현 완료를 의미하지 않는다. [구조 차이 목록](system-v18-2026-10-29/구조%20차이%20목록.md)의 컷신 복귀·타이틀3D·카메라 경계/전환·추격 시작·추격 이동/포획 5건은 미반영이며 이미지/하단 텍스트 분리는 이미 일치한다. [검증](system-v18-2026-10-29/테스트%20및%20검증%20항목.md)은 문서·정적 검사에 한정한다. 아래 날짜별 과거 상태와 검증 결과는 당시 기록이다.

2026-10-29 재발 후 재적용: HEAD=a3acf10의 저장 씬에서 앞선 적 보정 override 부재를 확인하고 다시 적용·저장했다. 직접 진입/Retry/Title→NewGame→Playing 재검증 성공, 새 오류0. Play 종료 후 새 미저장 씬 상태는 보호하고 강제 재열기하지 않았다. [재검증 상세](prototype-entry-fix-2026-10-29/테스트%20및%20검증%20항목.md).

## 2026-10-29 프로토타입 진입 복구

Stationary3개(비활성 포함)의 씬 인스턴스 물리 회전을 외형 자식으로 이관하고 Enemys/Patrol을 3cm 올렸다. 원본 적 prefab/스케일/코드는 보존했다. 실제 Stage 직접 진입, Retry, Title 복귀, NewGame→오프닝 Skip→Playing 및 플레이 옵션 열기/닫기를 확인했다. 새 Console 오류0. 아래 Icons 작업 때 기록한 진입 차단은 이번 실행에서 해소됐으며 HUD 전 상태·전체 플레이 검증을 뜻하지 않는다. [검증](prototype-entry-fix-2026-10-29/테스트%20및%20검증%20항목.md).

## 2026-10-29 Icons UI 적용·기존 크기 보존

옵션 Frame/Bar_bg/Bar/Button 4종을 GlobalUiRoot의 10개 Image에 연결하고 RGB 7개를 흰색으로 변경했다. 기존 HP/Q/E6종 참조는 유지했다. Sprite/Color 외 전체 프리팹 필드 동일, RectTransform39개·Transform1개 및 원본 Icons24파일 해시를 보존했다. 실제 타이틀 옵션에서 값9회·합성 드래그3개·닫기/재열기와 화면을 확인했다. 최댓값 손잡이와 100 글자 일부 겹침은 크기·배치 보존 조건에 따라 남겼다.

새 게임은 Stationary 물리 루트 회전/배율과 Patrol 안전높이 오류로 Recovering→Title 복귀하여 플레이 HUD/옵션은 미검증이다. 과거 통합 성공을 현재 통과로 승계하지 않는다. 원래 Stage isDirty=false로 복귀했으며 Git 작업은 없다. [명세](ui-icons-2026-10-29/기능%20명세서.md), [리뷰](ui-icons-2026-10-29/코드%20리뷰.md), [검증](ui-icons-2026-10-29/테스트%20및%20검증%20항목.md).

## 2026-10-18 리소스 참조 복구·실제 씬 왕복

Stage6구간의 끊어진 음악 GUID와 CinematicCatalog 원화8참조를 현재 제공 자산에 연결했다. 활성 Patrol의 지형 아래 순찰 끝점은 Stage 인스턴스 위치 override로 보정했다. 실제 Bootstrap→Title→Opening→Playing, 옵션 Title 복귀 후 재진입, Retry, Ending 자동 완료 후 재진입을 검증했다. 통합55개와 메모/음량10개 검사 통과다. ESC/F 합성 입력과 실제 UI 이벤트/API 준비를 사용했으며 전체 레벨 자연 동선이나 빌드 완료를 뜻하지 않는다. 이전 UI 작업의 음악/컷신 준비 차단과 Patrol 오류는 이번 실행에서 해소됐다.

[변경 검토](flow-resources-2026-10-18/코드%20리뷰.md) · [실행 결과와 한계](flow-resources-2026-10-18/테스트%20및%20검증%20항목.md).

## 2026-10-18 UI Ver2.1 레이아웃·아이콘·폰트

승인된 UI 프리팹6개와 Bootstrapper/Title/Core 배치에 새 레이아웃을 적용했다. 제공 HP/Q/E Sprite6개를 연결하고 프리팹35개/씬35개 TMP 텍스트를 IM_Hyemin-Bold SDF로 통일했다. 일반 플레이 설정 버튼은 제거하고 ESC 옵션 및 화면별 커서 숨김/표시를 연결했다. TMP 전역 설정과 보호 원본 import는 수동 변경하지 않았다.

실제 타이틀/옵션, 격리 UI Play의 HP/Q/E·커서·ESC·재바인딩·해제·기록 스크롤 및 렌더를 확인했다. 첫59개 및 최종57개 검사를 통과했으며 테스트 대역/직접 이벤트 준비를 사용한 격리 결과다. 현재 Stage6구간 음악참조 NULL로 새게임 진입이 실패하고 컷신 첫 원화 누락으로 Prepare가 실패한다. 메모는 기존 Placeholder 원화이고 미제공 배경·도구·조각 아이콘은 리소스 대기다. 빌드/Player/전체 동선 QA는 미실행이다.

[변경 명세](ui-v21-2026-10-18/기능%20명세서.md) · [코드 리뷰](ui-v21-2026-10-18/코드%20리뷰.md) · [검증 결과](ui-v21-2026-10-18/테스트%20및%20검증%20항목.md).

## 2026-10-16 Windows 빌드·커밋 인계

사용자의 현재 로컬 상태 빌드·PR 요청에 따라 미저장 Stage를 native 사본으로 보존한 뒤 저장했다. 앞선 외관 단계의 원본 저장 대기는 해소됐다. 전용 Cooked_Prototype_Windows Build Profile에 Bootstrapper/Title/InGameCore/Stage_Prototype4씬을 순서대로 등록하고 바탕화면 Cooked!_Prototype_Build에 Windows64/Mono 실제 빌드를 완료했다. BuildReport Succeeded, 오류0/경고52, 1,129,671ms, 206,450,282bytes다. 실행파일 Player 자체의 동선 QA는 이번 빌드 성공과 구분한다.

HMS 이번 순변경은 CharacterMovement의 선택적 접지 필터 계약과 OilSurface의 명시 기름 Trigger 두 파일이다. 기존 병합된 HMS12파일 이력과 구별하며, 누적 구현/기존 사용자·Editor 편집/이번 빌드 자동 변경의 출처를 정리했다. [변경 내역 및 HMS 검토](prototype-build-pr-2026-10-16/변경%20내역%20및%20HMS%20검토.md) · [빌드 검증](prototype-build-pr-2026-10-16/테스트%20및%20검증%20항목.md).

## 2026-10-16 프로토타입 외관 설정

새 코드 없이 A~C 웜톤/D~F 청보라색 실시간 보조광8개, 독립 Lighting/PostProcessing 루트와 Profile3개를 작성했다. 기존 Core 주광1개 유지, Core에 Linear Fog6~24m·Gradient 환경광, Bootstrap 카메라 인스턴스에 후처리 override를 저장했다. 실제 Play에서 세 시점, Volume 공간 블렌딩, 합성 Space 점프, 실제 발판3초 탑승, Retry와 Title 복귀를 확인했다. 1080p Editor 표본의 중앙값은 A6.91ms/D9.50ms이며 전 구간60fps 보장은 아니다.

Stage는 작업 전의 미저장 Plane/ProBuilder 편집을 유지한 채 새 루트를 추가한 dirty 상태다. 원본 Stage 저장은 사용자 확인 대기이며 최종 native 사본은 output/Tech_SYM/prototype-look-2026-10-16/Stage-authored-preview.unity다. 최종 색감 승인은 사용자 화면 검토로 남긴다. 기존 Patrol 배치 오류와 관측된 NativeArray 수명 경고는 해결 완료로 보지 않는다. 코드·공유 프리팹·아트·파이프라인 설정·Git 작업은 포함하지 않았다.

[외관 명세](prototype-look-2026-10-16/기능%20명세서.md) · [설정 리뷰](prototype-look-2026-10-16/코드%20리뷰.md) · [실행 검증](prototype-look-2026-10-16/테스트%20및%20검증%20항목.md).

## 2026-10-16 구간 통합·실제 플레이 검증

StageService가 구간·스냅샷·소비 이력을 소유하도록 통합했고 01_Stage_Prototype에 시작·끝6쌍/12CP를 연결했다. CameraZone·SafeAnchor·Alignment·카메라 위치 경계/Confiner를 제거했다. 모든 새 시작은 Capsule 중심을 보정하고 같은 시점은 blend만 생략하며 끝은 저장만 한다. Retry는 같은 Core/Stage에서 저장 맥락을 직접 복원한다.

버튼 cap/body 분리, Pot 피해 연결, Linear/Floating 신규 배치 기본형과 빈 무참조 자식을 정리하고65prefab을 감사했다. 속도1.8m/s·점프1/.5m·중력12 및 발판.6m/s, 도마/서랍 간격·센서 범위·Prompt 배율을 조정했다. 반대편 카메라 요청은 철회되어 기존 방향을 유지하고 HMS 입력 코드는 수정하지 않았다.

실제 Keyboard 입력으로12CP와F끝까지 도달했으며 사망·자동Retry를 포함한다. 변경부 저장 후 회귀, 정상/Small 평지 및 대표 경사 Linear13.646초 연속 운반/반전을 확인했다. 통합 검증은55항목과Memo/Options 보완5항목 통과. 실제Title NewGame/Opening, 전환 중 사망·취소·최신요청, Small 보정·Retry, POPO Completed→Ending 자연Completed→Title→새회차를 확인했다. 최초 검증기 실패를 보존하고 원인/재실행을 구분했다. 제품/자산 변경 없이 해당 범위 독립 검토를 통과했다.

현재 미완료는 사용자 배치 회전 전 백페이스 비노출, 실제 메모 본문 콘텐츠, 해당 E 동선의 적 기절 성공, Windows 빌드/Player·사람의 체감 평가다. 최종 저장판 무사망 단일 완주로 주장하지 않는다. 원래 미저장 StationaryPlatforms=(8,0,0)·dirty=true를 보존했고 Play를 종료했다. 보호789개는 최종 실행 전후 추가 변경0이며 작업 시작 대비 TimeManager 줄바꿈1개는 별도 이력이다. Git 작업은 없다.

[기능 명세](stage-flow-unification-2026-10-15/기능%20명세서.md) · [코드 리뷰](stage-flow-unification-2026-10-15/코드%20리뷰.md) · [테스트 및 검증](stage-flow-unification-2026-10-15/테스트%20및%20검증%20항목.md).

2026-10-16 최종 문서 대조 중 Stage 디스크의 추가 변경을 발견했다. 00:01 native 미저장 복사본과 비교하면 FloorPlatforms (11)의 루트 Y 회전0→180도만 다르며, 이전 미저장 StationaryPlatforms=(8,0,0)도 현재 디스크에 포함돼 있다. 이번 문서 작업은 씬을 저장하거나 회전시키지 않았고 변경 주체를 단정하지 않는다. 추가 배치를 보존하며 회전 이후의 충돌·화면 상태는 앞선 Play PASS로 확대하지 않는다. 근거: output/Tech_SYM/stage-flow-unification-2026-10-15/t7/stage-concurrent-diff.txt. 다른 동결 제품6개와 보호789개는 T6 이후 동일하다.

2026-10-16 최종 독립 리뷰 완료: approved, blocking issues0. 새 리뷰어가 명세 적합성·책임과 수명·실행 원자료와 주장 일치·현행 문서를 대조했다. 보고서는 output/Tech_SYM/stage-flow-unification-2026-10-15/t7/최종-독립-리뷰.md다. 사용량 제한 알림 전에 보고서 저장이 완료되었고 재개 응답으로 최종 판정임을 확인했다. 위 배치 변경과 미검증 항목은 비차단 한계로 명시되었으며 전체 QA 승인으로 확대하지 않는다.

최종 문서 import 후 재조회에서 Play=true, sceneCount=3, active=03_InGameCore가 관측됐다. 이번 최종 문서 단계에서는 Play 시작 명령을 실행하지 않았다. 앞서의 stopped/dirty=true 반환 기록은 당시 상태이며 현재 상태로 단정하지 않는다. 새 실행은 중지·원복하지 않았으며 이번 검증 결과에 추가하지 않았다.

## 아래 기록은 이전 시점의 검토·실행 이력

아래의 당시 최신/미해결/미실행 표현은 해당 기록의 시점이다. 현재 Zone·경계·Trigger 음악의 계약이나 Play 금지로 승계하지 않는다.

## 2026-10-15 레벨 작성·플레이 가능성 후속 검수

최신 저장본은 Side Zone2개가 추가됐으며 Small 크기 역전도 해소됐다. 실제 Play에서 버튼 cap/body 동반 하강, 경계에 의한 카메라 거리10→6.91m 보정, CP Cut 전환, SafeAnchor의 조건부 미사용, 현재 소형 플레이어와 점프/속도 비율을 확인했다. 주기·부유·추격·통로/Quarter Zone·순찰형은 현재 미활용이다. Z 회전/Quarter Z 이동 자체는 실제 지원됨을 확인했다. 체크포인트 중심 작성·미리보기·일반 전환 보정 제거 방향을 제안했으며 이번에는 제품 변경 없이 검수했다. [리뷰](playability-audit-2026-10-15/코드%20리뷰.md), [작성 구조 제안](playability-audit-2026-10-15/기능%20명세서.md), [실제 검증 범위](playability-audit-2026-10-15/테스트%20및%20검증%20항목.md).

## 2026-10-15 실제 Play 흐름·시점·선형 발판 수정

최신 직접 지시로 Play 금지를 철회했다. ActorBody 비활성 진입 대기, 체크포인트 접촉의 카메라 요청 누락, 발판 상승 시 물리 스텝이 다른 접점 판정을 수정했다. Playing, Quarter/Side 접촉 전환, 같은 Stage 재시도, 발판400스텝/점프, 메모/옵션, Title/새 게임/오프닝 자동 완료/POPO/Ending/재진입을 실제 확인했다. 최종 오류·경고0. 원래 Stage(isDirty=false) 복귀. 자연 키보드 전체 동선/빌드는 미검증이다. [리뷰](play-flow-verification-2026-10-15/코드%20리뷰.md), [검증과 한계](play-flow-verification-2026-10-15/테스트%20및%20검증%20항목.md). 아래 비 Play 기록은 당시 상태다.

## 2026-10-15 사용자 수동 변경 후 씬 흐름 재점검

현재 저장본은 플레이어 prefab 비활성·Stage 소속/Trigger/시작 음악·Memo 연결이 유효하며 Stage 중복 Player/Camera가 없다. 앞선 누락 기록은 현재 결함으로 승계하지 않는다. 과거 Title 복구의 직접 원인은 Actor prefab 활성 상태 거절이었다. 현재4씬 Missing Script/깨진 참조0, Stage/진입/대화/연출 계약 검사 통과. CameraZoneTrigger 부재 및 Small 충돌체 크기 역전은 남아 있다. [상세 리뷰](flow-diagnosis-2026-10-15/코드%20리뷰.md), [검증](flow-diagnosis-2026-10-15/테스트%20및%20검증%20항목.md). Play/Player는 미실행이며 현재 전체 실행 성공을 뜻하지 않는다.

## 2026-10-15 현재 사용자 배치 씬의 Core 누락 검수

현재 89루트 배치에서 StageManager(StageService)를 추가하고 기존 StageTriggerPoint를 시작 참조로 연결했다. 기존 7,047 객체/컴포넌트 직렬화 변화0. 저장본 Missing Script/깨진 참조0. 기존 트리거6개의 소속·Trigger 미설정과 시작 음악 누락으로 통합 진입은 미해결이다. 기존 Player/Camera 중복 위험과 후속 미작성 흐름을 명세했다. [코드 리뷰](prototype-core-audit-2026-10-15/코드%20리뷰.md), [검증](prototype-core-audit-2026-10-15/테스트%20및%20검증%20항목.md). 기존 객체는 수정하지 않았고 Play는 미실행. 말미 새 dirty 상태는 추가 저장하지 않았다. 아래 이전 목업 배치 검증을 현재 배치의 보증으로 사용하지 않는다.

## 2026-10-15 목업 기반 새 프로토타입 Stage

`Architecture/01_Stage_Prototype.unity`에 기존 프리팹 인스턴스 136개를 배치했다. 첫 CapsuleCollider 중심은 (0,0,0)이며 +X 측면 진행 → 깊이 통로·쿼터 구간 → Z=15 측면 추격·포포 순서다. 체크포인트 7개, 카메라 전환 5개(세 시점), 적 4개, 조각 7개, 메모·오븐 트레이·서랍·압력 연동·주기/흔들림/왕복/붕괴 발판과 원경을 연결했다. 조각 소유 상한 5는 기존 규칙대로 유지한다. 설치·Entry·BootstrapEntryRoute가 새 씬 하나를 참조하고 구 1_Stage 및 비어 있는 Prototype 폴더를 백업 후 삭제했다.

저장 후 Preview Scene의 실제 참조·물리 계약 40검사와 추가 배치 15검사와 식기 착지 5검사가 통과했다. 체크포인트 7곳의 높이2m/반경0.5m Capsule은 비 Trigger 지형 침투0, 지지면 여유 약0.02m다. Bootstrap/Title/Core/새 Stage 저장본의 Missing Script·깨진 참조0, 네 진입 경로와 명시적 빌드 목록을 확인했다. Unity 컴파일 실패 없음과 Console 오류0을 확인했다. 기존 외부 DLL importer/obsolete API 경고는 이번 배치에서 수정하지 않았다. 보호 원본1016파일의 SHA-256 변화0을 확인했다.

Play/Player/빌드/Git·자연 입력/물리·카메라 전환·실제 Title 언로드·전체 동선 QA는 미실행이다. 작업 전 미저장 씬은 output의 사본으로 보존했다. [명세](prototype-layout-2026-10-15/기능%20명세서.md), [코드 리뷰](prototype-layout-2026-10-15/코드%20리뷰.md), [검증 및 사람 확인](prototype-layout-2026-10-15/테스트%20및%20검증%20항목.md)를 따른다.

## 2026-10-14 메시 플랫폼 접촉·피해 영역

승인 계획에 따라 명시 Collider 목록·플레이어 아래 반구/최대 접지각·메시 주기 침투·피해/Oil Trigger 분리를 구현했다. 플랫폼19개 이관 대상 중16파일을 직접 수정하고26개의 실제 형상 충돌 Mesh를 추가했다. 기존 사용자 선택·빈 연결·메모 Box를 보존했다. 아래 과거 Box 기반 사이드뷰 전용 규칙은 이관된 메시 플랫폼에 적용하지 않는다. 이전 빈 연결 권고는 사용자 의도에 따라 결함에서 제외했으며 Memo는 앞선 후속 작업에서 이미 3D Box로 수정됐다.

Unity 컴파일 성공, 비 Play 기능/기하63개 통과·충돌 무시 전환1개 미평가,65프리팹/5씬 조회의 깨진 참조0. _Art660파일·기존 prefab meta65개·기존Transform/활성 선택·씬 파일 보존. 현재 미저장 씬을 저장하지 않았다. Editor 중 ProBuilder 설정 변화는 수동 편집 없이 관측되어 보존했다. Play/Player/빌드/Git은 미실행.

[명세](mesh-platforms-2026-10-14/기능%20명세서.md), [코드 리뷰](mesh-platforms-2026-10-14/코드%20리뷰.md), [검증·사람 확인](mesh-platforms-2026-10-14/테스트%20및%20검증%20항목.md)를 따른다.

## 2026-10-14 종류별 프리팹·플레이어 리소스 연결

최신 직접 지시로 `1_Stage`의 조립·이관을 취소하고 이번 프리팹화 이전 파일·GUID로 정확히 복구했다. 신규 Stage 프리팹31개와 참조 없는 이전 ShellRoot1개를 삭제했다. 기존 테스트 씬 참조를 보존하기 위해 블록아웃11개는 원래 소유 자산 경로로 되돌렸으며 향후 새 레벨에 재사용하지 않는다. `_Prefabs`의 Prototype/ProtoType 폴더는0개다. 기존 사용자28프리팹을 보존했고 Bootstrapper·Title·Core12기능 루트를 종류별 프리팹에 연결했다.

Core PlayerRoot에 지정 원본 분리 클립10개·실제 폼별 리그·이동/점프/등장 VFX와 사용자 Onion_Shell을 연결했다. Runtime 복제 클립과 피격 머테리얼 색 변경은 제거했다. 앞으로 활용 가능한 사용자 프리팹·같은 종류의 실제 계층을 먼저 사용하고 맞지 않는 기능은 독립 프리팹으로 구성한다. 공유 오브젝트 목록과 Dish 등 원형 메시+사각 지지 Collider의 사이드뷰 전용 조건을 작성 기준으로 반영했다. 실제 Stage는 향후 레벨 목업으로 다시 구성한다.

실제 Unity 컴파일 오류0, 비 Play Player Animator/파티클·수명150검사 통과, 등록4씬·65프리팹의 missing script/깨진 참조0, 원본 위치·scale494개·참조4,130개·설정457개 보존, 보호222파일 동일을 확인했다. TMP 폰트와 BootstrapCamera URP 자동 변경은 기록해 보존했다. 사용자 프리팹의 필수 작성 참조5개·Enemy VFX 연결2개·Memo2D/3D 경계는 수정 권고로 보고했다. Play/Player·물리 gameplay·빌드·전체 QA·Git 작업은 미실행이다.

[최종 기능 명세](prefab-authoring-2026-10-14/기능%20명세서.md), [기존 프리팹 수정 권고와 코드 리뷰](prefab-authoring-2026-10-14/코드%20리뷰.md), [실행 결과·사람 확인](prefab-authoring-2026-10-14/테스트%20및%20검증%20항목.md)을 따른다.

## 2026-10-14 체크포인트·씬 종료·음악 통합

10월13일 사용자 검토에서 확정한 범위로 CheckpointMarker와 네 Stage의 P_START/P_END 전용 등장·초기 카메라 오브젝트를 제거하고 체크포인트 StageTrigger 하나로 시작·저장을 작성하도록 이관했다. 저장·복귀는 트리거 위치에 플레이어 CapsuleCollider 월드 중심을 맞춘다. 세 카메라 구간은 같은 빈 Root/Sensor/Alignment 형제 구조다. 경계는 필요 목적을 설명하는 선택 참조다.

옵션/엔딩/복구의 Title 복귀는 세션 해제→Stage 전체→Core 언로드이며 retry는 같은 씬을 유지한다. 설치·Entry·Editor 통합 진입·빌드의 Stage는 Prototype/1_Stage 하나다. 구간 음악은 StageTrigger가 실제 Clip으로 요청하고 같은 Clip은 시간·게인·fade를 유지하며 다른 Clip만 교차 전환한다. 오프닝 후 첫 구간 선택을 보존했다.

실제 Unity 컴파일 오류0, 실제 DLL 비 Play 검사223개, Editor 씬 캐시 검사9개를 통과했다. 네 Stage와 Bootstrap/Title/Core 재읽기의 Missing Script/깨진 참조0, 기존 월드 Transform423개·CP 감지 볼륨·기존 프리팹/meta70파일을 확인했다. Play/Player/빌드·자연 접촉·실제 화면/음원/런타임 언로드는 미실행이다. 기존 staged75항목 상태와 사용자 미저장 씬 작업을 보존했고 이번 Git 작업은 없다.

[기능 명세](stage-trigger-unification-2026-10-13/기능%20명세서.md), [코드 리뷰](stage-trigger-unification-2026-10-13/코드%20리뷰.md), [검증 및 사람 확인](stage-trigger-unification-2026-10-13/테스트%20및%20검증%20항목.md)을 따른다. 아래 기록은 당시 범위와 검증이며 타이틀 이후 씬 유지·현재 Actor 위치 저장·Stage3개 선로딩은 이번 직접 지시로 바뀌었다.

## 2026-10-13 런타임 카메라·스테이지 추격

최신 직접 요청에 따라 LevelCameraService의 세 구도 수치를 런타임에 변경 감지하여 반영하고, StageTrigger가 같은 스테이지 ChaseSwarm을 연결하도록 변경했다. Core의 필수 Driver 참조와 무리 배치를 제거했다. Prototype/1_Stage와 03_3_Stage의 기존 추격 트리거 아래에 기존 프리팹 인스턴스를 연결하고 기존 Core의 물리 override를 유지했다.

실제 Unity 컴파일 오류0, 비 Play 카메라 검사115개, 저장 후 네 씬 Missing Script/깨진 참조0을 확인했다. 원래 Transform ID 480개와 3D 배치428개·UI ID52개, 관련 없는 컴포넌트 설정585개를 확인했다. 실제 화면 프레임·일시정지·전환·Actor 교체·포획과 전체 게임은 Play/Player 금지에 따라 미실행이다. 원본 프리팹·meta는 변경하지 않았으며 이번 Git 작업은 없다.

[기능 명세](runtime-camera-stage-chase-2026-10-13/기능%20명세서.md), [코드 리뷰](runtime-camera-stage-chase-2026-10-13/코드%20리뷰.md), [검증 및 사람 확인](runtime-camera-stage-chase-2026-10-13/테스트%20및%20검증%20항목.md)을 따른다. 아래 2026-10-12 결과는 당시 증거다.

## 2026-10-12 V1.6 구현·소유 자산 이관

현행 원본은 [V1.6](https://github.com/CK2026-Team20/FeelsGoodOnionProject/blob/a3acf102ffcdd9fd593b47aac8f0e85860e1689f/Assets/_Docs/Cooked_%EC%8B%9C%EC%8A%A4%ED%85%9C_%EA%B8%B0%ED%9A%8D%EC%84%9C_V.1.6.docx)이며 최신 직접 답변이 우선한다. [실행 명세](ready-system-v16-2026-10-11/spec.md)에 따라 SYM 104개 전수 검수, 앱/한 판 종료 통합, UI·오디오 수명 정리, 고정 Root/내부 앵커/이동 Body, 세 시점 및 두 DOMove 통로 차단을 구현했다. Bootstrapper와 SessionShutdownOperation을 통합 삭제하여 최종 SYM 소스는102개다. HudIconGraphic은 실제 자원 연결 전 예외 유지한다.

HMS 누적12파일의 변경57개를 소비 근거로 재검토해 불필요 분리 카메라 확장·getter 등을 제거했다. PR 기준 PlayerCameraController는 원본과 같으며 필요한 피해·사망·회수·애니메이션·스킬 연결은 유지했다. 보호 HMS 기준 씬과 _Art·Packages·ProjectSettings의 수동 수정 권한을 확대하지 않았다.

| 결과 | 현재 판정 |
|---|---|
| 소스·Unity 컴파일 | 전체 Runtime150/Editor12 exit0, 실제 Editor compile failure 없음 |
| 소유 자산 | 12씬·61프리팹 직렬화 오류0/Missing0, 기존 물리·렌더1,030개 보존, 붕괴 연결7개 통과 |
| 카메라·통로 비 Play | 실제 DOMove/물리 검사85 assertions 통과, 전체 판정 PARTIAL. 다음 실제 프레임 입력 복귀·public Kill 비활성 전이 미평가 |
| 실제 게임·리소스 | Play/Player/새 빌드 미실행. 실제 애니메이션·Sprite·최종 구도·전체 한 판/재시도는 사람 확인 |
| Git/PR | 최종 반영 대상은 feature/core/systems와 기존 초안 PR #26이다. 원격 반영 결과는 해당 PR의 최신 head를 확인한다 |

[코드 리뷰](ready-system-v16-2026-10-11/코드%20리뷰.md), [전수 판정](ready-system-v16-2026-10-11/전수%20검수%20대상.md), [구조 및 연결 안내](ready-system-v16-2026-10-11/구조%20및%20연결%20가이드.md), [HMS 변경 근거](ready-system-v16-2026-10-11/HMS%20변경%20검토.md), [카메라 검토](ready-system-v16-2026-10-11/카메라%20검토.md), [실행 검사·사람 확인](ready-system-v16-2026-10-11/테스트%20및%20검증%20항목.md)을 따른다.

최신 미해결/협의는 [findings](findings.md)에 있다. 이전 상태 전체는 [변경 전 보관본](archive/2026-10-12-before-v16-status.md)에 보존했다. 과거 Play·빌드 성공은 이번 소스의 실행 성공이 아니다.

최종 소스·자산·문서 독립 검토에서 차단 사항은 없었고 과거 검증 링크 권고는 최신 경로로 수정했다. 구조 이관 커밋은 855781a이며 실제 검증과 사람 확인의 경계는 위 문서를 따른다.


