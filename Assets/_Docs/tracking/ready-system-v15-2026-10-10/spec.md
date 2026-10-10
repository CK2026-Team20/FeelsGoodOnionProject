# V1.5 실행 명세 — Inspector·씬 배치·충돌·구조 정비

목표는 최신 V1.5 기능을 기준으로 현재 구현을 정합화하고, 사람이 코드 없이 씬·프리팹을 조립할 수 있게 하는 것이다. 위치·대상 키의 수기 입력, 관리자 부모에 종속된 씬 배치, 외형과 충돌체의 다른 이동, 동일 수명의 전달 객체를 정리한다. 현재 개발 목표는 **응집된 Prototype Stage 하나에서 게임 전체 흐름을 한 번 보여주는 것**이다.

이 문서는 사용자가 승인한 실행 명세다. 구현 결과는 status.md와 실제 검증 기록을 함께 확인한다. 이전 V1.1 READY를 대체하며 과거 결과/원본은 보존한다. 기획 목표와 실제 수행·미실행 범위를 구분한다.

## R01 — 권위·지침·쓰기 범위

최신 직접 지시 → 첨부 V1.5의 활성 기능 본문 → 현재 구현 순서로 판단한다. DOCX의 색·취소선·삭제문과 후반 버전 이력을 구분한다. 이전의 Position/Fragments 두 항목 저장, 현재Stage 능력 일괄 재잠금, 안내 대화 완료 기록 삭제, 주기 시간 고정은 V1.5와 맞지 않으므로 새 구현 기준으로 사용하지 않는다. 취소선의 적 처치 기름·수동 Retry/이어하기·붕괴 자동 복구는 복구하지 않는다.

첨부 원본 SHA256은 `d5698cbc3e50bcb1a4ea8c4f31eae325b43af7221bbcc97a150fdb8b28aa8ebe`다. 원본을 다시 편집하지 않고 같은 바이트의 V1.5를 프로젝트의 기획 원본으로 보관하고 planning-index의 지정 버전/적용 범위를 갱신한다. 현재 제품 기획 원본은 `Assets/_Docs/Cooked_시스템_기획서_V.1.5.docx`이며 같은 바이트의 준비 증거를 보존한다.

Tech_SYM 코드·소유 씬/설정의 최소 참조 이관, 프로젝트 Markdown·대응 메타데이터가 기본 수정 범위다. `_Art`, 타 Tech/art/QA·공용 원본, 보호 prefab/meta/import, Packages/ProjectSettings의 수동 수정·Apply·이동·삭제는 제외한다. 별도 예외는 R12의 HMS 세 파일에 명시된 최소 변경뿐이다. 앞선 HMS 9파일 허가를 재사용하지 않는다.

루트 AGENTS/CLAUDE 및 관련 지역 지침에 다음 규칙을 일치시킨다. 위치/대상은 실물 참조, 수치 조절은 목적·단위가 분명한 값, 간단한 한국어 표시 이름/Tooltip, 씬 관리자 부모 배치 금지, 실제 물리 몸체와 외형 이동 일치, Play 검증은 사람, 사람의 prefab 제거/제작 소유, 삭제 전 소비 이관이다. 과거 dated 기록을 현재 목표로 승계하지 않도록 표시한다.

## R02 — 사람이 사용할 Inspector의 계약

작성 위치/끝점/경로/CP/조작부/카메라 정렬점은 Transform·CheckpointMarker·실제 컴포넌트로 연결한다. Scene 파일은 SceneAsset, 리소스는 prefab/Sprite/Texture/AudioClip/AnimationClip/TextAsset로 연결한다. 런타임 생성 Actor는 Host가 현재 인스턴스를 주입한다. 생성되지 않은 Actor를 사람이 Scene asset에 저장하지 않는다.

직접 작성할 키는 없앤다. StageTrigger의 공용 string `value`를 종류별 CP 참조/능력 선택/대화 선택으로 나눈다. 대화 Code는 이미 연결된 JSON TextAsset에서 검증한 Code 선택 목록이며 테이블 데이터를 중복 새 자산에 복제하지 않는다. Scene 경로/Stage 식별과 Audio 용도 키는 편집/빌드 경계가 생성·검증하는 내부 값이다. MemoModel의 수동 memoID도 실제 MemoModel 참조/자동 식별로 바꾼다. 숨은 생성 값은 런타임에서 허용하되 사람이 다시 입력하게 만들지 않는다.

Inspector 이름은 간단한 한국어다. Tooltip 첫 문장은 연결 대상 또는 조절 효과를 말한다. 단위·0 의미·좌표계·Gizmo 색은 그 설정에 필요할 때 짧게 덧붙인다. “AI/개발자만 알고 있는 계산값”을 일반 작성 항목으로 노출하지 않는다. enum 번호는 기존 직렬화 의미를 보존한다. 한국어 필드 라벨을 위한 SYM 한정 PropertyDrawer는 허용하며 보호 타입의 전역 Inspector를 덮어쓰거나 새 패키지를 도입하지 않는다.

속도/시간/높이/각도·이동 방향/음량/회수는 조절 값으로 남긴다. Vector3 저장 위치·Physics 인자·실행 중 계산한 변위 자체를 금지하지 않는다. 실제 필드별 이관 기준은 [Inspector 연결 전환 명세](Inspector%20연결%20전환%20명세.md)의 실제 최종199개 표다. 정규식 밖 직렬화도 실제 SerializedObject와 대조한다.

필수 authoring 변환은 Stage의 Transform 경로 배열/추격 시작점, CP의 respawnPoint, Zone의 alignmentPoint, 플랫폼의 start/end 또는 start/target, 장치의 closed/openPose, 적의 patrolStart/end, F prompt/interactionPoint다. CameraZoneTrigger가 모드/정렬점을 작성하고 CheckpointMarker의 initialCameraZone에는 실제 Trigger를 드래그한다. 기존 CameraZoneData는 실행 값 struct이며 컴포넌트/자산으로 붙이지 않는다. Player의 cameraAnchor/visualRoot는 기존 PlayerDamagePresentation의 명시 참조로 연결한다. 임의 이름 검색 fallback을 새 API에 남기지 않는다.

## R03 — 프로토타입 흐름·씬 독립 배치·수명

현재 새 게임은 Bootstrap→Title→Opening→응집형 Prototype Stage→포포 대화 완료/Skip→Ending→Title의 한 번 흐름이다. Opening/Ending은 Core 화면이다. Stage 1→2→3 자동 순차 연결이나 본편 레벨 배치는 이번에 추가하지 않는다. 현재 등록된 Stage2/3과 향후 연결한 Stage는 각 씬을 선택하여 독립 진입할 수 있어야 한다. 통합 직접 진입은 기존 Bootstrap 훅/EditorStart로 선택 Stage를 활성화하며 새 게임용 Opening을 생략한다. 기존 단독 기능 씬의 SessionActorHost 독립 모드도 보존한다.

Core/등록 Stage는 최초 additive 로드 뒤 유지한다. 활성 Stage 하나만 현재 Actor/트리거/기믹/외부 작업과 연결한다. 사망은 같은 Stage 초기 재준비+Actor 교체, Title/다음 새 게임은 같은 Core의 명시 해제/재연결이다. 이후 본편 연결에서도 등록 씬/현재 Stage 선택/연결 해제 경계를 사용하여 독립 진입을 유지한다. 미래 순차 진행을 위한 무소비 Service/추상화는 지금 만들지 않는다.

관리자·출력 Camera·Canvas·리그·Stage 월드 오브젝트는 독립 씬 루트다. 관리자를 전체 Level 부모로 만들지 않는다. 개별 완성 prefab의 기능상 부모·자식은 유지한다. App 출력 Camera의 `IsChildOf(manager)` 조건과 Stage/Core의 관리자 자식 탐색을 제거한다. SceneComponentLookup의 씬 소속 조회/단일성 검증 및 명시 참조를 사용한다. 비활성 루트도 초기 재준비 대상으로 찾고, 씬 밖 객체는 연결하지 않는다.

부모를 해제할 때 월드 pose/scale, prefab override, Collider 범위와 참조를 보존한다. Bootstrap은 이미 유지되는 씬이며 새 전체 월드 DontDestroyOnLoad 부모를 만들지 않는다. 출력 Camera/Brain/AudioListener는1개, Core Side/Quarter 리그와 Actor는 별도 수명이다.

## R04 — 체크포인트·능력·추격 직전 저장

CheckpointSnapshot은 불변 값이며 정확히 `Position: 유한한 월드 Vector3`, `Fragments: 정수0..5`, `UnlockedAbilities: 해금 마스크` 세 항목만 가진다. 마스크는 None=0, FormChange=1, Tear=2, Both=3만 유효하다. 기존 AbilityId(FormChange=0/Tear=1)의 번호는 변경하지 않는다. StageId/CheckpointId/대화 완료/월드 객체/HP/폼/껍질/속도/카메라/시간/피격·무적을 저장 payload에 추가하지 않는다. 디스크 진행 저장을 만들지 않는다.

GameSessionService가 현재 해금과 마지막 완료 snapshot을 소유한다. 플레이 중 새 해금은 현재 값에 반영하지만 사망 복귀의 권위는 마지막 snapshot 마스크다. 출처 Stage dictionary/독립 해금 유지 경로는 제거한다. UI는 이 현재 값을 읽으며 독립 해금 bool을 만들지 않는다.

첫 유효 접촉 전에는 저장 snapshot이 없음을 표현한다. 초기 Marker/진입 설정은 스테이지 시작 구성이지 완료된 CP 저장으로 표시하지 않는다. 첫 저장 전 사망은 해당 Stage 초기 구성으로 다시 시작한다. 새 유효 CP 접촉은 현재 ActorBody 위치/보유 조각/현재 마스크를 먼저 전부 검증하여 한 번에 교체한다. 실패·중단은 이전 정상 snapshot을 유지하며 저장 성공 사건을 발행하지 않는다.

마지막으로 저장한 물리 Marker의 재접촉은 저장을 갱신하지 않는다. 중복 억제는 실제 Marker/캡처 source의 런타임 참조로 처리하며 저장 ID를 만들지 않는다. 동일 CP 억제 상태를 단지 Actor 교체 때문에 비우지 않는다. 현재 시작/끝 Marker 배치는 이관하되 V1.5에 없는 정확히2개 강제/pair 밖 거절은 제거한다.

추격은 실제 몸 전체 시작선 통과·문 닫힘 안전·시작 가능 조건 확인→현재 위치/조각/해금 원자 저장 성공→추격 시작과 퇴로 차단 순이다. 일반 CP 중복 억제가 추격 직전 저장을 누락시키지 않는다. 한 accepted 추격 시도에서 한 번만 저장하며 아직 진행 중인 추격의 Stay 접촉은 반복 저장하지 않는다. 저장 실패/죽음/전환 중이면 추격·문 닫힘을 시작하지 않는다. 같은 프레임 포포와 포획 조건은 포획이 우선한다.

사망 복귀는 같은 Stage 초기 상태(적/장치/타이머/소비 조각 Root·Collider/Trigger)를 재준비한 뒤 저장 위치·조각·마스크를 복원한다. HP3, Normal, 껍질0, 임시 스킬/운동/피격/무적 초기화이며 추가 재등장 무적은 없다. 일반 사망은 실제 Dead 완료/누락 뒤 한 번, 데드존은 HP/무적 무시·표현 생략 복귀다. 해금/HUD는 snapshot을 따르고 재접촉으로 다시 해금한다. **완료/Skip한 안내 대화 기록은 유지한다.** 취소된 안내만 다음 접촉에서 다시 시작한다.

## R05 — 실제 물리 이동과 표시 이동

TearFragment의 회전/상하 이동은 pickupRoot 전체에 적용한다. Sensor와 Render가 같은 물리 위치를 따라가고 초기 pose/active/Collider/수집 상태를 동일 객체로 복구한다. 표시 외형 Transform만 움직여 보이지 않는 다른 위치에서 수집되는 구조는 제거한다. 수집1, 최대5일 때 객체 유지다.

플랫폼 Rigidbody의 유일한 지속 위치/회전 작성자는 KinematicPlatformMotion이다. 다른 기능은 목표/효과를 요청한다. 실제 움직이는 PhysicsBody 밑에 Collider·Render가 함께 이동하도록 조립한다. 고정 목표/웨이포인트는 움직이는 Body/적의 자식으로 두지 않는다. 물리 바닥과 Actor 탑승은 동일 Body 변위/속도를 사용한다.

피격 색·방향 시각 회전·붕괴 경고 흔들림·추격 군집 bob처럼 물리 이동을 뜻하지 않는 순수 표현은 허용한다. 모든 Renderer 애니메이션을 무조건 Collider 이동으로 바꾸지 않는다. 옵션 정지/Disable/retry/Actor 제거에서 자기 Tween·작업·빌린 참조를 해제한다.

## R06 — 주기 발판 끼임·껍질 복귀

주기 상태는 표시/소멸/형성이다. 초기 시간5/2/3초와 시작 위상은 조절 가능하다. 각 시간은 유한한0이상, 총 주기는0보다 커야 한다. 0 길이 phase는 건너뛴다. 형성 중 Collider는 비활성이다. phase 경계 처리에 무한 순환·한 틱 반복 부작용이 없어야 한다. 사람의 수치를5/2/3으로 다시 쓰는 OnValidate와 실행 `%10` 상수는 제거한다.

Collider 등장 순간과 실제 이동 중 겹침을 검사한다. 후보의 bounds 겹침만으로 판정하지 않고 실제 Collider 교차를 확인한다. 정상 상부 착지는 끼임에서 제외한다. 침투 비율은 **실제 Player 몸체의 수직 높이 중 발판 내부와 겹친 수직 구간/현재 몸 높이**다. 현재 Capsule의 scale·Normal/Small을 반영한다. ComputePenetration의 최소 분리 거리와 이 비율을 혼동하지 않는다.

비율<0.25이면 실제 Rigidbody 몸체를 발판 상부로 보정하여 다음 물리 접지에서 정상 지지/착지가 성립하고 피해·사망은 없다. 정확히0.25 이상이면 HP0·행동 취소와 기존 Dead 표현을 요청하며 **실제 몸체와 Collider를 위로 밀어내는 이동**을 수행한다. Collider 안에서 Render만 Tween하지 않는다. HMS CharacterMovement가 물리 작성 책임을 유지하도록 공개 외력/물리 명령과 조정된 보정을 사용한다. 또 하나의 무조정 지속 Transform writer를 만들지 않는다.

끼임 이동의 시계는 피격 clip 길이와 독립이다. 분리 시간은 조절 값(초기0.35초)이며 누락/Legacy clip의0ms로 이동이 사라지지 않는다. 일반 사망 복귀는 분리 완료와 Dead Completed/Missing의 단일 종결을 기다린다. 옵션은 이동·표현 시계를 정지하고 retry/제거는 낡은 Actor의 진행을 취소한다. 한 접촉의 ForceDeath/Retry는 한 번이다.

PlayerDebris를 PlayerFacade로 승격해 소유 플레이어를 죽이지 않는다. 껍질은 실제 Rigidbody/BoxCollider만 발판 밖으로 최소 분리한다. 살아 있는 현재 Actor가 소유한 같은 껍질이 데드존에 닿으면 현재 ActorBody 위치로 반환하고 이전 linear/angular velocity를 정리한다. IgnoreCollision/무게/Owner/Small 상태·객체 수는 유지한다. RecoverDebris, Q 성장, 파괴 후 재생성으로 처리하지 않는다. 오래된/다른 Owner·종료 중 Actor는 반환 대상이 아니다.

데드존은 배치한 실제 Volume/Trigger다. 리터럴 worldY plane은 제거한다. 통합 StageTrigger(Fall)는 Stage의 currentActor/Bridge, 독립 씬 Fall은 명시된 SessionActorHost의 currentActor/Bridge로 요청한다. Player와 OwnedDebris 분기는 하나의 데드존 접촉 경계에서 구별한다. 독립 모드에 이전에 없던 통합 Flow Retry를 추가하지 않는다.

## R07 — 플레이어·일반 적·장치·공통 피격

기획 본문의 방향키/Space/Q/E/F, Side·Quarter5m/s, 전환0.6초, Normal 점프3/Small2, 공중 추가 점프/버퍼 없음, 성공 Q 즉시1초·복귀 공간 실패 보존, E5조각/3m 직접 몸체 차폐/2초 누적, F2.5m/±60도/가까운 비차폐 대상 규칙을 적용한다. 실제 Root 배율은 유지하고 HMS Form의 Collider 크기 전환에 Root scale 축소를 중복 적용하지 않는다.

HP3/피해1/일반 무적2초는 HMS 소유 API로 처리한다. 모든 일반 피해원의 수평·상향 넉백 세기는 Bridge의 한 설정으로 통일한다. 방향은 피해원에서 현재 몸체로 향하는 작성 평면의 수평 방향, 수직 성분은 항상 상향이다. 위치가 같아 수평 방향을 정할 수 없으면 현재 논리 정면 반대 방향을 사용한다. 크기는 조절 값이며 초기 수평3m/s·상향3m/s다. source별 정규화 결과나 EnemyDefinition/DamageFloor 개별 knockbackSpeed를 더하지 않는다. 수락된 피해에만 같은 실제 Knockback clip ms를 적용한다. 무적 끝 지속접촉은 다시 피해, 기절 적은 접촉 공격 없음이다.

일반 적은 Idle/Patrol/Stunned만 있고 X/Y 실제 지점 순찰·즉시 끝점 반전·기절시간 누적을 유지한다. HP/죽음/드롭 기름/Chase/Return을 추가하지 않는다. 상태·기절은 EnemyActor, 실제 hurt Trigger 접촉 집합/반복 피해는 EnemyContactSensor, 지형·이동은 EnemyMotor다. 렌더러에는 게임 규칙을 붙이지 않는다.

고정 발판/찬장에는 기능 script를 만들지 않는다. 부유는 같은 착지 반응을 중복 실행하지 않는다. 붕괴는 Normal 상부 접촉→초기1초 경고→실제 지지 제거/붕괴이며 시도 중 되돌리지 않는다. 압력은 Normal/껍질만, Small 제외다. 연결의 수동 원본은 PressureLinkedPlatform.pressurePlate 한 곳이며 Plate는 눌림 상태·시간을 제공한다. Linked는 실제 목표로 이동·빈 판에서 복귀·복귀 중 재누름 반전을 수행한다.

OvenTrayObject의 설정 조합으로 오븐 문/트레이/각 독립 서랍을 만든다. 명령·표현은 동시에 시작하고 장치 이동 중 추가 토글을 거절하되 Player 이동/점프를 막지 않는다. 목표 위치·열림 회전은 실제 Pose 지점이며 Motion 한 작성자를 사용한다. 배치 기름은 기존 HMS OilSurface 조합, 상시 열원 on/off는 배치로 설정한다. 시간/속도/경고를 조절 항목으로 노출했다면 실제 실행도 해당 값을 사용한다.

## R08 — 대화·연출·UI 동작 보존과 Skip

DialogueOutcome/CinematicOutcome는 각각 None/Completed/Skipped/Cancelled를 구별하고 기존 번호를 유지한다. Code 전체 Skip은 Completed와 같이 완료 장부에 기록하되 결과는 Skipped로 남긴다. 사망/Title/Disable Cancelled는 완료 기록에 넣지 않는다. 완료한 Code를 능력 재잠금 때문에 다시 열지 않는다.

DialogueView.skipButton→VM.SkipAll→Service 한 명령 경로를 연결한다. CanSkip은 활성 대화이고 Options 정지/LogOpen/Dispose가 아닐 때 true다. Enter/패널 클릭은 reveal완성/다음행을 구별하고0.3초 간격·입력1회 명령1회를 유지한다. Auto는 완전 표시 뒤3초, Log는 완전 표시 행만·Auto 해제·표시 정지·닫아도 Auto 재개 없음이다. Options는 typing/예약 잔여시간 정지, 대화/로그 자체는 월드 정지 없음이다.

포포 종료 대화는 currentActor/currentSession의 Completed/Skipped에서만 Ending1회, 포획·취소·낡은 callback은 Ending을 시작하지 않는다. Opening/Ending은 임의 개수의 순서 컷, 초기4초씩, 클릭 다음 컷 없음, 전체 Skip/완료로 다음 Flow다. Options 중 Skip은 막고 아래 입력으로 전파하지 않는다.

HUD3하트/0..5조각/Q·E 아이콘 음영/해금 표시, 메모 F Opening 중 닫기·Closing 입력 제한·자기 월드정지, Options 자기 토큰·환경음량1/.7/1과 기존 PlayerPrefs 실패 처리, Title 복귀 수명은 유지한다. UI는 ViewModel 상태·명령 바인딩, ViewModel은 View 직접 참조 없음, Dispose 이후 호출0·재Bind 중복0·자기 listener만 제거다.

## R09 — 실제 책임 이관과 폐기 소스 제거

삭제 대상23개와 정확한 path/GUID/소비는 [스크립트 필요성 및 삭제 계획](스크립트%20필요성%20및%20삭제%20계획.md)이 정본이다. Editor 자동 제작/Repair12개는 제거한다. 런타임11개는 GameEventBus/EventContracts/CinematicPresentationState/UIScaleTween/GameUiRoot/GlobalUiRoot/HudIconGraphic/EnemyStateMachine/EnemyCombat/ActorFallGuard/ApplicationService다.

같은 수명 참조·VM 생성은 AppComposition/CoreUiBinding, 메모 Tween은 MemoView, 연출 표시 원자 발행은 CinematicService, 적 상태는 Actor, 적 접촉은 Sensor, 종료 한 동작은 주입 Action으로 이관한다. 범용 버스의 실제 사망→Flow 명령과 Stage 알림은 소유자별 좁은 명령/이벤트로 바꾼다. 독립 모드에 Retry Flow가 없는 계약은 명시하여 매프레임 거절 반복을 만들지 않는다.

선행 조건은 C#·Editor/build 소비/자산 GUID·nested 원본 참조 이관이다. 참조0 확인 전 `.cs`/자기 `.meta`를 삭제하지 않는다. 보호 원본은 고치지 않는다. 미해소 사람 자산은 경로/담당/이유와 삭제 대기로 남긴다. 새 forwarding wrapper/partial/생성기로 삭제 수량을 숨기지 않는다. 단일성·유한 값·Actor세대·재진입·finally 같은 실제 방어를 “코드 단순화”라는 이유로 지우지 않는다.

HudIconGraphic의 Image/Sprite 이관은 **사람의 실제 하트 리소스 연결 후** 수행한다. 리소스 없을 때 이 source를 삭제하거나 placeholder로 대체하지 않는다. 그동안 기존 prototype 하트 표시 소비를 유지하며 임시 구형 분기는 실제 Image 연결·GUID 해소 시 함께 제거하는 종료 조건을 적는다. 새 제작 guide에서는 Image 경로만 사용한다. 이 조건은 삭제 대기이며 완료로 집계하지 않는다. SceneComponentLookup/CoroutineOperationRunner/SessionShutdownOperation/HeavyState/KinematicPlatformMotion은 실제 공유 규칙/수명/공개 물리 계약이 있어서 유지한다.

각 유지 source의 builder 전용 Configure/무소비 overload/폐기 Feature 상수·이벤트/캐시/using은 정확한 소비 확인 후 제거한다. GameRuntimeService의 무소비 GameplayOutputChanged 전달, Stage 출처 reset·ForgetCompletedCodes, pair 전용 캡처, 순수 미래용 SelectActiveStage 미소비 경로를 검토·제거한다. 유지한109개 SYM source의 필요성/생명주기/대체 근거와 삭제한22개 및 조건부1개의 책임 이관을 최종 diff에 맞춰 갱신한다.

## R10 — 사람이 직접 만들 prefab과 연결 안내

원래 AI prefab 제거·아트 연결·새 prefab 생성/Apply는 사람이 한다. 에이전트가 삭제/재생성/placeholder/Repair 실행으로 대체하지 않는다. 실제 source 수정에 필요한 자기 소유 소비 참조의 최소 migration은 가능하며 이관 전에 위 guide와 자산별 삭제표를 먼저 제공한다.

제작 guide는 씬 흐름→작업 순서→붙일 컴포넌트/자산/일반 C# 구분→위치/필수 물리 설정→drag 대상→현재/수정 후 차이→삭제 선행 조건을 포함한다. PlayerRoot/ActorBody, Shell의 물리 Root 예외, Platform의 actual Rigidbody 아래 Render, 움직이지 않는 Point, Tear 전체Root+Sensor, Enemy hurt Trigger, Memo/UI/CP/Chase/Zone/DeadZone를 빠짐없이 설명한다. 실제 final Inspector와 일치할 때만 구현 후 매뉴얼로 전달한다.

PlayerFacade/Movement/Form/Rigidbody/CapsuleCollider 동거와 PlayerDebris/Rigidbody/BoxCollider/HeavyState 동거는 보존한다. Shell 위 별도 wrapper를 만들면 Remove가 Body만 파괴하는 계약을 명시한다. Root/Render/Collider 예시를 기계적으로 강제하여 보호 API를 깨지 않는다.

## R11 — Locator와 조립의 책임

Locator는 타입 등록·조회·scope 소유·Dispose, Composition은 구체 서비스 생성/Unity 실물 연결/부분 실패 해제다. 실제로 서로 다른 수명이므로 유지한다. 일반 소비자는 Locator를 매프레임 검색하지 않고 명시 주입된 작은 계약을 사용한다. Locator에 씬 배치/물리/UI/세계 저장을 몰아넣거나 새 네트워크·추측 factory 계층을 만들지 않는다.

## R12 — HMS 변경 명세와 제한된 우선순위 수정

이전 관련 commit `6f2a386`(2files,+12/-30), `5d1770d`(9files,+362/-20), 고유10개 소스의 추가/삭제/변경 이유·API·단위·기본값·독립 소비·해제 계약을 상세 기록한다. 과거 테스트 수/Unity 실행을 이번 결과로 승계하지 않는다. PR은 이번에 만들지 않으며 이후 최종 diff 기준으로 이 기록을 PR에 포함한다.

이번 허용 범위는 `Assets/_Code/Core/Tech_HMS/Features/PlayerAnimationPlayback.cs`의 Interact priority2→4 및 관련 설명, `.../PlayerAnimationController.cs`의 우선순위 설명, `.../CharacterMovement.cs`의 `MaximumGroundAngle => maximumGroundAngle` 읽기 전용 속성 하나와 설명이다. 마지막 항목은 실제 Inspector 접지각을 외부 접촉 판정에서 읽도록 별도로 승인받았다. 원래 필드·기본값·Range·접지 동작은 변경하지 않는다. 기존 enum/RequestId/결과·피해·입력·독립 소비는 변경하지 않는다. 실제 우선순위는 Dead>Knockback>Interact>Tear>Form>Land>기본 표현, 중단 one-shot 재개 없음, 이동/점프 실행 제한과 분리다. 다른 HMS 기본 jump/속도는 소유 prefab의 기존 fields로 맞춘다.

## R13 — 검증·보고의 경계

에이전트는 Editor Play 버튼, Play Mode API/CLI/자동 pipeline, batch PlayMode tests, 직접 플레이를 실행하지 않는다. 사람이 실제 플레이를 한다. 비 Play source/serialized reference/Editor import/compile/EditMode 검사·명시 build는 실제 프로젝트 상태와 허용 범위에서 실행할 수 있다. 열려 있는 Editor/미저장 씬을 강제로 닫거나 검증을 위해 source scene을 덮어쓰지 않는다.

필수 관찰 결과는 snapshot 원자성/마스크 복원·동일CP·추격 전 저장,1/4 아래/동일/위의 실제 분류·정상접지·껍질 owner HP불변/데드존 같은객체 반환,root+sensor pose,공통 피해/기절 반복,Completed/Skipped/Cancelled/낡은Actor,Pause/Dispose/재Bind1입력1명령,외부 참조0/보호diff0/독립진입이다. 내부 코드를 그대로 복제한 테스트를 만들지 않는다. 순수 규칙과 공개 결과·실제 엔진 경계는 각 적합한 방법으로 구분한다.

실제 Rigidbody 접지/애니메이션/전체 수동 동선·리소스 확인은 사람 체크 항목으로 남긴다. 정적 검사 성공만으로 통과시키지 않는다. QA/FunQA 문서를 임의 생성/평가하지 않는다. 검증 기록은 실제 명령·결과·source 식별·미실행 범위·남은 자산 대기를 포함한다.

## R14 — 완료 산출물·조건

제품 동작/권한/데이터 모델의 미결 선택은 없다. 승인 후 작업은 plan의 각 gate로 확인한다. 실제 리소스가 아직 없는 HudIconGraphic은 조건 충족 전 삭제 대기, 사람의 새 prefab/플레이는 사람의 다음 작업으로 명시한다. engineering 구현·비 Play 검사·수동 리소스/플레이 완료를 서로 다른 상태로 보고한다.

planning-index/지역 지침, 변경 소스와 최소 소유 자산 이관, script별 최종 필요성/삭제 근거, 사람이 보는 최종 제작 guide/Inspector 대응표, 이전·신규 HMS 변경 설명, 코드 리뷰·검증 결과가 있어야 한다. 실제 무단 보호 diff/Missing Script/해제 실패를 남겨 놓고 완료라고 하지 않는다. 준비 문서 검수, 실제 구현·삭제, 비 Play 검증, 사람의 리소스·플레이 확인은 각각 실제 결과로 판단한다.

## 2026-10-10 포포 최종 대화 재생 예외

2026-10-10 사용자 직접 선택: “2번 예외를 허용하는 방안으로 지속합니다”. 포포 최종 대화만 Completed/Skipped 기록이 있어도 재도착 시 첫 행부터 새 대화 회차로 다시 재생할 수 있다. 일반 안내 대화의 완료 장부는 유지하며 반복을 허용하지 않는다. 예외는 실제 포포 종료 트리거(StageTriggerKind.Escape) 접촉과 현재 활성 Stage·살아 있는 Actor·세대·회차 검증을 통과한 경로에만 사용한다. 다시 재생한 최종 대화의 Completed/Skipped만 엔딩을 한 번 예약하며, 같은 프레임 사망·포획, Cancelled, 낡은 callback은 엔딩으로 연결하지 않는다. Options/로그/입력·해제 계약은 동일하다.
