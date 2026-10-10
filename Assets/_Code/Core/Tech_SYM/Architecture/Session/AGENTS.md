# 세션과 플레이어 연결

체크포인트 payload는 유한한 현재 ActorBody 월드 Position, 정수 Fragments 0..5, UnlockedAbilities 마스크 None=0/FormChange=1/Tear=2/Both=3의 정확히 세 항목이다. 모든 값을 검증한 뒤 한 번에 교체하며 실패 시 이전 snapshot을 지킨다. 첫 저장 전에는 초기 진입 구성을 사용한다. 같은 실제 Marker의 재접촉은 갱신하지 않고 추격 직전 저장은 별도 수락 경계로 처리한다. StageId/CheckpointId·월드 상태·HP·폼·카메라·시간·대화 완료를 저장하지 않는다.

사망은 같은 Stage의 초기 배치·기믹·소비 조각 Root/Collider를 재준비하고 저장 위치·조각·마스크를 새 Normal/HP3 Actor에 복원한다. Stage 출처별 능력 초기화는 사용하지 않는다. 완료/Skipped 대화 장부는 한 판 동안 유지한다. 일반 안내는 Cancelled만 재시작 가능하고 최종 포포 Escape(7)만 장부를 지우지 않은 새 회차를 허용한다. 추가 재등장 무적·월드 저장 등록자를 만들지 않는다.

HMS HP/폼/운동은 Facade/ActorBody가, Actor 생성·제거는 SessionActorHost가 소유한다. Reader/Router 각 하나, 공개 스킬/API와 입력 차단 토큰을 사용한다. 실제 visualRoot/cameraAnchor를 연결하며 ActorRemoving 전에 빌린 참조를 해제한다. 깊은 끼임은 몸체 분리와 Dead 종결 후 Retry 한 번, 데드존은 HP/무적을 무시한다. 독립 모드에 통합 Retry를 추가하지 않는다.

현행 공통 규칙은 Assets/_Docs/planning-index.md와 루트 AGENTS.md를 따른다. 에이전트 Play는 금지하며 구현/검증 상태는 tracking/status.md다.
