# 메모·상호작용 표시

현재 시스템 기준은 Assets/_Docs/planning-index.md의 V1.8이며 이전 사용자 기능 명세와 기존 구현보다 우선한다. 구현·검증 완료는 tracking/status.md와 실제 증거로 확인한다. 보호 원본·메타·아트의 쓰기 권한은 최신 파일별 허용 범위로 한정한다.

최신 입력은 F 상호작용이다. 대상 선택은 PlayerInteractionService 한곳에서2.5m/전방±60도/차폐/가까운 유효 대상/중복 Collider를 처리하고 SessionActorHost가 한 번 전달한다. CheckInteract는 현재 소스에 없으며 WorldPromptView/WorldPromptViewModel은 실제 표시·명령 소비를 유지한다. 같은 이름이나 JSON preview의 instanceID0만으로 미사용·연결 누락을 판정하지 않는다.

MemoObject/Model은 ID/이미지와 요청, MemoUIController는 Opening/Open/Closing 수명·자기 플레이어 입력 차단과 월드 정지, VM/View는 표시/명령을 소유한다. 메모는 F로만 열고 닫으며 Opening 중 닫기를 수락하고 Closing 완료까지 입력을 막는다. 옵션을 위에 열 수 있고 옵션 입력을 아래 F에 흘리지 않는다. 메모는 자기 입력 차단·월드 정지 토큰을 획득하고 닫기·피해·사망·제목 복귀에서 자기 토큰만 해제한다. 대화/로그 자체는 월드 정지 토큰을 소유하지 않는다. 화면과 비동기 작업도 해당 소유자가 취소한다.

월드 안내와 화면 본문 Canvas는 구분하고 안내는 대상 중심 위 F 한 개만 표시한다. ID 양수/중복 없음·필수 이미지·소유 구독 해제를 검증한다. 자동 대화와 메모가 겹칠 때의 최종 규칙은 준비 명세의 실제 답변을 따른다.

현행 지침: Inspector 위치/대상은 실물 참조, 수치는 목적·단위가 명확한 값이다. 씬 월드 객체를 관리자 자식에 묶지 않는다. 에이전트 Play 금지는 철회됐으며 공식 Unity 도구로 승인된 검증을 실행한다. 기존 프리팹 삭제/재생성/Apply·자동 Builder 실행은 해당 작업의 명시 허용 범위를 따른다. 이전 실행 관찰 수는 당시 증거이고 이번 통과가 아니다.
