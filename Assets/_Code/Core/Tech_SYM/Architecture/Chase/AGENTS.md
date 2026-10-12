# 추격

2026-10-11 직접 지시: 시작 트리거 접촉 → 퇴로 안전/시작 가능 → 현재 상태 저장 성공 → 추격 시작/문 닫힘. 전신 통과, 진행 경로, 추격 경로와 경로상 포획 간격은 사용하지 않는다.

ChaseRuntimeDriver는 시작 시 플레이어 뒤8m의 Y/Z를 고정하고 월드 +X로 이동하며 실제 Physics 자식 BoxCollider와 현재 Actor CapsuleCollider의 접촉/상대 이동 sweep으로 포획한다. 같은 프레임 포획은 Stage의 최종 대화보다 먼저 처리한다. 옵션/메모 정지, 낡은 Actor 거절, 공통 ForceDeath와 자기 구독 해제를 유지한다. ChaseSwarmPresentation은 외형 tween만, ChaseDefinition은 속도 설정만 맡는다.

지역 퇴로와 시작 수락은 StageTrigger 소유다. 최종 포포 대화는 Stage의 기존 세대/회차/선택 검증과 Completed/Skipped 장부를 보존한다. 완료/Skipped만 Ending이며 취소·낡은 callback·동시 사망/포획은 거절한다.

현행 공통 규칙은 Assets/_Docs/planning-index.md와 루트 AGENTS.md를 따른다. 에이전트 Play는 금지하며 구현/검증 상태는 tracking/status.md다.
