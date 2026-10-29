# 협업·데이터 계약

> 아래 구조·API 설명은 현재 구현 사실이다. V1.8 요구를 이미 구현했다는 뜻이 아니다. 컷신 복귀·타이틀·카메라·추격 차이는 [구조 차이 목록](tracking/system-v18-2026-10-29/구조%20차이%20목록.md)을 따른다.

> 현행 시스템 권위는 V1.8 활성 본문 → 이전 사용자 기능 명세 → 기존 구현 설명 순서다. 지정 원본·예외는 [planning-index](planning-index.md)를 따른다. 구현과 검증 상태는 tracking/status.md 및 실제 증거로 확인하며 문서 개정만으로 완료를 선언하지 않는다.

## 상태와 외부 공개 API

HP·현재 조각·폼·물리 상태 원본은 HMS PlayerFacade/Model이다. AddSkillFragments는 실제 추가량, TryConsumeSkillFragments는 충분한 보유량에서 성공 여부다. Bridge/UI에 별도 가변 원본을 만들지 않는다. Attach/Detach는 빌린 Model/Actor/Transform과 자기 이벤트·요청을 연결/해제하며 observer 오류가 다른 해제를 막지 않는다.

| API | 실제 계약 |
|---|---|
| IInteractable.Interact | bool 수락/성공. 한 입력은 선택된 대상 한 번 실행 |
| IDamageable.Damage / IsInvincible | 실제 적용 피해량 / 현재 무적. 일반 피해와 강제사망 구분 |
| IStunnable.TryStun / IStunState | 적용 기절 시간은 ms. 상태/이벤트는 읽고 자기 구독만 해제 |
| IKnockbackable.ApplyKnockback | 초기 속도·ms 제어 제한. 경계에서 초로 변환 |
| HMS PlaybackEnded | 요청ID와 Completed/Interrupted/Missing. 낡은 ID·중복 종료 거절 |

HMS 독립 Reader 기본F와 통합 Host가 지정한 E를 구분한다. 기존 opt-in 피해 무적 기본0은 독립 정책이고 통합 Host가2초를 설정한다. 실제 clip 누락은 Missing/안전 진행이며 실제 재생 성공이 아니다. 현재 PlayerRoot의 이동 모드 속도는1.8m/s, 정상/Small 점프 설정은1/.5m, 중력은12m/s²다. 공개 설정의 단위와 실제 물리 관측을 구별한다. HMS 쓰기 예외는 security.md와 최신 파일별 직접 지시를 따른다. 피해·강제사망·애니메이션 종료·스킬 추가조건·눈물 표적 검증의 현재 공개 경계를 보존한다.

## CP와 현재 Actor

CheckpointSnapshot은 접촉한 체크포인트 Transform의 유한 월드 Position, 접촉 순간 Fragments 정수0..5, UnlockedAbilities 마스크0/1/2/3의 정확히 세 값이다. StageService가 유효한 최초 접촉에서 스냅샷과 활성화 이력을 콜백 전에 원자 확정한다. 수락 전 실패는 이전 저장을 유지하고, 수락 후 표현 취소·사망은 이미 수락한 저장과 소비 이력을 유지한다. CapsuleCollider 월드 중심을 Position에 맞추며 HP·폼·껍질·음악·시점·세계 상태는 payload에 추가하지 않는다. 첫 저장 전에는 첫 구간 시작과 Entry 초기 값을 사용하고, 추격 직전 예외만 당시 플레이어 Capsule 중심을 저장한다.

재시도는 같은 Core/Stage와 StageService의 소비 이력을 유지하고 마지막 유효 저장 및 당시 구간·음악·시점을 직접 복원한다. 소비한 센서를 다시 발동하지 않는다. 현재 해금은 GameSessionService 원본이고 저장 시점의 해금은 별도 불변 스냅샷 값이다. Title 복귀에서 세션 종료 후 Stage 전체→Core 순서로 언로드하며 새 게임은 새 객체와 빈 체크포인트 장부로 시작한다. 이미 활성화된 모든 체크포인트는 재접촉·역주행·Actor 교체·Disable/Enable·취소 후에도 다시 수락하지 않는다. 새 유효 체크포인트는 배열 순번과 관계없이 수락한다.

Retry는 같은 Stage 초기 재준비 후 새 Normal/HP3/껍질0 Actor에3값을 복원한다. 현재 해금은 Session 한 원본이며 Stage 출처별 reset이 없다. 일반 Completed/Skipped 대화 장부는 같은 판에 유지한다. Cancelled는 완료가 아니며 최종 포포 대화만 새 회차 예외다.

## Scene·Camera·위치 참조

Scene 설정은 실제 SceneAsset을 사용한다. StageService가 같은 씬의 논리 구간, 첫 구간, 현재 구간, 활성화한 체크포인트 집합, 마지막 스냅샷과 그 복귀 구간을 소유한다. 구간에는 서로 다른 시작·끝 StageTrigger, 시점 종류, 실제 음악 Clip·음량·전환 시간, 진입 보정·카메라 전환 시간을 연결한다. 이름·배열 순번은 저장 키가 아니다. Trigger는 접촉을 전달하며 시점·음악·저장 장부를 별도로 편집하지 않는다. 내부 SceneReference/runtimePath 등 빌드 캐시는 실제 참조에서 생성하며 별도 수기 ID를 저장 키로 추가하지 않는다.

추격 시작 트리거의 chaseDriver는 같은 씬의 실제 ChaseRuntimeDriver다. BindChase는 작성한 무리와 주입된 실행기의 일치, 같은 씬 Stage/퇴로, 고정 센서·퇴로가 움직이는 무리 밖에 있는 것을 검증한다. Core는 활성 스테이지 트리거에서 받은 실행기만 중복 없이 초기화하고 연결 당시 참조로 Shutdown한다. 추격이 없는 스테이지는 실행기를 요구하지 않는다. 기존 추격의 저장→시작→퇴로 닫기 순서는 유지한다.

App 출력 Camera/Brain/AudioListener는 각1개이고 Core의 Side·Corridor·Quarter 리그를 LevelCameraService가 표현한다. 편집 구도 원본은 StageService의 세 프로필이다. 피사체 중심 반경(m), 수평각·수직각·롤(도), 시선 중심 오프셋·화면 위치·감쇠·시야각을 조절한다. [현재 구현·V1.8 미반영] 카메라 위치 바운더리와 Confiner, 별도 CameraZoneTrigger·SafeAnchor·Alignment는 사용하지 않는다. 플레이어 CameraAnchor는 추적 중심을 제공한다. Side는 X, Corridor/Quarter는 XZ 이동이며 보호 HMS 입력 구현은 유지한다.

구도 수치의 원본은 StageService의 sideView/corridorView/quarterView다. LevelCameraService는 변경된 값만 실제 출력에 적용한다. 잘못된 값은 마지막 유효 구도를 보존하며 일시정지 중에는 표시만 갱신하고 전환 시간을 진행하지 않는다.

새 시작 체크포인트마다 플레이어 Capsule 중심을 해당 시작 위치로 DOMove 보정한다. 끝은 저장만 하고 현재 구간·시점을 바꾸지 않는다. 같은 시점의 새 시작은 카메라 재선택·blend 재시작만 생략하며 보정·저장·음악은 수행한다. [현재 구현·V1.8 미반영] 새 유효 시작은 이전 보정·전환을 취소하고 최신 요청을 수행한다. V1.8 목표는 전환 중 추가 전환 금지다. 사망·취소·Actor/Stage 해제 때 자기 트윈·보정 소유권·입력 제한을 반환하며 낡은 callback은 상태를 바꾸지 못한다. 사용 중인 차단 장치만 플레이어 보정 뒤 고정 목표로 이동한다.

초기 준비와 Retry는 서비스의 첫/복귀 구간으로 카메라를 직접 준비한다. 공간 포함 검사나 소비된 트리거의 재접촉으로 복귀 구도를 선택하지 않는다. 카메라 맥락은 StageService 실행 상태이며 Snapshot payload가 아니다.

구간 음악은 StageService의 실제 Clip·음량·전환 시간으로 작성하고 새 구간 시작 및 복귀 맥락에서 요청한다. 같은 실제 Clip이면 재생 위치·게인·진행 중 fade를 다시 시작하지 않는다. 첫 구간 선택을 Opening 동안 유지해 Playing에서 적용하며, 타이틀·오프닝·엔딩·추격 음악의 기존 우선순위는 유지한다.

## 수명·완료·취소

현재 등록 Stage는 01_Stage_Prototype 하나이며 설치·진입 설정·빌드 목록을 함께 맞춘다. Retry는 같은 Core/Stage 인스턴스·scope·일반 대화 장부를 유지한다. Title에서는 Actor 제거 전 borrowed Camera/월드/UI를 해제하고 세션 종료 후 실제 로드된 Stage 전체→Core 순서로 언로드한다. 보존된 폐기 Stage의 종료도 허용하지만 실행 로드는 거절한다. 씬 언로드 완료 때 SceneService의 원본 Root 캐시를 지우며 다음 새 게임은 새 인스턴스를 사용한다.

최종 Escape(7) 대화는 current Stage/Trigger/selection 객체/Table/Code/Actor 세대/Round/Session을 수락 전후와 종료에서 검증한다. 현재 맥락의 Completed/Skipped만 Ending을 한 번 예약한다. Busy/알 수 없는 선택/거절은 센서를 영구 소비하지 않는다. Cancelled·낡은 맥락·같은 프레임 사망/포획은 Ending을 열지 않으며 자기 예약·구독만 정리한다.

View 입력은 VM Command 한 경로, VM은 View 직접 참조 없음이다. 재Bind는 자기 listener·작업만 정리하고 다른 소비자의 구독을 일괄 제거하지 않는다. Dispose 뒤 명령/콜백은 없고 비동기 작업의 취소·finally 실패는 결과에 남긴다. 메모/Options는 자기 정지 토큰만 반환하며 대화/로그 자체는 WorldPause를 가지지 않는다.

## 물리와 prefab 교체

일반 적의 현행 배치 계약(2026-10-29 T2)은 고정 EnemyActor Root 내부의 Body와 Start/End 앵커다. Body에는 BoxCollider Trigger와 소유 EnemyContactSensor를 함께 연결하고 앵커는 같은 씬의 Root 내부·Body 외부 고정 지점으로 연결한다. EnemyMotor는 Body의 월드 위치만 변경한다. 첫 목표는 End이며 XYZ 왕복, 선분 밖 초기 위치, 회전·양수 비균등 배율을 허용한다. 같은 두 앵커에서는 현재 위치에 정지하고 Stationary는 앵커를 요구하지 않는다. 지형 Mask/검색/충돌 회피·침투/갇힘 계약과 첫 방향 enum은 제거했다. 필요한 참조·유한값·양수 크기/실제 배율·Rigidbody 없는 적 계층 계약은 유지한다. ResetForRetry는 최초 Body localPosition과 초기 상태·End 목표를 복원하며 배치 오류는 로그와 IsPrepared=false로 해당 적에 한정한다. 회전·배율은 복원 과정에서 정규화하지 않는다.

PlayerRoot는 관리 단위, ActorBody는 Facade·Movement·Form·RB·Capsule이 동거하는 실제 몸체다. VisualRoot와 CameraAnchor는 서로 다른 직계 자식이다. Shell의 PlayerDebris/Box/RB/HeavyState는 실제 Root에 동거한다. 움직이는 발판 Render는 Motion이 이동하는 Body 아래, 고정 Marker는 밖이다. Tear는 pickupRoot 전체 이동으로 Sensor가 따라간다. 생성 발판 실제 침투<1/4는 지지 보정, >=1/4는 actual Body를 위로 분리하며 사망한다. 소유 Shell만 별도 분리/데드존 반환하고 같은 객체·HP·폼·소유·IgnoreCollision을 유지한다.

같은 이름의 새 prefab이 기존 GUID 소비를 자동 대체하지 않는다. Host.actorPrefab, Form.debrisPrefab, MemoUIController.viewPrefab, App/Core View와 Stage의 실제 인스턴스 참조를 사람이 옮긴다. HudIconGraphic Source와 fallback은 실제 하트와 Q/E/조각 Graphic 소비가0이 된 뒤 제거한다. 원본 prefab 삭제·재생성·Apply·아트 제작은 사람이 한다.
