# 기능 테스트 케이스 정본

2026-10-06 갱신. [인계 명세](2026-10-05-functional-qa-spec.md)와 최신 직접 지시 기준. 7개 대분류·26개 결합 기능·39개 핵심 케이스. 이 파일은 output의 엑셀 생성 내용이 Git ignore 때문에 유일한 공유 근거가 되지 않도록 보존한 요구 명세다. 실행 결과·빌드별 이력은 해당 엑셀에서 기록하며 여기에는 통과 판정을 복제하지 않는다. 변경 시 이 정본과 엑셀을 함께 갱신한다.

## FT-001 · 게임 흐름 / 새 게임 / 정상 진입

- 중요도: 핵심
- 시작 조건: 빌드의 제목 화면, 새 실행 상태
- 실행: 새 게임 선택 후 오프닝 자연 종료 또는 스킵
- 기대 결과: 첫 프로토타입 스테이지와 HUD 진입; 플레이어 하나, 이동 가능
- 진입 위치: 제목 → 새 게임
- 근거: `Assets/_Docs/planning-index.md`; `Assets/_Docs/Cooked_시스템_기획서확정_V.1.1.docx §5.1/4.2`; `Assets/_Code/Core/Tech_SYM/Architecture/Foundation/GameFlowService.cs`

## FT-002 · 게임 흐름 / 새 게임 / 중복 전환 방지

- 중요도: 핵심
- 시작 조건: 제목에서 새 게임 실행 직전
- 실행: 새 게임을 빠르게 두 번 선택하고 전환 중 다시 입력
- 기대 결과: 세션·플레이어·오프닝이 중복 시작되지 않음
- 진입 위치: 제목 → 새 게임
- 근거: `Assets/_Docs/business-rules.md`; `Assets/_Code/Core/Tech_SYM/Architecture/Foundation/GameFlowService.cs`

## FT-003 · 플레이어 / 이동·점프 / 기본 조작 결합

- 중요도: 핵심
- 시작 조건: 첫 스테이지, 바닥에 선 상태
- 실행: 방향키 이동·정지 → Space 점프; 누른 채 유지하고 공중에서 재입력
- 기대 결과: 허용 방향으로 이동·착지; 키 유지로 반복 점프하거나 공중 추가 점프하지 않음
- 진입 위치: P_START 주변
- 근거: `Assets/_Docs/Cooked_시스템_기획서확정_V.1.1.docx §1.1`; `Assets/_Code/Core/Tech_HMS/Features/PlayerInputReader.cs`; `Assets/_Scenes/Tech_SYM/Architecture/Prototype/1_Stage.unity`

## FT-004 · 플레이어 / 카메라·이동축 / 구간 전환

- 중요도: 핵심
- 시작 조건: 사이드뷰에서 통로 입구에 접근
- 실행: 통로 A → 쿼터 A → 통로 B → 쿼터 B 순서로 실제 이동; 전환 중 Esc로 정지·재개
- 기대 결과: 트리거 자신의 로컬 오프셋으로 표시한 파란 점에 Side의 월드 Z/통로의 월드 X가 정렬됨; 실제 Cinemachine Brain 블렌드와 정렬 완료 뒤 HMS 이동축 적용; 옵션 중 전환 정지·해제 후 입력 잠금 해제
- 진입 위치: 동선: X 진행 → Z 통로 → XZ → Z 통로 → XZ
- 근거: `Assets/_Docs/planning-index.md`; `Assets/_Scenes/Tech_SYM/Architecture/Prototype/1_Stage.unity`; `Assets/_Code/Core/Tech_SYM/Architecture/Level/LevelCameraService.cs`

## FT-005 · 플레이어 / 카메라·이동축 / 재시도 후 정렬

- 중요도: 핵심
- 시작 조건: 통로 또는 쿼터뷰에서 체크포인트 저장 후 실패
- 실행: 실패·재시도한 뒤 방향키로 이동
- 기대 결과: 새 Actor의 카메라·Follow/LookAt·월드 UI 참조와 저장 지점 좌표·HMS 이동축 복구; 이전 Actor 카메라와 입력 잠금이 남지 않음
- 진입 위치: 해당 구간 체크포인트
- 근거: `Assets/_Docs/Cooked_시스템_기획서확정_V.1.1.docx §1.6/2.4`; `Assets/_Code/Core/Tech_SYM/Architecture/Session/PlayerBridgeService.cs`; `Assets/_Scenes/Tech_SYM/Architecture/Prototype/1_Stage.unity`

## FT-006 · 플레이어 / 크기 전환·껍질 / 축소와 원거리 회수

- 중요도: 핵심
- 시작 조건: P_FORM 해금, 기본 크기, 복귀 공간 충분
- 실행: Q로 축소 → 껍질에서 멀어짐 → Q로 복귀
- 기대 결과: 껍질 한 개 생성; 거리와 무관하게 회수되고 기본 크기로 복귀
- 진입 위치: P_FORM 이후 좁은 통로
- 근거: `Assets/_Docs/planning-index.md`; `Assets/_Docs/Cooked_시스템_기획서확정_V.1.1.docx §1.2`; `Assets/_Code/Core/Tech_SYM/Architecture/Session/PlayerBridgeService.cs`

## FT-007 · 플레이어 / 크기 전환·껍질 / 공간 부족 보호

- 중요도: 핵심
- 시작 조건: 작은 상태로 낮은 천장 아래, 껍질 존재
- 실행: Q 복귀 요청 → 천장 밖으로 이동 → Q 다시 입력
- 기대 결과: 공간 부족 때 작은 상태·껍질 유지; 공간 확보 후 재입력하면 정상 복귀
- 진입 위치: SmallTunnelRoof 아래 → 밖
- 근거: `Assets/_Docs/Cooked_시스템_기획서확정_V.1.1.docx §1.2`; `Assets/_Scenes/Tech_SYM/Architecture/Prototype/1_Stage.unity`; `Assets/_Code/Core/Tech_HMS/Features/PlayerFormController.cs`

## FT-008 · 플레이어 / 눈물 수집·스킬 / 5개 수집과 범위 기절

- 중요도: 핵심
- 시작 조건: P_TEAR 해금, 조각 5개, 적이 범위 안에 있음
- 실행: F 한 번 입력; HUD·범위 안 적·플레이어 움직임 확인
- 기대 결과: 조각 5→0; 범위 내 적 기절·접촉 공격 중단, 기절 종료 후 행동 복구; 이동·점프 가능
- 진입 위치: P_TEAR 이후 조각·적 구역
- 근거: `Assets/_Docs/planning-index.md`; `Assets/_Docs/Cooked_시스템_기획서확정_V.1.1.docx §1.3`; `Assets/_Code/Core/Tech_SYM/Architecture/Level/LevelTearFragment.cs`; `Assets/_Scenes/Tech_SYM/Architecture/Session/TearSkill.asset`

## FT-009 · 플레이어 / 눈물 수집·스킬 / 부족·상한 보호

- 중요도: 핵심
- 시작 조건: 해금 전 또는 조각 0~4개; 별도 상황에서 5개 보유
- 실행: 부족 상태에서 F → 5개 상태로 추가 조각 접촉
- 기대 결과: 부족 상태에서 발동·소모 없음; 5개를 넘지 않고 Architecture의 추가 조각은 남음
- 진입 위치: Prototype의 LevelTearFragment만 대상
- 근거: `Assets/_Docs/planning-index.md`; `Assets/_Docs/business-rules.md`; `Assets/_Code/Core/Tech_SYM/Architecture/Level/LevelTearFragment.cs`

## FT-010 · 플레이어 / 피해·사망 / 피해에서 재등장까지

- 중요도: 핵심
- 시작 조건: 체력이 남은 기본 플레이 상태
- 실행: 비기절 적 몸통/위험 바닥 접촉으로 체력을 0까지 감소
- 기대 결과: 실제 피해에 체력 HUD·피격 표시 반영; 사망 후 체크포인트 재등장, 최대 체력·조작 복구
- 진입 위치: 일반 적 또는 GasBurner
- 근거: `Assets/_Docs/Cooked_시스템_기획서확정_V.1.1.docx §1.5/1.6`; `Assets/_Docs/business-rules.md`; `Assets/_Code/Core/Tech_SYM/Enemies/Combat/EnemyCombat.cs`

## FT-011 · 저장·복구 / 체크포인트 / 수량·해금 복원

- 중요도: 핵심
- 시작 조건: 새 체크포인트 도달 때 조각 수 기록
- 실행: 저장 뒤 조각 획득/사용 → 사망 또는 다시 시작
- 기대 결과: 저장 당시 조각 수와 위치 복원; 해금 유지, 체력 최대로·기본 크기, 이전 껍질 제거
- 진입 위치: P_FORM/P_TEAR/P_CHASE
- 근거: `Assets/_Docs/Cooked_시스템_기획서확정_V.1.1.docx §2.4/1.6`; `Assets/_Docs/business-rules.md`; `Assets/_Code/Core/Tech_SYM/Architecture/Session/PlayerBridgeService.cs`

## FT-012 · 저장·복구 / 체크포인트 / 같은 지점 덮어쓰기 방지

- 중요도: 핵심
- 시작 조건: 체크포인트 저장 뒤 조각 수 변경
- 실행: 같은 체크포인트 다시 밟기 → 다시 시작; 다음 새 지점에서도 반복
- 기대 결과: 같은 지점은 최초 수량 유지; 다른 지점 도달 시 새 수량으로 갱신
- 진입 위치: 서로 다른 체크포인트 두 곳
- 근거: `Assets/_Docs/Cooked_시스템_기획서확정_V.1.1.docx §2.4`; `Assets/_Docs/business-rules.md`; `Assets/_Scenes/Tech_SYM/Architecture/Prototype/1_Stage.unity`

## FT-013 · 저장·복구 / 체크포인트 / 연속 재시도 정리

- 중요도: 핵심
- 시작 조건: 작은 상태·껍질 또는 열린 화면 경험 후 정상 플레이
- 실행: 다시 시작을 수행하고 복귀 후 한 번 더 수행
- 기대 결과: 플레이어 및 활성 출력 Camera/AudioListener 각각 하나; 이전 Actor·화면·입력 연결 해제, 새 Actor로 카메라·UI 재연결
- 진입 위치: 옵션 다시 시작 / 낙하 실패
- 근거: `Assets/_Docs/Cooked_시스템_기획서확정_V.1.1.docx §1.6`; `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/GameSessionRuntime.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Session/SessionActorHost.cs`

## FT-014 · 스테이지 기믹 / 선형 이동 플랫폼 / 왕복·탑승·점프

- 중요도: 핵심
- 시작 조건: 플랫폼 윗면에 올라탈 수 있는 상태
- 실행: Editor에서 부모 기준 X/Y/Z 도착점 이동량·속도를 설정하고 부모 위치·회전·배율 및 청록 기즈모 확인 → 두 번 왕복 동안 탑승 → 중간에 점프해 이탈
- 기대 결과: 부모 기준 3차원 두 종점 사이 무대기 연속 왕복; 현재 월드 거리/속도에 맞는 편도시간, 탑승 이동 전달, 점프 후 계속 끌려가지 않음
- 진입 위치: LinearPlatform / 쿼터 A
- 근거: `Assets/_Docs/Cooked_시스템_기획서확정_V.1.1.docx §2.2`; `Assets/_Scenes/Tech_SYM/Prefabs/LinearPlatform.prefab`; `Assets/_Code/Core/Tech_SYM/Platforms/LinearShuttlePlatform.cs`; `Assets/_Code/Core/Tech_SYM/Platforms/KinematicPlatformMotion.cs`

## FT-015 · 스테이지 기믹 / 선형 이동 플랫폼 / 정지와 접촉 해제

- 중요도: 핵심
- 시작 조건: 플랫폼 이동 중, 윗면 탑승 또는 옆면 접촉
- 실행: 옵션 열기·닫기 → 플랫폼에서 내림 → 옆면 접촉; Editor 별도 표본에서 0 이동량과 비활성화·재활성화 확인
- 기대 결과: 옵션 중 이동 정지·해제 후 이어짐; 내린 대상·옆면 접촉자를 계속 운반하지 않음; 0 이동량은 정지하고 재활성화 때 이전 운반 상태가 남지 않음
- 진입 위치: LinearPlatform / 쿼터 A
- 근거: `Assets/_Docs/Cooked_시스템_기획서확정_V.1.1.docx §2.2/5.2`; `Assets/_Code/Core/Tech_SYM/Platforms/PlatformContacts.cs`; `Assets/_Scenes/Tech_SYM/Prefabs/LinearPlatform.prefab`

## FT-016 · 스테이지 기믹 / 압력판·연동 장치 / 무게와 경로 변화

- 중요도: 핵심
- 시작 조건: 해금 후 압력판 접근, 기본 몸·작은 몸·껍질 준비 가능
- 실행: 기본 몸으로 누름 → 이탈 → 작은 몸으로 접촉 → 껍질 배치
- 기대 결과: 기본 몸/껍질은 누르고 연결 장치 이동; 작은 몸은 누르지 않음; 무게 제거 시 복귀
- 진입 위치: ShellPressurePlate → ShellGate
- 근거: `Assets/_Docs/Cooked_시스템_기획서확정_V.1.1.docx §2.2`; `Assets/_Scenes/Tech_SYM/Architecture/Prototype/1_Stage.unity`; `Assets/_Code/Core/Tech_SYM/Platforms/PressurePlatePlatform.cs`; `Assets/_Code/Core/Tech_SYM/Platforms/PressureLinkedPlatform.cs`

## FT-017 · 스테이지 기믹 / 압력판·연동 장치 / 중간 전환·복수 무게

- 중요도: 핵심
- 시작 조건: 몸 또는 껍질이 압력판을 누르고 있음
- 실행: 움직임 중 이탈·재진입; 몸과 껍질 중 하나만 제거
- 기대 결과: 현재 진행 위치에서 부드럽게 방향 전환; 유효 무게가 하나라도 남으면 눌림 유지
- 진입 위치: ShellPressurePlate
- 근거: `Assets/_Docs/business-rules.md`; `Assets/_Code/Core/Tech_SYM/Platforms/PressurePlatePlatform.cs`; `Assets/_Code/Core/Tech_SYM/Platforms/PlatformContacts.cs`

## FT-018 · 스테이지 기믹 / 붕괴 플랫폼 / 붕괴와 비복구

- 중요도: 핵심
- 시작 조건: 프로토타입의 붕괴 발판, 기본 크기
- 실행: 윗면 착지 후 곧바로 이탈 → 경고·Collider 해제·붕괴 후처리까지 관찰; 실제 클립을 연결한 빌드에서는 전체 재생 완료도 확인
- 기대 결과: 떠나도 경고가 계속되고 완료 후 지지 해제; 클립 미연결은 한 실행에 경고 한 번과 외형 숨김, 연결 시 실제 Animator 완료 후 숨김; Prototype은 이번 시도 중 자동 재생성 없음
- 진입 위치: ShatteredPlatform / 사이드 구간
- 근거: `Assets/_Docs/planning-index.md`; `Assets/_Docs/Cooked_시스템_기획서확정_V.1.1.docx §2.2`; `Assets/_Scenes/Tech_SYM/Architecture/Prototype/1_Stage.unity`; `Assets/_Code/Core/Tech_SYM/Features/ShatteredPlatform.cs`

## FT-019 · 스테이지 기믹 / 붕괴 플랫폼 / 조건·재시도 복구

- 중요도: 핵심
- 시작 조건: 작은 몸 또는 껍질만 윗면에 위치; 별도 시도에서 붕괴 완료
- 실행: 작은 몸/껍질·옆면·밑면 접촉 → 기본 몸 윗면으로 붕괴 → 옵션 정지·재개 → 다시 시작; Editor 별도 표본에서 도중 비활성화·풀 재활성화 확인
- 기대 결과: 작은 몸·껍질·옆면·밑면으로 붕괴하지 않음; 정지 중 경고/재생/복구 시간이 멈춤; 비활성화·풀 재사용에서 이전 작업·자세가 남지 않음; retry는 스테이지 재준비로 발판 복구
- 진입 위치: Prototype 배치의 canRegenerate=false
- 근거: `Assets/_Docs/planning-index.md`; `Assets/_Docs/business-rules.md`; `Assets/_Code/Core/Tech_SYM/Features/ShatteredPlatform.cs`; `Assets/_Scenes/Tech_SYM/Architecture/Prototype/1_Stage.unity`

## FT-020 · 스테이지 기믹 / 주기 플랫폼 / 존재·경고·충돌 주기

- 중요도: 핵심
- 시작 조건: 주기 발판이 보이는 구간, 옵션 닫힘
- 실행: 생성 → 숨김 → 등장 경고 → 생성까지 한 주기 관찰·탑승 시도
- 기대 결과: 현재 자산 5초 지지/2초 숨김/3초 경고; 숨김·경고 중 지지 없음, 생성 시 다시 지지
- 진입 위치: PeriodicPlatform / 쿼터 A
- 근거: `Assets/_Docs/Cooked_시스템_기획서확정_V.1.1.docx §2.2`; `Assets/_Scenes/Tech_SYM/Prefabs/PeriodicPlatform.prefab`; `Assets/_Code/Core/Tech_SYM/Platforms/PeriodicPlatform.cs`

## FT-021 · 스테이지 기믹 / 부유 플랫폼 / 착지와 반동

- 중요도: 핵심
- 시작 조건: 부유 플랫폼 근처, 바닥 또는 점프 가능한 상태
- 실행: 한 번 착지 → 계속 서 있기 → 이탈 후 재착지
- 기대 결과: 착지 때 눌림·반동·복귀; 계속 서 있다고 착지 반응 반복 없음, 재착지 때 정상 반응
- 진입 위치: FloatingPlatform / 쿼터 A
- 근거: `Assets/_Docs/Cooked_시스템_기획서확정_V.1.1.docx §2.2`; `Assets/_Scenes/Tech_SYM/Prefabs/FloatingPlatform.prefab`; `Assets/_Code/Core/Tech_SYM/Platforms/FloatingPlatform.cs`

## FT-022 · 스테이지 기믹 / 오븐 트레이 / 상호작용·탑승·중복 입력

- 중요도: 핵심
- 시작 조건: 이동 가능한 트레이 손잡이 앞
- 실행: E로 열기 → 이동 중 E 반복 → 탑승/닫기 확인
- 기대 결과: 이동 중 중복 실행·안내 없음; 두 종점에 완료, 윗면 탑승자는 이동을 따라감
- 진입 위치: DrawerStep_1~3 / 사이드 구간
- 근거: `Assets/_Docs/Cooked_시스템_기획서확정_V.1.1.docx §2.2`; `Assets/_Scenes/Tech_SYM/Architecture/Prototype/1_Stage.unity`; `Assets/_Code/Core/Tech_SYM/OvenTray/OvenTrayObject.cs`

## FT-023 · 스테이지 기믹 / 위험 바닥 / 피해 간격과 재접촉

- 중요도: 핵심
- 시작 조건: 체력 충분, 위험 바닥에 접근
- 실행: 접촉 → 짧게 이탈·재접촉; 피해 시각·체력 변화 기록
- 기대 결과: 성공 피해당 HP 1, 같은 대상 피해 간격 최소 1초; 재접촉으로 간격 초기화되지 않음
- 진입 위치: GasBurner / 마지막 쿼터 구간
- 근거: `Assets/_Docs/business-rules.md`; `Assets/_Scenes/Tech_SYM/Architecture/Prototype/1_Stage.unity`; `Assets/_Code/Core/Tech_SYM/Platforms/DamageFloorPlatform.cs`

## FT-024 · 스테이지 기믹 / 기름 바닥 / 미끄러짐과 회복

- 중요도: 핵심
- 시작 조건: 기름 바닥 전 정상 이동
- 실행: 기름 위에서 이동·정지 → 이탈 후 상태 회복 관찰
- 기대 결과: 출발·정지가 둔해짐; 체력은 기름만으로 감소하지 않음, 설정 지속 후 정상 이동 복귀
- 진입 위치: Oil / 마지막 쿼터 구간
- 근거: `Assets/_Docs/Cooked_시스템_기획서확정_V.1.1.docx §2.3`; `Assets/_Scenes/Tech_SYM/Architecture/Prototype/1_Stage.unity`; `Assets/_Code/Core/Tech_HMS/Features/OilSurface.cs`

## FT-025 · 화면·상호작용 / 메모 / E 열기·닫기

- 중요도: 핵심
- 시작 조건: 메모 대상 근처, 다른 모달 없음
- 실행: E로 열기 → 이동/능력 입력 → E로 닫기
- 기대 결과: 본문 열림 동안 월드·플레이어 정지; E로 닫고 조작 복구; 마우스 종료 버튼 없음
- 진입 위치: Memo_1001 / 시작 부근
- 근거: `Assets/_Docs/planning-index.md`; `Assets/_Docs/business-rules.md`; `Assets/_Code/Core/Tech_SYM/Interaction/MemoUIController.cs`; `Assets/_Code/Core/Tech_SYM/Interaction/MemoView.cs`

## FT-026 · 화면·상호작용 / 메모 / 옵션 중첩·반복 사용

- 중요도: 핵심
- 시작 조건: 메모 본문 열림
- 실행: Esc 옵션 → E 입력 → 옵션 닫기 → E 메모 닫기; 한 번 더 열기
- 기대 결과: 옵션에서 아래 메모로 E가 흐르지 않음; 옵션만 닫으면 메모 정지 유지; 중복 표시·입력 없음
- 진입 위치: Memo_1001
- 근거: `Assets/_Docs/business-rules.md`; `Assets/_Code/Core/Tech_SYM/Architecture/Session/GameplayInputRouter.cs`; `Assets/_Code/Core/Tech_SYM/Interaction/MemoViewModel.cs`

## FT-027 · 화면·상호작용 / 대화 / 표시 완료와 다음 진행

- 중요도: 핵심
- 시작 조건: 지정 대화 트리거 진입, 옵션 닫힘
- 실행: 표시 중 진행 입력 → 표시 완료 후 다음 입력; 빠르게 연속 입력
- 기대 결과: 첫 입력은 현재 문장 완성, 다음 유효 입력은 다음 문장; 0.3초 간격·한 입력 한 처리; 월드 정지
- 진입 위치: Dialogue 트리거 / 탐색 구간
- 근거: `Assets/_Docs/planning-index.md`; `Assets/_Docs/Cooked_시스템_기획서확정_V.1.1.docx §4.1`; `Assets/_Code/Core/Tech_SYM/Architecture/Dialogue/DialogueService.cs`

## FT-028 · 화면·상호작용 / 대화 / 로그·옵션·종료 복구

- 중요도: 핵심
- 시작 조건: 대화 진행 중 자동 모드 활성
- 실행: 로그 열기·닫기 → 옵션 열기·닫기 → 대화 스킵 또는 완료
- 기대 결과: 로그가 자동 진행 해제·표시 정지; 옵션 중 표시/타이머 정지 후 이어짐; 종료 후 자기 차단 해제
- 진입 위치: Dialogue 트리거
- 근거: `Assets/_Docs/business-rules.md`; `Assets/_Docs/Cooked_시스템_기획서확정_V.1.1.docx §4.1`; `Assets/_Code/Core/Tech_SYM/Architecture/Dialogue/DialogueViewModel.cs`

## FT-029 · 화면·상호작용 / 오프닝·엔딩 / 원화 표시·정지·스킵

- 중요도: 핵심
- 시작 조건: 새 게임 오프닝 또는 탈출 엔딩
- 실행: 원화 순서 확인 → 옵션 열기 → 스킵 시도 → 옵션 닫고 스킵
- 기대 결과: 원화 한 장 전체화면 순서 표시; 옵션 중 진행·스킵 정지; 오프닝은 플레이, 엔딩은 제목으로 종료
- 진입 위치: 오프닝 / 엔딩
- 근거: `Assets/_Docs/planning-index.md`; `Assets/_Docs/Cooked_시스템_기획서확정_V.1.1.docx §4.2`; `Assets/_Code/Core/Tech_SYM/Architecture/Cinematics/CinematicService.cs`

## FT-030 · 화면·상호작용 / 옵션·음량 / 정지·재개·저장

- 중요도: 핵심
- 시작 조건: 정상 플레이 중 움직이는 발판/적이 보임
- 실행: Esc → 음량 세 종류 조절 → 계속하기 → 앱 재실행 후 음량 확인
- 기대 결과: 월드는 정지, UI 조작 가능; 닫으면 이어짐; 음량 즉시 적용·기기 저장값 유지
- 진입 위치: 제목 및 플레이 옵션
- 근거: `Assets/_Docs/Cooked_시스템_기획서확정_V.1.1.docx §5.2`; `Assets/_Docs/business-rules.md`; `Assets/_Code/Core/Tech_SYM/Architecture/UI/Runtime/OptionsService.cs`

## FT-031 · 화면·상호작용 / 옵션·음량 / 상태별 메뉴 허용

- 중요도: 핵심
- 시작 조건: 제목/오프닝/플레이/엔딩 각각 옵션 진입 가능 상태
- 실행: 각 상태에서 옵션 메뉴 사용; 플레이에서 제목 복귀 선택
- 기대 결과: 재시도·제목 복귀는 플레이 중만 허용; 전환 중 중복 열림 없음; 복귀 후 월드 잔존 없음
- 진입 위치: 각 안정 상태의 옵션
- 근거: `Assets/_Docs/Cooked_시스템_기획서확정_V.1.1.docx §5.2`; `Assets/_Docs/business-rules.md`; `Assets/_Code/Core/Tech_SYM/Architecture/UI/Runtime/ViewModels.cs`

## FT-032 · 화면·상호작용 / HUD·화면 배치 / 상태와 표시 일치

- 중요도: 핵심
- 시작 조건: 첫 진입 후 피격·수집·해금 발생 가능
- 실행: HP·조각·능력·주변 안내 변화 확인; 1920×1080과1280×800에서 옵션도 확인
- 기대 결과: HP/조각0~5/해금 안내가 실제 상태와 일치; 텍스트 잘림·버튼 가림 없음; 컷신 중 표시 적절
- 진입 위치: 주 화면 / 옵션 / 메모
- 근거: `Assets/_Docs/Cooked!_UI기획서Ver1.0.pdf p5~7`; `Assets/_Docs/Cooked_시스템_기획서확정_V.1.1.docx §5.1`; `Assets/_Docs/planning-index.md`; `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/HudView.cs`

## FT-033 · 추격·탈출 / 껍질 퍼즐 / 통과 조건 판정

- 중요도: 핵심
- 시작 조건: 형태 해금, 껍질과 통과 지점 사용 가능
- 실행: 기본 몸으로만 판 누름 → 껍질을 남기고 작은 몸으로 통과
- 기대 결과: 몸으로만 눌러 지나가는 것은 해결 아님; 껍질 점유 중 통과해야 해결·차단 해제
- 진입 위치: ShellPressurePlate → ShellPuzzle
- 근거: `Assets/_Docs/Cooked_시스템_기획서확정_V.1.1.docx §2.1`; `Assets/_Scenes/Tech_SYM/Architecture/Prototype/1_Stage.unity`; `Assets/_Code/Core/Tech_SYM/Architecture/Level/ShellPressureLatch.cs`

## FT-034 · 추격·탈출 / 추격 / 시작·퇴로·포획

- 중요도: 핵심
- 시작 조건: 구출·유효 체크포인트·껍질 퍼즐 조건을 만족
- 실행: 추격 시작선 통과 → 앞 충돌체에 실제 접촉
- 기대 결과: 추격 수락 후 퇴로 차단; 거리 숫자만으로 포획하지 않음; 실제 접촉 시 체크포인트 재시도
- 진입 위치: P_CHASE → Chase / 마지막 구간
- 근거: `Assets/_Docs/planning-index.md`; `Assets/_Docs/business-rules.md`; `Assets/_Scenes/Tech_SYM/Architecture/Prototype/1_Stage.unity`; `Assets/_Code/Core/Tech_SYM/Architecture/Chase/ChaseFrontContact.cs`

## FT-035 · 추격·탈출 / 추격 / 정지·새 시도

- 중요도: 핵심
- 시작 조건: 추격 진행 중
- 실행: 옵션 열기·닫기 → 포획 → 다시 시작선 통과
- 기대 결과: 옵션 중 추격 정지·해제 후 이어짐; 포획 뒤 기존 무리·잠금 정리, 새 추격 한 번 시작
- 진입 위치: Chase 구간
- 근거: `Assets/_Docs/Cooked_시스템_기획서확정_V.1.1.docx §3.2`; `Assets/_Docs/business-rules.md`; `Assets/_Code/Core/Tech_SYM/Architecture/Chase/ChaseService.cs`

## FT-036 · 추격·탈출 / 탈출·다음 판 / 조건과 세션 종료

- 중요도: 핵심
- 시작 조건: 탈출 조건 일부 미충족; 이후 전체 조건 충족
- 실행: 미충족 탈출 접근 → 구출·퍼즐·추격 조건 충족 → 탈출 → 엔딩 → 새 게임
- 기대 결과: 미충족 시 엔딩 없음; 충족 시 엔딩·제목 전환, 새 판은 체크포인트·해금 초기화·중복 객체 없음
- 진입 위치: Rescue → ShellPuzzle → P_CHASE → Escape
- 근거: `Assets/_Docs/planning-index.md`; `Assets/_Docs/Cooked_시스템_기획서확정_V.1.1.docx §2.1/4.2`; `Assets/_Docs/business-rules.md`; `Assets/_Code/Core/Tech_SYM/Architecture/Level/StageService.cs`



## FT-037 · Editor 작업 / 씬 진입 / 선택한 씬 Play

- 중요도: 핵심
- 시작 조건: Unity Edit Mode, 저장된 씬과 이전 startScene 설정
- 실행: 통합 여섯 씬 각각 Play/Stop → 기존 Stage1·HMS 기준·SYM 독립 씬 Play
- 기대 결과: 정확한 통합 경로만 Bootstrap을 거쳐 선택 Stage/Title 진입; 나머지는 활성 씬 직접 Play, 첫 Stage 강제 없음
- 진입 위치: 프로그래머 가이드의 여섯 정확한 경로와 비대상 씬
- 근거: `Assets/_Docs/tracking/ready-editor-entry-hms-2026-10-06/프로그래머 수정 가이드.md`; `Assets/_Code/Core/Tech_SYM/Architecture/Foundation/BootstrapEntryRoute.cs`


## FT-038 · Editor 작업 / 진입 설정 / Stop·취소·Reload 복원

- 중요도: 핵심
- 시작 조건: startScene=null/Bootstrap/외부 씬, Reload 설정 조합
- 실행: 대상 → 비대상 → 대상으로 전환해 Play/Stop; 진입 취소; 원래 설정 비교
- 기대 결과: 선택한 씬 진입 유지; 임시 설정과 일회성 route 정리, 원래 startScene·Reload 설정 및 편집 씬 복원
- 진입 위치: Unity Editor Play/Stop 및 Enter Play Mode 설정
- 근거: `Assets/_Code/Core/Tech_SYM/Architecture/Foundation/Editor/BootstrapPlayModeHook.cs`; `Assets/_Docs/operations.md`; 이전 자동 진입 결과는 날짜별 실행 이력으로 보존


## FT-039 · 플레이어 / 독립 HMS 조립 / 폼·스킬·Cinemachine 연결

- 중요도: 핵심
- 시작 조건: Brokenable/MultiFunc/InGame 독립 씬 직접 Play, HMS 초기 쿨다운 종료
- 실행: Q 한 번 → 껍질·작은 표시 → Q 한 번 복귀 → F 한 번; 방향키·공개 모드별 카메라 확인
- 기대 결과: 한 Reader/Controller 경로로 폼 전환과 껍질 회수 각각 한 번, 조각 5→0; 네 고유 Cinemachine 카메라의 실제 선택과 축 일치, 활성 출력·Listener 각각 하나
- 진입 위치: Assets/_Scenes/Tech_SYM/01_BrokenablePlatformTest, 02_MultiFuncPlatformTest, 03_InGame
- 근거: `Assets/_Scenes/Tech_HMS/01_PlayerMovementTest.unity`; `Assets/_Code/Core/Tech_SYM/Architecture/Session/Editor/HmsActorCameraAuthoring.cs`

## 2026-10-06 빌드별 자동 증거 인계

이번 빌드 ID는 Windows-platform-20261006-034111Z다. 엑셀의 Editor 자동 검증에 AUTO-12~18 및 LIMIT-03을 추가하고 수동39판정과 과거13자동행은 보존했다. 빌드 ID 사본·읽기 전용 PDF16쪽·FunQA 원본 양식을 output/Tech_SYM/qa-functional에 제공한다. 실행 결과와 제한은 [이번 검증 기록](ready-platform-hms-cleanup-2026-10-06/테스트 및 검증 항목.md)을 따른다. 이 정본은 요구 케이스를 소유하며 수동 판정이나 자동 통과를 각 케이스에 복제하지 않는다.
