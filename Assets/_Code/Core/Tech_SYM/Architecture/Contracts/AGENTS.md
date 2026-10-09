# 협업 계약

## 책임과 경계

Flow·Session·Control·Dialogue·Operation의 공개 입력·출력과 수명 계약을 소유한다. Unity 실행 객체 생성과 화면 구현은 이 모듈의 책임이 아니다.

## 유지할 계약

CheckpointSnapshot은 유한한 현재 ActorBody 월드 위치·정수0..5 조각·해금 마스크0/1/2/3 세 항목이다. 잘못된 입력은 원자 거절하고 이전 저장을 유지한다. Stage/Marker는 런타임 실물 참조로 구별하며 HP/폼/월드/대화/시간/디스크 진행을 저장하지 않는다. 요청 수락/실행 완료/취소는 구별한다.

구체 View·Level·Integration 구현을 계약으로 역참조하지 않는다. 공용 Assets/_Code/Core/Interfaces와 Tech_HMS API는 참조 전용이다.

## 검증

잘못된 스냅샷 범위, 미완료 결과, 실패·취소 전달, 이벤트 구독 해제 계약을 검증한다.

현행 지침: Inspector 위치/대상은 실물 참조, 수치는 목적·단위가 명확한 값이다. 씬 월드 객체를 관리자 자식에 묶지 않는다. 에이전트 Play 실행·기존 프리팹 삭제/재생성/Apply·자동 Builder 실행은 금지다. 이전 실행 관찰 수는 당시 증거이고 이번 통과가 아니다.
