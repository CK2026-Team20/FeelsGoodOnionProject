# 0005 앱 단일 출력과 독립 카메라 리그

2026-10-06 사용자는 앱 Camera·CinemachineBrain·AudioListener 각1개와 플레이어에서 분리된 리그를 선택했다. 새 SYM 제품은 Side/Quarter만 사용한다. 0004의 Actor 동시 생성은 과거 구현 설명으로 보존하며 새 설계 기준에서는 이 결정으로 대체한다.

이전 구조는 HMS 첫 Awake 참조 검사/재연결 API 부재와 retry 해제 안전성 때문에 채택됐다. 출력 교대·Actor 자식 탐색·중복 우선순위 비용을 줄이려면 HMS 명시적 Bind/Unbind/Preview/Commit을 구현하고 App Output/Core 가상 카메라/Actor 목표 수명을 구분한다. CameraRig라는 이름만 바꾸거나 부모만 옮겨서는 작동하지 않는다. 보호 파일 허가와 실제 세션/retry/옵션/Brain 검증 후 기존 ActorCameraRig를 삭제한다.

상세 근거: [구조 검토](../ready-system-v11-refactor-2026-10-06/원본 대조 및 구조 검토.md). 현재 구현이 변경됐다는 기록이 아니며 준비 단계다.
