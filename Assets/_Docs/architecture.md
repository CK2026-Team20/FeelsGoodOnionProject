# 시스템 책임과 수명

> 현행 목표는 V1.5 실행 명세다. 구현 완료는 tracking/status.md와 실제 증거로 판단한다.

| 수명/경계 | 상태의 단일 소유자와 필요한 책임 | 현재 책임과 종료 계약 |
|---|---|---|
| 앱 | AppComposition·GameFlowService·SceneService·음량/AudioService | Output Camera/Brain/Listener 각1개를 앱 수명으로 유지 |
| Core 씬/한 판 | CoreComposition·Core 리그2개·UI는 씬 수명; GameSessionRuntime·GameSessionService·CoreWorldBinding은 한 판 수명 | retry에는 같은 서비스 유지, Title에는 한 판 범위/빌린 참조 해제, NewGame에는 같은 씬에 새 범위 연결 |
| Stage | StageService·각 기믹 상태 소유자 | 배치/트리거와 실행 중 상태만 소유; retry에는 초기 재준비, 월드 상태 저장 없음 |
| Actor | SessionActorHost·HMS Facade/Model/Form/Movement | 생성/파괴와 원본 HP/조각/폼/속도; 카메라를 자식에서 제거 |
| 카메라 구간 | LevelCameraService + HMS PlayerCameraController + Brain | 전환/정렬 조정, 단일 우선순위 작성자, 실제 출력 Transform 작성자 구분 |
| 입력/제한 | HMS Reader + GameplayInputRouter + GameplayControlService | E/F/Q 단일 소비, 메모/대화 입력 제한과 옵션 전체 정지를 각 토큰으로 집계 |
| 적 | EnemyActor·EnemyMotor·EnemyContactSensor | X/Y 순찰/Idle/기절, 지형 물리, 피해원; 적 HP/사망/Float 모델 제거 |
| 발판/장치 | 기능 컨트롤러·KinematicPlatformMotion·PlatformContacts | 진행 목적/상태, 단일 물리 이동, 접촉 경계를 유지하고 같은 문/트레이/서랍을 설정 조립 |
| 화면 | Service/Model → ViewModel → View | View는 명령/표시만, VM은 View 참조 없음; 중복 Prompt/Instruction 제거 |
| 대화/연출 | DialogueService / CinematicService | 행/완료 장부와 단일 컷 시계, 취소/완료/Skip 구분, Cancel만 하는 Driver/Timeline 호환 계층 제거 |
| 외부 실행 | Audio/Scene/Settings/DOTween/Animator 어댑터 | 실제 자원 소유·해제 경계만 유지, 구현마다 새 서비스/인터페이스를 추가하지 않음 |

## 카메라 구조의 변화와 이유

현재 구성은 App Output Camera/Brain/Listener 각1개와 분리된 Core Side/Quarter 리그를 사용한다. PlayerRoot의 기존 CameraRig는 소유 자산에서 선택적으로 제거했고 실제 몸체/아트와 GUID를 보존했다. Actor 자식 리그·출력 교대 설명은 변경 전 이력이다. CoreLevelCamera는 출력 Camera가 아니라 LevelCameraService 객체다.

HMS PlayerCameraController의 명시적 Bind/Unbind/Preview/Commit으로 App 출력과 Core 리그를 Actor에서 분리한다. HMS는 우선순위, Brain은 출력 Transform, LevelCameraService는 요청·입력 제한·Side Z 정렬과 실제 완료 확인을 소유한다. 초기 Quarter는 실제 Brain 준비 후 입력을 연다.

## 생성·재시도·종료

Title 명령 → Core 최초1회 로드 → 등록 Stage3개 전부 최초 additive 로드 → 한 판 서비스 범위 생성/활성 Stage 초기 준비 → Actor 생성/Position·Fragments 복원 → 카메라 연결/초기 모드 준비 → Opening 또는 Play 순서다. 이후 retry·Title 복귀·NewGame에 Core/Stage 씬 인스턴스를 유지하고 활성 Stage 하나만 연결한다. Title 씬 자체는 NewGame 때 언로드할 수 있다. App 로케이터는 유지되며 새 판의 App 부모 Core 범위·능력/완료 대화 장부는 새로 만든다. 완료 대화 장부는 checkpoint payload가 아니며, 해금은 마지막 snapshot 마스크로 복원한다.

retry에는 대화/메모/추격·미완료 Actor 동작을 취소하고 카메라/월드의 빌린 참조·자기 구독·접촉·Tween을 먼저 해제한다. StageService가 발판/장치/적/조각/트리거의 기존 typed 소유자 ResetForRetry를 호출해 초기 배치를 준비한 뒤 Position·Fragments·해금 마스크를 복원한다. 소비한 TearFragment는 동일 객체의 전체 Root pose·Collider·소비 플래그를 복구한다. 능력/HUD는 snapshot 마스크를 따르고 완료/Skipped 대화 기록은 유지한다. retry에는 Core 서비스/VM·App Output을 유지하고 Title에는 CoreComposition.ReleaseSession/SessionActorHost.ReleaseSession으로 한 판 참조를 정리한 뒤 같은 씬의 다음 판에 재연결한다. 비활성 Stage의 Actor/트리거/추격/외부 작업은 연결하지 않는다. WorldSave DTO·ParticipantRegistry·객체 상태 ID를 추가하지 않으며 종료 실패는 집계하고 성공으로 숨기지 않는다.

## 계층과 물리 예외

새 SYM 자원은 빈 Root 아래 Visual/Physics/VFX/SFX를 명시적으로 연결한다. 움직이는 발판의 로직은 Root, 물리 콜백은 실제 Body 자식이다. 필요한 소유 SYM 코드는 주입 참조로 바꾸며 과거 RequireComponent가 Root에 물리를 자동 추가하는 경로를 제거한다.

HMS 플레이어는 Facade·Movement·Form·Rigidbody·Collider가 같은 ActorBody에 있어야 하는 공개 계약이 있다. 이를 단순 계층 예시만으로 분리하지 않고 실제 Player 루트에 동거하는 물리 예외로 명세한다. CameraAnchor는 실제 움직이는 ActorBody의 자식이다. Trigger 센서와 EnemyContactSensor 역시 물리 콜백 자식의 이유를 명세하며 단지 Root를 맞추려고 전달자 파일을 늘리지 않는다. 전체 연결은 [프리팹 가이드](<tracking/ready-system-v11-refactor-2026-10-06/미래 프리팹 연결 가이드.md>)에 있다.

## 조립과 정리 원칙

32개 지정 소스와 소유 메타는 기능·호출·직렬화 참조를 이관한 뒤 삭제했다. 기존 자산은 선택적으로 이관했으며 원래 GUID와 실제 몸체·아트 배치를 보존했다. HMS의 이번 한정 공개 확장 외 원본 수정 권한은 없다.

기능/상태/물리/표시 소유가 같은 중복은 합치고, 실제 자원 수명/외부 실행/여러 소비자를 구분하는 경계는 유지한다. _Art/공용/Packages/ProjectSettings를 변경하거나 Timeline 패키지를 삭제하는 계획이 아니다. 현재 서버/네트워크/앱 재시작 후 이어하기는 없다.

## 2026-10-10 현행 V1.5 기준

현재 시스템 원본은 `Assets/_Docs/Cooked_시스템_기획서_V.1.5.docx`다. 최신 직접 지시 → V1.5 활성 본문 → 기존 구현 순서로 적용한다. 승인된 실행 명세는 `Assets/_Docs/tracking/ready-system-v15-2026-10-10/spec.md`, 구현·검증 상태는 tracking/status.md다. 이전 버전과 당시 실행 증거는 보존하되 현행 정책으로 승계하지 않는다.

- 체크포인트는 현재 ActorBody 월드 위치·보유 조각·해금 마스크 세 항목만 원자 저장한다. 첫 유효 저장 전 상태를 구별하고 같은 물리 Marker 재접촉은 갱신하지 않는다. 전신 통과·퇴로 안전 확인 뒤 추격 시작 직전에 현재 상태를 저장한다. 사망은 같은 Stage 초기 재준비 후 마지막 세 값을 복원하며, 완료/Skipped 대화 장부는 능력 복원 때문에 지우지 않는다.
- 현재 목표는 응집형 Prototype Stage 한 곳에서 전체 게임 흐름 1회다. Stage 1→2→3 자동 연결을 추가하지 않으며 등록 씬과 향후 본편의 독립 진입을 보존한다.
- 위치·끝점·경로·대상은 실제 Transform/컴포넌트/자산을 드래그한다. 속도·시간·높이·방향은 명확한 수치/선택으로 남긴다. 한국어 이름과 Tooltip은 무엇을 연결/조절하는지 짧게 설명하고 필요한 단위·0·좌표계·기즈모만 덧붙인다.
- 씬 관리자·카메라·Canvas·월드 오브젝트는 독립 씬 루트다. 빈 Root/Render/Collider 규칙은 개별 기능 프리팹에 적용한다. HMS ActorBody 동거, Shell 실제 물리 Root, 움직이는 Rigidbody 아래 Render의 예외를 명시한다. 눈물은 수집 Root와 Collider가 함께 움직인다.
- 주기 발판 5/2/3초는 조절 가능한 초기값이다. 등장 및 이동 중 실제 세로 침투 비율이 1/4 미만이면 정상 착지 보정, 1/4 이상이면 실제 몸체를 위로 분리하며 사망한다. 껍질은 별도 분리하며 데드존에서 같은 소유 껍질을 현재 플레이어로 반환한다.
- 에이전트는 Editor Play 버튼·PlayMode API/CLI/pipeline/batch 테스트·직접 플레이를 실행하지 않는다. 비 Play 검증과 실제 사람 플레이를 구분한다. QA/FunQA 평가 문서를 요청 없이 작성하지 않는다.
- 기존 프리팹 삭제·재생성·Apply·리소스 제작은 사람이 한다. 소스 정리는 실제 호출·GUID·중첩 참조 이관 후 수행한다. HudIconGraphic은 실제 하트 리소스 연결 전 삭제 대기다. 자동 Builder로 자산을 다시 만들지 않는다.
- 이번 HMS 예외는 PlayerAnimationPlayback.cs의 Interact 우선순위 2→4와 관련 설명, PlayerAnimationController.cs의 관련 설명만이다. 이전 9개 파일의 한정 허가는 종료됐으며 다른 보호 원본·meta/import/Packages/ProjectSettings 수정 권한을 확대하지 않는다.
