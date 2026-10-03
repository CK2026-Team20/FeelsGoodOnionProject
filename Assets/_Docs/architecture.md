# 시스템 구성

Cooked의 실행 구조는 앱 전체, 한 판의 세션, 다시 불러오는 스테이지의 세 수명으로 나뉜다. 플레이어와 월드 자산을 다시 생성하더라도 앱 설정과 현재 세션을 같은 수명으로 취급하지 않는다.

| 경계 | 소유 책임 | 연결 방향 |
|---|---|---|
| Foundation | 부팅, 씬 전환, 작업 결과, 앱 서비스 등록과 이벤트 전달 | Contracts를 통해 장면·페이드·실행 서비스 호출 |
| Integration | 앱 및 Core 조립, 월드·UI 연결, 스테이지 등록, 종료 조정 | 구체적인 Foundation·Session·UI·Level·Audio·Chase·Dialogue·Cinematics 조립 |
| Session | 체크포인트·능력 해금, 입력 차단, 플레이어 연결, 임시 객체 수명 | 공용 계약과 Tech_HMS 플레이어 공개 API 사용 |
| Tech_HMS | 체력·조각 원본, 이동·폼·스킬·애니메이션·플레이어 물리 | Tech_SYM은 원본 수정 없이 PlayerFacade와 읽기 전용 모델을 소비 |
| Level / Chase | 스테이지 체크포인트 위치·기믹·구출·탈출 / 추격 진행 | 스테이지 이벤트를 세션과 흐름으로 전달; 추격 성공 응답 후 문 닫기 |
| UI / Dialogue / Cinematics | 화면용 상태·명령 / 대화 진행 / Timeline 연출 | View는 ViewModel에 연결; 서비스가 업무 규칙과 수명을 소유 |
| Audio | 앱 수명의 재생 정책·채널·음량 | 드라이버가 Unity AudioSource 및 DOTween 실행 |
| Enemies / Platforms / Features / Interaction / OvenTray | 적 전투, 이동 발판, 붕괴·수집, 메모·프롬프트, 트레이 | PlayerFacade·공용 인터페이스를 통해 플레이어와 협업 |

## 한 판의 진입과 종료

제목 화면 입력 → ViewModel의 새 게임 명령 → GameFlowService → 제목 씬 해제 → Core 추가 로드 및 활성 씬 지정 → 세션 초기화 → 세 스테이지 추가 로드 → 체크포인트 위치에 플레이어 생성 → 오프닝 → 플레이 화면 표시 순서다. UI가 직접 씬을 로드하지 않는다.

재시도는 대화를 취소하고 플레이어와 임시 객체를 정리한 뒤 스테이지를 재로드한다. Core와 그 세션·UI는 유지한다. 체크포인트의 식별자·조각 수는 세션 소유이며 실제 위치는 새로 로드된 스테이지에서 다시 조회한다. 제목 복귀는 Core와 세션까지 종료한다. 앱 수명의 객체에 세션 연출이나 파괴된 플레이어 참조를 보관하지 않는다.

## 자산과 구현 경계

실행 씬 묶음은 Assets/_Scenes/Tech_SYM/Architecture/Configuration/ArchitectureInstallation.asset에 저장된 Bootstrapper·Title·InGameCore와 Stage 1~3이다. 기존 Tech_SYM 씬도 남아 있고 Editor 진입 경로 변환의 대상이다. 씬 파일이 남아 있다는 이유로 별개의 최종 게임 흐름으로 단정하지 않는다.

Assets/_Code/Core/Interfaces는 직군 간 공용 전투·상호작용 계약이다. Tech_HMS와 이 공용 폴더는 현재 작업자의 수정 영역이 아니다. 해당 경계의 지역 지침은 루트에서 관리하며 그 안에 새 문서를 추가하지 않는다.

_Art의 모델·이펙트·애니메이션은 참조 원본이며 Tech_SYM 소유 씬·프리팹에서 조합한다. 외부 패키지와 플러그인은 기능 모듈로 재작성하지 않는다. URP는 렌더링, Input System은 입력, Timeline은 연출, DOTween은 전환·이동·표시 보간에 연결되어 있다. 현재 서버나 네트워크 서비스는 필요하지 않다.
