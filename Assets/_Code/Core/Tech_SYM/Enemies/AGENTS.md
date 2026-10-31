# 일반 적

2026-10-29 승인된 T2 기준: EnemyActor가 Idle/Patrol/Stunned 상태·기절 시간·현재 목표 끝점을 소유한다. EnemyMotor는 고정 Start/End 앵커의 월드 위치를 기준으로 Body Transform의 위치만 변경하고, EnemyContactSensor는 실제 Trigger 접촉 피해를 담당한다. Root와 앵커는 이동시키지 않는다. Stationary 또는 XYZ 대각선을 포함한 Patrol 배치를 지원한다. 삭제한 EnemyStateMachine/EnemyCombat을 다시 붙이거나 생성하지 않으며 적 HP·처치·Dead·Chase/Return·사망 기름·몸체 bob을 추가하지 않는다.

고정 EnemyActor Root 아래의 별도 Body에 BoxCollider Trigger와 EnemyContactSensor를 함께 붙이고 body/owner를 실제 컴포넌트로 연결한다. 순찰 Start/End는 같은 씬의 Root 내부·Body 외부에 고정 지점으로 둔다. 움직이는 객체 아래의 지점은 허용하지 않는다. Body는 Root의 자손이면 되며 직접 자식이나 localPosition=0을 강제하지 않는다. 편집한 초기 Body 위치에서 첫 목표 End로 이동한 뒤 Start/End를 왕복한다. 첫 이동 방향 파라미터·동일축·초기 선분 위 배치 검사는 없다. 두 앵커가 같은 위치면 Body는 현재 위치에 정지하며 Stationary는 앵커를 요구하지 않는다.

Root/Body 회전과 양수 비균등 배율을 허용한다. Inspector 속도는 Root 배율과 무관한 Body의 월드 이동 거리(m/s)이며 기존 Transform을 정규화하지 않는다. 지형 LayerMask·검색거리·안전 높이·Cast/Sweep·침투 보정·갇힘 판정은 제거했고 지형을 피하는 경로는 배치자가 책임진다. 필요한 참조·유한값·양수 크기/실제 배율, Trigger·소유 센서·고정 앵커 계약은 유지한다. Root·조상·자식에 Rigidbody를 추가하지 않으며 Player의 동적 Rigidbody로 Trigger 접촉한다.

ResetForRetry의 준비 오류는 해당 적이 원인 로그를 남기고 비활성화하며 IsPrepared=false로 알린다. 잘못된 센서 연결도 해당 센서만 연결을 생략한다. 정상 배치를 다시 준비하면 복구할 수 있으며 잘못된 적 하나의 작성 오류를 전체 씬 준비 실패로 전파하지 않는다.

배치 OilSurface와 기존 HMS IStunState VFX는 실제 다른 기능이므로 조합한다. 기절 중 이동·접촉 공격을 멈추고 재기절은 기존 남은 시간 추가 계약을 사용하며 임의 상한을 만들지 않는다. PlayerFacade의 공통 피해1/무적2초와 Bridge의 공통 넉백을 사용하고 피해원별 속도/타이머를 복제하지 않는다. 지속 접촉은 무적 종료 뒤 다시 판정하며 낡은 Actor는 거절한다.

Stage 초기 재준비는 최초 기록한 Body localPosition을 복원하고 첫 목표 End·초기 대기·기절·접촉 상태를 재준비한다. Root/Body의 회전·배율을 덮어쓰지 않는다. 비활성 Stage는 target·자기 작업을 해제한다. Hurt Collider는 실제 Body이며 Sensor 후보라는 이유로 눈물 차폐 검사에서 무조건 제외하지 않는다. 수평·수직·XYZ 순찰, 회전·비균등 배율에서의 Body 월드 속도, 선분 밖 초기 배치·동일 앵커, 기절/재기절·지속 접촉·Options·Retry/비활성 Stage를 기능 단위로 검증한다.

Inspector 이름·Tooltip은 연결 대상/조절 효과를 짧은 한국어로 설명한다. 씬 월드 Root를 Manager의 자식으로 묶지 않는다. 에이전트 Play 금지는 철회됐으며 공식 Unity 도구를 사용한다. prefab 삭제/재생성/Apply·자동 Builder·리소스 제작은 해당 작업의 명시 허용 범위를 따른다. 실제 컴파일/격리 엔진/API 검사와 사람의 자연 충돌·VFX·플레이를 구분해 보고한다.
