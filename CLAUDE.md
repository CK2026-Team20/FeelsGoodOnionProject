# Cooked 프로젝트 작업 지침

Unity 6000.6.0f1 기반의 로컬 플레이 게임이다. 플레이어·스테이지 기믹·추격·대화·연출을 여섯 씬의 통합 흐름으로 구성하며 기존 개별 기능 씬도 보유한다.
현재 서버·네트워크는 필요하지 않다. 현재 구현은 사용자 확인을 받은 의도된 동작이며 변경 요구는 따로 지정한다.

## 반드시 지킬 경계

1. 현재 작업자는 Tech_SYM이다. 다른 Tech_*, art_*, QA_* 폴더와 .meta는 참조 전용이며 같은 직군의 명시적 파일 허용만 수정 예외다.
2. 비아트 작업자는 _Art 원본을 수정하지 않는다. 승인된 문서 작업은 루트 지침과 Assets/_Docs의 프로젝트 Markdown 및 대응 메타데이터를 포함한다. 이 예외는 기존 PDF·공용 코드·프리팹·Packages·ProjectSettings 수정 권한이 아니다.
3. 상태 소유자를 하나로 두고 UI는 ViewModel 상태·명령에 연결한다. ViewModel은 View를 직접 참조하지 않으며 자기 구독·작업만 해제한다.
4. Unity 원본·GUID·직렬화 참조를 보호한다. 기존 객체 배치의 물리 계약을 일반 계층 예시만으로 재작성하지 않는다.
5. 실제 실행하지 않은 검증을 통과로 기록하지 않는다. 수정 범위 밖 정리와 승인되지 않은 Git 작업을 수행하지 않는다.

## 프로젝트 문서와 지역 지침

프로젝트 공통 문서의 경로는 프로젝트 루트 기준 Assets/_Docs/다. 모든 문서 읽기·작성·갱신은 아래 트리의 실제 경로를 사용한다. 루트 AGENTS.md·CLAUDE.md와 모듈별 AGENTS.md는 아래 위치를 유지한다. 기존 PDF는 보존한다.

```text
project-root/
├── AGENTS.md → 공통 진입 지침
├── CLAUDE.md → 같은 내용의 진입 지침
└── Assets/
    ├── _Docs/
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
    │       ├── 코드 리뷰.md → 문서·원본 교차 검토
    │       ├── 테스트 및 검증 항목.md → 완료 기준과 확인 결과
    │       └── decisions/
    │           ├── index.md → 결정 목록
    │           ├── 0001-folder-ownership.md → 폴더 권한 결정
    │           └── 0002-current-offline-scope.md → 현재 범위 결정
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

## 작업 시작 전

목표 → 참고할 코드·자산 → 제약 → 완료 기준을 먼저 정한다. Assets/_Docs/standards.md, Assets/_Docs/engineering-notes.md와 대상 모듈의 AGENTS.md를 읽는다. 기능 명세는 Assets/_Docs/business-rules.md와 해당 실제 자산을 함께 확인한다. 구버전 README로 현재 동작을 덮어쓰지 않는다.

- 플레이어·재시도 변경: Assets/_Docs/architecture.md의 수명, Assets/_Docs/contracts.md의 플레이어·체크포인트 경계와 SessionActorHost를 먼저 확인한다.
- 씬 진입·빌드 변경: ArchitectureInstallation.asset, BootstrapEntryRoute와 Assets/_Docs/operations.md의 명시적 씬 빌드를 확인한다.
- UI·대화·연출 변경: 옵션 정지와 자기 차단 해제, 중복 입력 방지, 표시 완료·취소 계약을 확인한다.
- 아트·프리팹 연결: Assets/_Docs/security.md의 권한과 대상 GUID·원본 소유자를 먼저 확인한다.

## 문제 보고

타인 폴더 무단 변경, _Art 원본 손상, .meta GUID 단절, 세션 종료 실패로 입력 잠금·객체가 남는 현상은 즉시 사용자에게 보고한다. 다른 미해결 문제는 재현 조건·영향·현재 해결하지 못하는 이유를 Assets/_Docs/tracking/findings.md에 기록한다.
