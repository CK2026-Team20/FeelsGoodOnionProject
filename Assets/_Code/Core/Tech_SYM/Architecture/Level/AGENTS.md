# 스테이지와 카메라 구간

StageService는 초기 Marker·음악·실제 카메라 경계 설정, 공유 진행/최종 대화 수명과 초기 재준비를 소유한다. 지역 추격 설정은 StageTrigger, 카메라 정렬점은 CameraZoneTrigger가 소유한다. 관리자 자식을 전제하지 않고 같은 씬의 비활성 루트까지 조회한다.

체크포인트 payload는 유한한 현재 ActorBody 월드 Position, 정수 Fragments 0..5, UnlockedAbilities 마스크 None=0/FormChange=1/Tear=2/Both=3의 정확히 세 항목이다. 모든 값을 검증한 뒤 한 번에 교체하며 실패 시 이전 snapshot을 지킨다. 첫 저장 전에는 초기 진입 구성을 사용한다. 같은 실제 Marker의 재접촉은 갱신하지 않고 추격 직전 저장은 별도 수락 경계로 처리한다. StageId/CheckpointId·월드 상태·HP·폼·카메라·시간·대화 완료를 저장하지 않는다.

추격은 StageTrigger 접촉 진입·퇴로 안전 확인 → 원자 저장 성공 → 추격 시작/문 닫힘이다. 진행/추격 경로와 전신 통과는 2026-10-11 직접 지시로 제거했다. 포획은 같은 프레임 포포보다 우선하며 최종 대화 Completed/Skipped만 Ending으로 연결한다. StageTriggerKind 1..7과 Side1/Quarter3 의미를 보존하고 Corridor2 구도를 사용한다. 중복 Enter(0)는 제거했으며 숫자0을 재사용하지 않는다. CameraZoneData는 실행 값 struct이고 작성은 실제 CameraZoneTrigger/자식 정렬 Transform이다. CheckpointMarker의 등장 Transform도 해당 Marker의 자식이다. Stage 간 자동 전환을 추가하지 않는다.

App 출력 Camera/Brain/AudioListener는 각 1개다. Core의 서로 다른 Side·Corridor·Quarter 세 CinemachineCamera와 구도·우선순위·추적 대상·경계·전환은 LevelCameraService가 소유한다. Brain이 출력하고 서비스는 최종 위치도 Stage의 cameraBounds 안으로 제한한다. Side(1)는 X, Corridor(2)와 Quarter(3)는 XZ 이동이다. HMS PlayerCameraController는 기존 독립 네 rig용이며 통합 분리 Bind/Preview/Commit/Unbind 확장은 사용하지 않는다.

CameraZoneTrigger의 실제 접촉과 현재 Actor·Stage·구간 세대를 확인한다. 통로 차단이 있는 전환은 실물 safeAnchor로 플레이어를 먼저 보정한 뒤 blockerTarget으로 차단 Rigidbody를 이동하며 둘 다 DOMove를 사용한다. 기본 전환 시간은 0.6초이고 각 이동 시간·Ease는 구간 설정이다. 실제 이동과 Brain 블렌드 완료 후 이동축을 확정하고 자기 입력 차단을 반환한다. 옵션/메모 정지, 취소·사망·Actor/Stage 교체 시 자기 작업 해제와 오래된 요청 거절을 유지한다.

현행 공통 규칙은 Assets/_Docs/planning-index.md와 루트 AGENTS.md를 따른다. 에이전트 Play는 금지하며 구현/검증 상태는 tracking/status.md다.

최종 포포 Escape(7)만 완료 장부와 별개로 새 회차를 허용한다. 실제 Trigger·선택 객체·Table·Code·활성 Stage·Actor 세대·Round·Session을 수락 전후 및 완료 callback에서 검증하고 Completed/Skipped에서만 Ending을 한 번 예약한다. 동일 프레임 사망/포획이 우선이고 Cancelled/낡은 callback은 현재 판에 전달하지 않는다.
