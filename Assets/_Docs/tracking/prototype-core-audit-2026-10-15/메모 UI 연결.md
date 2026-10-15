# 메모 UI 연결 명세 및 검증 — 2026-10-15

## 목표·참고·제약
사용자 후속 지시: Stage에 MemoUIController/Canvas를 배치하고 기존 MemoObject의 담당을 연결한다. 해당 연결 외 기존 객체는 보존한다. MemoObject/MemoUIController/MemoView/CoreWorldBinding 실제 소스와 공식 unity:ui-ugui 스킬을 확인했다. Play/Player는 실행하지 않는다.

## 현재 상태와 적용
작업 시작 시 씬은 이미 dirty=true였고 MemoUIRoot/MemoUIController 및 MemoObject.memoUI 연결이 존재했다. 기존 연결을 유지하고 비어 있던 MemoUIController.screenRoot/viewPrefab만 채웠다.

신규 독립 빈 루트 MemoCanvasRoot 아래 MemoCanvas 및 전체 화면 MemoMount를 만들었다. Canvas는 ScreenSpaceOverlay, 정렬100(HUD20 위, Options200/Fade300 아래), CanvasScaler는 1920x1080/ScaleWithScreenSize/Match0.5다. GraphicRaycaster를 포함한다. 기존 MemoUIRoot 계층은 변경하지 않았다.

MemoUIController.screenRoot → MemoMount, viewPrefab → Assets/_Prefabs/UI/Memo/MemoView.prefab. 기존 MemoObject.memoUI → MemoUIRoot 연결은 이미 올바르므로 재작성하지 않았다.

## 검증·완료 기준
기존 6,593개 객체/컴포넌트 직렬화 비교 결과 변경은 MemoUIController 1개뿐이며 변경 필드는 위 두 참조다. 새 객체/컴포넌트는9개다. 메모 원본 Memo_1001과 Sprite 연결을 확인했다. MemoMount 실제 크기1920x1080, prefab의 memoImage/scaleRoot 참조 유효. Editor 컴파일 실패 false. Play false.

원본 씬은 작업 전부터 미저장 상태여서 SaveScene/SaveAssets를 호출하지 않았다. 변경은 열린 Editor 씬에 적용됐으며 사용자가 저장해야 한다. 실제 F 열기/닫기·정지 및 통합 흐름은 미실행이다. 증거: output/Tech_SYM/scene-audit/memo-ui-2026-10-15/before.json 및 verification.txt.
