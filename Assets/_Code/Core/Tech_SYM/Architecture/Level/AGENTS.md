# 스테이지 규칙

## 책임과 경계

체크포인트 Pose 등록, 트리거, 껍질·트레이 기믹 및 눈물 조각 수집, 구출·탈출과 카메라를 소유한다. 세션 저장 스냅샷이나 전체 씬 전환은 소유하지 않는다.

## 유지할 계약

Stage·Checkpoint ID 중복과 초기 지점 누락을 거절한다. 추격 시작 수락 전에 후퇴문을 닫지 않는다. 탈출은 구출·해당 퍼즐·설정된 추격 경로의 수락 조건을 함께 검사한다. 새 프로토타입은 체크포인트에서 해금하고 트리거에서만 카메라를 전환한다. 통로 X 정렬/사이드 Z 보간 완료 후 HMS 공개 API로 입력과 Rigidbody 축을 함께 확정한다.

Side→HMS Side, Quarter→Quarter, Corridor→BackFixed로 연결한다. CameraZoneData는 Id·Movement·트리거 자신의 로컬 AlignmentOffset 설정이며 Resolve가 CameraZoneRequest의 월드 AlignmentPosition으로 변환한다. 실제 접촉과 항상 표시하는 파란 OnDrawGizmos는 같은 정렬점을 사용한다. Side는 월드 Z, Corridor는 월드 X를 정렬하며 Quarter는 위치 정렬을 하지 않는다. CheckpointMarker의 bodyOffset은 별도 월드 생성 위치 계약이다.

실제 Cinemachine Brain 블렌드와 물리 정렬이 모두 끝난 뒤 TrySetMode로 축을 확정한다. SYM은 출력 Camera Transform을 매 프레임 쓰지 않는다. 전환 중 같은 구간은 중복 억제하며 다른 실제 트리거 요청은 이전 전환을 취소하고 수락한다. 잘못된 요청은 진행 중 전환을 취소하지 않는다. 옵션 정지에서는 완료를 유예하고 Disable/Destroy/Actor 제거는 자기 전환과 차단만 정리한다. HMS 컴포넌트의 소유 배치 설정은 허용하며 보호 원본 수정·Apply는 별도 파일 허가가 필요하다.

스테이지를 새로 로드하면 Pose를 다시 조회한다. LevelTearFragment의 상한 시 보존 규칙을 기존 TearFragment와 혼동하지 않는다. 공유 캐릭터·아트 원본을 수정하지 않는다.

## 검증

저장 위치 높이·회전, 트리거 중복, 체크포인트를 뛰어넘은 추격 진입, 퍼즐 미해결 탈출 차단, 가득 찬 조각, Stage 2 깊이 이동과 카메라를 확인한다.
