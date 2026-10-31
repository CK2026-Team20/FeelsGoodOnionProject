# 구현 경계에서 확인할 사항

> 아래 구조·API 설명은 현재 구현 사실이다. V1.8 요구를 이미 구현했다는 뜻이 아니다. 컷신 복귀·타이틀·카메라·추격 차이는 [구조 차이 목록](tracking/system-v18-2026-10-29/구조%20차이%20목록.md)을 따른다.

> 현행 시스템 권위는 V1.8 활성 본문 → 이전 사용자 기능 명세 → 기존 구현 설명 순서다. 지정 원본·예외는 [planning-index](planning-index.md)를 따른다. 구현과 검증 상태는 tracking/status.md 및 실제 증거로 확인하며 문서 개정만으로 완료를 선언하지 않는다.

## Actor와 같은 의미의 위치

일반 적의 위치 작성 대상은 EnemyActor Root가 아니라 자손 Body Transform이다(2026-10-29 T2). Start/End는 Root 내부·Body 외부의 고정 앵커이며 실제 월드 위치로 XYZ 경로를 정한다. Body 원점을 앵커 목표에 맞추므로 Collider.center와 앵커 위치를 혼동하지 않는다. 초기 Body 위치는 선분 밖이어도 보존하고 첫 목표 End 이후 왕복한다. 같은 두 앵커에서는 현재 위치에 정지한다. Retry는 최초 Body localPosition을 복원하고 End 목표·초기 대기·기절·접촉 상태를 재준비한다. Root/Body 회전·양수 비균등 배율을 허용하며 속도는 Body의 실제 월드 거리(m/s)다. 기존 Transform 값을 정규화하지 않는다. 지형 Mask·높이 제한·Cast/Sweep·침투 보정·갇힘 판정은 제거했고 플레이어 피해/기절 감지 Collider는 유지한다. 아래 눈물의 차폐 검사는 적 이동의 지형 검사와 다른 기능이므로 제거하지 않는다.

현재 위치는 PlayerFacade가 있는 실제 ActorBody의 월드 중심이다. 빈 관리 PlayerRoot나 Render Transform으로 CP·껍질 복귀·스킬 중심을 계산하지 않는다. ActorBody 아래 VisualRoot/CameraAnchor를 연결하고 실제 몸체·Collider 이동과 외형/추적이 함께 움직이게 한다. Root scale을 Form 전환 때 다시 줄여 Collider 전환과 이중 적용하지 않는다.

움직이는 발판은 하나의 KinematicPlatformMotion이 실제 Rigidbody를 이동한다. Render는 Body 아래, 시작/끝·닫힘/열림·경로 Marker는 Body 밖이다. 조작/안내 Point는 손잡이를 따라가야 하므로 Body의 자식이 될 수 있다. 고정 목적 Point와 현재 조작 위치를 혼동하지 않는다. Shattered는 Animator 없는 경고 Shake도 Visual을 움직인다. 그 Visual/자식의 고정 Point를 거절하고 static 논리 Root의 Marker 형제는 허용한다.

Chase visual은 자기 Root의 strict child·physics-free만 수락한다. 잘못된 Bind는 Unbind 이전에 거절하고 실제 수락한 boundVisual만 복원/숨긴다. Inspector가 이후 다른 외형을 가리켜도 borrowed 수락 객체의 해제 책임을 잃지 않는다. 포획은 실제 Physics 자식 BoxCollider와 현재 Actor의 물리 CapsuleCollider로 판정하며 메시 bob을 충돌 진실로 쓰지 않는다. ChaseRuntimeDriver가 +X 이동과 포획을 함께 담당한다.

TearFragment는 Sensor와 같은 GameObject에서 Trigger를 받되 pickupRoot 전체를 Tween한다. 같은 Root/센서를 소비하고 최대5에서는 남긴다. Stage 재준비는 비활성 Root까지 찾아 초기 pose/Collider/소비 플래그를 복구한다. 수집 객체의 새 저장 ID나 별도 pooled 교체를 추가하지 않는다.

## 접촉·피해·생성 발판

Physics.OverlapSphere는 후보 탐색이다. 실제 Hurt Collider까지의 선분 차폐를 확인하고 Trigger Body를 탐지 Sensor라는 이유로 일괄 제외하지 않는다. 후보 검증 뒤 dedupe하여 Sensor 후보가 먼저 나와 Body 후보를 소진하지 않게 한다. 기절은 일반 적만이고 공통 피해/무적을 적마다 다시 계산하지 않는다.

생성 발판의 최소 분리 거리와 수직 몸 높이 침투는 다른 값이다. 현재 몸 Collider의 실제 높이와 세로 침투로 분류한다. 정확히1/4는 사망, 얕은 경우 actual Body를 정상 지지 위치로 보정한다. 몸/Render만 움직이는 이중 writer·가짜 Grounded·별도45도 상수를 만들지 않는다. HMS MaximumGroundAngle getter로 실제 Inspector 접지 설정을 읽는다. 정상 상부 착지와 이동 중 materialization도 구분한다.

Shell 끼임은 플레이어 사망으로 변환하지 않는다. 현재 Form의 같은 OwnedDebris만 분리/반환하고 HP·Small·무게·Owner·IgnoreCollision을 보존한다. 데드존은 실제 Volume이며 임의 killHeight가 아니다. stale Owner/Actor/Stage의 접촉은 거절하고 반환은 새 생성·Q 회수가 아니다.

## CP와 최종 대화 맥락

시작 체크포인트와 저장 체크포인트는 같은 StageTrigger다. 최초 등장 전에는 완료 Snapshot이 없으며 초기 트리거의 Transform 위치와 Entry 조각·마스크를 사용한다. 접촉은 트리거의 고정 위치·현재 조각·마스크를 전부 검증하고 교체한다. 같은 트리거 재접촉 억제를 Actor 교체로 비우지 않는다. 등장·복귀는 플레이어 CapsuleCollider 월드 중심을 목적지에 맞춘다. 비활성 Collider의 attachedRigidbody는 비어 있으므로 같은 오브젝트의 Rigidbody를 직접 확인하고 Normal 준비 후 중심을 다시 맞춘다. 추격은 당시 Collider 중심 저장 성공 뒤 시작한다. 저장 실패가 문 닫힘·추격 시작으로 이어지지 않아야 한다.

일반 대화 Completed/Skipped 장부와 능력 마스크 rollback은 수명이 다르다. 해금을 복원해도 안내 완료 기록은 지우지 않는다. 최종 포포만 사용자가 선택한 재실행 예외이며 Code 전체 장부를 Clear해서 해결하지 않는다. 한 회차의 Stage·Trigger·선택 객체·Table·Code·Actor세대·Round·예상 Session을 캡처하고 준비/시작/종료/예약 실행에서 재검증한다. 값이 같은 다른 선택 객체, 같은 위치의 다른 트리거, 같은 Code의 다른 판을 같은 요청으로 취급하지 않는다.

완료 observer가 재진입하여 다음 대화/판/Actor를 만든 뒤 옛 콜백이 돌아오는 경우를 처리한다. 낡은 맥락은 자기 booking/subscription만 해제하고 현재 Session을 임의 취소하지 않는다. 같은 프레임 사망·포획이 POPO Ending보다 우선하며 취소는 완료가 아니다. Round/Session 증가와 expected ID는 경계에서 검증하고 overflow를 숨기지 않는다.

## UI·작업·해제

HMS Reader/PlayerController가 이동·점프·스킬 입력을 한 번 처리한다. Host는 F만 처리하고 Bridge는 슬롯 사용 조건·모달 제한·피드백을 연결한다. View 버튼 OnClick에 이미 VM에 연결한 게임 명령을 다시 등록하지 않는다. Auto/Log/Options는 표시 시계와 예약 잔여시간을 정확히 소유하며 로그 닫힘 뒤 Auto를 자동 재개하지 않는다. 메모 Opening F닫기와 Closing 제한, 중첩 Options의 자기 토큰 반환을 보존한다.

타이틀 복귀·사망·Actor 파괴·Dispose는 생성한 Coroutine/Tween·예약·이벤트를 취소/해제한다. 부분 초기화 실패에도 이미 획득한 자원을 정리하고 실패를 성공처럼 반환하지 않는다. CoroutineOperationRunner의 nested iterator/finally를 유지하고, 삭제된 SessionShutdownOperation의 집계 해제는 GameRuntimeService의 소유 종료 iterator에서 처리한다. App scope disposing 중 이미 파괴된 Core와 정상 실행 중 Core 누락을 같은 성공으로 취급하지 않는다.

## 조립과 씬 로드·종료의 경계

같은 씬의 실물 참조·고정 지점 검사는 이미 주입된 객체만 사용하므로 Awake/OnEnable에서 scene.isLoaded를 요구하지 않는다. 반면 FindAll/RequireSingle처럼 씬 전체 Root를 조회하는 작업은 로드 완료가 필요하다. AppComposition은 Start에서 출력 단일성을 확인한다. AppComposition이 Flow를 등록하고 옵션·페이드·오디오 소비자까지 연결한 후 Boot를 실행한다. 삭제한 Foundation/Bootstrapper.cs의 초기화·종료 책임을 다시 분리하거나 존재하지 않는 App.cs를 수정 대상으로 삼지 않는다. 서비스 준비와 전환 시작을 섞지 않는다.

CoreWorldBinding은 연결한 StageService·PeriodicPlatform 참조를 보관하고 그 객체만 해제한다. 정상 retry/Title 복귀에는 살아 있는 객체를 정리하며 Unity 종료 중 이미 파괴된 객체는 건너뛴다. 종료 시 Stage를 다시 검색하지 않는다. 실제 정리 실패는 집계하며, 정상 실행 중 미로드 씬 검색이나 잘못된 참조를 임의로 성공 처리하지 않는다. Flow revision/state/busy의 Debug.Log는 상태 기록이고 참조 오류·전환 실패와 구별한다.

## 실행 증거의 범위

Source compile, 직접 실제 DLL 공개 API 검사, Native import/SerializedObject·격리 엔진, Windows BuildReport, 사람 Play·실제 clip·QA/FunQA는 각각 수행한 범위다. helper compile-only·포트·예약 수락·파일 존재·NOT_EVALUATED를 평가한 assertion 성공으로 간주하지 않는다. 제품 Source를 helper와 함께 중복 compile해서 CS0436으로 실행해도 실제 DLL 검증이 아니다. source SHA/DLL MVID·loaded path·명령/exit와 실패 원문을 보존한다.

에이전트는 Play 버튼·API/CLI/pipeline·batch PlayMode·직접 플레이·Player 실행을 하지 않는다. 미저장 Scene/prefab을 덮거나 강제 종료하지 않는다. 편집/빌드 자동 변화는 원래 기준과 실제 전후 diff·원인으로 분류한다. TMP clearDynamicDataOnBuild의 cache, URP RuntimeSettings 파생 cache, EOL/index stat 변화는 원본 기능·아트 변경과 구분한다. 실제 byte 변화 없는 stat/EOL을 커밋 내용 수정으로 보고하지 않는다.

## 비 Play 직렬화·컴포넌트 이관·트윈 검사

Scene 참조는 native SerializedObject의 SerializedProperty.objectReferenceValue와 실제 대상 객체를 기준으로 확인한다. EditorJsonUtility의 preview에서 instanceID가0으로 표현되거나 base prefab의 필드가 null이어도 Scene instance override까지 누락됐다고 단정하지 않는다. 실제 씬·프리팹 내용을 로드해 해당 property와 대상 이름·씬을 확인하고 근거를 남긴다.

MoveComponentToGameObject를 사용할 때 RequireComponent 의존 컴포넌트의 동반 이동과 대상 GameObject에서 의존 컴포넌트가 자동 생성되는지 검토한다. 이동 전후 양쪽 컴포넌트 목록·직렬 참조를 비교해 중복·잔존·새 생성 여부를 확인한다. 의도한 컴포넌트 하나만 이동됐다고 가정하거나 보호 원본을 임의 정리하지 않는다.

비 Play 검사에서 DOTween.initialized가 false라면 Kill 경로를 실제 평가하지 못한 경우가 있다. 호출 반환이나 helper 성공만으로 트윈 종료를 PASS로 기록하지 않는다. 소유 트윈의 생성·활성 상태, Kill 후 실제 inactive, 다음 프레임 입력 차단 해제를 각각 확인하며 실행하지 않은 경로는 미평가로 남긴다. 검증을 통과시키기 위해 TimeManager의 전역 시간 설정을 수정하지 않는다.

## 체크포인트 완료와 검증 시점

시작 보정의 정확도는 제어 제한 해제 callback에서 동기로 Capsule 중심을 측정한다. 뒤 프레임의 중력·접지 skin 낙하와 섞지 않는다. 최종 Small 실행에서는 완료 순간 오차0 뒤 약.0675m 내려와 지지면에 접지했다.

공식 도구 안에서 비동기 작업을 끝까지 기다릴 때 PlayerLoop가 정체되는 실행 조건이 있었다. 검증 작업을 시작하고 즉시 반환한 뒤 상태를 조회하여 실제 프레임 진행을 확인한다. 도구 timeout은 작업 취소가 아니므로 자기 task·구독·가상 키보드·임시 InputSettings를 회수한다.

GameView의 camera 렌더는 Screen Space Overlay를 담지 못한다. 메모·옵션·연출·대화 화면은 실제 screen 캡처를 읽어야 하며 파일 생성만으로 시각 통과를 선언하지 않는다. 옵션을 닫는 프레임의 입력은 아래 메모에 전파하지 않는다. 다음 입력을 검증할 때 실제 다음 프레임 이후에 명령을 보낸다.
