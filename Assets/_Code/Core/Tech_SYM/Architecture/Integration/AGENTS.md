# 시스템 조립

## 책임과 경계

AppComposition·CoreComposition과 세션·UI·월드 바인딩, 설치 자산과 빌드·검증 진입점을 소유한다. 각 서비스의 규칙을 이곳에 복제하지 않는다.

## 유지할 계약

GameSessionRuntime은 재시도 동안 유지하고 제목 복귀에 폐기한다. 대화 시작은 직접 수락 경로 하나만 사용한다. Actor 제거 통지가 파괴보다 먼저 나가야 한다.

빌드는 명시적인 여섯 씬만 전달하고 ProjectSettings를 바꾸지 않는다. _Art·HMS 원본은 참조한다. 실패 시 부분 조립 자원까지 회수하고 원래 예외를 보존한다.

이 문장의 ProjectSettings 제한은 도구가 임의로 수동 설정을 바꾸지 않는다는 뜻이다. 2026-10-06 사용자 지시에 따라 Unity·Editor 자동 저장은 전후 diff·원인을 기록하고 커밋에 포함한다. 빌드의 엔진 결과·오류와 자동 설정 변경 감사 결과를 구분해 기록한다.

참조 허용은 공개 기능을 자기 소유 오브젝트에 추가·설정·연결하고 소유 배치 override를 저장하는 조립이다. 보호 원본 코드·자산·.meta·import 설정 직접 수정과 원본 Apply·이동·삭제·GUID 변경으로 조립을 우회하지 않는다.

Actor 생성 코루틴은 HMS Start 준비 → ActorReady의 월드·UI 카메라 연결 → 초기 Brain 선택 완료 순서로 기다리고 준비 전에 성공이나 Playing 표시를 확정하지 않는다. ActorRemoving은 UI 카메라·트리거·서비스의 빌린 참조를 비운다. UI 상태 구독은 Core 수명에 유지하며 카메라 교체마다 ViewModel 구독을 다시 만들지 않는다.

AppComposition은 자기 루트 하위의 presentationCamera와 같은 오브젝트의 AudioListener 하나를 소유한다. GameRuntimeService는 세션 GameplayVisible 및 ActorViewReady에서 실제 출력 여부를 도출해 통지한다. 준비된 Actor 출력에서는 앱 Camera/Listener를 함께 끄며 ActorRemoving은 이전 Actor 출력을 먼저 끈 뒤 앱 출력 복구를 통지한다. 재시도는 표시 요청이 true로 유지되어도 새 Actor 준비 완료를 통지해야 한다. 이전 세션의 구독만 해제하고 앱 구독은 다음 판까지 유지한다. 종료·실패에서도 자기 출력과 구독만 정리하며 Actor Camera Transform을 직접 갱신하지 않는다.

## 검증

실제 Prototype/1_Stage의 InputSystem 두 판 연속, 정확한 여섯 통합 경로 및 비대상 표본의 Editor 진입, 중간 초기화·종료 실패, 구독 중복, 빌드 전후 ProjectSettings 해시와 실행 파일 결과를 각각 확인한다.

현재 선택한 stage 하나만 로드한다. 신규 첫 스테이지는 Prototype/1_Stage이며 기존03_1_Stage는 보존하고 Stage2/3 직접 진입 계약을 유지한다. Play/build 전 loaded dirty 검사를 수행하고 자동 저장·폐기 없이 setup/startScene을 복원한다. 검증 도구가 호출한 서비스/teleport 결과와 실제 입력 동선 결과를 혼합하지 않는다.
