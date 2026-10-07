# 구현 사실과 새 명세 적용 시 주의점

2026-10-07 최종 선택 반영. 변경 전 구현 설명과 현재 목표를 구분한다. 목표는 business-rules.md와 확정안 적용 명세, 실제 실행 상태는 tracking/status.md를 따른다.

## 입력과 상태

변경 전 HMS Reader는 F 눈물, SYM Router/상호작용은 E였다. T1/T2 통합 경로는 E 눈물/F 상호작용이며 HMS의 독립 기본 F는 선택적 공개 설정으로 보존한다. 안내·메모 열기/닫기·독립 씬 소비자를 함께 바꿔야 한다. 이미 열린 메모의 F닫기는 gameplay CanAct 검사보다 먼저 처리하되 위에 Options가 있으면 아래 메모로 입력을 흘리지 않는다. 하나의 Reader/Router만 허용한다.

메모는 자기 WorldPause를 유지하고, 대화/로그 자체의 WorldPause만 제거한다. 플레이어 입력/표시 제한은 남으며 옵션 중첩 해제는 자기 토큰만 처리한다. 메모 Opening에서도 F닫기를 수락하고 Closing까지 입력을 막는다. 대화 중 월드가 진행하므로 피해/사망 취소가 실제로 작동해야 한다.

EnemyCombat의 consumedDamage와 피해 발판 자체1초 타이머는 공통2초 무적과 다르다. ActorFallGuard의 Damage(MaxHP)는 무적이면 실패할 수 있다. 데드존/포획은 강제 사망 계약을 사용하고 일반 피해와 합치지 않는다. 클립 완료 RequestId와 실제 일회성 우선순위를 확인하며 고정 초를 실제 재생 완료로 기록하지 않는다.

## 카메라/Play 진입

현재 Actor prefab의 CameraRig는 ActorBody와 형제다. Root가 아니라 실제 CameraAnchor를 추적한다. HMS ModeController는 Start 이후 준비되므로 Awake 직후 TrySetMode 실패를 정상 준비 완료로 처리하지 않는다. 새 App 출력/분리 리그에는 원본에 없는 Bind API가 필요하다. 권한/구현 전에 Inspector 연결을 완료했다고 쓰지 않는다.

새 Side/Quarter 전환은 실제 Brain 완료와 Side Z정렬 후 물리 축을 확정한다. 요청 중 다른 구간은 이전 작업을 취소하고 최신 요청을 처리하며 옵션은 블렌드/정렬/확정을 정지한다. 반복 요청이 새 토큰/명령을 만들지 않게 한다. DOTween을 강제 종료한 검사는 실제 Brain 완료 증거가 아니다.

ArchitectureInstallation의 정확한 여섯 씬만 부트 보정하고 비대상은 직접 Play한다. 통합 중 활성 씬은 Core이며 Core 1회 로드 뒤 모든 등록 Stage를 최초 additive 로드한다. 이후 Core/Stage를 재로드하지 않고 활성 Stage 루트와 Actor 연결만 선택한다. Build Settings 일반 목록과 설치의 여섯 빌드 경로가 다르므로 기존 IntegrationBuildPipeline의 명시적 목록/전후 설정 검사를 사용한다. 씬 경로/StageId를 새 원본의 본편 장 구분만으로 재작성하지 않는다.

## GUID·월드 복원·물리

TearFragment와 LevelTearFragment의 현재 상한 소비 차이를 확인했다. 최신은 최대5에서 보존이므로 성공 후 소비를 단일 TearFragment로 옮기고 GUID를 이관한다. 현재 상태를 보존할 이유로 잘못된 소비 규칙을 계속 남기지 않는다.

새 checkpoint는 **현재 ActorBody 월드 Position(Vector3)과 Fragments(int0..5) 두 값만** 저장한다. 다른 저장 항목을 추가하지 않는다. 시작/끝 두 CP만 같은 Actor 출처와 활성 Stage를 확인해 캡처한다. 같은 CP 재접촉은 덮어쓰지 않고 중간 해금/추격 시작은 캡처하지 않는다. 비유한 위치/잘못된 조각 수는 거절하고 이전 두 값을 지킨다. 배치 ID/씬 설정은 입력의 출처/중복 판별용 실행 문맥으로만 사용하고 payload에 저장하지 않는다. retry는 Stage를 초기 재준비해 조각의 실제 객체/Collider/소비 플래그도 복구하고 occupancy/새 Actor 참조를 다시 수집한다. 현재 Stage에서 얻은 능력과 해당 안내 완료만 재잠그며 이전 Stage 해금은 유지한다. 기믹 상태 등록자/DTO/범용 저장 계층은 만들지 않는다.

물리 접촉은 attachedRigidbody에서 Facade를 읽는 현재 계약을 확인한다. HMS ActorBody 동거 예외를 유지한다. 소유 SYM 발판은 Root 로직/Body 참조 주입으로 바꿀 수 있으나 현재 RequireComponent/GetComponent와 실제 콜백이 옮겨지는지 함께 확인한다. 동일 Rigidbody에 HMS/SYM 이동자를 둘 다 붙이지 않는다.

로컬 XYZ 종점/정렬 오프셋을 필드 이름만 바꾸면 기존 override를 잃는다. 기존 보존 자산은 변경 전 월드 기준점/GUID/enum 저장값을 캡처하고 native API로 이관한다. 사용자가 삭제한 prefab과 재작성할 새 prefab을 혼동하지 않는다. 자동 전체 빌더로 사용자 씬을 덮어쓰지 않는다.

## 표시와 종료 검증

DialogueDriver는 Tick하지 않고 Cancel만 한다. Core 종료가 이를 흡수해야 같은 사망/Title 취소가 유지된다. DialogueViewModel은 관찰자 예외에도 finally에서 자기 구독을 해제한다. 로그에는 완전 표시 행만 추가하고 Options/로그 정지 후 예약 콜백이 다른 Session/Row를 수정하지 않도록 한다.

Timeline의 Artwork Track/Clip/FrameBridge와4패널/scale 호환은 단일 Cut catalog/시계로 이관 후 제거한다. 실제 원화/하단 문구·4초/Skip/정지를 확인하고 패키지나 타인 연출 자산을 삭제하지 않는다. 빈 Animator Controller/요청 이벤트가 실제 clip 재생 증거가 되지 않는다. 누락 clip 대체의 입력 복구와 실제 아트/블렌딩 검증은 별도 상태다.

Destroy 요청 직후 같은 프레임에는 Actor가 남을 수 있다. 종료 완료와 저장된 isDirty=false를 확인하고 이전 세션 참조로 다음 판을 관찰하지 않는다. 검증 결과의 중첩 케이스/관찰 목록을 실제 JSON에서 확인한다. 마지막 scalar 완료값만으로 성공을 주장하지 않는다.

새 통합 기능 Play/빌드는 아직 하지 않았다. T1/T2 실제 Editor 컴파일·순수/저장 계약 검사는 status와 새 증거에 기록했다. 이전 플랫폼/진입 검사·Windows 빌드·FunQA 인계는 해당 날짜 문서의 과거 증거다. 기존 변경/자동 저장은 원인과 diff를 남기고 보존한다. 보호 HMS Missing Script와 엔진 Hierarchy 문제는 findings의 기존 항목을 무단 수정으로 처리하지 않는다.
