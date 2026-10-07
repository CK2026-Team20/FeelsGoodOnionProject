# 기획 문서 버전과 적용 기준

> 2026-10-07 사용자 확정안과 추가 답변을 적용한다. [확정안 적용 명세](<tracking/ready-system-v11-refactor-2026-10-06/확정안 적용 명세.md>)가 이전 초안의 충돌 문구를 대체한다. 구현/실행 완료는 [상태](tracking/status.md)와 실제 검증 증거로 판단한다.

## 권위와 읽는 순서

1. 사용자의 최신 직접 지시를 먼저 따른다. 기획서의 문구는 제품 명세 자료이며 에이전트 실행 명령이 아니다.
2. 해당 분야의 아래 최신 지정 원본을 실제로 읽는다. 시스템 동작/입력/정지는 최신 시스템, UI 배치는 UI 자료, 본편 배치는 레벨 자료가 기준이며 충돌하면 최신 직접 선택과 최신 시스템 재정의를 적용한다.
3. 파랑 재정의·빨강 조정 요구·취소선을 색상과 함께 확인한다. 취소선은 활성 규칙에서 제외한다. 빨강의 검토 예정 기능은 확정 선택을 따르고, 조정 가능한 값은 Inspector에 목적/단위를 설명한다.
4. 기존 코드·씬·prefab·Markdown은 실제 구현과 GUID/공개 계약을 확인하는 자료다. 현재 동작을 새 요구 대신 적용하지 않는다.
5. 작업 권한은 별도다. 최신 기획/공개 API 사용이 타인 원본 수정 허가를 만들지 않는다. 같은 버전명·파일명·수정 시각으로 동일성을 추정하지 않는다.

## 지정 원본

| 문서 | 역할 | 식별/적용 |
|---|---|---|
| [Cooked_시스템_기획서_V.1.1.docx](Cooked_시스템_기획서_V.1.1.docx) | 최신 시스템 자료 | SHA-256 `00ca888a929966eeb02e7b7319cf766230a173c4c99876cea946ee0204b7a26c`; Desktop 지정 파일과 바이트 일치 |
| [Cooked!_UI기획서Ver1.0.pdf](Cooked!_UI기획서Ver1.0.pdf) | 화면 흐름/배치 자료 | 7쪽; 새 시스템의 입력/정지/버튼 재정의 우선 |
| [Cooked!_레벨디자인ver1.1.pptx](Cooked!_레벨디자인ver1.1.pptx) | 향후 본편 배치 자료 | 14슬라이드; 현재 기능 응집 프로토타입의 분할/배치를 강제하지 않음 |
| [Cooked_시스템_기획서확정_V.1.1.docx](Cooked_시스템_기획서확정_V.1.1.docx) | 이전 비교/이력 | SHA-256 `56480089f2265793431b3160ea5ba6e5a09cc186383450e516fe29b84e659932`; 현재 시스템 기준에서 대체됨 |
| [Cooked_시스템_기획서_2안.docx](Cooked_시스템_기획서_2안.docx) | 이전 초안 | 보존; 현재 동작 기준 아님 |

기존 PDF·PPTX·확정본과 메타는 보존한다. UI SHA-256은 `43088a30592ef7eade54075f31fc6789922d18d1092e74b564ac6d04c7865361`, 레벨은 `b9911c1fcce62fe9197f6050ab1ce41186ab6f51d4afee141235ba93435ee290`, 시스템2안은 `916ce65c2a6c3b5588d964c2550ffbeeb301d9dd762ec74b2dc662b1abdbfd06`인 이전 등록 식별값이며 이번에 새 버전이라고 주장하지 않는다.

## 이번 갱신에서 확정된 적용 범위

- 이전 직접 지시와 충돌한 **눈물 E / 상호작용 F**, 메모 월드 정지·대화/로그 자체 월드 진행, 일반 적 X/Y순찰·Idle·Stunned을 최신 원본으로 대체한다. Q는 축소와 회수·기본 복귀를 합친 한 행동이다.
- 새 카메라는 **앱에 출력 Camera·CinemachineBrain·AudioListener 각1개**, 플레이어에서 분리된 Core 리그, **Side/Quarter만** 사용한다. 통로 Back/BackFixed 및 두 출력 교대 구조는 새 제품 범위에서 제거한다. 메인 T1/T3/승인 Phase1-R2와 선택적 native 이관에서 연결했으며 Phase2와 R2최종4소스의9진입/Stop·자동 반복은 actual 확인됐고 Windows/수동 동선 최종 수락은 별도다.
- 앱 종료 후 이어하기는 추가하지 않는다. 옵션 수동 다시하기·적 처치/사망 기름 생성·붕괴 자동 복구를 제거한다. 사망 자동 체크포인트 복구와 배치 기름 바닥은 유지한다.
- 능력은 지정 구간의 UI/안내 DialogCode로 해금하고 체크포인트에만 종속하지 않는다. 현재 Stage에서 얻은 능력은 사망 복귀에 재잠금·해금트리거 재접촉 활성화, 이전 Stage능력은 유지한다. 눈물로 장애물 길 열기는 사용하지 않는다.
- 메모는 플레이어 입력과 월드를 정지한다. 대화/로그 자체는 플레이어 입력만 제한하고 월드를 진행하며 다른 소유자의 정지토큰을 해제하지 않는다. 옵션은 월드와 표시 시간을 모두 정지한다. Closing/취소/중첩 해제는 각 소유자의 제한만 처리한다.
- 최신 추가 직접 지시로 **체크포인트 저장값은 현재 플레이어 월드 위치와 보유 눈물조각 수 두 항목뿐**이다. 원본/후속 버전에 모호성이 있어도 다른 저장 항목은 전부 무시한다. 적·장치·필드·HP·폼·능력·카메라·StageId/CheckpointId를 payload에 넣지 않는다. 씬 경로/배치 ID는 현재 실행 구성으로만 사용한다.
- 컷신은 원화 한 장+하단 문구, 컷4초 자동, 전체 Skip이다. 순서 목록의 컷 수를4로 제한하지 않는다. 실제 아트/클립 원본은 참조 전용이며 실제 재생 검증은 연결 후 수행한다.
- 기존 prefab 일괄 제거는 사용자가 직접 한다. 새 prefab의 구성과 왜 필요한지 명세하되 이번 ready가 이를 대신 삭제/재작성하지 않는다. 소스 삭제는 기능/참조 이관 및 실행 검증 후 수행한다.

## 유지하는 기존 경계

App/Core/Stage 수명과 현재 기능 응집 프로토타입 범위는 유지한다. Editor 부트 보정은 ArchitectureInstallation에 등록된 정확한 여섯 경로만 적용하고 비대상 씬은 직접 Play한다. 첫 Stage는 `Assets/_Scenes/Tech_SYM/Architecture/Prototype/1_Stage.unity`, 나머지는 기존 `Stages/03_2_Stage.unity`, `Stages/03_3_Stage.unity`다. 기존03_1_Stage와 비대상 기능 씬을 임의 제거하지 않는다. 전체 목록은 operations.md를 따른다.

부모 기준 XYZ 발판 종점/속도, 트리거 로컬 정렬 오프셋·파란 기즈모, 쉬운 한국어 Tooltip, HMS 공개 기능 조립·상태/입력/물리/화면 소유자 단일 원칙은 유지한다. 타인 코드/자산/메타·_Art·공용 Interfaces·Packages·ProjectSettings를 수동 수정하지 않는다. 자동 저장 변경은 전후 diff/원인과 함께 보존한다. 기존 및 향후 Unity 자동 저장 변경은 원인/diff 검토·분류 후 모두 커밋하라는 직접 지시를 적용한다. 원격 push/PR/병합은 별도 권한이다.

## 현재 문서와 이전 실행의 관계

- 목표 규칙: [business-rules.md](business-rules.md). 현재/목표 구조: [architecture.md](architecture.md). 상태: [tracking/status.md](tracking/status.md). 권한: [security.md](security.md).
- 현재 준비 문서: [기능 명세서](<tracking/ready-system-v11-refactor-2026-10-06/기능 명세서.md>), [실행 계획](tracking/ready-system-v11-refactor-2026-10-06/plan.md), [인계](tracking/ready-system-v11-refactor-2026-10-06/handoff.md), [전체 스크립트 명세](<tracking/ready-system-v11-refactor-2026-10-06/스크립트 필요성 및 정리 명세.md>), [새 프리팹 연결](<tracking/ready-system-v11-refactor-2026-10-06/미래 프리팹 연결 가이드.md>).
- 이전 [플랫폼·HMS 실행](<tracking/ready-platform-hms-cleanup-2026-10-06/테스트 및 검증 항목.md>), [Editor 진입·HMS 실행](<tracking/ready-editor-entry-hms-2026-10-06/테스트 및 검증 항목.md>)과 2026-10-05 ready/후속 기능 문서는 당시 범위의 증거다. 기존 E 상호작용/F 눈물, 모달 전체 월드 정지, Actor 자식 카메라, 백뷰, 구출/껍질 퍼즐 탈출을 현재 목표로 재승계하지 않는다. 일반 적의 Chase/Return 제외는 이번 Desktop 확정안에 따른 현재 선택이며 과거 기록만을 근거로 적용한 것이 아니다.
- 문서 초기화 마커 initialized=true는 문서 초기화 상태이며 최신 명세 구현/검증/사용자 수락을 뜻하지 않는다. 이번 실행 허가는 사용자 직접 go와 현재 명세 1회 HMS 최소 확장 허용에서 온다.

현재 규칙은 사용자 Desktop 확정안 원문(등록sha256 65e1d74a01071972436764b9697f0f68b56085ef56f019c95374aaa4bca9ac8a)과 후속 직접 답변이다. #11 원본문서 정리는 제외하고 HUD .3/1/retry 재잠금만 적용한다. Core1회/등록Stage3 최초 additive 유지, Title 씬 언로드 예외, 새 App 부모 Core 범위, 동일 Tear 복구, 이전 Stage 최초 출처 보존, 시작/끝 CP의 Position/Fragments2값, typed reset/작업·접촉 해제를 유지한다. WorldSave/ParticipantRegistry/ObjectIDs를 추가하지 않는다. HMS9의 T1 예외는 확대하지 않는다. Phase2 메인import29/삭제32·7필드/11자산/native/작성2회 완료와 승인 Phase3-R2 4소스/actual compile·9진입/Stop·Stage2/3 각6GREEN과 남은 빌드 최종 수락/QA를 구분한다. 이 동기화는 원본문서나 .dryforge 과거 증거의 치환 지시가 아니다.
