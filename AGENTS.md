# Cooked 프로젝트 작업 지침

Unity 6000.6.0f1 기반의 로컬 플레이 게임이다. 플레이어·스테이지 기믹·추격·대화·연출을 여섯 씬의 통합 흐름으로 구성하며 기존 개별 기능 씬도 보유한다.
현재 서버·네트워크는 필요하지 않다. 기존 구현은 변경 전 사실을 확인하는 자료이며, 최신 기획과 사용자의 직접 변경 지시를 대체하지 않는다.

## 기획 명세의 우선순위

시스템·UI·레벨 작업 전 `Assets/_Docs/planning-index.md`에서 현재 지정 버전과 적용 범위를 확인하고 해당 원본을 읽는다. 우선순위는 **사용자의 최신 직접 지시 → 해당 분야의 최신 지정 기획서 → 기존 구현 설명**이다. 기획서는 지속적으로 갱신되므로 이전 버전과 내용이 같다고 일반화하지 않는다. 확정 시스템 문서의 파란색 재정의와 빨간색 조정 요구는 색상을 포함해 확인한다. 현재 기능 응집형 프로토타입에는 레벨 기획의 본편 배치를 강제하지 않는다. 현재 적용 지시와 진행 상태는 각각 `Assets/_Docs/planning-index.md`와 `Assets/_Docs/tracking/status.md`에서 확인한다. 날짜별 기록은 해당 시점의 증거이며 현재 명세로 자동 승계하지 않는다. 지역 지침의 과거 동작 설명이 새 명세와 다르면 새 명세가 우선하되, 타인 파일 수정 권한은 별도로 지킨다.

## 반드시 지킬 경계

1. 현재 작업자는 Tech_SYM이다. 참조 허용은 타 작업자 원본 읽기, 공개 API·이벤트 사용, 자기 소유 씬·프리팹·오브젝트에 해당 컴포넌트 추가·설정·연결과 배치 override 저장을 포함한다. 다른 Tech_*, art_*, QA_*·공용·_Art 원본 코드·자산·.meta·import 설정의 직접 수정과 원본 prefab Apply·이동·삭제·GUID 변경은 금지하며 명시적 파일 허용만 수정 예외다.
2. 비아트 작업자는 _Art 원본을 수정하지 않는다. 승인된 문서 작업은 루트 지침과 Assets/_Docs의 프로젝트 Markdown 및 대응 메타데이터를 포함한다. 이 예외는 기존 PDF·공용 코드·프리팹·Packages·ProjectSettings 수정 권한이 아니다.
3. 상태 소유자를 하나로 두고 UI는 ViewModel 상태·명령에 연결한다. ViewModel은 View를 직접 참조하지 않으며 자기 구독·작업만 해제한다.
4. Unity 원본·GUID·직렬화 참조를 보호한다. 기존 객체 배치의 물리 계약을 일반 계층 예시만으로 재작성하지 않는다.
5. 실제 실행하지 않은 검증을 통과로 기록하지 않는다. 수정 범위 밖 정리와 승인되지 않은 Git 작업을 수행하지 않는다.

모든 Inspector 직렬화 필드는 간단한 한국어 표시 이름과 Tooltip을 제공한다. 첫 문장은 연결 대상 또는 조절 효과를 설명하며 단위·좌표 기준·0의 의미·기즈모 색은 해당 설정에 필요할 때만 덧붙인다. 위치와 오브젝트는 수기 좌표/키 대신 실물 참조로 작성한다. 상세 기준은 Assets/_Docs/standards.md의 Inspector 설명을 따른다.

HMS·SYM 조립에서는 상태·입력·물리·화면 출력의 소유자를 먼저 확인한다. HMS의 공개 기능으로 완전히 대체되는 SYM 코드는 자산·GUID·호출 참조를 이관한 뒤 삭제한다. 동작과 수명이 다른 기능을 이름만으로 중복 처리하지 않는다. 비교·삭제·검증 절차는 standards.md의 HMS·SYM 조립과 소스 정리를 따른다.

2026-10-06 사용자는 앞선 미커밋 변경과 앞으로 자동 저장으로 추가되는 변경도 검토·분류하여 전부 커밋하도록 지시했다. 자동 변경은 원인과 전후 diff를 기록하고 포함한다. 이 지시는 보호 원본을 수동으로 고칠 권한을 확대하지 않는다. 미저장 씬 보호와 저장소의 Library·Temp·output 제외 규칙은 유지한다.

## 프로젝트 문서와 지역 지침

프로젝트 공통 문서의 경로는 프로젝트 루트 기준 Assets/_Docs/다. 모든 문서 읽기·작성·갱신은 아래 트리의 실제 경로를 사용한다. 루트 AGENTS.md·CLAUDE.md와 모듈별 AGENTS.md는 아래 위치를 유지한다. 기존 PDF는 보존한다.

```text
project-root/
├── AGENTS.md → 공통 진입 지침
├── CLAUDE.md → 같은 내용의 진입 지침
└── Assets/
    ├── _Docs/
    │   ├── planning-index.md → 최신 기획 버전·직접 지시·원본 목록
    │   ├── architecture.md → 시스템 책임과 수명
    │   ├── business-rules.md → 현재 의도된 동작
    │   ├── security.md → 작업자별 권한
    │   ├── standards.md → 개발·검증 규칙
    │   ├── engineering-notes.md → 혼동하기 쉬운 동작
    │   ├── operations.md → 실행·빌드 절차
    │   ├── contracts.md → 협업·데이터 계약
    │   └── tracking/
    │       ├── status.md → 구현과 검증 상태
    │       ├── findings.md → 미해결 확인 사항
    │       ├── archive/ → 과거 상태 원문과 종결 증거
    │       ├── 2026-10-05-guidance-refresh-spec.md → 이전 지침 정비 범위
    │       ├── ready-editor-entry-hms-2026-10-06/ → 앞선 진입·HMS 명세와 실행 이력·수정 가이드
    │       ├── ready-platform-hms-cleanup-2026-10-06/ → 현재 플랫폼·조립·정리 명세와 검증·자산 연결 가이드
    │       ├── 코드 리뷰.md → 문서·원본 교차 검토
    │       ├── 테스트 및 검증 항목.md → 완료 기준과 확인 결과
    │       └── decisions/
    │           ├── index.md → 결정 목록
    │           ├── 0001-folder-ownership.md → 폴더 권한 결정
    │           ├── 0002-current-offline-scope.md → 오프라인 범위·이전 결정
    │           ├── 0003-current-authority.md → 현재 명세·문서 운영 기준
    │           ├── 0004-actor-camera-lifetime.md → 과거 Actor·카메라 동시 수명
    │           ├── 0005-independent-camera-output.md → 앱 출력과 Core 리그 분리
    │           └── 0006-resident-stage-lifetime.md → 씬 유지·초기 재준비·능력 출처
    ├── _Scenes/Tech_SYM/
    │   └── AGENTS.md → 소유 씬·프리팹·설정
    └── _Code/Core/Tech_SYM/
        ├── AGENTS.md → SYM 구현 공통
        ├── Enemies/AGENTS.md → 적
        ├── Features/AGENTS.md → 붕괴·수집 기능
        ├── Platforms/AGENTS.md → 물리 발판
        ├── Interaction/AGENTS.md → 기존 메모·프롬프트
        ├── OvenTray/AGENTS.md → 오븐 트레이
        └── Architecture/
            ├── Contracts/AGENTS.md → 협업 계약
            ├── Foundation/AGENTS.md → 앱 수명과 전환
            ├── Session/AGENTS.md → 세션과 플레이어 연결
            ├── Integration/AGENTS.md → 시스템 조립
            ├── UI/AGENTS.md → 화면과 옵션
            ├── Dialogue/AGENTS.md → 대화
            ├── Cinematics/AGENTS.md → 오프닝·엔딩
            ├── Audio/AGENTS.md → 소리
            ├── Chase/AGENTS.md → 추격
            └── Level/AGENTS.md → 스테이지 규칙
```

다른 작업자 코드·공용 Interfaces·_Art는 실제 구성 경계이지만 수정 권한이 없어 내부 지침을 추가하지 않는다. 해당 영역은 루트의 권한·연동 설명과 실제 공개 계약을 읽기 전용으로 확인한다. 설치 플러그인·생성 코드·테스트 전용 폴더는 별도의 제품 모듈로 취급하지 않는다.

## 에이전트 실행 체제

에이전트 운영은 적용하는 설치된 DryForge 스킬의 Markdown 지침에 따른다. 메인의 직접 수행·위임, 하위 에이전트 수·역할, 병렬화·독립 검토·재시도는 작업 규모·위험도·의존 관계·종료 조건으로 결정하며 인원이나 역할을 상시 고정하지 않는다. 사용자 직접 지시와 파일 소유권·수정 허용 범위·미저장 작업 보호·Git 승인 규칙이 우선한다. 운영 위임은 권한 확대나 Git 작업의 포괄 승인이 아니다.

## 작업 시작 전

기능 변경의 테스트 단위·QA 인계는 `Assets/_Docs/standards.md`의 **기능 테스트와 QA 인계**를 따른다. 컴포넌트 전수가 아니라 요구사항에 대응하는 사용자 기능 전체를 검증한다. 프로그래머·에이전트가 핵심 기능 테스트 케이스 엑셀을 작성하고, QA가 직접 작성하는 평가 문서는 원본 양식을 보존한 Cooked! FunQA DOCX 한 개다. 자동 기능 검증과 사용자/팀의 실제 빌드 검증 완료를 구분하며 Computer Use 직접 플레이를 기본 의무로 두지 않는다.

목표 → 참고할 코드·자산 → 제약 → 완료 기준을 먼저 정한다. Assets/_Docs/standards.md, Assets/_Docs/engineering-notes.md의 관련 절과 대상 모듈의 상위·지역 AGENTS.md를 읽는다. 해당 플랫폼의 루트 진입 문서 하나를 사용하며 동일한 AGENTS.md·CLAUDE.md를 중복해서 읽지 않는다. tracking 전체·필드별 감사표·과거 실행 기록은 필요할 때 관련 항목만 조회한다. 기능 명세는 Assets/_Docs/business-rules.md와 해당 실제 자산을 함께 확인한다. 구버전 README로 현재 동작을 덮어쓰지 않는다.

- 플레이어·재시도 변경: Assets/_Docs/architecture.md의 수명, Assets/_Docs/contracts.md의 플레이어·체크포인트 경계와 SessionActorHost를 먼저 확인한다.
- 씬 진입·빌드 변경: ArchitectureInstallation.asset, BootstrapEntryRoute와 Assets/_Docs/operations.md의 명시적 씬 빌드를 확인한다.
- UI·대화·연출 변경: 옵션 정지와 자기 차단 해제, 중복 입력 방지, 표시 완료·취소 계약을 확인한다.
- 아트·프리팹 연결: Assets/_Docs/security.md의 권한과 대상 GUID·원본 소유자를 먼저 확인한다.

## 문제 보고

타인 폴더 무단 변경, _Art 원본 손상, .meta GUID 단절, 세션 종료 실패로 입력 잠금·객체가 남는 현상은 즉시 사용자에게 보고한다. 다른 미해결 문제는 재현 조건·영향·현재 해결하지 못하는 이유를 Assets/_Docs/tracking/findings.md에 기록한다.

## 2026-10-10 현행 V1.5 기준

현재 시스템 원본은 `Assets/_Docs/Cooked_시스템_기획서_V.1.5.docx`다. 최신 직접 지시 → V1.5 활성 본문 → 기존 구현 순서로 적용한다. 승인된 실행 명세는 `Assets/_Docs/tracking/ready-system-v15-2026-10-10/spec.md`, 구현·검증 상태는 tracking/status.md다. 이전 버전과 당시 실행 증거는 보존하되 현행 정책으로 승계하지 않는다.

- 체크포인트는 현재 ActorBody 월드 위치·보유 조각·해금 마스크 세 항목만 원자 저장한다. 첫 유효 저장 전 상태를 구별하고 같은 물리 Marker 재접촉은 갱신하지 않는다. 추격 트리거 접촉 진입·퇴로 안전 확인 뒤 시작 직전에 현재 상태를 저장한다(2026-10-11 직접 지시: 전신 통과·진행 경로 제거). 사망은 같은 Stage 초기 재준비 후 마지막 세 값을 복원하며, 완료/Skipped 대화 장부는 능력 복원 때문에 지우지 않는다.
- 현재 목표는 응집형 Prototype Stage 한 곳에서 전체 게임 흐름 1회다. Stage 1→2→3 자동 연결을 추가하지 않으며 등록 씬과 향후 본편의 독립 진입을 보존한다.
- 위치·끝점·경로·대상은 실제 Transform/컴포넌트/자산을 드래그한다. 속도·시간·높이·방향은 명확한 수치/선택으로 남긴다. 한국어 이름과 Tooltip은 무엇을 연결/조절하는지 짧게 설명하고 필요한 단위·0·좌표계·기즈모만 덧붙인다.
- 씬 관리자·카메라·Canvas·월드 오브젝트는 독립 씬 루트다. 빈 Root/Render/Collider 규칙은 개별 기능 프리팹에 적용한다. HMS ActorBody 동거, Shell 실제 물리 Root, 움직이는 Rigidbody 아래 Render의 예외를 명시한다. 눈물은 수집 Root와 Collider가 함께 움직인다.
- 주기 발판 5/2/3초는 조절 가능한 초기값이다. 등장 및 이동 중 실제 세로 침투 비율이 1/4 미만이면 정상 착지 보정, 1/4 이상이면 실제 몸체를 위로 분리하며 사망한다. 껍질은 별도 분리하며 데드존에서 같은 소유 껍질을 현재 플레이어로 반환한다.
- 에이전트는 Editor Play 버튼·PlayMode API/CLI/pipeline/batch 테스트·직접 플레이를 실행하지 않는다. 비 Play 검증과 실제 사람 플레이를 구분한다. QA/FunQA 평가 문서를 요청 없이 작성하지 않는다.
- 기존 프리팹 삭제·재생성·Apply·리소스 제작은 사람이 한다. 소스 정리는 실제 호출·GUID·중첩 참조 이관 후 수행한다. HudIconGraphic은 실제 하트 리소스 연결 전 삭제 대기다. 자동 Builder로 자산을 다시 만들지 않는다.
- 이번 HMS 예외는 PlayerAnimationPlayback.cs의 Interact 우선순위2→4와 관련 설명, PlayerAnimationController.cs의 관련 설명, CharacterMovement.cs의 `MaximumGroundAngle => maximumGroundAngle` 읽기 전용 속성 한 개와 설명이다. 실제 Inspector 접지각을 읽으며 원래 필드·기본값·Range·접지 동작을 보존한다. 이전9파일 허가는 종료됐고 다른 보호 원본·meta/import/Packages/ProjectSettings 수정 권한을 확대하지 않는다. 추후 PR에 변경 의도와 한계를 명시한다.

최종 포포 Escape(7) 대화만 Completed/Skipped 장부를 보존한 채 재접촉·재시도 후 새 회차를 허용한다. 일반 대화 완료 장부는 지우지 않는다. 현재 Stage·Trigger·선택 객체·Table·Code·Actor 세대·Round·Session이 동일하고 완료/전체 Skip인 경우에만 Ending을 한 번 예약하며 취소·낡은 콜백·동시 사망/포획은 거절한다.

## 2026-10-11 HMS 기본 입력 조립 승인

사용자는 `Assets/_Code/Core/Tech_HMS/Features/PlayerSkillController.cs` 한 파일에 슬롯별 추가 사용 조건의 연결/해제와 요청 거절 이벤트를 추가하도록 허용했다. 해금 원본과 폼 공통 간격은 기존 SYM Session/Bridge에 둔다. HMS PlayerController를 ActorBody에 조립하고 Host가 F·사망 진행을 담당하며 GameplayInputRouter와 불필요한 전달 메서드를 제거한다. 소유 씬·프리팹 참조 이관을 포함하고 Play 없이 컴파일·참조를 검증한다. 다른 HMS 원본/meta 수정 허가는 확대하지 않는다. 최종 반영 브랜치는 `feature/core/systems`이며 해당 브랜치 push 확인 후 워크트리를 정리한다.
