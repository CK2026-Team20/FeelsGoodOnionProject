# 시스템 책임과 수명

> 아래 구조·API 설명은 현재 구현 사실이다. V1.8 요구를 이미 구현했다는 뜻이 아니다. 컷신 복귀·타이틀·카메라·추격 차이는 [구조 차이 목록](tracking/system-v18-2026-10-29/구조%20차이%20목록.md)을 따른다.

> 현행 시스템 권위는 V1.8 활성 본문 → 이전 사용자 기능 명세 → 기존 구현 설명 순서다. 지정 원본·예외는 [planning-index](planning-index.md)를 따른다. 구현과 검증 상태는 tracking/status.md 및 실제 증거로 확인하며 문서 개정만으로 완료를 선언하지 않는다.

Unity6000.6.0f1의 로컬 플레이 게임이다. 시스템 목표는 V1.8이며 현재 구현과 구별한다. 현재 실행 등록은 01_Stage_Prototype 하나다. 2026-10-15 직접 지시로 제공 레벨 목업과 `_Prefabs` 실제 크기에 따라 새 씬의 지형·원경·기능을 배치하고 구 Prototype/1_Stage를 폐기했다. 서버·네트워크·디스크 진행 저장은 없다. 기존 Core와 등록 Stage의 씬 수명, 한 판 서비스 수명, 현재 Actor 수명을 구분한다.

## 상태 원본과 실행 책임

| 범위 | 단일 원본/소유자 | 생성과 종료 |
|---|---|---|
| 앱 | AppComposition·GameFlowService·SceneService·Audio/Settings | AppComposition이 앱 scope와 시작·종료를 소유. 출력 Camera/Brain/Listener 각1개와 Options/Fade/Audio를 연결 |
| Core 씬 | CoreComposition·CoreUiBinding·Side/Corridor/Quarter 리그 | 새 게임에서 로드. Retry는 유지, Title 복귀에서 세션 종료 후 언로드 |
| 한 판 | GameSessionRuntime·GameSessionService·CoreWorldBinding·Dialogue·VM | App 부모의 새 scope. Retry는 유지, Title은 Release/Dispose, 다음 새 게임은 새 범위 |
| Stage | StageService와 각 발판/장치/적/수집 owner | 현재 01_Stage_Prototype 하나만 additive 로드. Retry는 유지·초기 재준비, Title에서는 모두 언로드 |
| Actor | SessionActorHost / HMS Facade·Form·Movement·Model | 비활성 Player prefab을 생성해 의존 연결 후 활성화. 교체 전에 빌린 참조·자기 구독·작업 해제 |
| 입력/제한 | HMS Reader·PlayerController / Host의 F / GameplayControlService | 실제 입력1개 경로. Options/메모/대화가 자기 토큰만 반환 |
| 화면 | ViewModel 상태·Command / View | View는 입력/표현만. VM은 View 참조 없음. 재Bind에서 자기 구독 해제 |
| 외부 장치/작업 | Scene/Audio/Settings/DOTween/Animator 어댑터 | 만든 작업·범위만 취소/해제. 빌린 Actor·Camera·Source를 임의 파괴하지 않음 |

HP·현재 조각·폼·운동은 HMS가 소유한다. StageService가 보유하는 마지막 CP는 현재 Player의 복제 원본이 아니라 저장 시점의 Position/Fragments/해금 마스크 불변값이다. 현재 해금의 원본은 GameSessionService, 일반 대화 완료 장부는 DialogueService다. UI가 별도 해금/HP bool을 진실로 유지하지 않는다.

Foundation/Bootstrapper.cs의 startup/shutdown 책임은 AppComposition으로 통합했고 해당 Bootstrapper 소스를 제거했다. App.cs라는 소스는 없다. Bootstrapper 씬 이름은 삭제한 클래스와 구별한다.

## 조립자와 로케이터

ServiceLocator는 Type 등록/조회·부모 조회·scope 소유·역순 Dispose만 한다. App/Core 조립자는 실제 구체 서비스 생성·Unity 오브젝트 참조·초기화 순서와 부분 실패 회수를 맡는다. 소비자는 작은 계약을 명시적으로 주입받는다. 로케이터가 Camera/물리/UI·게임 상태를 모두 생성하는 거대 전역 객체가 되지 않는다. 같은 인스턴스 별칭은 한 번만 해제하고 부모 소유 객체를 자식 scope가 재소유하지 않는다.

CoreWorldBinding은 Actor/Stage 교체 때 실제 재바인딩·카메라 초기 준비·추격 저장·최종 대화 예약/해제를 조정한다. 단순 전달과 다르다. 같은 수명 참조만 전달하던 GameUiRoot/GlobalUiRoot·범용 버스·표시 래퍼는 원본 owner로 합쳤다. 과거 V1.5 정리에서 자동 Builder12개와 중복 runtime/계약10개를 삭제했다. 이는 당시 범위의 기록이며 현재 소스 수량 변화와 구별한다. HudIconGraphic은 실제 리소스/모든 Graphic 소비 이관 전까지 유지한다.

## 지역 기능 소유권 (2026-10-11)

StageService가 같은 씬의 논리 구간, 첫 구간, 현재 구간, 활성화한 체크포인트 집합, 마지막 스냅샷과 그 복귀 구간을 소유한다. 구간에는 서로 다른 시작·끝 StageTrigger, 시점 종류, 실제 음악 Clip·음량·전환 시간, 진입 보정·카메라 전환 시간을 연결한다. 이름·배열 순번은 저장 키가 아니다. Trigger는 접촉을 전달하며 시점·음악·저장 장부를 별도로 편집하지 않는다. Stage는 공유 최종 대화 수명과 초기 재준비를 조정하며 각 장치·적 소유자의 상태를 복구한다.

StageTrigger가 추격 접촉, 같은 씬 무리·퇴로 참조와 안전 검사, 저장 후 시작 순서를 맡는다. CoreWorldBinding은 활성 스테이지 트리거의 무리를 중복 없이 초기화하고 연결 당시 참조로 해제하며 저장 명령을 연결한다. CoreComposition은 추격 실행기를 요구하지 않는다. ChaseRuntimeDriver는 X 이동·현재 Actor 판정·실제 전면 Box 포획·종료를 소유하고 ChaseSwarmPresentation은 외형 tween만 소유한다. 고정 시작 센서 아래에 움직이는 무리를 둘 수 있지만 시작 센서·퇴로가 무리의 자식이면 거절한다. 기존 ChaseDefinition 설정 자산은 GUID를 유지한다. ChaseService/Model/Path/Settings/IChaseActor 다섯 중간 소스는 삭제했다.

CheckpointSnapshot은 접촉한 체크포인트 Transform의 유한 월드 Position, 접촉 순간 Fragments 정수0..5, UnlockedAbilities 마스크0/1/2/3의 정확히 세 값이다. StageService가 유효한 최초 접촉에서 스냅샷과 활성화 이력을 콜백 전에 원자 확정한다. 수락 전 실패는 이전 저장을 유지하고, 수락 후 표현 취소·사망은 이미 수락한 저장과 소비 이력을 유지한다. CapsuleCollider 월드 중심을 Position에 맞추며 HP·폼·껍질·음악·시점·세계 상태는 payload에 추가하지 않는다. 첫 저장 전에는 첫 구간 시작과 Entry 초기 값을 사용하고, 추격 직전 예외만 당시 플레이어 Capsule 중심을 저장한다.

구간 음악은 StageService의 실제 Clip·음량·전환 시간으로 작성하고 새 구간 시작 및 복귀 맥락에서 요청한다. 같은 실제 Clip이면 재생 위치·게인·진행 중 fade를 다시 시작하지 않는다. 첫 구간 선택을 Opening 동안 유지해 Playing에서 적용하며, 타이틀·오프닝·엔딩·추격 음악의 기존 우선순위는 유지한다.

App 출력 Camera/Brain/AudioListener는 각1개이고 Core의 Side·Corridor·Quarter 리그를 LevelCameraService가 표현한다. 편집 구도 원본은 StageService의 세 프로필이다. 피사체 중심 반경(m), 수평각·수직각·롤(도), 시선 중심 오프셋·화면 위치·감쇠·시야각을 조절한다. [현재 구현·V1.8 미반영] 카메라 위치 바운더리와 Confiner, 별도 CameraZoneTrigger·SafeAnchor·Alignment는 사용하지 않는다. 플레이어 CameraAnchor는 추적 중심을 제공한다. Side는 X, Corridor/Quarter는 XZ 이동이며 보호 HMS 입력 구현은 유지한다.

구도 Inspector의 화면 위치·허용 폭/높이·감쇠·거리·각도·시야각은 런타임에 변경된 값만 갱신한다. 잘못된 값은 마지막 유효 구도를 보존하고 유효값 복귀까지 한 번 경고한다. Follow를 끊은 전환 구도는 목표를 잠시 연결해 재평가한 뒤 다시 끊으며 전환 상태·입력 차단·이동 Tween은 유지한다. 일시정지 중 구도 변경은 Brain delta 0으로 출력하고 전환 시간은 진행하지 않는다.

새 시작 체크포인트마다 플레이어 Capsule 중심을 해당 시작 위치로 DOMove 보정한다. 끝은 저장만 하고 현재 구간·시점을 바꾸지 않는다. 같은 시점의 새 시작은 카메라 재선택·blend 재시작만 생략하며 보정·저장·음악은 수행한다. [현재 구현·V1.8 미반영] 새 유효 시작은 이전 보정·전환을 취소하고 최신 요청을 수행한다. V1.8 목표는 전환 중 추가 전환 금지다. 사망·취소·Actor/Stage 해제 때 자기 트윈·보정 소유권·입력 제한을 반환하며 낡은 callback은 상태를 바꾸지 못한다. 사용 중인 차단 장치만 플레이어 보정 뒤 고정 목표로 이동한다.

## 한 판 흐름

Bootstrap→Title→새 게임→Core/Prototype1개 additive 로드→초기 준비·Actor 생성·카메라 준비→Opening→Playing→최종 Escape(7) 포포 대화→Ending→세션 종료→Stage/Core 언로드→Title이다. 옵션 복귀도 같은 종료·언로드 경로를 사용한다. 설치·Editor 통합 진입·빌드의 Stage는 01_Stage_Prototype 하나다. Stage2/3와 역사적 Stage1 파일은 보존하지만 실행 등록에서 제외한다. Opening/Ending은 Core 화면이며 별도 씬이 아니다.

사망은 미완료 메모·대화·추격·Actor 표현 요청을 취소하고 자기 연결을 해제한 뒤 같은 Stage의 초기 배치/기믹을 준비한다. 마지막 유효 CP3값 또는 첫 저장 전 초기 구성을 새 HP3/Normal/껍질0 Actor에 복원한다. 같은 체크포인트 재접촉 억제와 일반 Completed/Skipped 대화 장부는 유지한다. Title에는 한 판 범위·Actor·빌린 월드/UI 연결을 해제하고 실제 로드된 Stage 전체를 먼저, Core를 마지막으로 언로드한다. 취소·부분 로드의 복구도 같은 경로를 사용하며 실패를 Title 완료로 처리하지 않는다.

최종 포포 대화만 완료 장부와 별개로 새 회차를 허용한다. Stage·Trigger·선택 객체·Table·Code·Actor 세대·Round·Session을 캡처하고 종료 시 다시 확인한다. Completed/Skipped만 Ending1회이며 취소·낡은 callback·같은 프레임 사망/포획은 거절한다.

## Scene Root와 prefab 물리

일반 적의 현행 구조(2026-10-29 T2)는 고정 EnemyActor Root 아래의 Body와 고정 Start/End 앵커를 조합한다. EnemyActor는 상태·기절·목표 끝점·초기 Body localPosition을, EnemyMotor는 Body의 월드 위치 작성을, Body의 EnemyContactSensor는 실제 접촉과 반복 피해를 소유한다. Root와 앵커를 이동시키지 않으며 앵커는 Body 밖에 둔다. EnemyMotor는 지형 검사를 소유하지 않고 XYZ 방향과 Root/Body 회전·양수 비균등 배율을 허용한다. Body 속도는 월드 거리(m/s)이므로 Root 배율을 곱하지 않으며 Gizmo는 Body 변환 행렬로 표시한다.

씬 Manager/Camera/Canvas/월드 기능 객체는 서로 독립 root다. 개별 prefab은 빈 논리 Root와 Render/Body/표현 자식을 구분한다. Player의 실제 ActorBody는 Facade·Movement·Form·HeavyState·Rigidbody·Capsule이 동거하고 VisualRoot/CameraAnchor는 서로 다른 직계 자식이다. 빈 PlayerRoot는 생성/제거 관리 단위다. Shell은 HMS Remove 계약 때문에 PlayerDebris/Rigidbody/Box/HeavyState가 실제 Root에 동거하며 바깥 래퍼를 만들지 않는다.

움직이는 발판은 한 Motion이 실제 Body Rigidbody를 이동한다. Render는 그 Body 아래, 고정 목표는 Body 밖이다. 눈물은 pickupRoot 전체를 움직여 Sensor와 Render가 함께 이동한다. Shattered Visual의 경고 Shake와 Chase 표현 아래에는 고정 Marker를 두지 않는다. 기존 물리 Root template의 계약은 예시 계층 때문에 강제 재작성하지 않는다.

## 구간 작성과 수명

재시도는 같은 Core/Stage와 StageService의 소비 이력을 유지하고 마지막 유효 저장 및 당시 구간·음악·시점을 직접 복원한다. 소비한 센서를 다시 발동하지 않는다. 현재 해금은 GameSessionService 원본이고 저장 시점의 해금은 별도 불변 스냅샷 값이다. Title 복귀에서 세션 종료 후 Stage 전체→Core 순서로 언로드하며 새 게임은 새 객체와 빈 체크포인트 장부로 시작한다.

뒷면 메시를 생략한 아트는 구도·추적·전환 중간에서도 뒷면을 노출하지 않도록 배치한다. 최신 사용자 정정으로 반대편 카메라 이동은 철회했고 기존 방향을 유지한다. 사용자가 오브젝트 배치를 회전하는 작업은 에이전트가 완료한 것으로 기록하지 않는다. 보호 아트의 임의 수정·양면화나 HMS 입력 변환으로 우회하지 않으며 현재 백페이스 노출은 미해결 시각 항목이다.

버튼은 빈 기능 Root 아래 Cap의 Rigidbody·접촉면·Visual만 이동하고 Body는 고정 충돌체와 Visual을 유지한다. 고정 기준점은 움직이는 몸체 밖에 둔다. StageTrigger의 접촉 센서는 Unity callback을 받는 현재 물리 계약을 유지하며 예시 계층만을 위해 불필요한 Relay를 추가하지 않는다.
