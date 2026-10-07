# 협업·데이터 계약

> 2026-10-07 사용자 확정안과 추가 답변을 적용한다. [확정안 적용 명세](<tracking/ready-system-v11-refactor-2026-10-06/확정안 적용 명세.md>)가 이전 초안의 충돌 문구를 대체한다. 구현/실행 완료는 [상태](tracking/status.md)와 실제 검증 증거로 판단한다.

최신 목표와 현재 실재 API를 구분한다. 공개 API소비와 소유 SYM객체 조립은 허용하지만 HMS·공용·아트 원본/메타의 수정 권한은 별도다. 세부 목표는 [기능 명세서](<tracking/ready-system-v11-refactor-2026-10-06/기능 명세서.md>), 필수 예외는 [보호 원본 변경 요청](<tracking/ready-system-v11-refactor-2026-10-06/보호 원본 변경 요청.md>)를 따른다.

## 상태와 공용 경계

HP/조각 원본은 HMS PlayerFacade.Model/IReadOnlyPlayerModel이다. AddSkillFragments는 실제 추가량, TryConsumeSkillFragments는 충분한 보유량에서의 성공을 반환한다. UI/Bridge에 두 번째 가변 원본을 두지 않는다. Attach/Detach는 빌린 모델·Transform·이벤트를 연결·해제하고 관찰자 예외에도 나머지 정리를 수행한다.

| API | 현재 공용 의미 |
|---|---|
| IInteractable.Interact | bool수락·성공; 같은 입력을 다시 실행하지 않음 |
| IDamageable.Damage / IsInvincible | 실제 적용 피해량 / 현재 무적 |
| IStunnable.TryStun | 밀리초 단위 실제 적용 기절 시간 |
| IStunState | 읽기·이벤트, 소비자는 자기 구독만 해제 |
| IKnockbackable.ApplyKnockback | 초기 속도와 밀리초 제어 제한; 경계에서 초로 변환 |

공용 원본은 변경하지 않는다. 새 공통 피격·강제 사망은 HMS Facade의 파일 예외 공개 계약으로 연결한다. 요청 수락과 실제 효과 성공을 구분하며 UI/소리는 실제 성공 통지를 소비한다.

## 체크포인트 두 값

현재 CheckpointSnapshot은 StageId/CheckpointId/Fragments3필드다. 목표는 **Position(Vector3)/Fragments(int) 정확히2필드**다. Position은 캡처 시 ActorBody월드 중심으로 XYZ가 모두 유한해야 하며 Fragments는0..5다. 두 값을 검증한 뒤 한 번에 교체하고 실패면 이전 기록을 지킨다.

StageId/CheckpointId·회전·HP·폼·능력·대화·카메라·적·기믹·시간을 payload에 추가하지 않는다. 이후 원본의 다른 저장 요구도 무시한다. SceneCatalog/현재 FlowStage와 Marker ID/Pose는 실행의 씬/배치 설정이며 저장 항목으로 우회하지 않는다. 중복 CP판별은 입력 문맥에서 수행한다. 초기 진입은 Stage/Entry의 Pose를 조회해 두 값을 만들고 CP수락 때 현재 PC의 두 값으로 갱신한다. retry에는 이미 선택된 Stage를 초기 재준비한 뒤 저장 Position/Fragments만 복원한다. 월드 DTO·등록자·객체별 저장 서비스·앱 종료 후 진행 파일은 없다.

## 카메라: 현재 API와 계획 API

현재 HMS PlayerCameraController는 Mode와 네 가상 카메라를 Awake전에 요구하며 재연결 API가 없다. ActorCameraRig와 App출력 교대는 변경 전 구현이다.

목표의 **Bind/Unbind/Preview/Commit은 현재에 없는 계획 API**다. Bind는 준비된 Mode·CameraAnchor·서로 다른 Side/Quarter를 명시적으로 받는다. 재바인딩 전에 자기 구독을 해제한다. Unbind는 borrowed 목표·Mode·전환 참조를 비운다. Preview는 카메라 선택만 바꾸고 물리 축은 유지한다. 실제 Brain/Side Z정렬 뒤 Commit이 HMS모드를 확정한다. 우선순위는 HMS, 출력 Transform은 Brain, 요청/입력 제한은 LevelCameraService가 소유한다.

출력 Camera/Brain/Listener는 App각1개, 두 가상 카메라는 Core다. CameraZoneData의 ID/Movement/AlignmentOffset은 배치 설정이다. 새 SYM허용값은 Side=1/Quarter=3이며 Corridor=2는 명시적으로 Quarter배치로 이관하거나 폐기 참조를 제거한다. 저장값을 조용히 압축하지 않는다. Resolve는 자기 TransformPoint로 월드 요청을 만들고 파란 기즈모와 같은 계산을 쓴다. 초기 모드는 배치 구간 설정에서 결정하고 checkpoint에 저장하지 않는다.

## 입력·표시·비동기

Reader/Router/상호작용 선택은 각각 하나다. E눈물/F상호작용/Q폼을 처리한다. 열린 메모의 F닫기는 gameplay판정보다 먼저 처리하되 Options가 위에 있으면 전달하지 않는다. 장치 수락 한 입력에서 작동과 Interact표현이 시작된다. View는 VM의 명령/상태만 소비하며 VM은 View를 참조하지 않는다. 완료 RequestId는 다른 Actor·Session·낡은 표현에 적용하지 않는다.

Gameplay/World/Presentation은 독립적인 소유형 제한이다. Memo는 World토큰을 소유하며 Dialogue 자체는 World토큰을 갖지 않는다. Options는 전체 시간을 멈추고 닫을 때 자기 제한만 해제한다. 타이머·비동기·트윈·구독은 수명 종료에 취소/Dispose한다. OperationResult의 Pending→Succeeded/Failed/Cancelled, 요청 수락과 완료를 구분하며 복구 실패를 성공으로 숨기지 않는다.

## 대화와 연출 데이터

대화 JSON루트는 비어 있지 않은 Rows하나이며 행마다 다음 다섯 필드를 가진다.

| 필드 | 형식/제약 |
|---|---|
| Dialogue_Code | 공백 아닌 문자열, 앞뒤 공백 금지 |
| Sequence | Int32, 같은 코드에서1..N연속·중복 없음 |
| Name | 공백 아닌 문자열 |
| Context | 공백 아닌 문자열 |
| ContextRevealDuration |0이상 float.MaxValue이하 유한 초,0은 즉시 표시 |

중복키·추가 필드·잘못된 타입·빈 Rows·후행 JSON은 소스 문맥이 있는 FormatException이다. 전체 검증 후 읽기 전용으로 게시한다. 완료 코드 장부는 한 판의 런타임 상태이며 checkpoint저장 필드가 아니다.

연출 목표 Catalog는 Opening/Ending별 순서 있는 Cut목록이다. Cut은 원화 참조·하단 문구·Duration초(기본4, 양수 유한)다. 컷 수4개 제한은 없다. 원화 누락은 필수 자산 오류/연결 대기로 기록한다. Completed/Skipped/Cancelled와 Prepare→Play→Hide를 유지하며 새로운 PlayableAsset/Track호환 계층을 추가하지 않는다.

## 계층과 검증

빈 Root는 컨트롤러/데이터, Visual은 Renderer/Animator, Physics는 콜백/물리를 맡는다. HMS Facade/ActorBody동거는 GetComponent/attachedRigidbody계약 때문에 제한된 예외로 유지한다. 소유 SYM발판은 명시적 Body/Contacts참조로 이관한다. 같은 몸체에 이름만 다른 이동자를 중복 연결하지 않는다.

미래 프리팹 가이드의 새 필드/API는 목표다. 실제 저장·재로드·Play검증 전 완료로 표시하지 않는다. 예외를 허가받아도 원본 GUID/보호 prefab Apply/아트·Packages·ProjectSettings의 수정 허가로 확대하지 않는다.
