# Inspector 연결 전환 명세

현재 SYM에서 자동 추출한 직렬화 필드 201개를 모두 분류했다. 전체 Core는 300개이며 추출은 정규식 기반이다. 다중 줄 속성·public 직렬화·중첩 상속 등 추가 필드는 실행 단계에서 실제 SerializedObject로 대조한다. Tooltip 누락 자동 표시는 확정 오류가 아니며 문자열 연결로 작성된 Tooltip은 재확인한다.

## 사람이 설정하는 규칙

위치/조작부/경로/끝점/카메라 기준은 실제 Transform을 드래그한다. 현재 생성된 Actor는 Host가 실제 인스턴스 참조를 연결한다. 사람이 이미 존재하지 않는 Actor를 Scene asset에 연결하려 하지 않는다. 설정 자산·프리팹·Renderer·Collider·화면·버튼은 해당 실물 참조를 드래그한다.

속도 m/s, 높이 m, 시간 초, 방향/모드, 음량, 횟수는 목적이 분명한 수치/선택으로 남긴다. 세계 좌표 저장값, 변위 계산 결과, Physics API 인자, 외부 테이블의 내부 코드는 런타임 값으로 허용한다. Inspector에서 사람이 목표 좌표나 숨은 키를 입력하지 않게 하는 것이 요구다.

한국어 라벨을 각 필드에 지정하고 Tooltip은 “무엇을 연결/조절하는가”를 첫 문장으로 쓴다. 단위·좌표계·0·Gizmo 색은 해당 설정에 필요할 때만 짧게 덧붙인다. 모든 항목에 긴 공통 설명을 복제하지 않는다. enum용 InspectorName을 일반 필드 라벨에 잘못 사용하지 않는다. SYM 전용 필드 라벨/참조 선택 PropertyDrawer가 필요하면 한 편집 어댑터에서 처리하며 보호 컴포넌트의 전역 Inspector를 덮어쓰지 않는다.

예: `이동 속도` — “발판의 이동 속도입니다(m/s). 0이면 이동하지 않습니다.”
예: `추격 경로 지점` — “추격 경로에 배치한 빈 오브젝트를 진행 순서대로 연결하세요.”
예: `체크포인트 지점` — “이 Trigger에 대응하는 CheckpointMarker를 연결하세요. 실제 저장 위치는 접촉한 플레이어의 현재 위치입니다.”

## 여러 씬/외부 데이터의 키 제거

ArchitectureInstallation의 Bootstrap/Title/Core/Stage는 SceneAsset를 드래그하고 런타임 경로는 편집/빌드 경계가 숨은 값으로 생성·검증한다. Player 빌드에서 UnityEditor API를 호출하지 않는다.
StageService의 초기 위치는 같은 씬 CheckpointMarker를 드래그한다. IntegrationEntrySettings는 SceneAsset+조각+초기 해금만 작성하고 해당 Stage의 초기 Marker를 사용한다. 따라서 ScriptableObject가 Scene의 Marker 참조를 영속 저장하는 잘못된 cross-scene 연결을 만들지 않는다.
StageTrigger는 Kind에 맞는 CheckpointMarker/AbilityId/대화 선택/퇴로 등 참조만 표시한다. 이번 프로토타입에는 다음 Stage 자동 연결 필드를 추가하지 않는다. 대화는 이미 연결한 TextAsset 테이블에서 검증된 Code를 목록으로 선택한다. 원본 JSON의 의미 있는 Code 자체는 바꾸거나 중복 대화 자산으로 복제하지 않는다.
AudioCueCatalog는 소리 용도 선택+AudioClip 참조로 작성한다. Stage BGM은 Stage 설정의 AudioClip/용도 연결로 작성하고 씬 이름 문자열을 코드 dictionary에 적는 경로를 제거한다. 내부 매핑 키는 편집기가 생성하며 사람이 입력하지 않는다.

## 현재 필드 전수 표

| 현재 위치 | 현재 타입/필드 | 새 표시 이름 | 최종 처리 |
|---|---|---|---|
| `Assets/_Code/Core/Tech_SYM/Architecture/Audio/AudioCueCatalog.cs:13` | `string id` | 소리 용도 | 검증된 용도 선택; 내부 키는 자동 생성 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Audio/AudioCueCatalog.cs:15` | `AudioCueKind kind` | 종류 | 의미 있는 한국어 선택 목록/flags; 기존 enum 직렬화 번호 보존 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Audio/AudioCueCatalog.cs:17` | `AudioClip clip` | 재생 파일 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Audio/AudioCueCatalog.cs:19` | `float gain` | 기본 음량 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Audio/AudioCueCatalog.cs:28` | `Entry[] entries` | 항목 목록 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Audio/AudioRuntimeHost.cs:11` | `AudioCueCatalog catalog` | 설정 자산 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Audio/AudioRuntimeHost.cs:13` | `AudioSource[] sources` | 소리 재생기 목록 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Chase/ChaseDefinition.cs:8` | `float speed` | 이동 속도 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Chase/ChaseDefinition.cs:10` | `float captureGap` | 포획 거리 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Chase/ChaseRuntimeDriver.cs:14` | `ChaseDefinition definition` | 설정 자산 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Chase/ChaseRuntimeDriver.cs:16` | `ChaseSwarmPresentation presentation` | 군집 외형 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Chase/ChaseSwarmPresentation.cs:15` | `GameObject visual` | 외형 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Chase/ChaseSwarmPresentation.cs:17` | `float appearSeconds` | 등장 시간 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Chase/ChaseSwarmPresentation.cs:19` | `float exitSeconds` | 퇴장 시간 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Chase/ChaseSwarmPresentation.cs:21` | `float bobHalfPeriod` | 흔들림 반주기 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Chase/ChaseSwarmPresentation.cs:23` | `float bobHeight` | 상하 이동 높이 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Cinematics/CinematicCatalog.cs:12` | `Texture2D artwork` | 원화 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Cinematics/CinematicCatalog.cs:14` | `string caption` | 표시 문구 | 본문 문구는 유지; 대상 키인지 실제 문구인지 실행 단계 실제 소비 대조 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Cinematics/CinematicCatalog.cs:16` | `float durationSeconds` | 표시 시간 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Cinematics/CinematicCatalog.cs:46` | `CinematicCut[] openingCuts` | 오프닝 컷 목록 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Cinematics/CinematicCatalog.cs:48` | `CinematicCut[] endingCuts` | 엔딩 컷 목록 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Cinematics/CinematicHost.cs:11` | `CinematicCatalog catalog` | 설정 자산 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Cinematics/CinematicHost.cs:13` | `CinematicView view` | 화면 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Cinematics/CinematicView.cs:10` | `CanvasGroup visibility` | 화면 표시 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Cinematics/CinematicView.cs:12` | `RectTransform artworkViewport` | 원화 영역 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Cinematics/CinematicView.cs:14` | `RawImage artwork` | 원화 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Cinematics/CinematicView.cs:16` | `TMP_Text caption` | 표시 문구 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Cinematics/CinematicView.cs:18` | `Button skipButton` | 전체 스킵 버튼 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Dialogue/DialogueView.cs:15` | `CanvasGroup rootGroup` | 대화 화면 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Dialogue/DialogueView.cs:17` | `CanvasGroup logGroup` | 로그 화면 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Dialogue/DialogueView.cs:19` | `TMP_Text speaker` | 화자 이름 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Dialogue/DialogueView.cs:21` | `TMP_Text body` | 본문/몸체 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Dialogue/DialogueView.cs:23` | `TMP_Text autoLabel` | 자동 진행 표시 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Dialogue/DialogueView.cs:25` | `TMP_Text history` | 로그 본문 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Dialogue/DialogueView.cs:27` | `GameObject nextMarker` | 다음 문장 표시 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Dialogue/DialogueView.cs:29` | `UnityEngine.UI.Button panelButton` | 대화창 버튼 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Dialogue/DialogueView.cs:31` | `UnityEngine.UI.Button autoButton` | 자동 진행 버튼 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Dialogue/DialogueView.cs:33` | `UnityEngine.UI.Button historyButton` | 로그 열기 버튼 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Dialogue/DialogueView.cs:35` | `UnityEngine.UI.Button logCloseButton` | 로그 닫기 버튼 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Dialogue/DialogueView.cs:37` | `UnityEngine.UI.ScrollRect logScroll` | 로그 스크롤 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Foundation/ArchitectureInstallation.cs:11` | `string bootstrapPath` | Bootstrap 씬 | SceneAsset/SceneAsset[] 드래그; 경로는 숨은 런타임 생성 값 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Foundation/ArchitectureInstallation.cs:13` | `string titlePath` | Title 씬 | SceneAsset/SceneAsset[] 드래그; 경로는 숨은 런타임 생성 값 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Foundation/ArchitectureInstallation.cs:15` | `string corePath` | Core 씬 | SceneAsset/SceneAsset[] 드래그; 경로는 숨은 런타임 생성 값 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Foundation/ArchitectureInstallation.cs:17` | `string[] stagePaths` | 스테이지 씬 순서 | SceneAsset/SceneAsset[] 드래그; 경로는 숨은 런타임 생성 값 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Foundation/Bootstrapper.cs:10` | `TextAsset dialogueTable` | 대화 표 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/AppComposition.cs:17` | `Bootstrapper bootstrap` | 앱 시작 담당 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/AppComposition.cs:19` | `ArchitectureInstallation installation` | 씬 목록 설정 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/AppComposition.cs:21` | `GlobalUiRoot ui` | 게임 화면 연결 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/AppComposition.cs:23` | `AudioRuntimeHost audio` | 오디오 연결 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/AppComposition.cs:25` | `Camera presentationCamera` | 출력 카메라 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/CoreComposition.cs:17` | `SessionActorHost actor` | 플레이어 생성 담당/현재 플레이어 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/CoreComposition.cs:19` | `DialogueView dialogueView` | 대화 화면 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/CoreComposition.cs:21` | `CinematicHost cinematic` | 연출 연결 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/CoreComposition.cs:23` | `IntegrationEntrySettings entries` | 항목 목록 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/CoreComposition.cs:25` | `CoreUiBinding ui` | 게임 화면 연결 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/CoreComposition.cs:27` | `LevelCameraService cameraService` | 카메라 전환 담당 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/CoreComposition.cs:29` | `ChaseRuntimeDriver chase` | 추격 담당 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/CoreComposition.cs:31` | `CinemachineCamera sideCamera` | Side 카메라 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/CoreComposition.cs:33` | `CinemachineCamera quarterCamera` | Quarter 카메라 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/CoreUiBinding.cs:10` | `GameUiRoot ui` | 게임 화면 연결 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/CoreUiBinding.cs:12` | `WorldPromptView independentPrompt` | 독립 씬 F 안내 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/IntegrationEntrySettings.cs:15` | `string stageId` | 진입 스테이지 | SceneAsset 드래그; 내부 Stage 식별은 등록 씬에서 파생 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/IntegrationEntrySettings.cs:17` | `string checkpointId` | 체크포인트 지점 | 입력 제거; 선택 Stage의 initialCheckpoint 참조 사용 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/IntegrationEntrySettings.cs:19` | `int fragments` | 초기 눈물 조각 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/IntegrationEntrySettings.cs:21` | `AbilityId[] abilities` | 초기 해금 능력 | 의미 있는 한국어 선택 목록/flags; 기존 enum 직렬화 번호 보존 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/IntegrationEntrySettings.cs:43` | `Entry[] entries` | 항목 목록 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/CameraZoneTrigger.cs:8` | `CameraZoneData zone` | 카메라 구간 | 현재는 CameraZoneData 값 struct. Trigger의 모드/AlignmentPoint와 Marker의 실제 Trigger 참조로 교체 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/ChaseRetreatGate.cs:8` | `BoxCollider blocker` | 퇴로 차단 Collider | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/CheckpointMarker.cs:6` | `string checkpointId` | 체크포인트 지점 | 실물 Transform/Marker/종류별 참조로 교체; 기존 수치·키는 이관 후 제거 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/CheckpointMarker.cs:8` | `CameraZoneData cameraZone` | 시작 카메라 구간 | 현재는 CameraZoneData 값 struct. Trigger의 모드/AlignmentPoint와 Marker의 실제 Trigger 참조로 교체 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/CheckpointMarker.cs:11` | `Vector3 bodyOffset` | 재등장 지점 | 실물 Transform/Marker/종류별 참조로 교체; 기존 수치·키는 이관 후 제거 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/LevelCameraService.cs:12` | `string Id` | 구간 식별(자동) | 수동 입력 제거; 실제 씬/구간 참조로 식별, 내부 ID만 생성 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/LevelCameraService.cs:14` | `CameraMovementMode Movement` | 카메라 모드 | 의미 있는 한국어 선택 목록/flags; 기존 enum 직렬화 번호 보존 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/LevelCameraService.cs:16` | `Vector3 AlignmentOffset` | Side 정렬 지점 | 실물 Transform/Marker/종류별 참조로 교체; 기존 수치·키는 이관 후 제거 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/LevelCameraService.cs:38` | `float blendSeconds` | 카메라 전환 시간 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/StageService.cs:16` | `string stageId` | 진입 스테이지 | 수동 입력 제거; 실제 씬/구간 참조로 식별, 내부 ID만 생성 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/StageService.cs:18` | `string initialCheckpointId` | 초기 등장 체크포인트 | 실물 Transform/Marker/종류별 참조로 교체; 기존 수치·키는 이관 후 제거 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/StageService.cs:20` | `CheckpointMarker[] checkpoints` | 체크포인트 목록 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/StageService.cs:22` | `EnemyActor[] enemies` | 일반 적 목록 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/StageService.cs:24` | `Vector3[] chaseWaypoints` | 추격 경로 지점 | 실물 Transform/Marker/종류별 참조로 교체; 기존 수치·키는 이관 후 제거 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/StageService.cs:26` | `ChaseRetreatGate chaseRetreatGate` | 추격 퇴로 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/StageService.cs:28` | `float chaseStartProgress` | 추격 시작 지점 | 실물 Transform/Marker/종류별 참조로 교체; 기존 수치·키는 이관 후 제거 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/StageService.cs:30` | `Vector3[] progressWaypoints` | 진행 경로 지점 | 실물 Transform/Marker/종류별 참조로 교체; 기존 수치·키는 이관 후 제거 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/StageTrigger.cs:9` | `StageService stage` | 소속 스테이지 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/StageTrigger.cs:11` | `StageTriggerKind kind` | 종류 | 의미 있는 한국어 선택 목록/flags; 기존 enum 직렬화 번호 보존 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/StageTrigger.cs:13` | `string value` | 종류별 연결 대상 | 실물 Transform/Marker/종류별 참조로 교체; 기존 수치·키는 이관 후 제거 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/StageTrigger.cs:15` | `bool once` | 한 시도에 한 번 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Session/ActorFallGuard.cs:8` | `PlayerFacade actor` | 플레이어 생성 담당/현재 플레이어 | 해당 컴포넌트 삭제 전 새 소유자의 같은 역할 필드로 이관 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Session/ActorFallGuard.cs:10` | `float killHeight` | 데드존 범위 | 해당 컴포넌트 삭제 전 새 소유자의 같은 역할 필드로 이관 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Session/PlayerDamagePresentation.cs:11` | `PlayerFacade player` | 실제 플레이어 몸체 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Session/PlayerDamagePresentation.cs:13` | `Renderer[] targets` | 피격 외형 Renderer | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Session/PlayerDamagePresentation.cs:15` | `float duration` | 표시 시간 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Session/PlayerDamagePresentation.cs:17` | `Color damageColor` | 피격 표시 색 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Session/SessionActorHost.cs:16` | `GameObject actorPrefab` | 생성할 플레이어 프리팹 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Session/SessionActorHost.cs:18` | `GameObject independentActor` | 독립 씬 플레이어 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Session/SessionActorHost.cs:20` | `CoreUiBinding independentUi` | 독립 씬 화면 연결 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Session/SessionActorHost.cs:22` | `MemoUIController[] independentMemos` | 독립 씬 메모 목록 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Session/SessionActorHost.cs:24` | `Camera independentCamera` | 독립 씬 출력 카메라 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Composition/GameUiRoot.cs:7` | `HudView hud` | HUD 화면 | 해당 컴포넌트 삭제 전 새 소유자의 같은 역할 필드로 이관 |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Composition/GameUiRoot.cs:9` | `WorldPromptView prompt` | F 안내 화면 | 해당 컴포넌트 삭제 전 새 소유자의 같은 역할 필드로 이관 |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Composition/GameUiRoot.cs:11` | `Canvas worldCanvas` | 월드 안내 Canvas | 해당 컴포넌트 삭제 전 새 소유자의 같은 역할 필드로 이관 |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Composition/GameUiRoot.cs:13` | `RectTransform dialogueMount` | 대화 배치 영역 | 해당 컴포넌트 삭제 전 새 소유자의 같은 역할 필드로 이관 |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Composition/GameUiRoot.cs:15` | `RectTransform cinematicMount` | 연출 배치 영역 | 해당 컴포넌트 삭제 전 새 소유자의 같은 역할 필드로 이관 |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Composition/GlobalUiRoot.cs:7` | `OptionsView optionsView` | 옵션 화면 | 해당 컴포넌트 삭제 전 새 소유자의 같은 역할 필드로 이관 |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Composition/GlobalUiRoot.cs:9` | `FadeView fadeView` | 페이드 화면 | 해당 컴포넌트 삭제 전 새 소유자의 같은 역할 필드로 이관 |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Composition/TitleUiRoot.cs:7` | `TitleView view` | 화면 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/FadeView.cs:6` | `CanvasGroup cover` | 화면 덮기 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/HudIconGraphic.cs:11` | `Symbol symbol` | 하트 모양 | 해당 컴포넌트 삭제 전 새 소유자의 같은 역할 필드로 이관 |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/HudIconGraphic.cs:13` | `float fillAmount` | 하트 채움 비율 | 해당 컴포넌트 삭제 전 새 소유자의 같은 역할 필드로 이관 |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/HudView.cs:7` | `CanvasGroup group` | 표시 그룹 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/HudView.cs:9` | `TMP_Text health` | 이전 체력 문자열 | 중복/폐기 입력 제거; 공통 원본·새 UI 표시로 소비 이관 |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/HudView.cs:11` | `TMP_Text fragments` | 초기 눈물 조각 | 중복/폐기 입력 제거; 공통 원본·새 UI 표시로 소비 이관 |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/HudView.cs:13` | `TMP_Text abilities` | 초기 해금 능력 | 중복/폐기 입력 제거; 공통 원본·새 UI 표시로 소비 이관 |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/HudView.cs:15` | `HudIconGraphic[] hearts` | 하트 채움 Image 목록 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/HudView.cs:17` | `CanvasGroup formCard` | 크기 전환 아이콘 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/HudView.cs:19` | `CanvasGroup tearCard` | 눈물 아이콘 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/HudView.cs:21` | `TMP_Text fragmentCount` | 눈물 조각 표시 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/OptionsView.cs:8` | `CanvasGroup modal` | 옵션 표시 그룹 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/OptionsView.cs:11` | `UnityEngine.UI.Button close` | 닫기 버튼 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/OptionsView.cs:13` | `UnityEngine.UI.Button title` | 타이틀 복귀 버튼 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/OptionsView.cs:15` | `UnityEngine.UI.Slider master` | 전체 음량 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/OptionsView.cs:17` | `UnityEngine.UI.Slider music` | 음악 음량 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/OptionsView.cs:19` | `UnityEngine.UI.Slider sfx` | 효과음 음량 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/OptionsView.cs:21` | `TMP_Text values` | 음량 수치 표시 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/OptionsView.cs:23` | `TMP_Text error` | 저장 오류 표시 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/TitleView.cs:6` | `UnityEngine.UI.Button newGame` | 새 게임 버튼 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/TitleView.cs:8` | `UnityEngine.UI.Button quit` | 종료 버튼 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/TitleView.cs:10` | `UnityEngine.UI.Button settings` | 옵션 버튼 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/WorldPromptView.cs:7` | `CanvasGroup group` | 표시 그룹 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/WorldPromptView.cs:9` | `RectTransform anchor` | 월드 안내 표시 RectTransform | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/WorldPromptView.cs:11` | `TMP_Text label` | F 문구 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Enemies/Combat/EnemyContactSensor.cs:8` | `EnemyActor owner` | 소유 일반 적 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Enemies/Data/EnemyDefinition.cs:12` | `EnemyKind kind` | 종류 | 의미 있는 한국어 선택 목록/flags; 기존 enum 직렬화 번호 보존 |
| `Assets/_Code/Core/Tech_SYM/Enemies/Data/EnemyDefinition.cs:15` | `float speed` | 이동 속도 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Enemies/Data/EnemyDefinition.cs:17` | `float initialIdle` | 첫 대기 시간 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Enemies/Data/EnemyDefinition.cs:19` | `float knockbackSpeed` | 이전 개별 넉백 세기 | 중복/폐기 입력 제거; 공통 원본·새 UI 표시로 소비 이관 |
| `Assets/_Code/Core/Tech_SYM/Enemies/Runtime/EnemyActor.cs:9` | `EnemyDefinition definition` | 설정 자산 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Enemies/Runtime/EnemyActor.cs:12` | `BoxCollider body` | 본문/몸체 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Enemies/Runtime/EnemyActor.cs:14` | `PlayerFacade target` | 현재 플레이어(자동 연결) | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Enemies/Runtime/EnemyActor.cs:16` | `PatrolDirection initialDirection` | 첫 이동 방향 | 의미 있는 한국어 선택 목록/flags; 기존 enum 직렬화 번호 보존 |
| `Assets/_Code/Core/Tech_SYM/Enemies/Runtime/EnemyActor.cs:19` | `float patrolDistance` | 순찰 끝 지점 | 실물 Transform/Marker/종류별 참조로 교체; 기존 수치·키는 이관 후 제거 |
| `Assets/_Code/Core/Tech_SYM/Enemies/Runtime/EnemyActor.cs:21` | `LayerMask terrainMask` | 지형 레이어 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Enemies/Runtime/EnemyActor.cs:24` | `float heightSearchDistance` | 지형 검색 거리 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Enemies/Runtime/EnemyActor.cs:26` | `float surfaceClearance` | 지면 간격 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Enemies/Runtime/EnemyActor.cs:28` | `float manualLowerDistance` | 순찰 시작 지점 | 실물 Transform/Marker/종류별 참조로 교체; 기존 수치·키는 이관 후 제거 |
| `Assets/_Code/Core/Tech_SYM/Enemies/Runtime/EnemyActor.cs:30` | `float manualUpperDistance` | 순찰 끝 지점 | 실물 Transform/Marker/종류별 참조로 교체; 기존 수치·키는 이관 후 제거 |
| `Assets/_Code/Core/Tech_SYM/Features/ShatteredPlatform.cs:12` | `Transform visual` | 외형 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Features/ShatteredPlatform.cs:14` | `Collider supportCollider` | 실제 지지 Collider | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Features/ShatteredPlatform.cs:16` | `Animator collapseAnimator` | 붕괴 Animator | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Features/ShatteredPlatform.cs:18` | `PlatformContacts physicalContacts` | 물리 접촉 수집기 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Features/ShatteredPlatform.cs:20` | `float warningEndSeconds` | 붕괴 경고 시간 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Features/ShatteredPlatform.cs:23` | `float shakeAngleDegrees` | 외형 흔들림 각도 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Features/ShatteredPlatform.cs:26` | `float shakeFrequency` | 외형 흔들림 빈도 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Features/TearFragment.cs:9` | `GameObject pickupRoot` | 눈물 전체 루트 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Features/TearFragment.cs:11` | `Transform presentationVisual` | 눈물 외형 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Features/TearFragment.cs:13` | `float rotationDegreesPerSecond` | 회전 속도 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Features/TearFragment.cs:15` | `float bobHeight` | 상하 이동 높이 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Features/TearFragment.cs:17` | `float bobSeconds` | 상하 이동 시간 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Interaction/InteractionPromptAnchor.cs:8` | `Vector3 localOffset` | F 표시 지점 | 실물 Transform/Marker/종류별 참조로 교체; 기존 수치·키는 이관 후 제거 |
| `Assets/_Code/Core/Tech_SYM/Interaction/InteractionPromptAnchor.cs:10` | `Vector3 interactionLocalOffset` | 실제 조작 지점 | 실물 Transform/Marker/종류별 참조로 교체; 기존 수치·키는 이관 후 제거 |
| `Assets/_Code/Core/Tech_SYM/Interaction/MemoModel.cs:8` | `int memoID` | 메모 식별(자동) | 수동 번호 입력 제거; 실제 MemoModel 참조/내부 자동 식별 |
| `Assets/_Code/Core/Tech_SYM/Interaction/MemoModel.cs:10` | `Sprite memoImage` | 메모 이미지 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Interaction/MemoObject.cs:8` | `MemoModel memoModel` | 메모 데이터 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Interaction/MemoObject.cs:10` | `MemoUIController memoUI` | 메모 화면 담당 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Interaction/MemoUIController.cs:11` | `RectTransform screenRoot` | 메모 배치 영역 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Interaction/MemoUIController.cs:13` | `MemoView viewPrefab` | 메모 화면 프리팹 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Interaction/MemoView.cs:7` | `UnityEngine.UI.Image memoImage` | 메모 이미지 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Interaction/MemoView.cs:9` | `UIScaleTween scaleTween` | 이전 별도 확대 컴포넌트 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Interaction/UIScaleTween.cs:10` | `float duration` | 표시 시간 | 해당 컴포넌트 삭제 전 새 소유자의 같은 역할 필드로 이관 |
| `Assets/_Code/Core/Tech_SYM/OvenTray/OvenTrayObject.cs:16` | `OperationMode operationMode` | 장치 이동 방식 | 의미 있는 한국어 선택 목록/flags; 기존 enum 직렬화 번호 보존 |
| `Assets/_Code/Core/Tech_SYM/OvenTray/OvenTrayObject.cs:18` | `Vector3 travelOffset` | 이동 목표 지점 | 실물 Transform/Marker/종류별 참조로 교체; 기존 수치·키는 이관 후 제거 |
| `Assets/_Code/Core/Tech_SYM/OvenTray/OvenTrayObject.cs:20` | `Vector3 hingeEulerOffset` | 열림 자세 지점 | 실물 Transform/Marker/종류별 참조로 교체; 기존 수치·키는 이관 후 제거 |
| `Assets/_Code/Core/Tech_SYM/OvenTray/OvenTrayObject.cs:22` | `float moveDuration` | 장치 이동 시간 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Platforms/DamageFloorPlatform.cs:10` | `PlatformContacts physicalContacts` | 물리 접촉 수집기 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Platforms/DamageFloorPlatform.cs:12` | `float knockbackSpeed` | 이전 개별 넉백 세기 | 중복/폐기 입력 제거; 공통 원본·새 UI 표시로 소비 이관 |
| `Assets/_Code/Core/Tech_SYM/Platforms/FloatingPlatform.cs:13` | `float landingDepth` | 착지 눌림 깊이 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Platforms/FloatingPlatform.cs:16` | `float actionSeconds` | 눌림 시간 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Platforms/FloatingPlatform.cs:19` | `Ease actionEase` | 눌림 곡선 | 의미 있는 한국어 선택 목록/flags; 기존 enum 직렬화 번호 보존 |
| `Assets/_Code/Core/Tech_SYM/Platforms/FloatingPlatform.cs:21` | `float overshootHeight` | 반동 높이 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Platforms/FloatingPlatform.cs:24` | `float reactionSeconds` | 반동 시간 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Platforms/FloatingPlatform.cs:27` | `Ease reactionEase` | 반동 곡선 | 의미 있는 한국어 선택 목록/flags; 기존 enum 직렬화 번호 보존 |
| `Assets/_Code/Core/Tech_SYM/Platforms/FloatingPlatform.cs:30` | `float settleSeconds` | 안착 시간 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Platforms/FloatingPlatform.cs:33` | `Ease settleEase` | 안착 곡선 | 의미 있는 한국어 선택 목록/flags; 기존 enum 직렬화 번호 보존 |
| `Assets/_Code/Core/Tech_SYM/Platforms/FloatingPlatform.cs:35` | `float rearmGap` | 재접촉 분리 거리 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Platforms/KinematicPlatformMotion.cs:12` | `Rigidbody physicsBody` | 이동 Rigidbody | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Platforms/KinematicPlatformMotion.cs:14` | `PlatformContacts platformContacts` | 지지 접촉 수집기 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Platforms/LinearShuttlePlatform.cs:13` | `Vector3 travelOffset` | 이동 목표 지점 | 실물 Transform/Marker/종류별 참조로 교체; 기존 수치·키는 이관 후 제거 |
| `Assets/_Code/Core/Tech_SYM/Platforms/LinearShuttlePlatform.cs:16` | `float moveSpeed` | 이동 속도 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Platforms/PeriodicPlatform.cs:14` | `BoxCollider supportCollider` | 실제 지지 Collider | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Platforms/PeriodicPlatform.cs:16` | `float lifetime` | 표시 유지 시간 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Platforms/PeriodicPlatform.cs:18` | `float hiddenSeconds` | 소멸 유지 시간 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Platforms/PeriodicPlatform.cs:20` | `float warningSeconds` | 형성 시간 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Platforms/PeriodicPlatform.cs:22` | `float initialPhaseSeconds` | 시작 위상 시간 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Platforms/PeriodicPlatform.cs:24` | `int blinkCount` | 형성 깜빡임 횟수 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Platforms/PeriodicPlatform.cs:26` | `float blinkAcceleration` | 깜빡임 가속 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Platforms/PressureLinkedPlatform.cs:11` | `PressurePlatePlatform pressurePlate` | 연결 압력판 | 실제 컴포넌트/자산 드래그 유지; null/다른 씬/중복 연결은 편집 경계 검증 |
| `Assets/_Code/Core/Tech_SYM/Platforms/PressureLinkedPlatform.cs:14` | `Vector3 travelOffset` | 이동 목표 지점 | 실물 Transform/Marker/종류별 참조로 교체; 기존 수치·키는 이관 후 제거 |
| `Assets/_Code/Core/Tech_SYM/Platforms/PressureLinkedPlatform.cs:17` | `float returnSpeed` | 복귀 속도 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Platforms/PressurePlatePlatform.cs:13` | `float pressDepth` | 압력판 눌림 깊이 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Platforms/PressurePlatePlatform.cs:16` | `float travelSeconds` | 압력판 이동 시간 | 직관적인 조절 값 유지; 단위/허용 범위/0 의미 설명. 목표 지점 수치로 악용하지 않음 |
| `Assets/_Code/Core/Tech_SYM/Platforms/PressurePlatePlatform.cs:18` | `PressureLinkedPlatform linkedPlatform` | 연결 발판 | 중복/폐기 입력 제거; 공통 원본·새 UI 표시로 소비 이관 |

## 추가로 생기는 필수 연결

StageService의 시작/끝 Marker, 추격/진행 Transform 배열·추격 시작 Transform; CheckpointMarker의 실제 respawnPoint Transform; 카메라 구간의 alignmentPoint Transform; LinearShuttlePlatform의 startPoint/endPoint; PressureLinkedPlatform의 startPoint/targetPoint; OvenTrayObject의 closedPose/openPose; EnemyActor의 patrolStart/patrolEnd; InteractionPromptAnchor의 promptPoint/interactionPoint를 추가한다. 같은 부모로 이동할 기준점인지 월드에 고정할 기준점인지 제작 가이드에 명시한다.
PlayerDamagePresentation에 실제 visualRoot/cameraAnchor 참조를 추가하고 PlayerBridge/CoreWorldBinding의 VisualRoot/Visual/CameraAnchor 문자열 검색을 제거한다. 껍질 데드존 복귀 대상은 현재 PlayerFacade가 있는 실제 ActorBody다. 별도 저장된 위치나 고정된 씬 Marker로 돌아가지 않는다.
DialogueView에는 skipButton을 추가한다. CoreUiBinding은 GameUiRoot의 HUD/Prompt/Canvas/mount를 직접 연결하고 AppComposition은 GlobalUiRoot의 OptionsView/FadeView를 직접 연결한다. HP나 해금 bool을 Inspector에 복제하지 않는다.
주기별 lifetime/hiddenSeconds/warningSeconds는 5/2/3초를 초기값으로 사용하되 사람이 수정한 수치가 실제 상태 시계에 반영되어야 한다. 강제 OnValidate 재기입은 제거한다. 다른 이동/경고 시간도 조절 값으로 표시한 경우 실제 실행 수치와 하나로 맞춘다.

HMS 원본 필드는 명시된 우선순위 두 파일 외에 바꾸지 않는다. 필요 값은 소유 프리팹에서 기존 컴포넌트 설정으로 작성하고, 이번에 바꾸지 못하는 보호 Inspector 설명은 제작 가이드의 대응표로 설명한다. 전체 Inspector가 이미 수정되었다고 보고하지 않는다.
