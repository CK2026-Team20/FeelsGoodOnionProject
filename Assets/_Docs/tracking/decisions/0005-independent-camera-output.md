# 0005 앱 단일 출력과 독립 카메라 리그

2026-10-06 사용자는 앱 Camera·CinemachineBrain·AudioListener 각1개와 플레이어에서 분리된 리그를 선택했다. 새 SYM 제품은 Side/Quarter만 사용한다. 0004의 Actor 동시 생성은 과거 구현 설명으로 보존하며 새 설계 기준에서는 이 결정으로 대체한다.

이전 구조는 HMS 첫 Awake 참조 검사/재연결 API 부재와 retry 해제 안전성 때문에 채택됐다. 출력 교대·Actor 자식 탐색·중복 우선순위 비용을 줄이려면 HMS 명시적 Bind/Unbind/Preview/Commit을 구현하고 App Output/Core 가상 카메라/Actor 목표 수명을 구분한다. CameraRig라는 이름만 바꾸거나 부모만 옮겨서는 작동하지 않는다. 보호 파일 허가와 실제 세션/retry/옵션/Brain 검증 후 기존 ActorCameraRig를 삭제한다.

2026-10-08 구현 적용: 이번 한정9개HMS 공개 확장과 SYM 자산 이관으로 App 출력1/Core Side·Quarter2/Actor Anchor 구성을 사용한다. 실제9진입·Stop/초기Quarter/여러retry·다음판에서 수명과 Brain 준비를 확인했다. 과거0004와 구조 검토는 당시 선택 이유를 보존하며 현재 연결 규칙은 이 결정이다. 실제 아트 animation clip 재생과 팀 빌드 수락은 별도다.
