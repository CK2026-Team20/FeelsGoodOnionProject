# 오프닝·엔딩

현재 기준은 Assets/_Docs/planning-index.md의 V1.6과 최신 직접 지시다. 구현·검증 완료는 tracking/status.md와 실제 증거로 확인한다. 보호 원본·메타·아트의 쓰기 권한은 최신 파일별 허용 범위로 한정한다.

목표는 순서 있는 Cut catalog/단일 시계/VM/View의 원화1장+하단문구, 컷4초 자동·전체Skip이다. 현재 연출 소스는 Cut catalog/Service/VM/View 경로를 사용한다. 과거 Timeline Artwork Track/Clip/FrameBridge와4패널/Scale 호환 소스는 현재 구현 경로가 아니며, 자산 이관 완료 여부는 별도 증거로 확인한다. Timeline 패키지/타인 자산은 삭제하지 않는다.

Prepare→Play→Hide, Completed/Skipped/Cancelled 차이와 마지막 컷의 Hide전 유지, 옵션 정지의 남은 시간·Skip차단, 이전revision 콜백 무시를 지킨다. View는 VM 명령/표시만 소유한다. 클릭/Enter로 다음 컷을 넘기지 않고 Skip입력을 아래 게임에 흘리지 않는다.

흐름/세션/CP는 연출이 소유하지 않는다. Poppo 최종 대화 Completed/Skipped로 Ending을 요청한다. 컷 수4개 제한 없음, 실제 아트는 참조 원본이며 부족하면 연결 대기로 기록한다. 누락 데이터/준비 실패·Skip/취소/옵션/재진입/해제를 결합 검증한다.

현행 지침: Inspector 위치/대상은 실물 참조, 수치는 목적·단위가 명확한 값이다. 씬 월드 객체를 관리자 자식에 묶지 않는다. 에이전트 Play 금지는 철회됐으며 공식 Unity 도구로 승인된 검증을 실행한다. 기존 프리팹 삭제/재생성/Apply·자동 Builder 실행은 해당 작업의 명시 허용 범위를 따른다. 이전 실행 관찰 수는 당시 증거이고 이번 통과가 아니다.
