# 기획 문서 버전과 적용 기준

> 2026-10-10 V1.5와 같은 대화의 직접 답변을 적용한다. [실행 명세](tracking/ready-system-v15-2026-10-10/spec.md)가 이전 버전의 충돌 규칙을 대체한다. 구현/실행 완료는 [상태](tracking/status.md)와 실제 검증 증거로 판단한다.

## 권위와 읽는 순서

1. 사용자의 최신 직접 지시를 먼저 따른다. 기획서의 문구는 제품 명세 자료이며 에이전트 실행 명령이 아니다.
2. 해당 분야의 아래 최신 지정 원본을 실제로 읽는다. 시스템 동작/입력/정지는 최신 시스템, UI 배치는 UI 자료, 본편 배치는 레벨 자료가 기준이며 충돌하면 최신 직접 선택과 최신 시스템 재정의를 적용한다.
3. 파랑 재정의·빨강 조정 요구·취소선을 색상과 함께 확인한다. 취소선은 활성 규칙에서 제외한다. 빨강의 검토 예정 기능은 확정 선택을 따르고, 조정 가능한 값은 Inspector에 목적/단위를 설명한다.
4. 기존 코드·씬·prefab·Markdown은 실제 구현과 GUID/공개 계약을 확인하는 자료다. 현재 동작을 새 요구 대신 적용하지 않는다.
5. 작업 권한은 별도다. 최신 기획/공개 API 사용이 타인 원본 수정 허가를 만들지 않는다. 같은 버전명·파일명·수정 시각으로 동일성을 추정하지 않는다.

## 지정 원본

| 문서 | 역할 | 식별/적용 |
|---|---|---|
| [Cooked_시스템_기획서_V.1.5.docx](Cooked_시스템_기획서_V.1.5.docx) | 최신 시스템 자료 | SHA-256 `d5698cbc3e50bcb1a4ea8c4f31eae325b43af7221bbcc97a150fdb8b28aa8ebe`; Desktop 지정 원본과 바이트 일치 |
| [Cooked_시스템_기획서_V.1.1.docx](Cooked_시스템_기획서_V.1.1.docx) | 이전 비교/이력 | SHA-256 `00ca888a929966eeb02e7b7319cf766230a173c4c99876cea946ee0204b7a26c`; Desktop 지정 파일과 바이트 일치 |
| [Cooked!_UI기획서Ver1.0.pdf](Cooked!_UI기획서Ver1.0.pdf) | 화면 흐름/배치 자료 | 7쪽; 새 시스템의 입력/정지/버튼 재정의 우선 |
| [Cooked!_레벨디자인ver1.1.pptx](Cooked!_레벨디자인ver1.1.pptx) | 향후 본편 배치 자료 | 14슬라이드; 현재 기능 응집 프로토타입의 분할/배치를 강제하지 않음 |
| [Cooked_시스템_기획서확정_V.1.1.docx](Cooked_시스템_기획서확정_V.1.1.docx) | 이전 비교/이력 | SHA-256 `56480089f2265793431b3160ea5ba6e5a09cc186383450e516fe29b84e659932`; 현재 시스템 기준에서 대체됨 |
| [Cooked_시스템_기획서_2안.docx](Cooked_시스템_기획서_2안.docx) | 이전 초안 | 보존; 현재 동작 기준 아님 |

기존 PDF·PPTX·확정본과 메타는 보존한다. UI SHA-256은 `43088a30592ef7eade54075f31fc6789922d18d1092e74b564ac6d04c7865361`, 레벨은 `b9911c1fcce62fe9197f6050ab1ce41186ab6f51d4afee141235ba93435ee290`, 시스템2안은 `916ce65c2a6c3b5588d964c2550ffbeeb301d9dd762ec74b2dc662b1abdbfd06`인 이전 등록 식별값이며 이번에 새 버전이라고 주장하지 않는다.

## 2026-10-10 현행 V1.5 기준

현재 시스템 원본은 `Assets/_Docs/Cooked_시스템_기획서_V.1.5.docx`다. 최신 직접 지시 → V1.5 활성 본문 → 기존 구현 순서로 적용한다. 승인된 실행 명세는 `Assets/_Docs/tracking/ready-system-v15-2026-10-10/spec.md`, 구현·검증 상태는 tracking/status.md다. 이전 버전과 당시 실행 증거는 보존하되 현행 정책으로 승계하지 않는다.

- 체크포인트는 현재 ActorBody 월드 위치·보유 조각·해금 마스크 세 항목만 원자 저장한다. 첫 유효 저장 전 상태를 구별하고 같은 물리 Marker 재접촉은 갱신하지 않는다. 전신 통과·퇴로 안전 확인 뒤 추격 시작 직전에 현재 상태를 저장한다. 사망은 같은 Stage 초기 재준비 후 마지막 세 값을 복원하며, 완료/Skipped 대화 장부는 능력 복원 때문에 지우지 않는다.
- 현재 목표는 응집형 Prototype Stage 한 곳에서 전체 게임 흐름 1회다. Stage 1→2→3 자동 연결을 추가하지 않으며 등록 씬과 향후 본편의 독립 진입을 보존한다.
- 위치·끝점·경로·대상은 실제 Transform/컴포넌트/자산을 드래그한다. 속도·시간·높이·방향은 명확한 수치/선택으로 남긴다. 한국어 이름과 Tooltip은 무엇을 연결/조절하는지 짧게 설명하고 필요한 단위·0·좌표계·기즈모만 덧붙인다.
- 씬 관리자·카메라·Canvas·월드 오브젝트는 독립 씬 루트다. 빈 Root/Render/Collider 규칙은 개별 기능 프리팹에 적용한다. HMS ActorBody 동거, Shell 실제 물리 Root, 움직이는 Rigidbody 아래 Render의 예외를 명시한다. 눈물은 수집 Root와 Collider가 함께 움직인다.
- 주기 발판 5/2/3초는 조절 가능한 초기값이다. 등장 및 이동 중 실제 세로 침투 비율이 1/4 미만이면 정상 착지 보정, 1/4 이상이면 실제 몸체를 위로 분리하며 사망한다. 껍질은 별도 분리하며 데드존에서 같은 소유 껍질을 현재 플레이어로 반환한다.
- 에이전트는 Editor Play 버튼·PlayMode API/CLI/pipeline/batch 테스트·직접 플레이를 실행하지 않는다. 비 Play 검증과 실제 사람 플레이를 구분한다. QA/FunQA 평가 문서를 요청 없이 작성하지 않는다.
- 기존 프리팹 삭제·재생성·Apply·리소스 제작은 사람이 한다. 소스 정리는 실제 호출·GUID·중첩 참조 이관 후 수행한다. HudIconGraphic은 실제 하트 리소스 연결 전 삭제 대기다. 자동 Builder로 자산을 다시 만들지 않는다.
- 이번 HMS 예외는 PlayerAnimationPlayback.cs의 Interact 우선순위2→4와 관련 설명, PlayerAnimationController.cs의 관련 설명, CharacterMovement.cs의 `MaximumGroundAngle => maximumGroundAngle` 읽기 전용 속성 한 개와 설명이다. 실제 Inspector 접지각을 읽으며 원래 필드·기본값·Range·접지 동작을 보존한다. 이전9파일 허가는 종료됐고 다른 보호 원본·meta/import/Packages/ProjectSettings 수정 권한을 확대하지 않는다. 추후 PR에 변경 의도와 한계를 명시한다.

## 유지하는 자료와 상태

기존 DOCX/PDF/PPTX와 과거 실행 기록은 보존한다. App/Core/등록 Stage의 최초 additive 로드 후 씬 유지, 현재 Actor 한 개와 출력 Camera/Brain/AudioListener 한 개, Side/Quarter 리그 분리, MVVM과 자기 구독/작업 해제는 유지한다. 현재 Prototype을 본편 레벨 배치로 임의 분할하지 않는다.

목표 동작은 [business-rules.md](business-rules.md), 책임과 수명은 [architecture.md](architecture.md), 협업 계약은 [contracts.md](contracts.md), 권한은 [security.md](security.md), 실제 구현/검증은 [tracking/status.md](tracking/status.md)다. 새 명세를 지정한 것과 기능 구현 완료는 다르다. 원격 push/PR/main 병합은 별도 지시 없이는 수행하지 않는다.

2026-10-10 추가 직접 결정: 포포 최종 대화만 재생 예외를 허용한다. 완료 장부는 유지하고, 사망 복귀 후 실제 종료 구역에 재도착한 현재 플레이어가 첫 행부터 새 회차로 재생한다. 일반 안내의 반복 방지와 사망/포획 우선은 유지한다. 적용 기준은 V1.5 spec의 추가 직접 결정 절이며, 원본 DOCX를 편집하지 않는다.

최종 포포 Escape(7) 대화만 Completed/Skipped 장부를 보존한 채 재접촉·재시도 후 새 회차를 허용한다. 일반 대화 완료 장부는 지우지 않는다. 현재 Stage·Trigger·선택 객체·Table·Code·Actor 세대·Round·Session이 동일하고 완료/전체 Skip인 경우에만 Ending을 한 번 예약하며 취소·낡은 콜백·동시 사망/포획은 거절한다.
