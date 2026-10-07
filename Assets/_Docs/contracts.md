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

메인 CheckpointSnapshot은 **Position(Vector3)/Fragments(int) 정확히2개 공개 저장 속성**을 가진 읽기 전용 값이다. Position은 CP 접촉 시 ActorBody 월드 중심으로 XYZ가 모두 유한해야 하며 Fragments는0..5다. 생성/검증 성공 뒤 GameSessionService가 두 값을 함께 교체하고 실패면 이전 기록을 지킨다. 이전 StageId/CheckpointId/Fragments3필드 설명은 변경 전 계약이다.

StageId/CheckpointId·회전·HP·폼·능력·대화·카메라·적·기믹·시간을 payload에 추가하지 않는다. 이후 원본의 다른 저장 요구도 무시한다. SceneCatalog/현재 FlowStage와 Marker ID/Pose는 실행의 씬/배치 설정이며 저장 항목으로 우회하지 않는다. 중복 CP판별은 입력 문맥에서 수행한다. 초기 진입은 Stage/Entry의 Pose를 조회해 두 값을 만들고 CP수락 때 현재 PC의 두 값으로 갱신한다. retry에는 이미 선택된 Stage를 초기 재준비한 뒤 저장 Position/Fragments만 복원한다. 월드 DTO·등록자·객체별 저장 서비스·앱 종료 후 진행 파일은 없다.

## 카메라: 현재 API와 계획 API

HMS PlayerCameraController는 T1 opt-in 분리 리그 API와 기존 enum/기본값/독립 소비를 유지한다. App 출력1/Core Side·Quarter와 삭제32/7필드·11자산은 이관됐다. 최종 R2의 추가 공개 API는 ServiceLocator.IsDisposed 읽기 전용값뿐이며 serialized/meta 계약은 유지한다. ServiceLocator.IsDisposed는 기존 단조 disposed flag의 읽기 전용 공개값이다. Bootstrapper가 App Cleanup 전에 app scope를 Dispose하고 Core가 먼저 Unity-null이 된 Stop 순서에서 App은 cleaning 또는 자기 scope disposing일 때만 누락 Core의 해제를 건너뛴다. 정상 실행 중 Core 누락·실제 owner 해제 오류는 계속 보고한다. Flow의 사전 취소/Disposed Publish guard와 EnemyActor exactPhase2는 유지한다. `T10-route-stop-console-product-RED-R1.json`은 보존하고 R2 Stop console 새 오류0과 연결한다.

**현재 공개 Bind/Unbind/Preview/Commit 계약**을 사용한다. Bind는 Mode와 서로 다른 Side/Quarter를 받고 재바인딩 전에 자기 구독을 해제한다. Follow/LookAt의 CameraAnchor와 Brain 연결은 외부 조립자가 소유한다. Unbind는 자기 구독과 borrowed Mode/전환 참조만 비우며 빌린 객체를 파괴하지 않는다. Preview는 선택만 바꾸고 물리 축은 유지한다. 실제 Brain 선택/블렌드와 Side Z정렬 뒤 Commit이 HMS모드를 확정한다. 우선순위는 HMS, 출력 Transform은 Brain, 요청/입력 제한은 LevelCameraService가 소유한다. 초기 Quarter reset 보정은 Phase2 메인에 있으며 Options사망 두경로 각10GREEN이 실제ready를 확인했다. 최종4소스 compile 실패false/오류0와9진입/Stop은 확인했다. Windows 최종 수락·전체 수동 동선은 별도다.

출력 Camera/Brain/Listener는 App각1개, 두 가상 카메라는 Core다. CameraZoneData의 ID/Movement/AlignmentOffset은 배치 설정이다. 새 SYM허용값은 Side=1/Quarter=3이며 Corridor=2는 명시적으로 Quarter배치로 이관하거나 폐기 참조를 제거한다. 저장값을 조용히 압축하지 않는다. Resolve는 자기 TransformPoint로 월드 요청을 만들고 파란 기즈모와 같은 계산을 쓴다. 초기 모드는 배치 구간 설정에서 결정하고 checkpoint에 저장하지 않는다.

## 한 판 범위와 Stage 초기 재준비

App ServiceLocator는 앱 수명이고 GameSessionRuntime의 App 부모 Core 범위·Session/PlayerBridge/Dialogue·CoreWorldBinding/CoreUiBinding은 한 판 수명이다. SceneCatalog/LoadedStageRegistry는 현재 씬 실행 구성을 조회하며 월드 저장 참가자 등록자가 아니다. 같은 Core/Stage3 씬을 유지해도 Title에는 ReleaseSession, NewGame에는 새 범위/VM 연결이 필요하다. retry에는 같은 한 판 범위를 유지한다.

StageService의 명시적 typed ResetForRetry가 기존 발판/장치/적/TearFragment/StageTrigger/카메라 접촉·추격 gate 소유자를 재준비한다. reset 전에 Actor/빌린 참조/작업을 해제하고 동일 소비 Tear 객체의 Visual/Collider/소비 플래그를 복구한다. GameSessionService.ResetStageUnlocks는 최초 부여 출처가 현재 Stage인 능력만 제거하며 Dialogue.ForgetCompletedCodes는 해당 안내 코드만 다시 실행 가능하게 한다. 초기 배치 캐시는 checkpoint나 WorldSave/ParticipantRegistry/ObjectIDs가 아니다.

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

미래 프리팹 가이드의 준비 기록과 현재 저장 자산을 구분한다. Phase2 소스/meta32개 삭제, 7필드/11자산 재저장, native32/61/12/참조0, actual 작성2회에는 완료 증거가 있다. 이를 전체 Play/빌드/아트 clip 수락으로 확대하지 않는다. Phase3-R2는 Flow/App/Kinematic/ServiceLocator4개 승인으로 갱신됐고 IsDisposed 읽기 전용 API 추가 외 enum/serialized/meta 계약을 보존했다. HMS1회9파일 예외도 보호 폴더 전체 정리·원본 prefab Apply·_Art·Packages·ProjectSettings 허가가 아니다.
