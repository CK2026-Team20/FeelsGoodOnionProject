# 추격

경로 투영·진행 거리·포획은 Chase가, 전신 통과·퇴로 안전·현재 상태 저장·포포 최종 대화는 Stage/Session 연결이 맡는다. 실제 Transform 경로와 시작 지점을 작성한다.

전신 통과와 안전 확인 후 현재 위치·조각·해금 마스크 저장에 성공해야 한 번 시작한다. 초기 뒤 간격8m, 속도4m/s, 높이를 제외한 포획 간격0.8m는 조절 초기값이다. 옵션/메모 자기 월드정지를 유지하고 낡은 Actor를 거절한다. 최종 대화 Completed/Skipped는 Ending, Cancelled는 미완료이며 포획이 우선한다.

현행 공통 규칙은 Assets/_Docs/planning-index.md와 루트 AGENTS.md를 따른다. 에이전트 Play는 금지하며 구현/검증 상태는 tracking/status.md다.

최종 포포 Escape(7)만 완료 장부와 별개로 새 회차를 허용한다. 실제 Trigger·선택 객체·Table·Code·활성 Stage·Actor 세대·Round·Session을 수락 전후 및 완료 callback에서 검증하고 Completed/Skipped에서만 Ending을 한 번 예약한다. 동일 프레임 사망/포획이 우선이고 Cancelled/낡은 callback은 현재 판에 전달하지 않는다.
