# 협업 계약

## 책임과 경계

Flow·Session·Control·Dialogue·Operation의 공개 입력·출력과 수명 계약을 소유한다. Unity 실행 객체 생성과 화면 구현은 이 모듈의 책임이 아니다.

## 유지할 계약

CheckpointSnapshot은 유효한 ID와 0 이상 조각을 요구한다. OperationResult의 성공·실패·취소를 혼동하지 않는다. 반환 bool의 요청 수락과 실행 완료를 구분한다.

구체 View·Level·Integration 구현을 계약으로 역참조하지 않는다. 공용 Assets/_Code/Core/Interfaces와 Tech_HMS API는 참조 전용이다.

현재 작업자는 Tech_SYM이며 변경은 승인된 Tech_SYM 범위에 한정한다. Tech_HMS 등 다른 작업자 폴더, 공용 Interfaces, _Art 원본과 그 .meta는 참조 전용이다. 같은 직군의 별도 명시적 파일 허용이 없는 한 수정하지 않는다.

## 검증

잘못된 스냅샷 범위, 미완료 결과, 실패·취소 전달, 이벤트 구독 해제 계약을 검증한다.

실행하지 않은 검증을 통과로 기록하지 않는다. Editor의 자산 생성 메뉴와 읽기·검증 메뉴를 구분하며 다른 기능의 씬을 재생성하지 않는다.
