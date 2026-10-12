# 활성 씬 Play 및 HMS 기능 조립 인계

2026-10-06. 기존 프로젝트의 이번 변경 범위를 인계한다. ready 단계에서 제품 코드는 변경하지 않았다. 현재 명세와 계획을 사용자가 승인한 후 같은 대화의 go 단계에서 구현한다.

## 문서 역할과 충돌 처리

| 문서 | 역할 | 위치 |
|---|---|---|
| 이 인계 | 실행 경계·문서 역할·수명 위험·증거 위치 | `.dryforge/handoff.md` |
| 기능 명세 | 구현할 사용자 동작과 완료 계약 | `.dryforge/spec.md` |
| 실행 계획 | 작업 대상·의존 순서·검증 방법 | `.dryforge/plan.md` |
| 프로젝트 문서 원본 | 위 세 문서의 같은 내용과 프로젝트에 보존할 관련 기록 | `Assets/_Docs/tracking/ready-editor-entry-hms-2026-10-06/` |
| 코드 리뷰 | 현재 실제 소스·저장 자산 감사와 구현 후 리뷰 | 같은 폴더 `코드 리뷰.md` |
| 검증 항목 | 현재 조사 판정·구현 후 기능 케이스·증거 | 같은 폴더 `테스트 및 검증 항목.md` |
| 수정 가이드 | 프로그래머가 진입 경로/Stage를 직접 바꾸는 안내 | 같은 폴더 `프로그래머 수정 가이드.md` |

프로젝트 원본의 `기능 명세서.md`, `plan.md`, `handoff.md`와 `.dryforge` 세 파일은 같은 승인 버전의 복사본이다. 실행 시 차이가 발견되면 임의로 병합하지 말고 승인 버전을 확인해 동기화한다. 기능 명세가 동작을 결정하고 계획은 방법과 순서만 결정한다. 계획을 바꿀 수 있지만 명세의 동작·범위는 사용자 승인 없이 바꾸지 않는다. 최신 사용자의 직접 지시가 모든 문서에 우선한다. 지정 기획/기존 코드와의 충돌은 기획 우선순위 및 보호 파일 권한을 함께 확인한다.

## 실행 형태

현재 체크아웃과 실제 기존 씬을 우선 사용한다. T1은 Editor 진입 설정을, T2/T3는 HMS Actor/카메라 조립과 세션 수명을, T4는 독립 소유 배치를, T5는 지침/프로그래머 안내를, T6/T7은 실제 검증과 인계를 담당한다. 현재 인원이나 병렬 작업 수는 고정하지 않는다. 실제 Editor를 사용하는 자산 저장·씬 교체·Play 작업은 한 흐름으로 직렬화한다.

새 검증 씬을 만들어 기존 씬의 요구를 대신하지 않는다. 통합 Stage의 플레이어는 Core/SessionActorHost에서 생성하므로 각 Stage에 중복 플레이어를 설치하지 않는다. 현재 Prototype과 기존 `Stages/03_1_Stage`를 혼동하지 않고 후자는 독립 Play와 필요한 조립만 보완한다. 전체 레벨/자산 Builder 재실행은 승인 범위에 대한 좁은 수정으로 대체한다.

## 반드시 지킬 실행 조건

1. **승인 전 제품 수정 금지:** ready 결과는 이번 범위의 검토 가능한 문서다. 코드·씬·프리팹 조립 실행은 명세 승인 후다.
2. **원본과 소유 조립 분리:** Tech_HMS/다른 작업자 코드·씬·프리팹·metadata, 공유 코드/PlayerCharacter, _Art, Packages/ProjectSettings 원본은 읽기 전용이다. SYM 소유 배치에 컴포넌트·공개 API·참조·instance override를 조립할 수 있으며 보호 원본에 Apply하지 않는다. 문서 MD 및 필요한 대응 metadata는 승인된 문서 범위다.
3. **기존 작업 보존:** 이미 많은 사용자/다른 작업자의 변경이 있다. 기존 diff를 현재 작업 결과로 보고하거나 reset/restore하지 않는다. 이 작업이 만드는 변경만 구분한다. Git stage/commit/push/PR/merge는 별도 승인 사항이며 ready/go 호출 자체로 포괄 허가되지 않는다.
4. **진입 정책:** 정확한 여섯 통합 경로만 Bootstrap을 경유한다. Stage2/3는 선택한 Stage로, 나머지는 활성 씬에서 직접 Play한다. 시작 override와 일회성 route를 별도로 소유하고 abort/Stop/reload에서 정리한다. 남아 있는 Bootstrap baseline도 비대상 직접 Play를 막을 수 없어야 한다.
5. **첫 활성화와 retry 계약:** HMS CameraController의 유효 참조는 첫 Awake 전에 준비한다. ModeController의 Start 준비와 TrySetMode 성공을 확인한 후 초기 화면/입력을 표시한다. retry 때 mode/카메라 target/UI를 함께 새 Actor에 연결하고 자기 구독·차단만 해제한다.
6. **Cinemachine 및 단일 상태 소유자:** Brain이 실제 출력 블렌드를 수행한다. HMS 모드·Facade 상태·VisualController 책임을 SYM이 중복 소유하지 않는다. SYM은 trigger 요청·평면 정렬·input lease를 조정한다. 타이머 종료만으로 블렌드 중 입력을 열지 않는다.
7. **실행 증거:** Unity API 실행은 outer success와 inner compile/execute result를 둘 다 확인한다. scene dirty가 있으면 자동 저장·폐기 없이 보존한다. 저장 자산·Missing Script·직렬화·입력·실제 블렌드·retry·pause를 따로 확인한다. 구조 검사를 게임 기능/빌드 PASS로 기록하지 않는다.

## 구현 전에 알아야 할 실제 위험

`BootstrapPlayModeHook`이 저장한 PreviousStart가 Bootstrap인 상태를 실제로 관찰했다. 비대상에서 이전 값을 단순 복원하면 독립 Play가 막힐 수 있다. 경로 매핑 자체는 Stage2/3를 올바르게 반환하므로 무조건 Stage1 매핑으로 바꾸는 수정은 문제를 해결하지 않는다.

HMS ModeController는 Start 전에 TrySetMode를 거절하고 CameraController는 첫 Awake에서만 필수 참조를 검증한다. 비활성 내부 참조 조립과 ActorReady 시점을 함께 수정해야 한다. CoreUiBinding의 현재 카메라 필수 검사 또한 새 Actor 카메라 수명과 맞춰야 한다. 보호 private 필드의 런타임 reflection이나 보호 HMS 파일 수정은 실행 방법으로 채택하지 않는다.

CameraController는 ModeChanged 때 priority를, ModeController는 모드 적용 때 이동축을 즉시 바꾼다. 전환 동안의 요청 상태와 완료 후 권위 모드를 구분하는 소비 어댑터가 필요하다. 이 부분의 순서·실제 Brain 완료·옵션 정지·중단 검증을 계획의 T3/T6에서 빠뜨리지 않는다.

기준 씬의 수치와 최신 기획 수치에 대한 선택 질문은 별도 답변 완료로 기록하지 않았다. 승인 명세에는 최신 기획의 5m/s·약 0.6초를 조정 가능한 기본값으로 기록했다. 승인 전에 응답/수치 변경 지시가 도착하면 동기화한다. 이것은 조립 방식이나 카메라 메커니즘의 미결정이 아니다.

기존 Windows 빌드는 엔진이 보호 ProjectSettings를 정규화한 이력이 있다. 핵심 요구는 Editor Play 변경이다. 빌드가 보호 설정을 변경할 가능성을 실행 가능한 방법으로 통제할 수 없다면 쓰기 권한을 확대하지 않고 별도 미실행 항목으로 인계한다. 빌드·실행 파일 Play·QA 재미 평가는 각각 실제 실행 여부를 기록한다.

## 참고 원본과 조사 증거

- `Assets/_Docs/planning-index.md`와 지정 `Cooked_시스템_기획서확정_V.1.1.docx`: 최신 색상 포함 규칙과 조정 가능 기본값.
- `Assets/_Docs/standards.md`, `security.md`, `contracts.md`, `architecture.md`, `engineering-notes.md`, `operations.md`: 소유권·물리/세션 수명·검증/빌드·QA 인계.
- `Assets/_Scenes/Tech_HMS/01_PlayerMovementTest.unity`와 공유 PlayerCharacter 원본: HMS 조립/직렬화 기준, 읽기 전용.
- 현재 감사 증거: `output/Tech_SYM/audits/editor-entry-hms-2026-10-05/`의 native/serialized inventory, component matrix, HMS blueprint, 색상 원본 runs, readonly verification 및 baseline hashes.
- 이전 `.dryforge` 파일 보존: `output/Tech_SYM/planning/editor-entry-hms-2026-10-06/backup/`.
- 구현 후 검증 출력 예정: `output/Tech_SYM/verification/editor-entry-hms-2026-10-06/`.

이번 실제 조사에서 16씬/65프리팹 읽기 오류·생략 0, 저장 prefab Missing Script 0, 씬 Missing Script 3을 확인했다. 그중 HMS 기준 씬 PlatformManager는 보호 원본, SYM 두 카메라는 수정 대상이다. 감사 전후 원본 1,999개 해시·scene setup/active/start는 동일했다. 이 수치는 ready 조사 증거이며 제품 동작 수정 후 검증 결과가 아니다.

## 전달할 완료 상태

구현 후 사용자에게 현재 씬 진입 정책, 실제 수정한 소유 조립/카메라, 프로그램 수정 지점, 검증 증거 및 보호 결함/미실행 항목을 전달한다. `status.md`에는 구현/자동 기능 검증/팀 빌드 검증을 구분하고 미해결 인계는 `findings.md`에 기록한다. 이 명세/계획 외에 실행자가 알아야 할 추가 결정은 없다.
