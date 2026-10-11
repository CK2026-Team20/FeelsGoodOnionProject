# 협업·데이터 계약

> 현행 시스템 권위는 사용자의 최신 직접 지시 → V1.6 활성 본문 → 기존 구현 설명 순서다. 지정 원본·예외는 [planning-index](planning-index.md)를 따른다. 구현과 검증 상태는 tracking/status.md 및 실제 증거로 확인하며 문서 개정만으로 완료를 선언하지 않는다.

## 상태와 외부 공개 API

HP·현재 조각·폼·물리 상태 원본은 HMS PlayerFacade/Model이다. AddSkillFragments는 실제 추가량, TryConsumeSkillFragments는 충분한 보유량에서 성공 여부다. Bridge/UI에 별도 가변 원본을 만들지 않는다. Attach/Detach는 빌린 Model/Actor/Transform과 자기 이벤트·요청을 연결/해제하며 observer 오류가 다른 해제를 막지 않는다.

| API | 실제 계약 |
|---|---|
| IInteractable.Interact | bool 수락/성공. 한 입력은 선택된 대상 한 번 실행 |
| IDamageable.Damage / IsInvincible | 실제 적용 피해량 / 현재 무적. 일반 피해와 강제사망 구분 |
| IStunnable.TryStun / IStunState | 적용 기절 시간은 ms. 상태/이벤트는 읽고 자기 구독만 해제 |
| IKnockbackable.ApplyKnockback | 초기 속도·ms 제어 제한. 경계에서 초로 변환 |
| HMS PlaybackEnded | 요청ID와 Completed/Interrupted/Missing. 낡은 ID·중복 종료 거절 |

HMS 독립 Reader 기본F와 통합 Host가 지정한 E를 구분한다. 기존 opt-in 피해 무적 기본0은 독립 정책이고 통합 Host가2초를 설정한다. 실제 clip 누락은 Missing/안전 진행이며 실제 재생 성공이 아니다. 통합3/2점프·이동속도5는 소유 prefab 설정이며 실제 연결은 별도 확인한다. HMS 쓰기 예외는 security.md와 최신 파일별 직접 지시를 따른다. 피해·강제사망·애니메이션 종료·스킬 추가조건·눈물 표적 검증의 현재 공개 경계를 보존한다.

## CP와 현재 Actor

CheckpointSnapshot은 Position(현재 ActorBody의 유한 월드 Vector3), Fragments(정수0~5), UnlockedAbilities(None0/Form1/Tear2/Both3)의 정확히 세 불변 값이다. 기존 AbilityId 번호0/1은 보존한다. 전부 검증한 뒤 원자 교체하고 실패/중단은 이전 Snapshot과 성공 사건 없음 상태를 보존한다. 첫 유효 저장 전 Snapshot이 없으며 해당 Stage의 초기 Marker/Entry를 사용한다.

동일 실제 Marker 재접촉은 갱신하지 않으며 Actor 교체로 억제 상태를 지우지 않는다. 추격 시작 트리거는 접촉 진입·퇴로 안전·시작 가능 확인 뒤 현재3값 저장 성공을 먼저 요구한다. Stage/CP 참조는 실행 맥락이고 payload에 저장하지 않는다. HP·폼·껍질·세계/대화/카메라/시간·디스크 진행은 저장하지 않는다.

Retry는 같은 Stage 초기 재준비 후 새 Normal/HP3/껍질0 Actor에3값을 복원한다. 현재 해금은 Session 한 원본이며 Stage 출처별 reset이 없다. 일반 Completed/Skipped 대화 장부는 같은 판에 유지한다. Cancelled는 완료가 아니며 최종 포포 대화만 새 회차 예외다.

## Scene·Camera·위치 참조

Scene 설정 자산은 실제 SceneAsset, Stage의 초기 CP는 같은 씬 실제 컴포넌트다. 추격 퇴로는 시작 트리거가 참조하고 카메라 정렬점·CP 등장점은 해당 기능 객체의 자식 Transform이다. 진행/추격 경로는 작성하지 않는다. ScriptableObject에 다른 Scene Marker를 저장하지 않는다. 내부 SceneReference.runtimePath와 설치 Player 경로 캐시는 편집/빌드에서 생성하며 수기 입력값이 아니다. DialogueSelection은 현재 JSON TextAsset에서 검증한 Code 선택, Audio는 용도와 실제 Clip 연결이다.

App 출력 Camera/Brain/AudioListener는 각 1개다. Core의 서로 다른 Side·Corridor·Quarter 세 CinemachineCamera와 구도·우선순위·추적 대상·경계·전환은 LevelCameraService가 소유한다. Brain이 출력하고 서비스는 최종 위치도 Stage의 cameraBounds 안으로 제한한다. Side(1)는 X, Corridor(2)와 Quarter(3)는 XZ 이동이다. HMS PlayerCameraController는 기존 독립 네 rig용이며 통합 분리 Bind/Preview/Commit/Unbind 확장은 사용하지 않는다.

CameraZoneTrigger의 실제 접촉과 현재 Actor·Stage·구간 세대를 확인한다. 통로 차단이 있는 전환은 실물 safeAnchor로 플레이어를 먼저 보정한 뒤 blockerTarget으로 차단 Rigidbody를 이동하며 둘 다 DOMove를 사용한다. 기본 전환 시간은 0.6초이고 각 이동 시간·Ease는 구간 설정이다. 실제 이동과 Brain 블렌드 완료 후 이동축을 확정하고 자기 입력 차단을 반환한다. 옵션/메모 정지, 취소·사망·Actor/Stage 교체 시 자기 작업 해제와 오래된 요청 거절을 유지한다.

CameraZoneTrigger가 Side(1)/Corridor(2)/Quarter(3) 및 실제 alignmentPoint·safeAnchor·blockerBody·blockerTarget을 작성한다. 초기 Marker는 실제 Trigger를 연결한다. CameraZoneData/Request는 검증된 실행 값이며 ID/AlignmentOffset 수기 필드가 없다. 같은 실제 구간 참조로 중복 진입을 억제하고 마지막 유효 Collider 이탈 뒤 새 진입을 허용한다. 카메라를 CP payload에 넣지 않는다.

## 수명·완료·취소

등록 씬은 한 번 로드해 유지하되 한 판 scope는 Title에서 해제하고 NewGame에서 새로 만든다. Retry는 같은 scope/일반 대화 장부를 유지한다. 비활성 Stage는 현재 Actor·Trigger·추격과 연결되지 않고 자기 작업을 멈춘다. Actor 제거 전 borrowed Camera/월드/UI를 해제한다.

최종 Escape(7) 대화는 current Stage/Trigger/selection 객체/Table/Code/Actor 세대/Round/Session을 수락 전후와 종료에서 검증한다. 현재 맥락의 Completed/Skipped만 Ending을 한 번 예약한다. Busy/알 수 없는 선택/거절은 센서를 영구 소비하지 않는다. Cancelled·낡은 맥락·같은 프레임 사망/포획은 Ending을 열지 않으며 자기 예약·구독만 정리한다.

View 입력은 VM Command 한 경로, VM은 View 직접 참조 없음이다. 재Bind는 자기 listener·작업만 정리하고 다른 소비자의 구독을 일괄 제거하지 않는다. Dispose 뒤 명령/콜백은 없고 비동기 작업의 취소·finally 실패는 결과에 남긴다. 메모/Options는 자기 정지 토큰만 반환하며 대화/로그 자체는 WorldPause를 가지지 않는다.

## 물리와 prefab 교체

PlayerRoot는 관리 단위, ActorBody는 Facade·Movement·Form·RB·Capsule이 동거하는 실제 몸체다. VisualRoot와 CameraAnchor는 서로 다른 직계 자식이다. Shell의 PlayerDebris/Box/RB/HeavyState는 실제 Root에 동거한다. 움직이는 발판 Render는 Motion이 이동하는 Body 아래, 고정 Marker는 밖이다. Tear는 pickupRoot 전체 이동으로 Sensor가 따라간다. 생성 발판 실제 침투<1/4는 지지 보정, >=1/4는 actual Body를 위로 분리하며 사망한다. 소유 Shell만 별도 분리/데드존 반환하고 같은 객체·HP·폼·소유·IgnoreCollision을 유지한다.

같은 이름의 새 prefab이 기존 GUID 소비를 자동 대체하지 않는다. Host.actorPrefab, Form.debrisPrefab, MemoUIController.viewPrefab, App/Core View와 Stage의 실제 인스턴스 참조를 사람이 옮긴다. HudIconGraphic Source와 fallback은 실제 하트와 Q/E/조각 Graphic 소비가0이 된 뒤 제거한다. 원본 prefab 삭제·재생성·Apply·아트 제작은 사람이 한다.
