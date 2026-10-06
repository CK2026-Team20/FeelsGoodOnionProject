# 협업 및 데이터 계약

현재 네트워크 소비자용 API는 없다. 아래 계약은 다른 작업자의 플레이어·월드 기능을 연결하거나 프로젝트 데이터를 공급하는 개발자가 사용하는 경계다. 파일 수정 권한과 런타임 API 사용 권한은 다르며 공개 API를 호출한다고 원본 파일 수정 권한이 생기지 않는다.

공개 API·이벤트 소비와 자기 소유 씬·프리팹의 컴포넌트 추가·설정·연결 및 배치 override 저장은 참조 허용에 포함한다. 타 작업자 원본 코드·자산·.meta·import 설정의 직접 수정, 원본 Apply·이동·삭제·GUID 변경은 별도 명시적 파일 허가가 필요하다.

## 앱·플레이 출력 수명

GameSessionRuntime의 GameplayVisible은 표시 요청, ActorViewReady는 HMS/초기 Brain 준비 완료다. GameRuntimeService의 GameplayOutputChanged는 두 상태가 모두 참일 때 실제 플레이 출력이 준비되었다고 통지하며 독립적인 표시 상태를 저장하지 않는다. ActorRemoving에서 이전 ActorCameraRig 출력을 먼저 끄고 false를 통지한다. 재시도는 GameplayVisible이 계속 true여도 새 Actor 준비가 끝나면 다시 통지한다.

AppComposition은 자기 하위 presentationCamera와 같은 오브젝트의 AudioListener 하나만 소유한다. 플레이 출력 true에서는 둘을 끄고 false에서는 켠다. 이전 세션의 이벤트만 종료하고 앱 구독은 다음 판까지 유지한다. 앱 파괴에서는 자기 구독을 해제하고 자기 출력을 끈다. Actor 출력 Transform은 CinemachineBrain이 갱신한다.

## 공용 상호작용·전투

| 호출 | 입력 | 출력과 거절 |
|---|---|---|
| IInteractable.Interact() | 추가 인자 없음 | bool: 수락·성공 true, 불가 false; 반환값을 무시하고 별도 실행하지 않음 |
| IDamageable.Damage(int damage) | 요청 피해량 | 실제 적용 피해량; 호출했다는 사실만으로 피해 성공을 판단하지 않음 |
| IDamageable.IsInvincible() | 없음 | 현재 무적 여부 |
| IStunnable.TryStun(int stunDur) | 밀리초 단위 시간 | 실제 적용 기절 시간(밀리초); 초 단위 데이터는 경계에서 변환 |
| IStunState | IsStunned 조회, StunStateChanged 구독 | true 적용·false 해제 통지; 구독자는 자기 구독 해제 |
| IKnockbackable.ApplyKnockback(Vector3 velocity, float controlLockDuration) | 초기 속도와 밀리초 제어 제한 시간 | 반환 없음; 시간 0 이하는 추가 제어 제한을 요청하지 않지만 넉백 요청 자체는 유지 |

세부 거절 조건은 구현별로 다르다. 공용 인터페이스에 없는 효과를 성공 반환만 보고 추가로 추정하지 않는다.

## 플레이어 경계

PlayerFacade.Model은 IReadOnlyPlayerModel의 체력·최대 체력·조각·최대 조각·사망 여부와 변경 이벤트를 제공한다. 외부에서 필드나 컬렉션을 수정하지 않는다. AddSkillFragments(int)는 실제 추가량을 반환하고 TryConsumeSkillFragments(int)는 양수 요청과 충분한 보유량에서만 true를 반환한다.

PlayerBridgeService의 Tear/ChangeForm true는 요청 수락이다. 실제 효과음·성공 표시는 AbilitySucceeded에서 처리한다. 준비 취소를 발동 성공으로 표시하지 않는다. Attach에는 플레이어 Facade와 필수 스킬·폼·이동 컴포넌트가 필요하다. Detach 후 빌려 쓴 모델·Transform을 계속 사용하지 않는다.

Attach(null)은 ArgumentNullException, 폐기 후 Attach는 ObjectDisposedException, 해제 중 Attach 재진입은 InvalidOperationException으로 거절한다. 연결·해제 관찰자나 정리 작업의 실패도 호출자에게 전달될 수 있다. 예외가 발생했다는 사실만으로 연결·해제가 전혀 진행되지 않았다고 판단하지 않는다.

CheckpointSnapshot 입력은 공백 아닌 StageId·CheckpointId와 0 이상 Fragments다. 잘못된 값은 예외다. 체크포인트 위치 조회 TryGetCheckpointPose는 찾으면 true와 Pose, 없으면 false다. Pose는 현재 ActorBody 중심 기준이며 지면 높이 자체가 아니다.

## 대화 테이블 JSON

루트는 비어 있지 않은 Rows 배열 하나만 가진다. 각 행은 다음 다섯 필드를 정확히 가진다.

```json
{"Rows":[{"Dialogue_Code":"EXAMPLE","Sequence":1,"Name":"어니","Context":"대사","ContextRevealDuration":2.0}]}
```

Dialogue_Code·Name·Context는 공백만으로 이루어지지 않은 비어 있지 않은 문자열이다. 코드의 앞뒤 공백은 금지한다. Sequence는 양수 Int32이고 같은 코드 안에서 중복하지 않는다. 행은 Sequence 오름차순으로 정렬하며 번호의 연속성은 요구하지 않는다. ContextRevealDuration은 0 이상 float.MaxValue 이하의 유한한 초 값이다. 0은 즉시 표시 완료다.

중복 JSON 키, 추가 필드, 빈 배열, 뒤따르는 다른 JSON 내용, 잘못된 타입·수치는 FormatException이며 소스명과 필드 문맥을 포함한다. 전체 검증 성공 후에만 테이블을 게시한다. 초기화 뒤 재로딩은 InvalidOperationException이다. TryGetRows는 미로딩·미등록·null 코드에서 false와 null을 반환한다. 성공 시 읽기 전용 행 목록을 반환한다.

## 흐름·비동기 작업

IGameFlowService.Request의 bool은 상태상 요청 수락 여부다. 수락이 최종 전환 완료를 의미하지 않는다. FlowSnapshot의 State·IsBusy·Error를 관찰한다. 중복 전환과 허용되지 않은 상태의 명령은 false다.

OperationResult는 Pending에서 Succeeded·Failed·Cancelled 중 하나로 끝난다. 코루틴 실행 중 취소 토큰을 전달하고 취소를 성공으로 변환하지 않는다. 연출은 Prepare → Play → Hide 순서이며 이전 연출을 숨기지 않은 Prepare는 실패 결과다. 대화 TryStart는 이미 진행 중·옵션 정지·없는 코드일 때 false다. 명령을 이벤트와 직접 호출 양쪽으로 중복 전달하지 않는다.

## 프로토타입 연결 계약 (2026-10-05)

LoadedStageRegistry는 선택한 StageId와 일치하는 스테이지 하나만 요구한다. 설치의 첫 경로는 Prototype/1_Stage이며 기존 Stage2/3 직접 진입은 각 기존 설정을 사용한다. HMS PlayerMovementModeController가 모드·속도·물리 축을 소유한다. TrySetMode는 활성 상태와 Start의 IsInitialized 이후에만 수락하며 같은 모드는 이벤트를 재발행하지 않는다. Side/Quarter/Corridor 요청은 각각 Side/Quarter/BackFixed에 대응한다. 실제 Brain 블렌드와 통로 X/side Z 정렬 완료 후 모드를 확정하고 Bridge는 CharacterMovement의 공개 축 값을 소비한다. 초기 체크포인트 모드는 출력이 숨겨진 상태에서 적용하고 카메라 준비 전 생성 작업을 성공 처리하지 않는다.

MemoUIController.Initialize(control)는 정지 토큰 소유를 조립하며 E닫기는 Gameplay CanAct 검사보다 먼저 처리한다. 메모 View에는 마우스 닫기 버튼·리스너가 없으며 E만 열기·닫기를 요청한다. 최상위 옵션이 있으면 아래 메모를 닫지 않는다. 컷신 관찰자 예외가 결과를 Cancelled로 덮거나 정지 토큰 해제를 막지 않도록 결과 확정과 독립 정리 오류를 구분한다.

## 정렬 설정과 전환 요청

CameraZoneData의 직렬화 필드는 Id(string)·Movement(CameraMovementMode)·AlignmentOffset(Vector3)이다. Movement 저장값은 Side=1, Corridor=2, Quarter=3이다. AlignmentOffset은 소유 트리거/체크포인트 자신의 로컬 좌표이며 Resolve(Transform)가 TransformPoint로 월드 위치를 계산한다. readonly CameraZoneRequest는 Id·Movement·AlignmentPosition을 제공한다. LevelCameraService.EnterZone(request, snap)는 유효한 비어 있지 않은 ID·지원 모드·유한 XYZ 요청만 처리한다. 잘못된 요청은 기존 전환을 취소하거나 CurrentZoneId를 바꾸지 않는다. snap 초기 적용은 현재 Actor 생성 위치를 이동시키지 않는다.

CheckpointMarker의 CameraZone은 같은 Resolve 결과를 제공한다. 기존 빈 ID 체크포인트는 CoreWorldBinding이 StageId별 초기 모드로 대체한다. 체크포인트 bodyOffset은 별도 월드 생성 이동량이므로 정렬 오프셋으로 해석하지 않는다.

## 붕괴 Animator 자산 계약

ShatteredPlatform의 collapseAnimator는 지정한 visual 자체의 Animator를 참조한다. 지원 Collider는 루트에 유지하며 Visual와 자식에는 Collider/Rigidbody를 연결하지 않는다. Controller의 첫 Base Layer에 기본 Ready, Collapse, Completed 상태와 Collapse Trigger를 둔다. Ready→Collapse는 Trigger 조건·전환 시간 0, Collapse→Completed는 Exit Time 1·전환 시간 0이다. Ready/Completed Motion은 비우고 실제 단일 비반복·양수 길이 클립을 Collapse Motion에 연결한다.

Animator는 양수 속도·게임 시간에 따르는 Normal/Fixed 갱신·root motion 비활성을 사용한다. 실제 상태와 클립 재생 완료를 관찰하며 고정 시간으로 성공 완료를 대신하지 않는다. 클립 없음은 경고와 물리 붕괴·숨김을, 잘못된 Controller/상태/참조나 진행 정지는 오류와 같은 안전 후처리를 제공한다. 비활성화·재사용 시 자기 작업을 취소하고 Visual 자식의 원래 위치·회전·크기·렌더 상태와 Ready를 복원한다. 별도 Animator나 보호 원본 자산은 임의 변경하지 않는다.
