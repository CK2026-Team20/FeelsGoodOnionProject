# 시스템 조립

현재 기준은 Assets/_Docs/planning-index.md의 V1.6과 최신 직접 지시다. 구현·검증 완료는 tracking/status.md와 실제 증거로 확인한다. 보호 원본·메타·아트의 쓰기 권한은 최신 파일별 허용 범위로 한정한다.

AppComposition은 부모 없는 ServiceLocator와 앱 startup/shutdown을 직접 소유하며 앱 출력 Camera/Brain/AudioListener 각1개와 음량/Flow를 소유한다. Foundation/Bootstrapper.cs는 이 소유자에 통합 후 제거했으며 App.cs는 없다. Core는 앱을 부모로 하는 한 판의 서비스 범위·Side/Corridor/Quarter 리그·ActorHost·UI를 조립한다. 같은 상태/업무를 이곳에 복제하지 않는다. 통합 카메라 구도·우선순위·추적·경계·전환은 LevelCameraService가 소유한다. Brain 출력 뒤 최종 위치도 서비스가 경계로 제한한다. HMS 카메라 Controller는 기존 독립 네 rig용이다.

현재 Core와 01_Stage_Prototype 하나를 additive 로드한다. retry는 같은 Core/Stage와 GameSessionRuntime을 유지한다. Title 복귀는 world/runtime/UI/Host/연출의 작업·빌린 연결을 해제하고 Stage 전체→Core 순서로 언로드한다. 다음 새 게임은 새 씬·한 판 범위를 생성한다. 현재 활성 Stage만 Actor/트리거/추격에 연결하며 준비 중인 비활성 Stage의 작업·접촉을 중단한다.

추격 무리의 작성 참조는 같은 스테이지의 StageTrigger가 소유한다. CoreComposition의 필수 추격 필드는 없으며 CoreWorldBinding은 활성 트리거에서 받은 무리만 중복 없이 초기화하고 연결 당시 목록으로 해제한다. 초기화 실패도 같은 목록으로 회수한다. 추격이 없는 Stage에 무리를 강제하지 않는다.

ActorRemoving은 빌린 카메라 목표/모드/월드/UI 참조를 파괴 전에 해제한다. Spawn은 HMS Start→ActorReady 연결→실제Brain/Side정렬 완료→입력/표시 순서다. 사망 retry는 같은Stage 초기재준비·소비조각의 실제활성/Collider/소비플래그 복구·저장 위치/조각/해금 마스크를 새 Normal Actor에 복원하며 완료 대화 기록은 유지한다. 초기 Stage는 SceneAsset, Checkpoint는 같은 씬 실제 StageTrigger 참조다. 생성 경로는 내부 실행 값이며 저장payload가 아니다.

Native Editor API로 소유 자산만 연결/저장/재로드한다. 기존 prefab 일괄 제거는 사용자 몫이다. _Art/공용/HMS원본자산/meta/Packages/ProjectSettings 수동수정과 보호prefab Apply 금지다. HMS 파일별 쓰기 범위는 security.md와 최신 작업 지시로 확인한다. 과거 V1.5 정리의 삭제22개는 당시 호출/GUID/Editor/build 소비 이관 기록이다. 현재 소스 수량 변화와 합산하거나 같은 삭제 집합으로 해석하지 않는다. HudIconGraphic은 하트·Q/E·눈물 6소비 전체의 실제 리소스 이관 전까지 유지한다.

빌드는 설치의 Bootstrap/Title/Core/01_Stage_Prototype 네 씬을 명시적으로 전달하며 Entry 목록도 같은 Stage 하나만 등록한다. Unity 자동 저장 변경은 원인/diff를 검토·분류하며 수동 보호 수정 권한을 확대하지 않는다. Git은 현재 작업의 직접 허용 범위만 수행한다. 비 Play 검증에서도 미저장 씬·원래 setup/active/startScene을 보호한다. 실제 동선·반복 retry·다음 판 플레이 확인은 사람이 수행하며 이전 Play 이력을 이번 결과로 승계하지 않는다.

현행 지침: Inspector 위치/대상은 실물 참조, 수치는 목적·단위가 명확한 값이다. 씬 월드 객체를 관리자 자식에 묶지 않는다. 에이전트 Play 금지는 철회됐으며 공식 Unity 도구로 승인된 검증을 실행한다. 기존 프리팹 삭제/재생성/Apply·자동 Builder 실행은 해당 작업의 명시 허용 범위를 따른다. 이전 실행 관찰 수는 당시 증거이고 이번 통과가 아니다.

최종 Escape 대화만 완료 장부와 별개로 새 회차를 허용한다. Trigger/선택 객체/Table/Code/현재 Stage·Actor 세대·Round·Session을 수락 전후와 종료 콜백에서 검증한다. 낡은 context는 자기 예약·구독만 정리하고 현재 판의 Ending을 열지 않는다. 사망/포획이 같은 프레임 Ending보다 우선한다.
