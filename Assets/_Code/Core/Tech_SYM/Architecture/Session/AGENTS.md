# 세션과 플레이어 연결

체크포인트 payload는 체크포인트 StageTrigger의 유한 월드 Position, 정수 Fragments 0..5, UnlockedAbilities 마스크 None=0/FormChange=1/Tear=2/Both=3의 정확히 세 항목이다. 등장·복귀는 플레이어 CapsuleCollider 월드 중심을 Position에 맞춘다. 비활성 생성 직후와 Normal 준비 뒤 동일 정렬을 적용하며 attachedRigidbody 대신 같은 오브젝트의 Rigidbody를 확인한다. 수락 전 값 검증 실패는 이전 snapshot을 지킨다. 수락한 저장은 소비 이력과 원자 확정하며 이후 표현 취소·사망으로 되돌리지 않는다. 같은 트리거 재접촉 억제를 Actor 교체로 비우지 않는다. 추격 직전만 당시 Collider 중심을 저장한다. StageId/CheckpointId·월드 상태·HP·폼·카메라·시간·대화 완료는 저장하지 않는다.

사망은 같은 Stage의 초기 배치·기믹·소비 조각 Root/Collider를 재준비하고 저장 위치·조각·마스크를 새 Normal/HP3 Actor에 복원한다. Stage 출처별 능력 초기화는 사용하지 않는다. 완료/Skipped 대화 장부는 한 판 동안 유지한다. 일반 안내는 Cancelled만 재시작 가능하고 최종 포포 Escape(7)만 장부를 지우지 않은 새 회차를 허용한다. 추가 재등장 무적·월드 저장 등록자를 만들지 않는다.

HMS HP/폼/운동은 Facade/ActorBody가, Actor 생성·제거는 SessionActorHost가 소유한다. HMS Reader/PlayerController 각 하나(같은 ActorBody), Host가 F 입력 처리, 공개 스킬/API와 입력 차단 토큰을 사용한다. 실제 visualRoot/cameraAnchor를 연결하며 ActorRemoving 전에 빌린 참조를 해제한다. 깊은 끼임은 몸체 분리와 Dead 종결 후 Retry 한 번, 데드존은 HP/무적을 무시한다. 독립 모드에 통합 Retry를 추가하지 않는다.

현행 공통 규칙은 Assets/_Docs/planning-index.md와 루트 AGENTS.md를 따른다. 에이전트 Play 금지는 철회됐으며 실제 실행 범위와 미검증은 tracking/status.md에 구분한다.

StageService가 같은 씬의 논리 구간, 첫 구간, 현재 구간, 활성화한 체크포인트 집합, 마지막 스냅샷과 그 복귀 구간을 소유한다. 구간에는 서로 다른 시작·끝 StageTrigger, 시점 종류, 실제 음악 Clip·음량·전환 시간, 진입 보정·카메라 전환 시간을 연결한다. 이름·배열 순번은 저장 키가 아니다. Trigger는 접촉을 전달하며 시점·음악·저장 장부를 별도로 편집하지 않는다.

재시도는 같은 Core/Stage와 StageService의 소비 이력을 유지하고 마지막 유효 저장 및 당시 구간·음악·시점을 직접 복원한다. 소비한 센서를 다시 발동하지 않는다. 현재 해금은 GameSessionService 원본이고 저장 시점의 해금은 별도 불변 스냅샷 값이다. Title 복귀에서 세션 종료 후 Stage 전체→Core 순서로 언로드하며 새 게임은 새 객체와 빈 체크포인트 장부로 시작한다.

보정 완료 위치는 현재 폼의 Capsule 중심으로 확인한다. 재시도 새 Actor는 Normal로 준비하며 작은 폼을 저장 값에 추가하지 않는다.
