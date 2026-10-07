# 일반 적

최신 기준은 Assets/_Docs/planning-index.md와 ready-system-v11-refactor-2026-10-06 확정안 적용 명세다. 작성된 목표를 실제완료로 보고하지 않는다.

EnemyActor 조립, StateMachine 배타적 전이/기절시간, Motor 지형/실제이동, Combat/ContactSensor 접촉피해원의 책임을 구분한다. 일반 적은 X/Y 작성평면 순찰·Idle·Stunned만 가지며 Chase/Return은 제거한다. 기존 종류/방향/직렬화값은 실제 배치의 의미로 명시적으로 이관하고 재번호화로 추측하지 않는다.

적 HP/무적/머리밟기/처치/Dead/사망기름/몸체Float는 제거한다. EnemyModel의 남는 기절은 FSM 하나로 통합하고 IState 계약도 필요한 내부상태로 이관한다. 배치OilSurface와 HMS IStunState VFX는 다른 실제기능이므로 유지한다. 기절은 일반적만 대상, 재기절의 기존남은시간추가 계약 유지, 미선택상한을 추가하지 않는다. 기절중 접촉공격 없음이다.

PlayerFacade의 공통 피해1/무적2초/피해+넉백과 단일Knockback ms 조회를 사용한다. consumedDamage 일회성 대신 무적종료후 지속접촉을 다시 판정한다. 적 body는 루트바로아래 실제 BoxCollider Trigger이고 EnemyContactSensor는 그 몸체접촉relay다. 이름만 보고 탐지영역으로 제외하지 않는다. 적에 Rigidbody를 새로 요구하지 않는다. 몸체/루트 무회전·단위배율과 지형조건을 유지한다.

Stage의 retry/reset은 초기배치·기절/접촉/방향을 재준비하며 저장DTO/객체상태ID를 만들지 않는다. 비활성Stage는 target과 작업을 해제한다. 명시적HurtCollider 읽기계약을 제공해 눈물candidate와 탐지센서를 구분한다. 순찰X/Y·기절/재기절·해제/지속접촉·옵션·retry/비활성Stage를 결합 검증한다. 등록/Stage/Native변경과 실제삭제는 T9 단일작성자가 수행한다.
