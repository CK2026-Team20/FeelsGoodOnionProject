# V1.6 구조 정리 실행 계획

2026-10-11. 이 문서는 순서와 작업 대상을 정한다. 동작 기준은 같은 폴더 `spec.md`다. 아래 작업은 아직 실행 완료가 아니다.

## 진행 방식

전수 검수로 현재 책임·참조를 확정한 뒤 앱/한 판의 공통 수명을 정리한다. 이후 화면·대화, 지역 기능 객체, 카메라·통로 차단을 구현한다. 이관된 실제 소비를 근거로 HMS 누적 변경을 줄이고 최종 씬 연결·컴파일·비 Play 검증을 마친 뒤 문서·Git·PR을 완료한다.

Unity Editor/씬 자산 쓰기는 메인이 하나의 연결에서 직렬로 수행한다. 여러 작업자가 같은 Editor를 조작하지 않는다. 소스 작업을 위임할 때는 파일 쓰기 범위를 분리하고 공유 Contracts, App/Core 조립, 씬·프리팹은 메인이 각 묶음 종료 시 한 번 연결한다. 중복 변경이 발견되면 작업을 직렬화한다. 편의상 고정 인원·동일 역할을 상시 두지 않는다.

## T1 전수 판정과 이관 목록

- 대응 요구: R1, R2, R3, R8, R9.
- 목표: 104개 전체 소스와 지정 네 씬의 모든 컴포넌트/필드에 누락 없는 책임·참조·처분표를 완성한다. 종속 프리팹·ScriptableObject·독립 씬·Editor/빌드 소비를 포함한다.
- 대상: `Assets/_Code/Core/Tech_SYM/**`, 네 씬과 재귀 프리팹, `전수 검수 대상.md`, `코드 리뷰.md`; HMS12파일은 읽기 비교.
- 작업: 최신 실제 목록으로 갱신, 소스 본문과 필드 소비 확인, 유지/통합/삭제/예외 판정, 통합 후 단일 소유자와 호출 이관 경로 명시. 변경 전 GUID·pose·물리·dirty·자동 변경 기준점을 확보한다. 파일 이름만 보고 대량 삭제하지 않는다.
- 검증: 모든 대상 행에 실제 판정과 근거가 있고 미판정0. HudIconGraphic만 자원 연결 전 명시 예외. ‘빌드 도구/실제 진입 도구’를 테스트로 오분류하지 않는다. 기획 변경이나 추가 권한이 필요한 발견은 구체적인 사실로 별도 표시한다.
- 공유 쓰기: 아직 제품 코드/자산을 삭제하지 않는다. 이후 작업은 이 표의 이관 대상을 소비한다.

## T2 앱과 한 판의 고수준 조정 통합

- 대응 요구: R2, R4, R9.
- 목표: 앱 시작/조립/해제의 중복을 통합하고 GameFlow·Scene·Core·Session·Actor의 서로 다른 수명은 유지한다.
- 대상: `Architecture/Foundation/**`, `Architecture/Integration/Runtime/{AppComposition,CoreComposition,GameRuntimeService,GameSessionRuntime,CoreWorldBinding,LoadedStageRegistry,SessionShutdownOperation,SceneComponentLookup}.cs`, `Architecture/Session/**`, 관련 Contracts.
- 작업: T1의 실제 통합 대상 구현, 불필요 전달과 Inspector 참조 제거, 생성/구독/부분 실패/종료 owner 명시. 초기 서비스 연결 후 Boot, Actor 초기화 및 view 준비, 씬 유지, Stage reset, CP3필드, 최종 대화 세대/회차 검증 보존.
- 검증: 준비 이전 시작 없음, 중복 Dispose가 다른 소유자의 자원을 해제하지 않음, 실패와 취소 구별, 언로드 Stage 재검색 없음, 동일 Actor/세션당 입력 한 경로. 실제 제품 공개 계약에 대한 비 Play 검사.
- 공유 쓰기: Contracts와 공통 조립은 이 작업의 단일 담당이 먼저 확정한다. 후속 기능은 필요한 추가 연결을 메인에게 제출한다.

## T3 대화·화면·오디오·연출 및 Editor 소스 정리

- 대응 요구: R1, R2, R4, R7, R9.
- 목표: 고수준 화면/진행 책임에 불필요한 전달층을 흡수하고 MVVM·자기 구독·잔여 시간 계약을 유지한다. 이 작업 범위의 모든 소스를 판정대로 처리한다.
- 대상: `Architecture/{UI,Dialogue,Audio,Cinematics}/**`, `Interaction/**`, 관련 Integration Editor/Authoring 코드 및 T1에서 연결된 소비처.
- 작업: 같은 상태/시간을 중복 보유하지 않게 통합, 미사용 필드/메서드·실제 테스트/더미 제거. HudIconGraphic은 예외 유지와 제거 조건 명시. Options·Auto·Log·메모·컷신의 각 시간과 입력 소유를 유지한다.
- 검증: View 입력 한 번→명령 한 번, 재Bind 중복 없음, Disable/Dispose 후 호출 없음, VM→View 참조 없음, 옵션 뒤 잔여 시간, 일반 대화 장부·최종 예외, 기존 공개 API/빌드 진입 보존. 실제 아트 재생 미검증을 구별한다.
- 공유 쓰기: App/Core 공통 조립과 네 씬 저장은 메인이 처리한다. T2의 공통 타입을 다시 대체하지 않는다.

## T4 적·장치·발판의 기능 객체 내부 기준점

- 대응 요구: R1, R2, R3, R9.
- 목표: 고정 기능 Root·고정 목적 앵커·이동 Body 관계를 구현하고 외부 월드 앵커 의존을 제거한다.
- 대상: `Enemies/**`, `OvenTray/**`, `Platforms/**`, `Features/**`, 관련 지역 참조 검증과 Gizmo. 자산 이관 명세는 T7이 소비한다.
- 작업: EnemyActor/EnemyMotor 이동 writer와 초기/기절 복구를 실제 Body에 맞춰 정리. 장치/발판 목표는 같은 기능 Root 안의 Body 밖에서 연결. 정지형 적의 불필요 순찰 참조, KinematicPlatformMotion/PlatformContacts의 중복 역할 여부를 근거로 처리. 16개 확인 목표점 및 추가 발견 참조를 이관 목록에 포함.
- 검증: 목적점은 Body 이동을 따라가지 않음, 현재 물리 지지/접촉 및 reset 계약 보존, 임의 Root 회전/축 동작 확장 없음, 제거 참조0. 실제 자연 물리/플레이는 미실행으로 인계.
- 공유 쓰기: 씬/프리팹 직접 저장은 T7 단일 담당. 공통 SceneComponentLookup 변경은 메인이 직렬 반영한다.

## T5 세 시점 카메라와 DOMove 통로 차단

- 대응 요구: R2, R5, R6, R7, R9.
- 목표: 카메라 구도와 이동 허용축을 분리하고 기본→통로→쿼터 전환, 두 DOMove와 옵션/취소 수명을 구현한다.
- 대상: `Architecture/Level/{LevelCameraService,CameraZoneTrigger,StageService,CheckpointMarker}.cs`, 관련 Inspector 편집 코드, 카메라/차단 소비 계약. 기존 추격의 ChaseRetreatGate 의미를 통로 문과 혼동하지 않는다.
- 작업: Side1/Corridor2/Quarter3 이관, 화면 추적·고정 회전·실제 이동 범위, 초기 구도와 CP 복원 Cut. 현재 요청 종료 전 교체 금지와 완료 후 유효 접촉 재평가. 전환 트리거 내부 안전/차단 앵커, 플레이어 DOMove 완료 후 차단물 DOMove, 목표·경로 Gizmo. 실제 Rigidbody에 대해 단일 writer 계약을 적용한다.
- 검증: 두 대상이 각각 한 번 이동, 중복 Collider/Stay 접촉·옵션 pause/resume·사망/Actor 교체/Title/Stage reset·낡은 OnComplete 거절, 자기 tween만 취소. 안전하지 않은 목표는 시작 전 거절. 전환 중 세계 시간 진행과 Esc 가능, 타 토큰 유지. RealToon 코드는 추가하지 않는다.
- 공유 쓰기: HMS/공통 조립·씬 직접 쓰기는 하지 않는다. 필요 연결·제거는 메인에게 전달한다. 클래스별 필수 참조는 필수로 검증하되 독립 Stage 구성을 깨지 않는다.

## T6 HMS 누적 변경 최소화

- 대응 요구: R8, R9, R10.
- 목표: 새 SYM 소비가 확정된 상태에서 보호 HMS12파일의 순변경을 기능별 최소화한다.
- 대상: spec R8의 정확한 12파일과 해당 변경을 소비하는 SYM 소스. 비교는 실행 시 갱신한 PR base와 최신 작업 branch를 사용한다.
- 작업: 각 hunk의 최초 의도·현재 소비·유지/제거 이유를 기록. 카메라 구도 선택을 SYM이 담당하면서 필요 없어진 분리 rig 확장 등은 실제 독립/통합 소비를 먼저 이관하고 제거한다. 피해·회수·사망·애니메이션 결과·접지각·스킬 조건에 필요한 변경은 보존한다. 원본 파일 통째 원복으로 해결하지 않는다.
- 검증: 목록 밖 보호 변경 없음, API 호출 잔존 없음, 독립 PlayerMovementTest 자산 수정 없음, 접지 필드/Range/동작 보존, 함수/상태 owner 중복 없음. 12파일 최종 순변경표를 PR 참고로 제공한다.
- 의존 이유: T2~T5에서 실제 소비가 바뀐 뒤에만 불필요 HMS 변경을 판단할 수 있다.

## T7 실제 씬·프리팹 참조 이관과 구조 안내

- 대응 요구: R1, R3, R4, R5, R6, R8, R9, R10.
- 목표: 제품 코드를 실제 네 씬과 모든 종속 소비에 연결하고 사람이 재구성할 직관적인 씬 상태를 남긴다.
- 대상: 네 우선 씬, 관련 `Assets/_Scenes/Tech_SYM` 소유 프리팹/설정/독립 씬의 필요한 참조, Inspector/Gizmo와 `구조 및 연결 가이드.md`.
- 작업: 연결된 Edit Mode에서 현재 dirty/원래 scene setup을 먼저 확인. 기능 Root 안에 앵커와 Body 배치, 외부 기준점 제거/이관, 세 rig와 경계·통로 차단 연결, 삭제 컴포넌트/필드 참조를 실제 저장 형식에서 이관. 기존 프리팹 삭제·재생성·Apply·Unpack 금지. 보호 원본과 기존 meta GUID를 보존한다.
- 검증: 실제 씬+중첩 prefab 확장 검사에서 Missing Script/삭제 GUID 참조0, 조립 필수 참조 유효, 외부 고아 앵커0, pose/물리 전후 비교, 출력1·현재 Actor1 구성, 네 씬별 모든 컴포넌트 판정. 단순 YAML 검사만으로 native 성공을 기록하지 않는다.
- 공유 쓰기: 메인이 단독으로 씬 저장과 공통 조립 연결을 담당한다. 기존 미저장 변경이 있으면 해당 자산을 덮지 않고 보존 상태를 사용자와 해결한다.

## T8 최종 비 Play 검증과 문서 일치

- 대응 요구: R1~R10.
- 목표: 실제 구현·직렬화·컴파일·기능 경계 결과와 문서를 일치시키고 남은 사람 확인을 분리한다.
- 대상: 최종 전체 diff, `코드 리뷰.md`, `테스트 및 검증 항목.md`, `전수 검수 대상.md`, `구조 및 연결 가이드.md`, `planning-index/status/findings`와 영향받는 상위/지역 지침.
- 작업: Unity 상태/pipeline/실제 조회로 현재 연결 확인 후 실제 Runtime/Editor 컴파일, 공개 입력·상태/수명·DOMove/정지/취소의 적합한 비 Play 검사, 전체 연결·보호 범위 검증, 독립 코드 리뷰. 검증 스크립트는 output 아래에서만 사용하고 일회성 제품 소스로 남기지 않는다. 이전 검증 메뉴/로그를 현재 성공으로 쓰지 않는다.
- 검증: 모든 필수 자동 항목 통과 또는 구체적인 환경 제한/미평가 보고, 미해결 기능 오류를 성공으로 기록하지 않음. 코드 컴파일만으로 물리·화면·전체 플레이 통과를 선언하지 않음. QA/FunQA 미작성, Play/Player 실행0. 실제 사람에게 필요한 재현 순서와 기대 결과 명시.

## T9 한국어 커밋·push와 PR 재작성

- 대응 요구: R10.
- 목표: 실제 완료한 순변경을 최종 `feature/core/systems`에 올리고 기존 Draft #26을 읽기 쉽게 갱신한다.
- 대상: Git index/branch/remote, PR #26, 최종 문서. `yeet` 스킬을 실행 시 다시 적용한다.
- 작업: gh 인증/상태·원격 PR base/head와 템플릿 재확인. 시작 미커밋·자동 저장 포함 전체 diff를 기능별 분류하고 한국어 제목/본문으로 커밋. 생성물/Library/Temp/output 제외 유지. push 후 원격 SHA 확인. PR 제목·본문은 실명 경로/API를 제외하고 한국어로 쓰며 진행/이유/변경/구조/참고/확인을 포함한다. 본문은 실제 개행 파일을 `--body-file`로 전달한다.
- 검증: 기존 초안 상태 유지, 중복 PR 없음, 공개 이력 재작성/force-push/main 병합 없음, 새 커밋 설명 한국어, 최종 PR 내용과 순변경 일치, 과거 Play 결과와 이번 결과 구분, created/updated PR을 현재 채팅에 연결. worktree를 새로 만들었다면 최종 반영/push 및 보존 확인 후에만 정리한다.

## 요구 대응

| 요구 | 담당 작업 |
|---|---|
| R1 | T1, T3, T4, T7, T8 |
| R2 | T1, T2, T3, T4, T5, T8 |
| R3 | T1, T4, T7, T8 |
| R4 | T2, T3, T7, T8 |
| R5 | T5, T7, T8 |
| R6 | T5, T7, T8 |
| R7 | T3, T5, T8 |
| R8 | T1, T6, T7, T8 |
| R9 | T1~T8 |
| R10 | T6, T7, T8, T9 |

## 실행 의존 관계

T1의 이관표를 공통 통합 T2가 소비하고 이후 기능 구현이 해당 계약을 소비한다. T3/T4/T5는 소스 쓰기 집합을 분리할 수 있으나 공통 변경과 Editor 쓰기는 메인이 직렬 반영한다. T6은 새 소비자 상태를, T7은 최종 소스/API를 소비한다. 검증 이후에만 원격 반영한다. 자동 자산 생성 단계는 없으므로 재생성 명령을 추가하지 않는다.

```yaml
tasks:
  - id: T1
    depends: []
    risk: NONE
  - id: T2
    depends: [T1]
    risk: RISKY
  - id: T3
    depends: [T2]
    risk: RISKY
  - id: T4
    depends: [T2]
    risk: RISKY
  - id: T5
    depends: [T2]
    risk: RISKY
  - id: T6
    depends: [T3, T4, T5]
    risk: RISKY
  - id: T7
    depends: [T6]
    risk: RISKY
  - id: T8
    depends: [T7]
    risk: RISKY
  - id: T9
    depends: [T8]
    risk: NONE
regen_barriers: []
```
