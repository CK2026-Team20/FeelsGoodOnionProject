# 시스템 책임과 현재/목표 구조

> 2026-10-07 사용자 확정안과 추가 답변을 적용한다. [확정안 적용 명세](<tracking/ready-system-v11-refactor-2026-10-06/확정안 적용 명세.md>)가 이전 초안의 충돌 문구를 대체한다. 구현/실행 완료는 [상태](tracking/status.md)와 실제 검증 증거로 판단한다.

2026-10-06 최신 지정 원본을 반영했다. 아래의 **목표**는 새 리팩터링의 설계이며 현재 코드로 완료했다는 뜻이 아니다. 코드/자산 사실은 [구조 검토](<tracking/ready-system-v11-refactor-2026-10-06/원본 대조 및 구조 검토.md>), 파일별 필요성은 [스크립트 명세](<tracking/ready-system-v11-refactor-2026-10-06/스크립트 필요성 및 정리 명세.md>)를 따른다.

| 수명/경계 | 상태의 단일 소유자와 필요한 책임 | 목표 변경 |
|---|---|---|
| 앱 | AppComposition·GameFlowService·SceneService·음량/AudioService | Output Camera/Brain/Listener 각1개를 앱 수명으로 유지 |
| Core/한 판 | CoreComposition·GameSessionRuntime·GameSessionService | 체크포인트/독립 능력/완료 대화 장부, Core 리그2개와 UI 유지 |
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

현재 카메라 리그는 PlayerRoot 아래 ActorBody의 형제이며, 출력 Camera/Brain/HMS 컨트롤러와 네 가상 카메라를 포함한다. HMS Awake의 네 참조 검사와 재연결 API 부재 때문에 비활성 Actor 안에서 먼저 조립하고 같이 재생성했다. 이 구조는 당시 활성화/해제 안전성의 선택이며 Cinemachine의 부모 계층 조건이 아니다. App의 대기 Camera/Listener와 Actor 출력은 실제 준비 상태에 따라 교대한다. CoreLevelCamera는 출력 Camera가 아니라 LevelCameraService 객체다.

새 목표는 App Output 하나와 Core Side/Quarter 두 가상 카메라다. HMS 컨트롤러에 명시적 Bind/Unbind/Preview/Commit 계약을 마련한 뒤 ActorCameraRig·출력 교대 구독·Back/Corridor 경로를 제거한다. retry에는 이전 목표를 먼저 해제하고 새 Actor의 HMS Start 준비/실제 Brain 완료 후 입력을 연다. 단일 출력은 유지하며 준비 중 월드 표시만 가린다. 이 API는 이번 명세 1회에 허용된 HMS 최소 확장으로 작성하며 실제 컴파일/조립 검증 상태는 별도 기록한다.

## 생성·재시도·종료

Title 명령 → Flow 전환 → Core1회 로드/등록Stage 전부 최초 additive 로드/활성Stage 초기 준비 → Actor 생성/현재 저장 Position·Fragments 두 값 복원 → 카메라 목표 연결/초기 모드 준비 → Opening 또는 Play 순서다. 준비 중 입력을 허용하지 않는다. 새 게임은 체크포인트/능력/완료 대화 장부를 비우고 Editor 직접 진입은 명시된 EntrySettings를 사용한다. 해금/대화 장부는 한 판의 실행 상태이며 checkpoint 저장 필드가 아니다.

retry에는 대화/메모/미완료 Actor 동작을 취소하고 빌린 카메라/월드 참조를 해제한다. Actor/껍질과 Stage를 초기 재준비한 뒤 플레이어 위치와 조각만 저장값으로 복원한다. 적/장치/필드 진행은 복원하지 않는다. Core/Session/VM과 App Output은 유지한다. Title 복귀에는 한 판의 Session/바인딩을 종료하고 CoreComposition 참조를 비운다. Core/모든Stage 씬과 음량/App Output은 유지하며 다음 판에 새 세션 범위를 연결한다. 비활성Stage의 Actor/트리거/추격/외부Tween은 중단한다. 외부 관찰자 예외가 다음 Dispose를 막지 않도록 종료 오류를 집계한다.

## 계층과 물리 예외

새 SYM 자원은 빈 Root 아래 Visual/Physics/VFX/SFX를 명시적으로 연결한다. 움직이는 발판의 로직은 Root, 물리 콜백은 실제 Body 자식이다. 필요한 소유 SYM 코드는 주입 참조로 바꾸며 과거 RequireComponent가 Root에 물리를 자동 추가하는 경로를 제거한다.

HMS 플레이어는 Facade·Movement·Form·Rigidbody·Collider가 같은 ActorBody에 있어야 하는 공개 계약이 있다. 이를 단순 계층 예시만으로 분리하지 않고 실제 Player 루트에 동거하는 물리 예외로 명세한다. CameraAnchor는 실제 움직이는 ActorBody의 자식이다. Trigger 센서와 EnemyContactSensor 역시 물리 콜백 자식의 이유를 명세하며 단지 Root를 맞추려고 전달자 파일을 늘리지 않는다. 전체 연결은 [프리팹 가이드](<tracking/ready-system-v11-refactor-2026-10-06/미래 프리팹 연결 가이드.md>)에 있다.

## 조립과 정리 원칙

전체206개 코드의 근거를 검토했고 SYM160개 중32개는 대체/참조 이관 후 삭제 계획이다. 보호46개는 읽기만 했다. 필요한 코드 안에서도 적 HP/사망·붕괴 자동 복구·옵션 Retry·Recover 카드·거리 Instruction·구출/래치 조건·4패널/scale·Actor 자식 카메라 탐색을 제거한다. 타인 독립 씬의 실제 소비 계약까지 이름만으로 삭제하지 않는다.

기능/상태/물리/표시 소유가 같은 중복은 합치고, 실제 자원 수명/외부 실행/여러 소비자를 구분하는 경계는 유지한다. _Art/공용/Packages/ProjectSettings를 변경하거나 Timeline 패키지를 삭제하는 계획이 아니다. 현재 서버/네트워크/앱 재시작 후 이어하기는 없다.
