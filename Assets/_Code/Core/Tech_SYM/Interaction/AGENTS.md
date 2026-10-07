# 메모·상호작용 표시

2026-10-06 최신 목표는 Assets/_Docs/planning-index.md와 ready-system-v11-refactor-2026-10-06 명세를 따른다. 이 문서는 구현 완료 기록이 아니다. 기존 지침의 다른 동작을 새 목표로 승계하지 않는다. 보호 원본/메타/아트 수정은 파일별 허가가 필요하다.

최신 입력은 F 상호작용이다. 대상 선택은 PlayerInteractionService 한곳에서2.5m/전방±60도/차폐/가까운 유효 대상/중복 Collider를 처리하고 GameplayInputRouter가 한 번 전달한다. CheckInteract와 별도 Prompt View/VM은 독립 SYM 씬의 소비를 같은 구성으로 이관한 뒤 삭제한다.

MemoObject/Model은 ID/이미지와 요청, MemoUIController는 Opening/Open/Closing 수명·자기 PC입력 차단, VM/View는 표시/명령을 소유한다. 메모는 F로만 열고 닫으며 Opening 중 닫기를 수락하고 Closing 완료까지 입력을 막는다. 옵션을 위에 열 수 있고 옵션 입력을 아래 F에 흘리지 않는다. 메모는 월드 정지 토큰을 갖지 않는다. 피해/사망/제목 복귀는 화면/작업/자기 토큰을 취소한다.

월드 안내와 화면 본문 Canvas는 구분하고 안내는 대상 중심 위 F 한 개만 표시한다. ID 양수/중복 없음·필수 이미지·소유 구독 해제를 검증한다. 자동 대화와 메모가 겹칠 때의 최종 규칙은 준비 명세의 실제 답변을 따른다.
