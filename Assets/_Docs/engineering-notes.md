# 구현 경계에서 확인할 사항

> 2026-10-11 현행 시스템 원본은 [V1.6](Cooked_시스템_기획서_V.1.6.docx)이다. 아래 V1.5 동작·검증 설명은 기존 구현 자료이며 V1.6 구현 완료를 뜻하지 않는다. 새 변경과 미정 정책은 [사전 검토](tracking/ready-system-v16-2026-10-11/코드%20리뷰.md), 적용 우선순위는 [planning-index](planning-index.md)를 따른다. 소유권·Play 금지·미저장 작업 보호는 유지한다.

## Actor와 같은 의미의 위치

현재 위치는 PlayerFacade가 있는 실제 ActorBody의 월드 중심이다. 빈 관리 PlayerRoot나 Render Transform으로 CP·껍질 복귀·스킬 중심을 계산하지 않는다. ActorBody 아래 VisualRoot/CameraAnchor를 연결하고 실제 몸체·Collider 이동과 외형/추적이 함께 움직이게 한다. Root scale을 Form 전환 때 다시 줄여 Collider 전환과 이중 적용하지 않는다.

움직이는 발판은 하나의 KinematicPlatformMotion이 실제 Rigidbody를 이동한다. Render는 Body 아래, 시작/끝·닫힘/열림·경로 Marker는 Body 밖이다. 조작/안내 Point는 손잡이를 따라가야 하므로 Body의 자식이 될 수 있다. 고정 목적 Point와 현재 조작 위치를 혼동하지 않는다. Shattered는 Animator 없는 경고 Shake도 Visual을 움직인다. 그 Visual/자식의 고정 Point를 거절하고 static 논리 Root의 Marker 형제는 허용한다.

Chase visual은 자기 Root의 strict child·physics-free만 수락한다. 잘못된 Bind는 Unbind 이전에 거절하고 실제 수락한 boundVisual만 복원/숨긴다. Inspector가 이후 다른 외형을 가리켜도 borrowed 수락 객체의 해제 책임을 잃지 않는다. 포획은 실제 Physics 자식 BoxCollider와 현재 Actor의 물리 CapsuleCollider로 판정하며 메시 bob을 충돌 진실로 쓰지 않는다. ChaseRuntimeDriver가 +X 이동과 포획을 함께 담당한다.

TearFragment는 Sensor와 같은 GameObject에서 Trigger를 받되 pickupRoot 전체를 Tween한다. 같은 Root/센서를 소비하고 최대5에서는 남긴다. Stage 재준비는 비활성 Root까지 찾아 초기 pose/Collider/소비 플래그를 복구한다. 수집 객체의 새 저장 ID나 별도 pooled 교체를 추가하지 않는다.

## 접촉·피해·생성 발판

Physics.OverlapSphere는 후보 탐색이다. 실제 Hurt Collider까지의 선분 차폐를 확인하고 Trigger Body를 탐지 Sensor라는 이유로 일괄 제외하지 않는다. 후보 검증 뒤 dedupe하여 Sensor 후보가 먼저 나와 Body 후보를 소진하지 않게 한다. 기절은 일반 적만이고 공통 피해/무적을 적마다 다시 계산하지 않는다.

생성 발판의 최소 분리 거리와 수직 몸 높이 침투는 다른 값이다. 현재 몸 Collider의 실제 높이와 세로 침투로 분류한다. 정확히1/4는 사망, 얕은 경우 actual Body를 정상 지지 위치로 보정한다. 몸/Render만 움직이는 이중 writer·가짜 Grounded·별도45도 상수를 만들지 않는다. HMS MaximumGroundAngle getter로 실제 Inspector 접지 설정을 읽는다. 정상 상부 착지와 이동 중 materialization도 구분한다.

Shell 끼임은 플레이어 사망으로 변환하지 않는다. 현재 Form의 같은 OwnedDebris만 분리/반환하고 HP·Small·무게·Owner·IgnoreCollision을 보존한다. 데드존은 실제 Volume이며 임의 killHeight가 아니다. stale Owner/Actor/Stage의 접촉은 거절하고 반환은 새 생성·Q 회수가 아니다.

## CP와 최종 대화 맥락

Stage 초기 Marker는 첫 진입 위치이고 완료 Snapshot이 아니다. 실제 접촉 당시 현재 위치·조각·마스크를 전부 검증하고 교체한다. 같은 실제 Marker 재접촉 억제를 Actor 교체로 비우지 않는다. 추격 트리거 진입·퇴로 안전을 먼저 보고 저장 성공 뒤 시작한다. 전신 통과 및 진행 경로 투영은 2026-10-11 직접 지시로 제거했다. 저장 실패가 문 닫힘·추격 시작으로 이어지지 않아야 한다.

일반 대화 Completed/Skipped 장부와 능력 마스크 rollback은 수명이 다르다. 해금을 복원해도 안내 완료 기록은 지우지 않는다. 최종 포포만 사용자가 선택한 재실행 예외이며 Code 전체 장부를 Clear해서 해결하지 않는다. 한 회차의 Stage·Trigger·선택 객체·Table·Code·Actor세대·Round·예상 Session을 캡처하고 준비/시작/종료/예약 실행에서 재검증한다. 값이 같은 다른 선택 객체, 같은 위치의 다른 Marker, 같은 Code의 다른 판을 같은 요청으로 취급하지 않는다.

완료 observer가 재진입하여 다음 대화/판/Actor를 만든 뒤 옛 콜백이 돌아오는 경우를 처리한다. 낡은 맥락은 자기 booking/subscription만 해제하고 현재 Session을 임의 취소하지 않는다. 같은 프레임 사망·포획이 POPO Ending보다 우선하며 취소는 완료가 아니다. Round/Session 증가와 expected ID는 경계에서 검증하고 overflow를 숨기지 않는다.

## UI·작업·해제

HMS Reader/PlayerController가 이동·점프·스킬 입력을 한 번 처리한다. Host는 F만 처리하고 Bridge는 슬롯 사용 조건·모달 제한·피드백을 연결한다. View 버튼 OnClick에 이미 VM에 연결한 게임 명령을 다시 등록하지 않는다. Auto/Log/Options는 표시 시계와 예약 잔여시간을 정확히 소유하며 로그 닫힘 뒤 Auto를 자동 재개하지 않는다. 메모 Opening F닫기와 Closing 제한, 중첩 Options의 자기 토큰 반환을 보존한다.

타이틀 복귀·사망·Actor 파괴·Dispose는 생성한 Coroutine/Tween·예약·이벤트를 취소/해제한다. 부분 초기화 실패에도 이미 획득한 자원을 정리하고 실패를 성공처럼 반환하지 않는다. CoroutineOperationRunner의 nested iterator/finally, SessionShutdownOperation의 집계 해제는 실제 책임이 있어 유지한다. App scope disposing 중 이미 파괴된 Core와 정상 실행 중 Core 누락을 같은 성공으로 취급하지 않는다.

## 조립과 씬 로드·종료의 경계

같은 씬의 실물 참조·고정 지점 검사는 이미 주입된 객체만 사용하므로 Awake/OnEnable에서 scene.isLoaded를 요구하지 않는다. 반면 FindAll/RequireSingle처럼 씬 전체 Root를 조회하는 작업은 로드 완료가 필요하다. AppComposition은 Start에서 출력 단일성을 확인한다. Bootstrapper.Initialize는 Flow를 등록하고, App이 옵션·페이드·오디오 소비자까지 연결한 후 Boot를 실행한다. 서비스 준비와 전환 시작을 섞지 않는다.

CoreWorldBinding은 연결한 CameraZoneTrigger·PeriodicPlatform 참조를 보관하고 그 객체만 해제한다. 정상 retry/Title 복귀에는 살아 있는 객체를 정리하며 Unity 종료 중 이미 파괴된 객체는 건너뛴다. 종료 시 Stage를 다시 검색하지 않는다. 실제 정리 실패는 집계하며, 정상 실행 중 미로드 씬 검색이나 잘못된 참조를 임의로 성공 처리하지 않는다. Flow revision/state/busy의 Debug.Log는 상태 기록이고 참조 오류·전환 실패와 구별한다.

## 실행 증거의 범위

Source compile, 직접 실제 DLL 공개 API 검사, Native import/SerializedObject·격리 엔진, Windows BuildReport, 사람 Play·실제 clip·QA/FunQA는 각각 수행한 범위다. helper compile-only·포트·예약 수락·파일 존재·NOT_EVALUATED를 평가한 assertion 성공으로 간주하지 않는다. 제품 Source를 helper와 함께 중복 compile해서 CS0436으로 실행해도 실제 DLL 검증이 아니다. source SHA/DLL MVID·loaded path·명령/exit와 실패 원문을 보존한다.

에이전트는 Play 버튼·API/CLI/pipeline·batch PlayMode·직접 플레이·Player 실행을 하지 않는다. 미저장 Scene/prefab을 덮거나 강제 종료하지 않는다. 편집/빌드 자동 변화는 원래 기준과 실제 전후 diff·원인으로 분류한다. TMP clearDynamicDataOnBuild의 cache, URP RuntimeSettings 파생 cache, EOL/index stat 변화는 원본 기능·아트 변경과 구분한다. 실제 byte 변화 없는 stat/EOL을 커밋 내용 수정으로 보고하지 않는다.
