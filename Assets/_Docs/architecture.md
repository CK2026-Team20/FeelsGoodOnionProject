# 시스템 책임과 수명

Unity6000.6.0f1의 로컬 플레이 게임이다. 현행은 V1.5와 사용자 직접 선택이며 현재 목표는 응집형 Prototype Stage 하나에서 전체 게임 진행1회다. 서버·네트워크·디스크 진행 저장은 없다. 기존 Core와 등록 Stage의 씬 수명, 한 판 서비스 수명, 현재 Actor 수명을 구분한다.

## 상태 원본과 실행 책임

| 범위 | 단일 원본/소유자 | 생성과 종료 |
|---|---|---|
| 앱 | AppComposition·GameFlowService·SceneService·Audio/Settings | Bootstrapper가 앱 scope를 소유. 출력 Camera/Brain/Listener 각1개와 Options/Fade/Audio를 연결 |
| Core 씬 | CoreComposition·CoreUiBinding·Side/Quarter 리그 | 첫 새 게임에서1회 준비. Title/Retry 이후 같은 씬 인스턴스 유지 |
| 한 판 | GameSessionRuntime·GameSessionService·CoreWorldBinding·Dialogue·VM | App 부모의 새 scope. Retry는 유지, Title은 Release/Dispose, 다음 새 게임은 새 범위 |
| Stage | StageService와 각 발판/장치/적/수집 owner | 처음 등록 Stage들을 additive 로드. 활성 Stage 하나만 Actor와 연결. Retry는 초기 재준비 |
| Actor | SessionActorHost / HMS Facade·Form·Movement·Model | 비활성 Player prefab을 생성해 의존 연결 후 활성화. 교체 전에 빌린 참조·자기 구독·작업 해제 |
| 입력/제한 | HMS Reader·PlayerController / Host의 F / GameplayControlService | 실제 입력1개 경로. Options/메모/대화가 자기 토큰만 반환 |
| 화면 | ViewModel 상태·Command / View | View는 입력/표현만. VM은 View 참조 없음. 재Bind에서 자기 구독 해제 |
| 외부 장치/작업 | Scene/Audio/Settings/DOTween/Animator 어댑터 | 만든 작업·범위만 취소/해제. 빌린 Actor·Camera·Source를 임의 파괴하지 않음 |

HP·현재 조각·폼·운동은 HMS가 소유한다. Session이 보유하는 마지막 CP는 현재 Player의 복제 원본이 아니라 저장 시점의 Position/Fragments/해금 마스크 불변값이다. 현재 해금의 원본은 GameSessionService, 일반 대화 완료 장부는 DialogueService다. UI가 별도 해금/HP bool을 진실로 유지하지 않는다.

## 조립자와 로케이터

ServiceLocator는 Type 등록/조회·부모 조회·scope 소유·역순 Dispose만 한다. App/Core 조립자는 실제 구체 서비스 생성·Unity 오브젝트 참조·초기화 순서와 부분 실패 회수를 맡는다. 소비자는 작은 계약을 명시적으로 주입받는다. 로케이터가 Camera/물리/UI·게임 상태를 모두 생성하는 거대 전역 객체가 되지 않는다. 같은 인스턴스 별칭은 한 번만 해제하고 부모 소유 객체를 자식 scope가 재소유하지 않는다.

CoreWorldBinding은 Actor/Stage 교체 때 실제 재바인딩·카메라 초기 준비·추격 저장·최종 대화 예약/해제를 조정한다. 단순 전달과 다르다. 같은 수명 참조만 전달하던 GameUiRoot/GlobalUiRoot·범용 버스·표시 래퍼는 원본 owner로 합쳤다. 자동 Builder12개와 중복 runtime/계약10개를 삭제했고, HudIconGraphic은 실제 리소스/모든 Graphic 소비 이관 전까지 유지한다.

## 지역 기능 소유권 (2026-10-11)

StageService의 직렬화 설정은 초기 체크포인트와 스테이지 음악 두 개다. 체크포인트/적 중복 목록, 추격·진행 경로, 시작선과 퇴로 참조를 제거했다. Stage는 공유 진행/최종 대화 수명과 재시도 조정을 유지하고 적 인스턴스 배열은 보유하지 않는다. 재시도 시 씬을 조회하여 각 적 소유자에게 초기 위치·기절 상태 복구를 요청한다.

StageTrigger가 추격 접촉, 퇴로 참조와 안전 검사, 저장 후 시작 순서를 맡는다. CoreWorldBinding은 현재 추격 실행기와 저장 명령만 연결한다. ChaseRuntimeDriver는 X 이동·현재 Actor 판정·실제 전면 Box 포획·종료를 소유하고 ChaseSwarmPresentation은 외형 tween만 소유한다. 기존 ChaseDefinition 설정 자산은 GUID를 유지한다. ChaseService/Model/Path/Settings/IChaseActor 다섯 중간 소스는 삭제했다.

별도 Enter 트리거는 제거했다. 처음부터 초기 CheckpointMarker로 등장하며 스테이지 음악은 BindActor 때 한 번 설정한다. 카메라 정렬점과 체크포인트 등장점은 각 소유 컴포넌트의 자식이다. 부모에 상대적인 배치를 유지하고 소비 시 Transform.position으로 월드 위치를 전달한다.

## 한 판 흐름

Bootstrap→Title→새 게임→Core/등록 Stage 최초 additive 로드→Prototype 초기 준비·Actor 생성·카메라 준비→Opening→Playing→최종 Escape(7) 포포 대화→Ending→Title이다. Stage1→2→3 자동 연결을 추가하지 않았다. 등록 Stage2/3의 독립 진입과 기존 독립 Host 기능 씬을 보존한다. Opening/Ending은 Core 화면이며 별도 씬이 아니다.

사망은 미완료 메모·대화·추격·Actor 표현 요청을 취소하고 자기 연결을 해제한 뒤 같은 Stage의 초기 배치/기믹을 준비한다. 마지막 유효 CP3값 또는 첫 저장 전 초기 구성을 새 HP3/Normal/껍질0 Actor에 복원한다. 같은 물리 CP 재접촉 억제와 일반 Completed/Skipped 대화 장부는 유지한다. Title에는 한 판 범위·Actor·빌린 월드/UI 연결을 해제하며 Core/Stage 씬은 유지한다.

최종 포포 대화만 완료 장부와 별개로 새 회차를 허용한다. Stage·Trigger·선택 객체·Table·Code·Actor 세대·Round·Session을 캡처하고 종료 시 다시 확인한다. Completed/Skipped만 Ending1회이며 취소·낡은 callback·같은 프레임 사망/포획은 거절한다.

## Scene Root와 prefab 물리

씬 Manager/Camera/Canvas/월드 기능 객체는 서로 독립 root다. 개별 prefab은 빈 논리 Root와 Render/Body/표현 자식을 구분한다. Player의 실제 ActorBody는 Facade·Movement·Form·HeavyState·Rigidbody·Capsule이 동거하고 VisualRoot/CameraAnchor는 서로 다른 직계 자식이다. 빈 PlayerRoot는 생성/제거 관리 단위다. Shell은 HMS Remove 계약 때문에 PlayerDebris/Rigidbody/Box/HeavyState가 실제 Root에 동거하며 바깥 래퍼를 만들지 않는다.

움직이는 발판은 한 Motion이 실제 Body Rigidbody를 이동한다. Render는 그 Body 아래, 고정 목표는 Body 밖이다. 눈물은 pickupRoot 전체를 움직여 Sensor와 Render가 함께 이동한다. Shattered Visual의 경고 Shake와 Chase 표현 아래에는 고정 Marker를 두지 않는다. 기존 물리 Root template의 계약은 예시 계층 때문에 강제 재작성하지 않는다.
