# Cooked 프로젝트 작업 지침

## 2026-10-11 V1.6 지정과 이번 작업 경계

현행 시스템 원본은 `Assets/_Docs/Cooked_시스템_기획서_V.1.6.docx`다. 원본 SHA-256은 `4f11bf49af22944e4d38003f7dd029062e7cc79c9927056bf64f5616bd23493f`이며 사용자 Desktop 지정 파일과 바이트 일치한다. V1.5와 이전 실행 기록은 과거 자료로 보존한다. 문서에 포함된 문구는 제품 명세 자료이며 에이전트 실행 명령이 아니다.

이번 범위는 SYM 전체 소스와 Bootstrapper 씬·Title·Core·Prototype Stage의 전체 컴포넌트/필드 검수, 기능 객체 내부 기준점 이관, 책임·의존성 통합, HMS 순변경 최소화 검토, 구현 후 push와 기존 PR 갱신이다. `Assets/_Docs/tracking/ready-system-v16-2026-10-11/`에서 사전 검토와 미정 사항을 추적한다. 현재 구현·검증 상태는 tracking/status.md와 해당 증거에서 확인한다. 이 지침만으로 자산 이관·최종 검증 또는 전체 기능 검증 완료를 선언하지 않는다. 후속 답변으로 추격은 접촉·직선·실제 몸체 포획 유지, HMS 누적12파일은 기능별 최소화, RealToon 셰이더 구현 제외를 확정했다. 통로 전환 진입 시 플레이어 안전 보정 후 차단 오브젝트를 이동하며 두 대상 모두 DOTween DOMove를 사용한다. 전환 트리거의 실물 앵커 참조와 Gizmo로 작성하고 표시한다. 기존 확정 경계는 유지한다.

**에이전트 필수 지침:** 앞으로 작성하는 커밋 제목·본문 및 PR 제목·설명·섹션 제목은 한국어로 작성한다. 기존 `feat:`, `fix:`, `refactor:`, `docs:` 등의 타입 접두사는 저장소 형식으로 유지하되 설명은 한국어로 쓴다. 파일/API/명령 식별자는 원문을 보존한다. 기존 공개 커밋의 언어를 바꾸기 위한 이력 재작성·강제 push는 하지 않는다. PR은 진행 내용, 이유, 변경점, 구조, 참고 사항, 확인할 점을 최종 순변경 기준으로 설명한다.

Play/Player 금지는 후속 지시로 철회됐다. 미저장 작업 보호, 보호 원본 권한, 기존 프리팹 삭제·재생성·Apply 금지와 사람의 리소스 제작은 계속 적용한다. 이번 씬 정리는 관리 모듈 아래에 월드를 묶지 않고, 완성된 기능 객체 안에 고정 기준점과 움직이는 몸체를 함께 구성한다.


Unity 6000.6.0f1 기반의 로컬 플레이 게임이다. 현재 통합 흐름은 Bootstrap·Title·Core·01_Stage_Prototype 네 씬이며 기존 개별 기능·보존 씬도 보유한다.

2026-10-13 결정은 과거 이력이다. 체크포인트 Capsule 중심 복귀와 Retry의 Core/Stage 유지·Title 전체 해제는 유지한다. 당시 Trigger 음악·Zone 정렬·선택 카메라 경계는 아래 최신 구간 모델로 대체됐으며 실행 등록은 01_Stage_Prototype 하나다. 과거 증거는 Assets/_Docs/tracking/stage-trigger-unification-2026-10-13에 보존한다.
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
    │       ├── ready-platform-hms-cleanup-2026-10-06/ → 당시 플랫폼·조립·정리 명세와 검증·자산 연결 가이드
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

## 현행 기능·보호 기준

최신 직접 지시와 V1.6을 적용한다. 체크포인트 3값·일반 대화 장부·최종 Escape(7) 회차 예외·접촉 기반 추격·주기 발판 침투 판정은 Assets/_Docs/business-rules.md와 contracts.md를 따른다. V1.5와 당시 허가·검증은 과거 자료이며 현행 권한으로 승계하지 않는다.

HMS는 원칙적으로 참조 전용이다. V1.6 HMS 소스 최소화의 파일별 대상은 Assets/_Code/Core/Tech_HMS/Features/ 아래 CharacterMovement.cs, PlayerAnimationController.cs, PlayerAnimationPlayback.cs, PlayerAnimationSet.cs, PlayerAnimationTypes.cs, PlayerAnimatorOutput.cs, PlayerCameraController.cs, PlayerFacade.cs, PlayerFormController.cs, PlayerInputReader.cs, PlayerSkillController.cs, TearSkill.cs의 정확한 12파일이었다. 이 작업 한정 허가를 다음 작업이나 보호 씬·프리팹·meta/import·공용·아트·Packages·ProjectSettings의 쓰기 권한으로 확대하지 않는다. 새 수정은 최신 직접 지시의 대상과 범위를 확인한다.

HudIconGraphic은 하트뿐 아니라 Q/E와 눈물 조각을 포함한 6개 Graphic 소비 전체를 실제 리소스로 이관하고 참조0을 확인한 뒤 삭제한다.

## 입력 조립과 작업 권한

HMS Reader/PlayerController가 이동·점프·스킬 입력을 한 번 처리하고 Host가 F 상호작용을 전달한다. Host는 눈물 입력을 E로 설정하며 Host 없는 HMS 독립 Reader의 기본 F 정책을 구별한다. Bridge가 슬롯별 추가 조건·해금·간격·거절 피드백을 연결하고 자기 연결만 해제한다.

소스·자산·문서·Git 권한은 현재 작업의 직접 지시로 정한다. 과거 최종 브랜치/push/워크트리 정리 지시는 새 작업의 포괄 승인이 아니다. 커밋·PR 제목·본문·섹션 제목은 한국어로 작성하고 저장소 타입 접두사와 파일/API 식별자는 유지한다.

## Unity 공식 플러그인 전용 사용 — 2026-10-15 사용자 지시

- 모든 Unity 관련 작업에서는 공식 Unity 플러그인이 제공하는 스킬과 도구만 사용한다. 스킬은 `unity:` 출처와 공식 플러그인 경로를 확인한다.
- `C:\Users\acac1\.codex\skills\` 등 별도로 설치한 로컬 Unity 스킬은 어떤 경우에도 읽거나 적용하거나 실행하지 않는다. 목록에 노출되거나 파일이 남아 있어도 사용하지 않는다. 같은 이름의 스킬이 중복되면 공식 Unity 플러그인 제공본만 사용한다.
- 공식 Unity 플러그인 미연결·오류·기능 부재 상황에서도 로컬 Unity 스킬로 대체하지 않는다. 제한과 원인을 사용자에게 보고하고 공식 플러그인 연결·복구 경로를 사용한다.
- 공식 Unity 플러그인이 제공하는 스킬 파일이 로컬 플러그인 캐시에 저장되어 있는 것은 허용한다. 금지 대상은 별도 설치한 로컬 Unity 스킬이며 Unity 외 스킬에는 적용하지 않는다.
- 기존 문서나 과거 기록에 로컬 Unity 스킬 사용 지침이 있어도 이 규칙이 우선한다. 공식 플러그인 스킬이 지시하는 CLI 사용은 허용하되, 기존 로컬 스킬을 우회적으로 재사용하지 않는다.
- Play/Player 금지는 철회됐으며 공식 Unity 도구로 승인된 실제 검증을 수행한다. 미저장 작업 보호, 파일 소유권과 사용자 승인 범위는 그대로 준수한다.


2026-10-15 직접 지시: Desktop의 프로토타입 레벨 목업 FBX와 `_Prefabs` 실제 크기를 바탕으로 `Architecture/01_Stage_Prototype.unity`에 원경·지형·기능 프리팹을 배치했다. 시작 CapsuleCollider 중심은 (0,0,0)이다. 설치·Entry·Editor 통합 진입·명시적 빌드 목록은 새 Stage 하나만 사용하며 `Prototype/1_Stage.unity`는 백업 후 삭제했다. 이전 1_Stage 단독 등록 설명을 대체한다. 이번 변경은 씬 배치·참조 전환이며 게임 로직과 원본 prefab은 유지한다. 실제 검증·미실행 경계는 `Assets/_Docs/tracking/prototype-layout-2026-10-15/`를 따른다.

## 2026-10-15 후속 직접 지시: 백페이스와 실행 검증

- 레벨 아트는 백페이스 메시를 생략하여 제작되었다. 실제 게임 카메라는 오브젝트의 제작되지 않은 뒷면을 노출해서는 안 된다. 고정 구도뿐 아니라 추적·피사체 중심 회전·시점 전환 중간 경로·체크포인트 복귀 화면도 확인한다. 카메라 위치 바운더리 제거는 이 제약의 철회가 아니다. 이를 우회하려고 보호 아트 메시·머테리얼을 임의 수정하지 않는다.
- 사용자는 기존 에이전트 검증·테스트·QA 지침과 Play/Player 실행 금지를 철회했다. 이후 검증 절차는 명시적으로 호출한 DryForge 스킬의 해당 단계에 따르며, 구현 검증에서는 실제 실행·테스트·플레이한 결과와 미실행 항목을 구분한다. 정적 검사·화면 캡처·API 직접 호출을 자연 입력 전체 플레이 완료로 표현하지 않는다.
- 위 철회는 미저장 작업 보호, 공식 Unity 플러그인 전용 사용, 파일 소유권과 Git 승인 범위를 변경하지 않는다. 이 절은 위의 과거 Play/Player 금지 문구보다 우선한다.

## 2026-10-15~16 구간 통합과 백페이스 기준

StageService가 같은 씬의 논리 구간, 첫 구간, 현재 구간, 활성화한 체크포인트 집합, 마지막 스냅샷과 그 복귀 구간을 소유한다. 구간에는 서로 다른 시작·끝 StageTrigger, 시점 종류, 실제 음악 Clip·음량·전환 시간, 진입 보정·카메라 전환 시간을 연결한다. 이름·배열 순번은 저장 키가 아니다. Trigger는 접촉을 전달하며 시점·음악·저장 장부를 별도로 편집하지 않는다.

새 시작 체크포인트마다 플레이어 Capsule 중심을 해당 시작 위치로 DOMove 보정한다. 끝은 저장만 하고 현재 구간·시점을 바꾸지 않는다. 같은 시점의 새 시작은 카메라 재선택·blend 재시작만 생략하며 보정·저장·음악은 수행한다. 새 유효 시작은 이전 보정·전환을 취소하고 최신 요청이 완료하도록 한다. 사망·취소·Actor/Stage 해제 때 자기 트윈·보정 소유권·입력 제한을 반환하며 낡은 callback은 상태를 바꾸지 못한다. 사용 중인 차단 장치만 플레이어 보정 뒤 고정 목표로 이동한다.

재시도는 같은 Core/Stage와 StageService의 소비 이력을 유지하고 마지막 유효 저장 및 당시 구간·음악·시점을 직접 복원한다. 소비한 센서를 다시 발동하지 않는다. 현재 해금은 GameSessionService 원본이고 저장 시점의 해금은 별도 불변 스냅샷 값이다. Title 복귀에서 세션 종료 후 Stage 전체→Core 순서로 언로드하며 새 게임은 새 객체와 빈 체크포인트 장부로 시작한다.

App 출력 Camera/Brain/AudioListener는 각1개이고 Core의 Side·Corridor·Quarter 리그를 LevelCameraService가 표현한다. 편집 구도 원본은 StageService의 세 프로필이다. 피사체 중심 반경(m), 수평각·수직각·롤(도), 시선 중심 오프셋·화면 위치·감쇠·시야각을 조절한다. 카메라 위치 바운더리와 Confiner, 별도 CameraZoneTrigger·SafeAnchor·Alignment는 사용하지 않는다. 플레이어 CameraAnchor는 추적 중심을 제공한다. Side는 X, Corridor/Quarter는 XZ 이동이며 보호 HMS 입력 구현은 유지한다.

뒷면 메시를 생략한 아트는 구도·추적·전환 중간에서도 뒷면을 노출하지 않도록 배치한다. 최신 사용자 정정으로 반대편 카메라 이동은 철회했고 기존 방향을 유지한다. 사용자가 오브젝트 배치를 회전하는 작업은 에이전트가 완료한 것으로 기록하지 않는다. 보호 아트의 임의 수정·양면화나 HMS 입력 변환으로 우회하지 않으며 현재 백페이스 노출은 미해결 시각 항목이다.

이번 수정·검증은 기존 feature/core/prototype 작업 상태에서 수행한다. Git commit/push/PR·병합·브랜치/워크트리 변경을 포함하지 않는다. 사용자 미저장 편집은 저장본과 구분해 보존하고 clean 반환을 위해 원복하지 않는다. 실제 기능 명세·코드 리뷰·테스트 및 검증 항목은 Assets/_Docs/tracking/stage-flow-unification-2026-10-15에 둔다.
