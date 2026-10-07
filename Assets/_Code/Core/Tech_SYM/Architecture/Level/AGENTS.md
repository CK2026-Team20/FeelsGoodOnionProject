# 스테이지와 카메라 구간

2026-10-06 최신 목표는 Assets/_Docs/planning-index.md와 ready-system-v11-refactor-2026-10-06 명세를 따른다. 이 문서는 구현 완료 기록이 아니다. 기존 지침의 다른 동작을 새 목표로 승계하지 않는다. 보호 원본/메타/아트 수정은 파일별 허가가 필요하다.

StageService는 실행 구성의 Stage/시작·끝 체크포인트 식별과 초기 배치·센서/기믹 연결을, Session은 현재 PC 위치/조각 두 값 저장을 소유한다. 적/장치/필드 snapshot/등록자를 만들지 않는다. 능력은 지정 구간+UI/DialogCode로 해금하고 CP에만 종속하지 않는다. Poppo 최종 대화 완료로 종료하며 rescued/래치/눈물게이트/별도Escape 퍼즐 결합은 제거한다.

카메라는 Side/Quarter만 사용한다. CameraZoneData는 ID/Movement/자기 로컬 AlignmentOffset이며 Resolve가 월드 요청을 만든다. 파란 Gizmo와 실제 정렬은 같은 계산이다. Side는 Z정렬, Quarter는 정렬 없음이다. Brain과 실제 정렬 완료 후 HMS 모드를 확정하며 출력 Transform은 Brain만 쓴다. 초기 spawn의 모드는 정해진 배치/구간 설정에서 결정하고 checkpoint에 저장하지 않는다.

같은 요청 중복 억제·최신 다른 요청 취소/수락·잘못된 요청의 기존작업 보존·옵션 정지·Disable/Actor 제거 시 자기 제한 해제를 지킨다. Back/Corridor/Actor 자식 탐색·중복 우선순위는 제거한다. ID enum/소유GUID/override는 native 이관 뒤 제거한다.

추격은 PC 전신의 시작선 통과·Chase 수락 이후 문을 닫는다. 추격 시작은 체크포인트 캡처를 하지 않는다. 스테이지의 체크포인트는 시작과 끝 두 개뿐이며, 중간 해금 트리거는 저장하지 않는다. 거리gap 포획과 일반 적 추적을 구분한다. LevelTearFragment는 단일 TearFragment로 이관한다. 보호 캐릭터/아트 원본을 수정하지 않는다.

Core 1회 로드 뒤 모든 등록 Stage를 최초 additive 로드하고 이후 Core/Stage를 다시 로드하지 않는다. 활성 Stage만 Actor를 연결하며 비활성 Stage의 자기 작업·Tween·추격·접촉을 정리한다. retry는 기능 소유자별 typed reset으로 초기 배치를 복구하고 현재 Stage에서 얻은 능력과 해당 안내 완료만 재잠근다.

카메라 초기/전환/중첩/옵션·실제 Body 복원·동일CP·전신통과·Poppo 완료·최대 조각을 실제 기능으로 확인한다.
