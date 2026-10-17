# 일반 적

EnemyActor가 Idle/Patrol/Stunned 상태·기절 시간·현재 방향을 소유한다. EnemyMotor는 지형과 실제 Root Transform 이동, EnemyContactSensor는 실제 Trigger 접촉 피해를 담당한다. 삭제한 EnemyStateMachine/EnemyCombat을 다시 붙이거나 생성하지 않는다. X/Y 순찰 또는 Stationary 배치만 사용하며 적 HP·처치·Dead·Chase/Return·사망 기름·몸체 bob을 추가하지 않는다.

EnemyActor Root의 직접 자식 Body에 BoxCollider Trigger와 EnemyContactSensor를 함께 붙이고 body/owner를 실제 컴포넌트로 연결한다. Root·조상·자식에 Rigidbody를 추가하지 않는다. Body localPosition=0, Root/Body 월드 무회전·단위 배율, 유효 지형 LayerMask를 유지한다. Player의 동적 Rigidbody로 Trigger 접촉한다. 순찰 start/end는 실제 고정 Scene 지점이며 움직이는 EnemyRoot 밖에 둔다. Stationary 종류는 두 지점을 쓰지 않는다.

배치 OilSurface와 기존 HMS IStunState VFX는 실제 다른 기능이므로 조합한다. 기절 중 이동·접촉 공격을 멈추고 재기절은 기존 남은 시간 추가 계약을 사용하며 임의 상한을 만들지 않는다. PlayerFacade의 공통 피해1/무적2초와 Bridge의 공통 넉백을 사용하고 피해원별 속도/타이머를 복제하지 않는다. 지속 접촉은 무적 종료 뒤 다시 판정하며 낡은 Actor는 거절한다.

Stage 초기 재준비는 기존 적의 pose·방향·기절·접촉을 복원한다. 비활성 Stage는 target·자기 작업을 해제한다. Hurt Collider는 실제 Body이며 Sensor 후보라는 이유로 눈물 차폐 검사에서 무조건 제외하지 않는다. 순찰·기절/재기절·지속 접촉·Options·Retry/비활성 Stage를 기능 단위로 검증한다.

Inspector 이름·Tooltip은 연결 대상/조절 효과를 짧은 한국어로 설명한다. 씬 월드 Root를 Manager의 자식으로 묶지 않는다. 에이전트 Play 금지는 철회됐으며 공식 Unity 도구를 사용한다. prefab 삭제/재생성/Apply·자동 Builder·리소스 제작은 해당 작업의 명시 허용 범위를 따른다. 실제 컴파일/격리 엔진/API 검사와 사람의 자연 충돌·VFX·플레이를 구분해 보고한다.
