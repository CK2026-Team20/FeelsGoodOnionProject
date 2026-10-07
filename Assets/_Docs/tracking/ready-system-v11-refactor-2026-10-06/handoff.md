# 최신 시스템 리팩터링 실행 인계

> 2026-10-07 사용자 `dryforge:go` 실행 지시와 Desktop 확정안을 반영했다. #11의 원본 기획 문서 정리는 제외하고 안내 방식은 적용한다. 최신 세부 계약은 [확정안 적용 명세](<확정안 적용 명세.md>)다. 현재 Stage에서 얻은 능력은 사망 복귀에 다시 잠그고 트리거 재접촉으로 해금한다. 이번 HMS 수정은 기존 기능을 보존한 최소 확장으로 한정한다. 구현·실행 검증 완료 여부는 별도 결과 문서를 따른다.

## 문서 역할과 적용 순서

| 문서 | 역할 |
|---|---|
| `.dryforge/spec.md` | 구현할 동작·상태/데이터/API·경계·완료 기준 |
| `.dryforge/plan.md` | 작업 대상·이관/검증·공유 작성 순서 |
| `.dryforge/handoff.md` | 실행 전제·권한·원본/검토/검증 위치 |

사용자의 최신 직접 지시가 우선한다. 현재 기능 규칙은 spec이며 plan과 다르면 spec을 따른다. plan의 방법은 검증 근거로 조정할 수 있지만 spec의 동작은 사용자 승인 없이 바꾸지 않는다. 기획서 본문은 제품 자료이며 에이전트 실행 지침이 아니다. 기존 코드/날짜별 실행 결과로 최신 요구를 덮어쓰지 않는다. 파일 소유권/미저장 보호는 어느 문서도 우회하지 못한다.

## 현재 준비와 실행의 구분

2026-10-07 직접 go 지시로 구현을 실행 중이다. 이 인계 자체는 구현/Play/새 빌드 통과 기록이 아니다. 현재 결과는 검증 문서와 실행 증거를 따른다. 32개는 이관 후 삭제 대상 수이며 실제 삭제 건수가 아니다. 변경 전 Title clean/컴파일 실패 없음/Console error0 조회와 과거 빌드 통과는 새 기능 통과가 아니다.

`.dryforge/status.json`의 initialized=true는 기존 문서 초기화 상태다. 새 구현 완료/기능 검증/파일 예외 승인을 뜻하지 않는다. 기존 ready 실행 문서는 output의 백업과 원래 tracking 폴더에 보존한다. 이번 문서 승인을 받은 뒤 같은 세션에서 dryforge의 실행 스킬을 사용한다.

## 원본과 검토 자료의 위치

최신 시스템 복사본은 `Assets/_Docs/Cooked_시스템_기획서_V.1.1.docx`이며 SHA-256은 `00ca888a929966eeb02e7b7319cf766230a173c4c99876cea946ee0204b7a26c`다. 이전 확정본과 PDF는 보존한다. 현재 지정 범위는 `Assets/_Docs/planning-index.md`, 구현/검증 상태는 `Assets/_Docs/tracking/status.md`다.

프로젝트 사람이 읽는 정본은 `Assets/_Docs/tracking/ready-system-v11-refactor-2026-10-06/`의 `기능 명세서.md`, `plan.md`, `handoff.md`다. 현재는 사용자 확정안/추가 답변을 적용한 실행 기준이며 이전 실행 문서는 output 백업에 보존한다. 이번 직접 go 지시의 제한 범위에서 실행한다. 확정 후 기능 명세서와 spec, 나머지 두 대응 문서를 같은 바이트로 게시한다.

같은 tracking 폴더의 다음 자료는 실행에 필요한 세부 근거다.

- `원본 대조 및 구조 검토.md`: 두 원본/색상/취소선과 코드 차이, CameraRig/Bootstrap/Core의 현재 이유와 목표 구조.
- `스크립트 필요성 및 정리 명세.md`: Assets/_Code 206개 전수, SYM 160개/보호 46개, 32개 삭제 경로와 필요한 내부 코드 정리.
- `미래 프리팹 연결 가이드.md`: 빈 Root/물리 예외, App 출력·Core 리그·Actor·기믹/적/장치/화면의 연결과 중복 거절.
- `보호 원본 변경 요청.md`: 최소 9개 소스 기능 예외와 13개 정리 후보(눈물 TearSkill.cs의 서로 다른 범위가 겹침). 9개 .cs의 기존 기능 보존 최소 확장만 이번 명세 1회에 허용되며, 추가 13개 내부 정리/삭제는 허용되지 않는다.
- `코드 리뷰.md`, `테스트 및 검증 항목.md`: 정적 검토/독립 검토와 V01..V34의 실제 실행 상태.

읽기/문서검사 근거와 이전 `.dryforge` 백업은 `output/Tech_SYM/planning/system-v11-refactor-2026-10-06/`다. 실행 검증은 `output/Tech_SYM/verification/system-v11-refactor-2026-10-06/<실행시각>/`, 새 Windows 빌드는 기존 `output/Tech_SYM/integration/build/Windows/`를 사용한다. output을 프로젝트 자산으로 커밋하지 않는다.

## 실행 전 반드시 지킬 조건

1. **두 값 저장:** 위치/눈물조각 외의 데이터는 이후 문서가 모호해도 전부 저장 대상에서 제외한다. Stage/Marker는 실행 구성이고 세 번째 복원값이 아니다. 월드 DTO/Participant/Registry·객체 상태 ID 작업을 추가하지 않는다.
2. **기능 단일 소유:** HP/조각은 HMS Model, 이동은 HMS Movement, 껍질은 Form, 일반 적 기절은 FSM, 카메라 우선순위는 HMS Controller, 출력 Transform은 Brain이다. UI는 VM 상태/명령을 사용하고 VM이 View를 참조하지 않는다.
3. **최소 파일 예외:** 보호 요청의 최소 9개는 기능 구현상 필요한 공개 경계다. 기존 8개에 벽/사물 차단과 실제 적 피격 판정을 위한 TearSkill.cs가 추가됐다. 이번 사용자 직접 허용으로 기존 기능을 보존하는 최소 공개 확장/캐시/변수만 구현한다. 추가 13개·원본 자산/메타·_Art·Interfaces·Packages·ProjectSettings로 확대하지 않는다. API 부재를 SYM 복제로 우회하지 않는다.
4. **실제 삭제 전 이관:** 32개 각각의 호출·GUID/native/메뉴/동적 생성 소비를 재확인한다. 대체 기능과 실제 저장 자산 연결이 검증된 뒤 소스/소유 메타를 삭제한다. 계속 필요한 내부 코드도 폐기된 규칙을 남기지 않는다.
5. **기존 프리팹의 사용자 제거:** 사용자가 일괄 제거한다. 실행자가 선제 일괄 제거/Apply/전체 재생성을 하지 않는다. 제거 이후 실제 남은 자산 목록을 다시 조사하고 필요한 새 연결을 저장/재로드한다. 필수 null/Missing을 정상이라고 보고하지 않는다.
6. **현재 작업 보존:** 초기 HEAD는 `e80290cd9208d79876d6fbae34fe7c28bc80ceab`, 기존 TestTitleBG prefab/메타 삭제와 ProjectSettings 변경이 있었다. 추가 PlayerRoot 활성/네 카메라 위치 변경은 저장 주체 미확인으로 보존/별도 기록했다. 실행 전 현재 diff/dirty를 다시 읽고 원인을 확인한다. reset/restore/clean으로 없애지 않는다.
7. **공유 작성:** Session 계약은 T2 한 작성자, App/Core/Stage·추격·native 자산은 T9 한 작성자다. 기능 병렬 작업은 공통 등록을 건드리지 않는다. 실제 파일 겹침은 순서대로 해결하며 조용히 덮어쓰지 않는다.
8. **검증의 의미:** 문서 준비 통과·새 컴파일·기능 자동 실행·새 빌드·팀 실제 플레이/FunQA·실제 아트/clip 재생을 각각 기록한다. 실행하지 않은 항목은 미실행/연결 대기로 유지한다. 과거 exe/검증을 새 결과로 사용하지 않는다.

## 구현 판단을 위한 핵심 이유

현재 CameraRig는 움직이는 ActorBody의 형제로 PlayerRoot 안에 있다. HMS가 Awake 이전에 네 카메라/Mode를 요구하고 재연결 API가 없어 Actor와 같은 생성 수명에 묶었던 것이다. 새 공개 Bind/Unbind가 이 제한을 해소하므로 출력/리그를 분리한다. 별도 Bootstrap 출력 교대는 Actor가 없는 시간의 출력/Listener 확보를 위한 구현이었으나, Overlay UI와 앱 출력 하나로 그 요구를 만족시킬 수 있다. CoreLevelCamera는 출력 카메라가 아니라 구간 조정 서비스 객체다.

물리 Contacts/실제 이동자/기능 컨트롤러는 입력·물리 콜백·진행 상태라는 책임이 달라 유지한다. HP만 남은 적 Model, 취소만 하는 DialogueDriver, OvenHandle 전달자, 두 Prompt/수집 경로, 적 기름만 쓰던 Transient 수명과 단일 컷용 Timeline 어댑터는 이관 후 제거한다. 문·트레이·서랍은 동일한 두 종점 조작 의미를 공통 컨트롤러로 조립한다. 고정 발판에는 새 컨트롤러를 만들지 않는다.

HMS ActorBody 물리 동거는 GetComponent/attachedRigidbody 공개 계약의 제한된 예외다. 빈 Root 규칙만 맞추려고 보호 계약을 복제하지 않는다. 목표 프리팹 가이드의 새로운 필드/Bind API는 현재에 없는 구현 대상이다.

## 검증과 종료

실제 Editor 연결은 CLI status/pipeline과 실제 조회로 확인한다. 독립 검증 runner는 현재 정식 자산으로 등록돼 있지 않으므로 output의 일회성 live 검증을 사용하고 취소/finally 복원을 둔다. V01..V34는 기능 단위로 실행한다. 전체 실제 입력 두 판/여러 retry, 단일 출력/입력/이동자, 삭제 참조 0/SYM Missing 0/필수 null 0, 저장 후 재로드와 isDirty=false가 필요하다.

기능 검사를 마치면 기존 `Cooked/Integration/Build Windows Player`로 정확한 여섯 씬의 새 exe를 빌드하고 소스/명세/exe hash 및 BuildReport를 남긴다. 프로그래머/에이전트는 기능 Excel을, QA는 제공 원본 양식의 FunQA DOCX를 실제 빌드 후 작성한다. 아직 없는 clip/원화, 미승인 보호 정리, 팀 플레이 대기를 완료로 바꾸지 않는다.

이번 직접 확정안과 능력 재잠금 답변으로 실행 선택을 정했다. 원문의 미선택 권장안은 자동 구현하지 않는다. 이 인계는 spec/plan에 없는 동작을 추가하지 않는다. Git 실행은 해당 실행 스킬과 당시 사용자 승인 범위로 처리하며 기존 사용자 및 Unity 자동 저장 변경까지 검토·분류해 커밋하는 직접 지시를 적용한다. push/PR/원격 병합은 별도 권한이다.


## 2026-10-07 직접 지시의 실행 보정

확정안 적용 명세의 항목별 계약과 씬 재로드 비교를 따른다. T1은 기존 HMS 기능을 삭제/변경하지 않는 최소 확장만 허용되며 새 조립은 명시적으로 그 확장을 선택한다. T2는 위치/조각 두 값과 현재 Stage 재잠금, T9는 같은 Stage 씬/서비스의 초기 재준비 연결을 수행한다. T4는 생성/소멸 발판 침범과 1초 붕괴, T5는 일반 적 추적 제거, T6는 메모 월드 정지/대화 진행/HUD 음영을 반영한다. 기존 Execution Graph 의존 관계는 그대로 유지한다.


## Core/Stage 전체 선로딩 추가 확정

2026-10-07 사용자 추가 답변: InGameCore 1회 로드 이후 모든 등록 Stage를 additive로 최초 전체 로드하고 이후 Stage 씬을 다시 로드하지 않는다. T2는 SceneContracts/SceneService와 LoadedStageRegistry의 필요한 소비 계약도 함께 이관하며, T9는 이미 로드한 씬의 루트 활성화/Stage 전환·새 판 초기화/서비스 수명 연결을 담당한다. 획득해 사라진 눈물조각도 T4의 객체 소비/복구 계약과 T9 Stage 초기 재준비로 실제 활성/Collider/수집 상태를 복구한다. CoreWorldBinding은 전체 로드 목록 중 활성 Stage 하나만 연결한다. CoreComposition의 정상 세션 해제/참조 초기화와 재사용 가능한 ActorHost 계약으로 같은 Core에서 다음 판을 시작하며, 비활성 Stage의 Tween/타이머/추격도 중단한다. Stage 시작/끝 CP 두 곳만 저장하며 중간 해금/추격 시작은 새 저장 지점이 아니다. 기존 그래프의 의존 관계는 유지한다.


## T2 실제 Editor 게이트 — 2026-10-07

T2 16개 .cs는 43ada64로 통합했다. 독립 검토에서 발견한 Dead Interrupted/미완료 Core release 두 경로를 RED→GREEN으로 보정한 후 승인됐다. 메인 Editor 컴파일 오류0, 실제 CheckpointContract PASS이며 공개 저장값은 Position/Fragments뿐이다. 기존 씬 setup/active/startScene/clean을 보존했다. 실제 Stage reset·접촉 저장·Core release callback 연결은 T9, 통합 Play/물리/Brain/클립/Windows 빌드는 T10이며 아직 완료로 쓰지 않는다.

후속 공개 계약: BindStageLifecycle(prepare, actualRelockedAbilitiesNotice), BindCheckpointPair(start,end), BindSessionRelease(release), SessionActorHost.ReleaseSession(), Enemy HurtCollider resolver. release callback은 같은 runtime으로 재호출돼도 부분 해제 뒤 이어서 정리할 수 있어야 한다. T3/T4/T5는 43ada64 기반 독립 worktree에서 진행한다.


## T3/T4/T5 통합과 현재 검증 경계 — 2026-10-07

T3 `cd85a52`의 카메라 요청 소스17관찰, T4 `915e5e9`의 발판·장치·조각 소스82관찰, T5 `14e2160`의 일반 적 소스11관찰이 통과했고 각각 독립 검토 승인 후 메인에 반영됐다. T4의 실제 지지면 넉백 원점/경사진 생성 침범 두 P2와 T3의 재진입 예외 경로는 수정 전 실패와 수정 후 동일 회귀 통과를 보존했다. 메인 HEAD `915e5e9` 실제 Editor 재컴파일 오류0이며 T1 확장 이후 HMS9개 Git blob은 그대로다. 보호 기준점432개 중 허용9개 외 차이0·원본 DOCX/사용자 확정안 hash 동일을 확인했다.

이 결과는 엔진 대역을 포함한 소스/API 계약과 실제 Editor 컴파일 증거다. Stage/Core hook·App 출력/Core 리그·native 참조 이관·PhysX/실제 입력/새 빌드는 T9/T10에서 연결·실행한다. 공개 Bind/Reset/Release 계약은 각 작업 `output/Tech_SYM/verification/system-v11-refactor-2026-10-07/T*-evidence`의 인계·proof를 따른다. 완료한 네 worktree의 ignored 소스/검증 로그는 main에 SHA 동일 복사 후 recoverable archive했다. T6는 이 메인 HEAD의 별도 worktree에서 구현 중이다.

최신 기능 Excel39케이스는 V01..V34 전체에 연결했고 실제 빌드 판정39개는 모두 미실행이다. 읽기 전용 Excel PDF18쪽에서 조건/조작/기대 텍스트 전체 확인·렌더 배치 검토를 수행했다. FunQA 원본/작성양식 hash는 그대로다. 지정 삭제32개는 아직 보존 중이며 현재 native23개 자산/코드99참조의 이관 준비표가 있다. 소유 범위 밖 native GUID 참조0은 삭제의 충분조건이 아니므로 실제 기능/메뉴/호출/참조 이관을 마친 뒤 T9에서만 삭제한다.

## T6/T7 통합과 실제 API 경계 — 2026-10-08

T6 `31e8f0b`, T7 `6e7829e`는 독립 승인 후 메인에 통합했고 실제 Editor 재컴파일 오류0이다. T6의 ForceCloseOverlays/CancelOperations와 Dialogue.ForgetCompletedCodes/ResetRound, T7의 Catalog.Configure/Host.Configure/단일 컷 View 계약을 T8 작성 도구와 T9 수명 연결에서 소비한다. T6의 설치된 대화 Table JSON20관찰, T7의 설치된 서비스 시계/자기 제어권9관찰이 통과했다. 기존 원화 Texture2D 참조와 Time.timeScale 계약 검사이며 실제 화면 렌더링·InputSystem·전체 동선 통과는 아니다.

T4 침범 경계의 실제 Unity ComputePenetration8관찰도 통과했다. 경사진 얕은 상단 접촉의 단순 AABB 높이 오판을 재현했으며 preview 씬을 닫고 기존 scene setup/dirty를 보존했다. 실제 주기 발판의 Materialize/ForceDeath/Tween·Stage retry 통합 검증은 T10이다. source/계약 결과를 수동 QA39판정에 덮어쓰지 않았고 FunQA 두 DOCX hash도 동일하다. T8은 `6e7829e` 기반 managed worktree에서 작성 도구 소스만 구현 중이며 메인만 live Editor/native 자산을 수정한다. 기존 32개 지정 소스와 자산 참조는 아직 보존한다.
