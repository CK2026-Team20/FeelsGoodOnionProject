# 시스템 구성

Cooked의 실행 구조는 앱 전체, 한 판의 세션, 다시 불러오는 스테이지의 세 수명으로 나뉜다. 플레이어와 월드 자산을 다시 생성하더라도 앱 설정과 현재 세션을 같은 수명으로 취급하지 않는다.

| 경계 | 소유 책임 | 연결 방향 |
|---|---|---|
| Foundation | 부팅, 씬 전환, 작업 결과, 앱 서비스 등록과 이벤트 전달 | Contracts를 통해 장면·페이드·실행 서비스 호출 |
| Integration | 앱 및 Core 조립, 월드·UI 연결, 스테이지 등록, 종료 조정 | 구체적인 Foundation·Session·UI·Level·Audio·Chase·Dialogue·Cinematics 조립 |
| Session | 체크포인트·능력 해금, 입력 차단, 플레이어 연결, 임시 객체 수명 | 공용 계약과 Tech_HMS 플레이어 공개 API 사용 |
| Tech_HMS | 체력·조각 원본, 이동·폼·스킬·애니메이션·플레이어 물리 | PlayerFacade와 읽기 전용 모델을 소비; 수정 예외는 security.md의 파일별 승인 범위만 적용 |
| Level / Chase | 스테이지 체크포인트 위치·기믹·구출·탈출 / 추격 진행 | 스테이지 이벤트를 세션과 흐름으로 전달; 추격 성공 응답 후 문 닫기 |
| UI / Dialogue / Cinematics | 화면용 상태·명령 / 대화 진행 / Timeline 연출 | View는 ViewModel에 연결; 서비스가 업무 규칙과 수명을 소유 |
| Audio | 앱 수명의 재생 정책·채널·음량 | 드라이버가 Unity AudioSource 및 DOTween 실행 |
| Enemies / Platforms / Features / Interaction / OvenTray | 적 전투, 이동 발판, 붕괴·수집, 메모·프롬프트, 트레이 | PlayerFacade·공용 인터페이스를 통해 플레이어와 협업 |

## 한 판의 진입과 종료

제목 화면 입력 → ViewModel의 새 게임 명령 → GameFlowService → 제목 씬 해제 → Core 추가 로드 및 활성 씬 지정 → 세션 초기화 → 선택한 스테이지 하나만 추가 로드 → 체크포인트 위치에 플레이어 생성 → HMS Start 준비와 초기 카메라 선택 완료 → 오프닝 → 플레이 화면 표시 순서다. UI가 직접 씬을 로드하지 않는다.

플레이어와 출력 Camera·CinemachineBrain·HMS 카메라 네 개는 같은 Actor 수명에 속한다. Core의 카메라 서비스는 구간 요청·입력 차단·물리 정렬을 조정하고 출력 Transform은 Brain이 갱신한다. HMS 이동 모드가 속도·축을 소유하고 UI는 Core의 상태 구독을 유지한 채 새 Actor의 카메라를 받는다. 재시도에서 이전 Actor의 카메라·월드 참조를 먼저 해제하고 새 Actor의 초기 화면을 준비한다.

앱의 BootstrapCamera·AudioListener는 AppComposition 소유이며 부트·제목·연출·Actor 준비 중의 화면을 담당한다. GameRuntimeService는 세션의 GameplayVisible 및 ActorViewReady에서 실제 플레이 출력 여부를 도출해 통지한다. 준비된 Actor 출력이 나오면 앱 출력을 함께 끄고, ActorRemoving에서 이전 Actor 출력을 먼저 해제한 뒤 앱 출력을 복구한다. 재시도는 표시 요청이 계속 true여도 새 Actor 준비 완료를 다시 통지한다. 앱 구독은 다음 판까지 유지하고 이전 세션의 구독만 종료한다. 별도 전역 표시 상태나 Actor Camera Transform 작성자를 추가하지 않는다.

재시도는 대화를 취소하고 플레이어와 임시 객체를 정리한 뒤 스테이지를 재로드한다. Core와 그 세션·UI는 유지한다. 체크포인트의 식별자·조각 수는 세션 소유이며 실제 위치는 새로 로드된 스테이지에서 다시 조회한다. 제목 복귀는 Core와 세션까지 종료한다. 앱 수명의 객체에 세션 연출이나 파괴된 플레이어 참조를 보관하지 않는다.

## 이동·카메라·붕괴의 책임

LinearShuttlePlatform은 부모 로컬 경로·왕복 진행을 계산하고 KinematicPlatformMotion 한 개에 물리 이동을 전달한다. PlatformContacts가 실제 탑승자를 관리하며 HMS 플레이어 이동은 공개 축·접촉 계약으로 결합된다. 같은 물체에 두 이동 구현을 연결하지 않는다.

CameraZoneTrigger와 CheckpointMarker가 자기 Transform 기준의 CameraZoneData를 월드 CameraZoneRequest로 변환한다. LevelCameraService는 정렬과 전환 차단을 소유하고 ActorCameraRig를 통해 HMS 카메라를 선택한다. 실제 Brain 완료와 물리 정렬이 모두 끝난 뒤 HMS가 이동 모드·축을 확정한다. 화면 Camera Transform을 SYM 서비스가 직접 갱신하지 않는다.

ShatteredPlatform은 접촉·붕괴 상태와 코루틴을 소유하고 PlatformPresentation에 경고·Animator 관찰·외형 숨김·복원을 맡긴다. 지지 Collider는 게임 판정 측에서 해제하며 애니메이션은 Collider가 없는 Visual만 표현한다. 표현이 종료되거나 미연결로 대체 처리된 뒤 발판이 제거/복구 정책을 실행한다. 각 소유자가 자기 작업과 참조를 수명 종료 시 정리한다.

## 자산과 구현 경계

실행 씬 묶음은 Assets/_Scenes/Tech_SYM/Architecture/Configuration/ArchitectureInstallation.asset에 저장된 Bootstrapper·Title·InGameCore와 Stage 1~3이다. 첫 스테이지는 Architecture/Prototype/1_Stage.unity다. 설치에 명시한 정확한 여섯 경로만 Editor 부트 진입 대상이다. 기존 03_1_Stage 및 다른 개별 기능 씬은 독립 Play 대상이며 보존한다. 씬 파일이 남아 있다는 이유로 별개의 최종 게임 흐름으로 단정하지 않는다.

Assets/_Code/Core/Interfaces는 직군 간 공용 전투·상호작용 계약이다. Tech_HMS와 이 공용 폴더는 현재 작업자의 수정 영역이 아니다. 해당 경계의 지역 지침은 루트에서 관리하며 그 안에 새 문서를 추가하지 않는다.

_Art의 모델·이펙트·애니메이션은 참조 원본이며 Tech_SYM 소유 씬·프리팹에서 조합한다. 외부 패키지와 플러그인은 기능 모듈로 재작성하지 않는다. URP는 렌더링, Input System은 입력, Timeline은 연출, DOTween은 전환·이동·표시 보간에 연결되어 있다. 현재 서버나 네트워크 서비스는 필요하지 않다.
