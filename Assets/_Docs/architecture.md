# 시스템 책임과 수명

> 2026-10-07 사용자 확정안과 추가 답변을 적용한다. [확정안 적용 명세](<tracking/ready-system-v11-refactor-2026-10-06/확정안 적용 명세.md>)가 이전 초안의 충돌 문구를 대체한다. 구현/실행 완료는 [상태](tracking/status.md)와 실제 검증 증거로 판단한다.

| 수명/경계 | 상태의 단일 소유자와 필요한 책임 | 현재 책임과 종료 계약 |
|---|---|---|
| 앱 | AppComposition·GameFlowService·SceneService·음량/AudioService | Output Camera/Brain/Listener 각1개를 앱 수명으로 유지 |
| Core 씬/한 판 | CoreComposition·Core 리그2개·UI는 씬 수명; GameSessionRuntime·GameSessionService·CoreWorldBinding은 한 판 수명 | retry에는 같은 서비스 유지, Title에는 한 판 범위/빌린 참조 해제, NewGame에는 같은 씬에 새 범위 연결 |
| Stage | StageService·각 기믹 상태 소유자 | 배치/트리거와 실행 중 상태만 소유; retry에는 초기 재준비, 월드 상태 저장 없음 |
| Actor | SessionActorHost·HMS Facade/Model/Form/Movement | 생성/파괴와 원본 HP/조각/폼/속도; 카메라를 자식에서 제거 |
| 카메라 구간 | LevelCameraService + HMS PlayerCameraController + Brain | 전환/정렬 조정, 단일 우선순위 작성자, 실제 출력 Transform 작성자 구분 |
| 입력/제한 | HMS Reader + GameplayInputRouter + GameplayControlService | E/F/Q 단일 소비, 메모/대화 입력 제한과 옵션 전체 정지를 각 토큰으로 집계 |
| 적 | EnemyActor·StateMachine·Motor·Combat | X/Y 순찰/Idle/기절, 지형 물리, 피해원; 적 HP/사망/Float 모델 제거 |
| 발판/장치 | 기능 컨트롤러·KinematicPlatformMotion·PlatformContacts | 진행 목적/상태, 단일 물리 이동, 접촉 경계를 유지하고 같은 문/트레이/서랍을 설정 조립 |
| 화면 | Service/Model → ViewModel → View | View는 명령/표시만, VM은 View 참조 없음; 중복 Prompt/Instruction 제거 |
| 대화/연출 | DialogueService / CinematicService | 행/완료 장부와 단일 컷 시계, 취소/완료/Skip 구분, Cancel만 하는 Driver/Timeline 호환 계층 제거 |
| 외부 실행 | Audio/Scene/Settings/DOTween/Animator 어댑터 | 실제 자원 소유·해제 경계만 유지, 구현마다 새 서비스/인터페이스를 추가하지 않음 |

## 카메라 구조의 변화와 이유

현재 구성은 App Output Camera/Brain/Listener 각1개와 분리된 Core Side/Quarter 리그를 사용한다. PlayerRoot의 기존 CameraRig는 소유 자산에서 선택적으로 제거했고 실제 몸체/아트와 GUID를 보존했다. Actor 자식 리그·출력 교대 설명은 변경 전 이력이다. CoreLevelCamera는 출력 Camera가 아니라 LevelCameraService 객체다.

HMS PlayerCameraController의 명시적 Bind/Unbind/Preview/Commit으로 App 출력과 Core 리그를 Actor에서 분리한다. HMS는 우선순위, Brain은 출력 Transform, LevelCameraService는 요청·입력 제한·Side Z 정렬과 실제 완료 확인을 소유한다. 초기 Quarter는 실제 Brain 준비 후 입력을 연다.

## 생성·재시도·종료

Title 명령 → Core 최초1회 로드 → 등록 Stage3개 전부 최초 additive 로드 → 한 판 서비스 범위 생성/활성 Stage 초기 준비 → Actor 생성/Position·Fragments 복원 → 카메라 연결/초기 모드 준비 → Opening 또는 Play 순서다. 이후 retry·Title 복귀·NewGame에 Core/Stage 씬 인스턴스를 유지하고 활성 Stage 하나만 연결한다. Title 씬 자체는 NewGame 때 언로드할 수 있다. App 로케이터는 유지되며 새 판의 App 부모 Core 범위·능력/완료 대화 장부는 새로 만든다. 이 장부는 checkpoint payload가 아니다.

retry에는 대화/메모/추격·미완료 Actor 동작을 취소하고 카메라/월드의 빌린 참조·자기 구독·접촉·Tween을 먼저 해제한다. StageService가 발판/장치/적/조각/트리거의 기존 typed 소유자 ResetForRetry를 호출해 초기 배치를 준비한 뒤 Position·Fragments만 복원한다. 소비한 TearFragment는 동일 객체·자식 Visual·Collider·소비 플래그를 복구한다. 현재 Stage가 최초 부여한 능력과 연결 안내 완료만 지우며 이전 Stage 출처는 보존한다. retry에는 Core 서비스/VM·App Output을 유지하고 Title에는 CoreComposition.ReleaseSession/SessionActorHost.ReleaseSession으로 한 판 참조를 정리한 뒤 같은 씬의 다음 판에 재연결한다. 비활성 Stage의 Actor/트리거/추격/외부 작업은 연결하지 않는다. WorldSave DTO·ParticipantRegistry·객체 상태 ID를 추가하지 않으며 종료 실패는 집계하고 성공으로 숨기지 않는다.

## 계층과 물리 예외

새 SYM 자원은 빈 Root 아래 Visual/Physics/VFX/SFX를 명시적으로 연결한다. 움직이는 발판의 로직은 Root, 물리 콜백은 실제 Body 자식이다. 필요한 소유 SYM 코드는 주입 참조로 바꾸며 과거 RequireComponent가 Root에 물리를 자동 추가하는 경로를 제거한다.

HMS 플레이어는 Facade·Movement·Form·Rigidbody·Collider가 같은 ActorBody에 있어야 하는 공개 계약이 있다. 이를 단순 계층 예시만으로 분리하지 않고 실제 Player 루트에 동거하는 물리 예외로 명세한다. CameraAnchor는 실제 움직이는 ActorBody의 자식이다. Trigger 센서와 EnemyContactSensor 역시 물리 콜백 자식의 이유를 명세하며 단지 Root를 맞추려고 전달자 파일을 늘리지 않는다. 전체 연결은 [프리팹 가이드](<tracking/ready-system-v11-refactor-2026-10-06/미래 프리팹 연결 가이드.md>)에 있다.

## 조립과 정리 원칙

32개 지정 소스와 소유 메타는 기능·호출·직렬화 참조를 이관한 뒤 삭제했다. 기존 자산은 선택적으로 이관했으며 원래 GUID와 실제 몸체·아트 배치를 보존했다. HMS의 이번 한정 공개 확장 외 원본 수정 권한은 없다.

기능/상태/물리/표시 소유가 같은 중복은 합치고, 실제 자원 수명/외부 실행/여러 소비자를 구분하는 경계는 유지한다. _Art/공용/Packages/ProjectSettings를 변경하거나 Timeline 패키지를 삭제하는 계획이 아니다. 현재 서버/네트워크/앱 재시작 후 이어하기는 없다.
