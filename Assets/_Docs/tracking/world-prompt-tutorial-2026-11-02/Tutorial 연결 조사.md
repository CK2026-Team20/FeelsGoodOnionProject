# Tutorial 진입 누락 및 사용자 연결 가이드

2026-11-02, 현재 열린 Assets/_Scenes/01_Stage_Tutorial.unity를 읽기 전용으로 조사했다. 아래 연결은 수행하지 않았다.

## 실제 확인한 실패

- ArchitectureInstallation의 SceneAsset GUID는 현재 Tutorial로 정상 해석된다. 파일에 남은 Stage_tuto 경로 캐시는 오래됐지만 Editor CreateCatalog는 실제 SceneAsset 경로를 다시 읽는다. 따라서 GUID 단절이 직접 원인은 아니다.
- IntegrationEntrySettings.entries에는 01_Stage_Prototype만 남아 있다. Installation 복제본의 CreateCatalog 결과에 EntrySettings 복제본의 ValidateCatalog를 호출하면 '등록되지 않은 Stage: 01_Stage_Prototype'이 발생한다. GameSessionRuntime 생성자는 ValidateCatalog를 호출하므로 정상 세션 진입을 차단한다.
- Tutorial StageService.ValidateSections 호출은 '구간 CP_A의 서로 다른 시작·끝과 음악을 연결하세요'로 실패한다. CP_A/B/C 모두 start/end/music=None이며 firstSectionStart=None이다.
- 세 구간 correctionSeconds/transitionSeconds/blockerSeconds가 모두 0이다. 현재 검증은 차단체 미사용이어도 세 시간 모두 0.01초 이상을 요구한다. musicGain=0은 유효하지만 무음이다. musicTransitionSeconds=0은 유효하다.
- StageTrigger 11개 모두 소속 스테이지=None이다. 여섯 체크포인트의 활성 BoxCollider와 Is Trigger, 종류 Checkpoint는 정상이다.
- 직접 씬 테스트 선택은 False, DirectSceneTestSettings는 0개이다. 별도 직접 씬 테스트 경로를 선택할 경우 설정 하나와 시작점이 없으면 DirectSceneTestEntry가 진입을 거절한다. 일반 Bootstrap 진입과 구분한다.

## 직접 연결할 순서

1. ArchitectureInstallation의 스테이지 씬에 현재 01_Stage_Tutorial SceneAsset이 연결됐는지 확인한다. 현재 GUID는 정상이다. Inspector 검증/저장 및 공식 빌드 경로로 캐시도 갱신한다. YAML 경로를 수기로 바꿀 필요는 없다.
2. IntegrationEntrySettings의 스테이지 진입 설정을 같은 Tutorial 씬으로 맞춘다. Tutorial만 등록할 계획이면 기존 Prototype 항목을 Tutorial로 교체한다. 둘 다 사용할 계획이면 Installation과 EntrySettings에 둘 다 일치하게 등록한다. 초기 조각/해금은 의도한 시작 상태로 작성한다.
3. Tutorial의 StageService > 구간을 연결한다. 아래 이름의 최상위 빈 Root가 아니라 그 안 Visuals/Trigger의 StageTrigger 컴포넌트를 드래그한다.

|구간|시작|끝|
|---|---|---|
|CP_A|===Level===/Root_LEVEL/CheckPoint/CP_A_S/Visuals/Trigger|===Level===/Root_LEVEL/CheckPoint/CP_A_E/Visuals/Trigger|
|CP_B|===Level===/Root_LEVEL/CheckPoint/CP_B_S/Visuals/Trigger|===Level===/Root_LEVEL/CheckPoint/CP_B_E/Visuals/Trigger|
|CP_C|===Level===/Root_LEVEL/CheckPoint/CP_C_S/Visuals/Trigger|===Level===/Root_LEVEL/CheckPoint/CP_C_E/Visuals/Trigger|

4. 첫 구간 시작에는 실제 첫 구간의 시작 트리거를 연결한다. 이름대로 A부터 시작할 의도라면 CP_A_S/Visuals/Trigger이다. 구간 순서를 이름만으로 자동 확정하지 않았다.
5. 각 구간에 실제 AudioClip, 의도한 음량/시점을 연결하고 진입 보정 시간·시점 전환 시간·차단 이동 시간을 각각 0.01초 이상으로 작성한다. 코드 초기 기본은 0.6초이나 설계값은 사용자가 정한다. 차단 몸체·목표는 둘 다 비어 있어도 된다. 사용할 때만 함께 연결한다.
6. CheckPoint의 여섯 트리거와 Trigger 아래 T_Form, T_Tear, T_Dial, T_POPO, T_Dead_Z의 Visuals/Trigger 모두 '소속 스테이지'에 같은 씬 StageService를 연결한다. 체크포인트 위치는 Trigger Transform 자체가 등장 Capsule 중심 기준이므로 의도한 위치인지 확인한다.
7. T_Dial의 EXPLORE_OVEN_01, T_POPO의 POPO 연결은 값이 존재한다. Tutorial에 맞는 대사인지 별도로 검토한다. 코드가 비어 있는 해금 안내는 선택 사항이다. 추격 시작 트리거가 없으므로 추격 필드를 일괄 필수로 채우지 않는다.
8. 일반 진입은 Bootstrap/Title→Core+Tutorial 조립으로 검증한다. Tutorial에 플레이어/앱 카메라가 없다는 이유만으로 복제해 넣지 않는다.
9. '현재 씬 직접 테스트'를 별도로 쓸 경우 DirectSceneTestSettings 하나 및 실제 시작 Transform을 작성하고 해당 테스트 모드를 선택한다. 이 모드의 성공이 본편 세션 연결 성공을 뜻하지 않는다.
10. StageService 구간 검증, Installation/EntrySettings 일치 검증 후 실제 진입·체크포인트·메모·해금·대화·데드존을 확인한다. 빌드는 프로젝트의 IntegrationBuildPipeline이 읽는 Installation 명시적 씬 목록을 기준으로 한다.

## 근거와 검증 한계

- ArchitectureInstallation.CreateCatalog/RefreshAuthoring: Editor에서 SceneReference로 경로 재해석.
- IntegrationEntrySettings.ValidateCatalog 및 GameSessionRuntime 생성자: 등록 스테이지 일치 강제.
- StageService.Section.Validate, ValidateSections, FirstSection: 시작/끝/음악/소속/시간/첫 구간 검증.
- StageTrigger.CheckpointPose: 같은 씬 소속과 Trigger BoxCollider 및 고정 기준점 필요.
- DirectSceneTestEntry.Enter: 설정 한 개와 StartPoint 필요.
- 원본 Tutorial/Installation/EntrySettings를 수정하지 않았다. Play 또는 Player를 실행하지 않았으며 전체 레벨의 런타임 성공을 보증하지 않는다.
