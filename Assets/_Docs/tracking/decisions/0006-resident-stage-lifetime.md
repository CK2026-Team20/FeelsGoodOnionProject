# Core와 Stage 유지, 초기 재준비와 능력 출처

2026-10-07 직접 답변으로 InGameCore 최초1회 로드 뒤 등록Stage3개를 전부 additive 최초 로드하고 이후 같은 씬을 유지한다. 사망뿐 아니라 Title 복귀와 다음판에도 Core/Stage를 언로드하거나 재로드하지 않는다. Title 씬의 새게임 언로드는 허용한다.

체크포인트에는 현재 플레이어 월드Position과 Fragments만 저장한다. START/END 두 곳이며 중간 능력·추격은 새저장 지점이 아니다. 현재Stage에서 최초 얻은 능력은 사망에 다시 잠그고 트리거 재접촉에 해금한다. 이전Stage 출처는 보존한다. 출처와 안내 완료 장부는 한 판의 런타임 상태이고 저장 payload나 별도snapshot이 아니다.

씬 재로드도 Core의 해금을 자동으로 지우지 못한다. 재로드는 Stage 객체/참조/구독과 작업이 끊겨 새 인스턴스와 오래된Core 콜백이 어긋날 수 있다. 씬 유지에서는 초기화가 빠진 적·장치·소비조각·점유·Tween이 남는다. 그러므로 같은 씬의 typed owner를 명시적으로 재준비하고 자기 작업·접촉·빌린Actor 참조를 정리한 뒤 두 값을 복원한다. 소비조각은 같은 객체/Visual/Collider/소비 플래그로 다시 나타난다.

App locator/출력은 앱 수명이다. Core 리그/호스트/Stage 배치는 씬 수명이고 Session·PlayerBridge·Dialogue·화면바인딩은 한 판 범위다. 재시도에는 같은 한 판 서비스, Title에는 ReleaseSession, 새게임에는 같은Core에 새App부모 범위를 연결한다. 비활성Stage는 Actor·외부Tween·추격·타이머를 실행하지 않는다.

실제 반복 사망/다음판과 Stage2/3 이전능력 보존, 동일씬·소비조각 identity를 확인했다. 위치 준비/서비스 요청을 포함한 자동 범위이며 원본 전체 수동동선과 팀빌드 평가는 별도다.
