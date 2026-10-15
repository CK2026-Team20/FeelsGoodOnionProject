# 협업 계약

## 책임과 경계

Flow·Session·Control·Dialogue·Operation의 공개 입력·출력과 수명 계약을 소유한다. Unity 실행 객체 생성과 화면 구현은 이 모듈의 책임이 아니다.

## 유지할 계약

CheckpointSnapshot은 접촉한 체크포인트 Transform의 유한 월드 Position, 접촉 순간 Fragments 정수0..5, UnlockedAbilities 마스크0/1/2/3의 정확히 세 값이다. StageService가 유효한 최초 접촉에서 스냅샷과 활성화 이력을 콜백 전에 원자 확정한다. 수락 전 실패는 이전 저장을 유지하고, 수락 후 표현 취소·사망은 이미 수락한 저장과 소비 이력을 유지한다. CapsuleCollider 월드 중심을 Position에 맞추며 HP·폼·껍질·음악·시점·세계 상태는 payload에 추가하지 않는다. 첫 저장 전에는 첫 구간 시작과 Entry 초기 값을 사용하고, 추격 직전 예외만 당시 플레이어 Capsule 중심을 저장한다.

구체 View·Level·Integration 구현을 계약으로 역참조하지 않는다. 공용 Assets/_Code/Core/Interfaces와 Tech_HMS API는 참조 전용이다.

## 검증

잘못된 스냅샷 범위, 미완료 결과, 실패·취소 전달, 이벤트 구독 해제 계약을 검증한다.

현행 지침: Inspector 위치/대상은 실물 참조, 수치는 목적·단위가 명확한 값이다. 씬 월드 객체를 관리자 자식에 묶지 않는다. 에이전트 Play 금지는 철회됐으며 공식 Unity 도구로 승인된 검증을 실행한다. 기존 프리팹 삭제/재생성/Apply·자동 Builder 실행은 해당 작업의 명시 허용 범위를 따른다. 이전 실행 관찰 수는 당시 증거이고 이번 통과가 아니다.
