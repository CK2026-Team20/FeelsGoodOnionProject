# 세션과 플레이어 연결

최신 기준은 Assets/_Docs/planning-index.md와 ready-system-v11-refactor-2026-10-06의 확정안 적용 명세다. 구현/검증 상태는 별도 기록한다.

GameSessionService는 한 판의 능력 출처와 Position/Fragments 정확히 두 값 체크포인트를 소유한다. PlayerBridgeService는 HMS 공개 API의 명령/모델/표현 연결, SessionActorHost는 Actor 생성/제거를 소유한다. ID/HP/폼/능력/대화/적/장치/필드/시간을 저장payload나 별도snapshot에 추가하지 않는다. 유한XYZ와 조각0..5를 원자 검증하며 실패 시 이전 두값을 유지한다. Marker Pose는 초기 진입구성이고 실제 CP접촉 캡처는 플레이어루트 현재월드위치다.

Core/모든Stage 씬을 최초로드 후 유지한다. retry에는 같은Stage 초기재준비와 소비조각 객체복구 후 Position/Fragments만 새Normal Actor/HP최대/껍질0/속도·피격·무적 초기값에 복원한다. 현재Stage 획득능력/연결안내 완료만 다시 잠그고 트리거 재접촉으로 해금하며 이전Stage능력은 유지한다. 능력 출처는 런타임 소유상태다. CP는 시작/끝 두곳, 중간해금/추격 새캡처 없음이다.

새 플레이어는 비활성prefab으로 Awake전에 실제 의존을 갖춘다. 플레이어 종속 스크립트는 실제Player루트에 둔다. HMS Facade/Movement/Form/Rigidbody/Collider의 물리동거는 GetComponent/attachedRigidbody 계약 예외이며 Visual/Animator·CameraAnchor는 자식이다. 루트스케일은 유지한다. Reader/Router 각1개, E눈물/F상호작용/Q변신. PlayerController/CheckInteract/Temporary 중복입력은 제외한다. Q의 실제성공한 크기전환 사이1초이며 실패/표현완료에 종속하지 않는다.

App 출력1/Core Side·Quarter 리그는 Actor와 분리한다. ActorRemoving 전에 borrowed 목표/모드/검증함수와 자기구독을 해제한다. 한Knockback clip길이를 공통ms로 전달하고 기존 시간제한 기능/Dead HeavyState·외력을 유지한다. 일반/포획 사망은 현재Actor의 실제Dead 완료/누락 뒤Retry 한 번, 데드존만 HP/무적무시·표현생략이다. 낡은RequestId/Actor/중단 후콜백을 거절한다.

메모는 자기 월드정지·입력 제한, 대화 자체는 입력 제한, 옵션은 월드+표시 정지다. 자기토큰만 해제한다. 같은Core에서 정상세션 종료→다음판 재연결을 허용하며 세션해제를 앱영구shutdown과 혼동하지 않는다. DTO/실제위치·현Stage재잠금/이전Stage보존·사망예외·준비/종료실패·Dispose후호출0·단일구독을 기능으로 검증한다.
