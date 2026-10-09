# 시스템 조립

현재 목표는 Assets/_Docs/planning-index.md와 ready-system-v15-2026-10-10의 spec다. 실제 구현/검증 완료는 tracking의 결과로 확인한다. 사용자의 최신 직접 지시가 과거 동작 설명보다 우선한다.

AppComposition은 부모 없는 ServiceLocator를 Bootstrapper에 제공하며 앱 출력 Camera/Brain/AudioListener 각1개와 음량/Flow를 소유한다. Core는 앱을 부모로 하는 한 판의 서비스 범위·Side/Quarter 리그·ActorHost·UI를 조립한다. 같은 상태/업무를 이곳에 복제하지 않는다. 카메라 우선순위는 HMS Controller, 출력 Transform은 Brain, 전환과 정렬은 LevelCameraService가 소유한다.

Core1회 로드 뒤 등록Stage 전부를 최초 additive 로드하고 이후 Core/Stage를 unload/reload하지 않는다. 현재 활성Stage 하나만 Actor/트리거/추격에 연결한다. 비활성Stage의 자기 Tween/타이머/접촉을 중단한다. GameSessionRuntime은 retry에 유지하며 Title에서 세션만 해제한다. CoreComposition의 정상 해제 경로는 world/runtime/UI/Host/연출 참조를 비우고 같은 Core에서 다음 판을 생성할 수 있게 한다. 씬 유지와 데이터 수명을 혼동하지 않는다.

ActorRemoving은 빌린 카메라 목표/모드/월드/UI 참조를 파괴 전에 해제한다. Spawn은 HMS Start→ActorReady 연결→실제Brain/Side정렬 완료→입력/표시 순서다. 사망 retry는 같은Stage 초기재준비·소비조각의 실제활성/Collider/소비플래그 복구·저장 위치/조각/해금 마스크를 새 Normal Actor에 복원하며 완료 대화 기록은 유지한다. 초기 Stage/Checkpoint IDs는 authoring 실행구성이며 저장payload가 아니다.

Native Editor API로 소유 자산만 연결/저장/재로드한다. 기존 prefab 일괄 제거는 사용자 몫이다. _Art/공용/HMS원본자산/meta/Packages/ProjectSettings 수동수정과 보호prefab Apply 금지다. HMS 두 파일의 명시된 최소 범위만 허용한다. 삭제 후보23개는 호출/GUID/메뉴/동적생성·소유배치가 이관된 후 삭제한다.

빌드는 설치의 정확한6씬을 명시적으로 전달한다. 기존 및 향후 Unity자동저장 변경은 원인/diff를 검토·분류해 커밋한다. 자동변경 포함 지시는 수동보호수정권한을 확대하지 않는다. 비 Play build 전 loaded dirty·원래setup/active/startScene을 보호한다. 에이전트는 가능한 비 Play 범위에서 씬 유지·Actor/출력/입력단일성·실패취소/해제 계약을 검증한다. 실제InputSystem의 전체 동선·반복retry·다음판 플레이 확인은 사람이 수행하며 이전 Play 이력을 이번 결과로 승계하지 않는다.

현행 지침: Inspector 위치/대상은 실물 참조, 수치는 목적·단위가 명확한 값이다. 씬 월드 객체를 관리자 자식에 묶지 않는다. 에이전트 Play 실행·기존 프리팹 삭제/재생성/Apply·자동 Builder 실행은 금지다. 이전 실행 관찰 수는 당시 증거이고 이번 통과가 아니다.
