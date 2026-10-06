# 작업 시 주의할 동작

2026-10-06 구현 기준. 실제 실행 결과와 미검증은 tracking/status.md를 참조한다.

## 빌드 씬과 실행 씬의 차이

일반 Build Settings에는 01_Samples만 등록되어 있지만 Architecture 설치 자산에는 여섯 실행 씬이 있다. 이 상태에서 일반 빌드 설정만 보면 통합 게임이 빠진 것처럼 보인다. 통합 Windows 빌드 메뉴는 여섯 씬을 명시적으로 전달하고 ProjectSettings 해시를 전후 비교한다. 일반 빌드 목록을 자동 수정하는 것으로 해결하지 말고 빌드 결과의 scenes와 changedProjectSettings를 확인한다.

## Editor 진입과 실제 장면

설치 자산의 정확한 여섯 경로만 Editor hook이 부트 진입을 제공한다. 기존 03_InGame·03_1_Stage·발판 테스트 씬은 독립 Play한다. 비대상 진입에서 이전 startScene을 복원만 하면 그 이전 값이 Bootstrap일 때 다시 부트로 시작한다. 실제 Play 직전 비대상은 null을 소유하고 Stop·취소·재로드에서 자신의 값일 때만 이전 설정을 복원한다. 외부 변경을 원래 값이라고 덮지 않는다. 열었던 씬 이름만으로 검증 대상을 판단하지 않는다. 실제 로드된 Bootstrap·Core·Stage와 입력 경로를 확인한다.

기준 HMS MovementModeController는 Start에서 초기화되므로 Awake 직후 TrySetMode는 false다. 첫 카메라가 Side로 잠깐 보이거나 UI가 사라지면 초기 모드보다 먼저 표시했는지 확인한다. 비활성 Actor 내부에 네 카메라와 HMS 참조를 직렬화로 조립하고, 활성화 후 IsInitialized와 Brain의 선택 완료를 기다린다. 재시도에서 기존 UI 모델을 재생성하지 않고 출력 카메라만 교체한다.

통합 플레이 중 활성 씬은 Core이며 선택 Stage는 추가 로드된다. 검증에서 활성 씬이 Stage라는 별도 조건을 넣지 않는다. GameFlowService의 CurrentStageId와 실제 로드된 경로를 함께 확인한다. 제목 복귀 후 이전 세션 참조로 다음 판을 관찰하지 말고 현재 GameRuntimeService.CurrentSession을 다시 조회한다.

BootstrapCamera는 앱 소유 대기·연출 출력이다. 준비된 Actor 출력과 함께 켜 두면 Camera/AudioListener가 두 개가 된다. AppComposition은 세션 GameplayVisible 및 ActorViewReady에서 도출한 실제 출력 통지로 자기 Camera·Listener를 전환한다. ActorRemoving에서는 이전 Actor 출력을 먼저 끄고 앱 출력을 켠다. 재시도·오프닝·엔딩·제목·다음 판에서 안정된 출력/Listener가 하나인지 확인한다.

카메라 서비스가 다른 구간 요청을 수락해도 Trigger의 IsTransitioning 조기 반환이 남으면 실제 접촉은 버려진다. 전환 중 다른 실제 OnTriggerEnter와 Brain 완료를 함께 검사한다. DOTween 값을 강제로 끝내는 API 검사는 Cinemachine 블렌드 완료·정지·출력 수명을 증명하지 않는다. HMS 설정 기준은 01_PlayerMovementTest이며 현재 지정 기획의 속도 5m/s·블렌드 0.6초 기본값을 기준 씬의 Quarter 3m/s·0.5초와 구분한다.

## 중복 입력과 물리 참조

Architecture 플레이어에 기존 PlayerController나 CheckInteract를 추가하면 입력이 두 경로로 실행될 수 있다. SessionActorHost는 입력 리더와 라우터가 하나씩인지 검사하고 기존 제어·디버그 입력 컴포넌트를 거절한다. 모든 상호작용은 E, 눈물은 F, 형태 전환은 Q다. 옵션이 위에 있으면 아래 메모 E닫기를 소비하지 않는다.

플레이어 루트 기준으로 Collider를 옮기면 attachedRigidbody.gameObject에서 PlayerFacade를 찾는 센서가 깨질 수 있다. 현재 빈 PlayerRoot와 자식 ActorBody 구성을 기준으로 충돌체와 Facade의 위치를 함께 확인한다. 저장된 PlayerRoot는 비활성 상태이며 첫 활성화 전에 의존성을 준비한다.

## 수집 경로의 구분

Features/TearFragment와 Architecture/Level/LevelTearFragment는 가득 찬 인벤토리에 대한 소비 규칙이 다르다. 클래스 이름이 비슷하다는 이유로 교체하지 않는다. 대상 씬의 m_Script GUID와 연결된 컴포넌트를 확인하고 상한 상태에서 실제 아이템 활성 여부를 검사한다.

## 종료와 트윈 검증

대화 종료 관찰자가 예외를 던져도 DialogueViewModel의 서비스 구독은 finally에서 해제해야 한다. disposed 플래그만 먼저 설정하면 재호출이 조기 반환해 누락된 정리를 복구하지 못한다. 예외 전달을 유지하면서 자기 구독만 정리하고 다른 소비자와 다음 대화의 수명을 보존한다.

Coroutine의 Destroy 요청 직후에는 객체가 같은 프레임에 남아 있을 수 있다. Actor despawn은 한 프레임을 기다리고 종료 오류도 전달한다. 재시도 직후 객체 개수를 검증할 때 정리 완료 시점을 기다린다.

압력판의 트윈은 ManualUpdate로 물리 프레임에서 전진한다. 단순히 끝 위치로 이동시킨 검사는 도중 정지·취소·복귀를 검증하지 못한다. 해당 트윈의 실제 갱신 경로를 사용해 중간 상태와 비활성화 후 정리를 확인한다.

## 기존 메모의 입력 차단 범위

MemoUIController는 주입된 GameplayControl의 월드 정지 토큰을 소유한다. 기존 독립 CheckInteract 구성은 standalone 조립 경로로 같은 계약을 사용한다. 닫기/파괴 시 자기 토큰만 해제한다. bool 전역 해제로 중첩 옵션을 풀지 않는다.

## 보호와 검증 한계

기능 테스트는 사용자의 요구 결과를 기준으로 결합된 기능을 확인한다. 예를 들어 발판의 끝 위치만 맞거나 하위 컴포넌트 검사가 통과해도 탑승·점프·정지 복구가 확인됐다는 뜻이 아니다. 필요한 결합 검증은 자동 기능 검사 또는 팀의 실제 빌드 케이스로 명시하고 각각의 결과를 분리한다. 범주 선정·인계·FunQA 역할은 standards.md의 기능 테스트와 QA 인계를 따른다.

기존 03_1_Stage 디스크를 덮어쓰지 않는다. 이전 미저장 내용은 사용자가 직접 전환하여 보존 여부 미확인이다. 매 Play/build 전 모든 loaded scene clean을 확인하고 사전/사후 setup을 비교한다. TimeManager의 Unity native 형식 정규화는 의미 동일이나 raw hash는 다르다. ProBuilder 기존 외부 변경을 보존한다. Unity가 저장한 빈 m_Name 후행공백으로 전체 diffcheck 실패가 있으므로 코드 검사와 분리한다.

## HMS 폼 계층과 실행 준비

PlayerFormController의 visualRoot가 non-null이어도 바로 아래 NormalVisual/SmallVisual이 없으면 Awake에서 실패한다. 소유 Primitive 외형은 VisualRoot의 두 폼에 배치하고 그 하위에는 Collider/Rigidbody를 넣지 않는다. 빈 루트 일반 예시 때문에 Facade·물리·센서의 기존 공존 계약을 바꾸지 않는다. 기준 씬의 초깃값과 공개 cooldown block reason을 확인하고, Inspector override만 연결됐다는 이유로 Q/F 정상 실행을 추정하지 않는다.

retry의 새 ActorReady 이벤트 직후는 Cinemachine LateUpdate가 아직 끝나지 않을 수 있다. Brain의 실제 활성 카메라·블렌드 완료·HMS IsInitialized와 input lease를 함께 관찰한 뒤 준비 완료를 판단한다. 이전 UI 모델은 유지하고 borrowed 출력 참조만 해제·교체한다. Actor와 네 카메라의 수명을 함께 묶는 이유는 [카메라 수명 결정](tracking/decisions/0004-actor-camera-lifetime.md)에 기록한다.

## Editor teardown과 실행 증거

외부 파일을 메모리 컴파일한 검증에서 JsonUtility는 결과의 문자열은 저장했지만 중첩 관찰·케이스·로그 목록을 누락했다. 완료 상태만으로 판정하지 않고 예정 케이스와 관찰 목록이 실제 JSON에 있는지 확인한다. 이런 일회성 결과는 Newtonsoft.Json으로 DTO를 저장하며 불완전한 첫 증거도 따로 보존한다.

Stop 도중 isPlayingOrWillChangePlaymode가 false여도 Application.isPlaying/EditorApplication.isPlaying이 아직 true일 수 있다. 씬 setup 조회·복원과 aborted-entry 정리는 요청·Editor·실제 runtime Play 및 import/컴파일이 끝난 안정 update에서 수행한다. 이전 startScene/자기 소유 값/외부 변경을 구분하는 복원 계약은 유지한다. 최신 기능 결과와 별개로 전체 Domain Reload의 Unity Hierarchy UI Assertion은 findings의 F10을 확인한다.

실제 전체 동선 검사에서는 InputSystem과 기존 P_CHASE 접촉을 사용한다. API 요청·배치 이동으로 통과한 제어된 카메라/물리 검사를 전체 사용자 동선으로 기록하지 않는다. 탈출 후 ReturningToTitle은 Ending→Title까지 계속 관찰한다.

검증 캡처는 `output/Tech_SYM/verification/editor-entry-hms-2026-10-06/captures/<실행시각>/`에 남겨 과거 이미지와 구분한다. 이전 ready 렌더 폴더 일부 이미지는 앞선 실행에서 덮였으므로 그 이미지를 과거 실행 증거로 재사용하지 않는다. Runtime TMP atlas와 Editor는 자산·설정을 자동 저장할 수 있다. 2026-10-06 후속 지시부터 자동 변경은 기준점·현재 diff·원인을 기록하여 보존하고 커밋에 포함한다. 과거 복원 실행 이력과 현재 보존 정책을 혼합하지 않는다.

## 로컬 오프셋 이관과 실제 클립 없는 Controller

필드 이름만 바꾸면 기존 직렬화 값과 prefab override가 새 설정에 이관되지 않는다. 이번에는 변경 전 42개 월드 기준점을 캡처하고 각 소유 부모/트리거 Transform의 InverseTransformPoint로 이동량을 구해 native API로 저장했다. 재로딩 후 동일 월드 기준점을 비교하고 이전 방향·시간·수동 카메라 필드의 override도 제거했다. 이후 기존 생성 도구를 실행하더라도 같은 로컬 변환을 사용해야 한다.

Controller의 상태 존재와 실제 클립 재생은 별개다. 빈 Collapse Motion은 연결 대기 상태이며 물리 붕괴·Warning으로 처리한다. 현재/다음 Animator 상태의 실제 클립을 관찰하므로 매우 짧은 클립과 전환 직전도 고려한다. 재생 완료 후 자기 Animator를 멈추지 않으면 클립의 Renderer 활성/Transform 곡선이 숨김·복원을 다시 덮을 수 있다. 풀 복구 때 모든 Visual 자식의 기준 배치와 렌더 상태를 복원하고 실제 클립 연결 후 이 경로를 다시 확인한다.

MultiFunc의 예전 Main Camera 객체는 비활성 배치 참조로 남고 Camera/AudioListener도 disabled다. 이름이나 객체 수 대신 활성 HMS output/Brain/listener가 하나인지 검사한다.
