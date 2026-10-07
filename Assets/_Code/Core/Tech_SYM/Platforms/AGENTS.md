# 물리 발판

2026-10-06 최신 목표는 Assets/_Docs/planning-index.md와 ready-system-v11-refactor-2026-10-06 명세를 따른다. 이 문서는 구현 완료 기록이 아니다. 기존 지침의 다른 동작을 새 목표로 승계하지 않는다. 보호 원본/메타/아트 수정은 파일별 허가가 필요하다.

KinematicPlatformMotion은 물리 이동의 단일 작성자, PlatformContacts는 실제 윗면 콜백/점유, 종류별 컨트롤러는 진행 목적/상태를 소유한다. 신규 구조는 Root 로직과 명시적으로 주입받는 PhysicsBody/Contacts 자식이다. 현재 GetComponent/RequireComponent 및 콜백 위치를 실제 코드와 함께 이관하며 같은 물체에 HMS 이동자를 중복 조립하지 않는다.

LinearShuttlePlatform은 부모 로컬 XYZ 이동량/월드m/s, 양 종점0.5초 대기다. 이동량0은 정지이며 부모 변환은 끝점에 반영한다. FloatingPlatform은 한 착지당 눌림/반동이고 PhysicsBody의 지속 bob은 제거한다. Periodic은5/2/3초·생성 중 비충돌·개별 시작 위상이다. 압력은 기본PC/껍질만, 해제 복귀·재압력 반전은 현재 진행을 사용한다. DamageFloor는 피해원 자체1초 타이머 대신 플레이어 공통HP1/무적2초를 사용한다.

ManualUpdate 트윈은 물리 단계에서 진행하고 비활성화/제목/Stage 제거 시 자기 작업·점유를 해제한다. 발판/장치/적 상태는 체크포인트에 저장하지 않는다. retry는 Stage 초기 재준비다.

검증은 왕복·탑승·일반 물체 운반·점프/이탈 관성·정지/복귀·중간 재입력의 결합 기능으로 묶는다. 끝점 이동만으로 전체 기능 통과를 쓰지 않는다. 실제 물리 시간/모든 phase, 부모 변환·잘못된 값·재활성화·콜백 누락을 확인한다.
