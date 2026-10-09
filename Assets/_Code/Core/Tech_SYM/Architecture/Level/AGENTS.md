# 스테이지와 카메라 구간

StageService는 실제 Marker·경로·카메라 구간·기믹의 연결과 초기 재준비를 소유한다. 관리자 자식을 전제하지 않고 같은 씬의 비활성 루트까지 조회한다.

체크포인트 payload는 유한한 현재 ActorBody 월드 Position, 정수 Fragments 0..5, UnlockedAbilities 마스크 None=0/FormChange=1/Tear=2/Both=3의 정확히 세 항목이다. 모든 값을 검증한 뒤 한 번에 교체하며 실패 시 이전 snapshot을 지킨다. 첫 저장 전에는 초기 진입 구성을 사용한다. 같은 실제 Marker의 재접촉은 갱신하지 않고 추격 직전 저장은 별도 수락 경계로 처리한다. StageId/CheckpointId·월드 상태·HP·폼·카메라·시간·대화 완료를 저장하지 않는다.

추격은 전신 통과·퇴로 안전 확인 → 원자 저장 성공 → 추격 시작/문 닫힘이다. 포획은 같은 프레임 포포보다 우선하며 최종 대화 Completed/Skipped만 Ending으로 연결한다. StageTriggerKind 0..7과 Side1/Quarter3 의미는 보존한다. CameraZoneData는 실행 값 struct이고 작성은 실제 CameraZoneTrigger/정렬 Transform이다. Stage 간 자동 전환을 추가하지 않는다.

현행 공통 규칙은 Assets/_Docs/planning-index.md와 루트 AGENTS.md를 따른다. 에이전트 Play는 금지하며 구현/검증 상태는 tracking/status.md다.
