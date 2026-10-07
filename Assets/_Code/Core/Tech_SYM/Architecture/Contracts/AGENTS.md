# 협업 계약

## 책임과 경계

Flow·Session·Control·Dialogue·Operation의 공개 입력·출력과 수명 계약을 소유한다. Unity 실행 객체 생성과 화면 구현은 이 모듈의 책임이 아니다.

## 유지할 계약

CheckpointSnapshot은 유한한 플레이어 월드 위치와0~5 보유 눈물조각 두 값만 요구하며 잘못된 입력을 원자적으로 거부한다. StageId/CheckpointId와 HP·폼·능력·월드 객체 상태는 저장 payload에 넣지 않는다. Stage/Marker 식별자는 실행 구성으로만 사용한다. OperationResult의 성공·실패·취소를 혼동하지 않는다. 반환 bool의 요청 수락과 실행 완료를 구분한다.

구체 View·Level·Integration 구현을 계약으로 역참조하지 않는다. 공용 Assets/_Code/Core/Interfaces와 Tech_HMS API는 참조 전용이다.

## 검증

잘못된 스냅샷 범위, 미완료 결과, 실패·취소 전달, 이벤트 구독 해제 계약을 검증한다.
