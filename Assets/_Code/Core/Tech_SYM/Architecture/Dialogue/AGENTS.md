# 대화

현재 기준은 Assets/_Docs/planning-index.md의 V1.6과 최신 직접 지시다. 구현·검증 완료는 tracking/status.md와 실제 증거로 확인한다. 보호 원본·메타·아트의 쓰기 권한은 최신 파일별 허용 범위로 한정한다.

JSON 다섯 필드 전체 검증 후 읽기 전용 행을 게시하고 같은 코드 Sequence는1..N연속이다. 일반 완료 코드는 한 판에서 반복하지 않고 사망/제목 취소는 완료로 기록하지 않는다. retry 취소 후 row1부터 시작한다. 완료 장부는 checkpoint 저장 항목이 아니다. retry의 능력 복원과 대화 장부는 분리한다. Completed/Skipped 코드는 한 판에서 유지한다. 일반 안내는 Cancelled만 재접촉 재시작 가능하다. 2026-10-10 사용자 직접 선택에 따라 포포 최종 대화는 실제 포포 종료 트리거(StageTriggerKind.Escape) 재접촉 경로에서 완료 장부를 지우지 않고 첫 행부터 새 회차로 다시 재생할 수 있다. 현재 Stage·살아 있는 Actor·세대·회차 검증과 같은 프레임 사망/포획 우선은 유지한다.

Enter/창클릭, reveal완성/다음행 분리,0.3초 간격·완전 표시 뒤3초Auto, 완전 표시 행만 로그를 지킨다. 로그는 Auto를 끄고 표시를 정지하며 닫아도 자동재개 없음이다. 메모와 겹친 시작 수락은 spec.md의 확정 규칙과 GameSessionRuntime의 실제 조정 경로를 따른다.

대화/로그는 월드정지 토큰을 갖지 않으며 PC입력만 막는다. 옵션은 표시/예약 남은 시간을 정지한다. 사망/종료/Disable은 자기 표시/예약/구독/제한만 해제한다. VM은 View를 직접 참조하지 않고 낡은 Session/Row/Request 완료를 무시한다. Core가 DialogueService와 표시/입력 바인딩 수명을 직접 소유한다.

잘못된 JSON/코드 완료·취소·반복, 빠른 입력/Auto/로그/옵션, 피해/사망·Dispose 후 콜백/재바인딩을 사용자 기능으로 검증한다.

현행 지침: Inspector 위치/대상은 실물 참조, 수치는 목적·단위가 명확한 값이다. 씬 월드 객체를 관리자 자식에 묶지 않는다. 에이전트 Play 금지는 철회됐으며 공식 Unity 도구로 승인된 검증을 실행한다. 기존 프리팹 삭제/재생성/Apply·자동 Builder 실행은 해당 작업의 명시 허용 범위를 따른다. 이전 실행 관찰 수는 당시 증거이고 이번 통과가 아니다.

최종 포포 Escape(7)만 완료 장부와 별개로 새 회차를 허용한다. 실제 Trigger·선택 객체·Table·Code·활성 Stage·Actor 세대·Round·Session을 수락 전후 및 완료 callback에서 검증하고 Completed/Skipped에서만 Ending을 한 번 예약한다. 동일 프레임 사망/포획이 우선이고 Cancelled/낡은 callback은 현재 판에 전달하지 않는다.
