# 열린 문제와 검증 제한

2026-10-06. 아래는 최신 제한 목록이다. 과거 발견·재현·종결 전체 원문은 [보관본](archive/2026-10-05-before-guidance-refresh/findings.md)에 있다. 보관본은 내용과 판정을 유지하고 이동에 따른 상대 링크만 보정했다.

| ID | 상태·영향 | 처리 경계·근거 |
|---|---|---|
| F01 | HMS Tooltip 12파일62필드 보완 미승인 | [보호 영역 초안](2026-10-05-inspector-ux-보호영역.md). 이번 전체 정비 요청을 타인 파일 허가로 확대하지 않음 |
| F03 | TimeManager/GraphicsSettings native 정규화로 raw baseline 불변 미충족 | 과거 제한 승인 유지. 의미 동일 근거와 raw차이를 구분하며 ProjectSettings 무단 복원 금지 |
| F04 | ProBuilder 추가2키 저장 주체 미확정 | 기존 외부키 및 추가키 보존. [T9 최종 검증](2026-10-05-T9-최종검증.md) 참조 |
| F05 | 전체 diffcheck의 Unity m_Name 후행공백14곳 | C#/Markdown 결과와 분리. 기능과 무관한 자산 재저장 금지 |
| F06 | 강제 컴파일/설치 자산 fault주입 미실행; 실제 모델·클립·애니메이션 블렌딩·착지 타이밍과 red픽셀품질 제외 | 앞선 정상 Reload·Cinemachine 결과는 당시 증거다. 플랫폼 후속 작업은 Controller와7배치 Animator를 준비했으며 실제 붕괴 클립은 자산 제공 후 연결한다. 연결 전 물리 붕괴 성공을 실제 클립 재생 성공으로 기록하지 않는다. 자산·품질 검증은 사용자 지정 후속 범위다. |
| F07 | 과거 사용자가 직접 씬 전환하기 전 미저장 메모리의 보존 여부 확인불가 | 이번 사용자가 승인한 Prototype 마지막 저장 상태 reload는 별도 증거다. 이번 복원으로 과거 메모리를 복구했다고 주장하지 않음 |
| F08 | HMS PlayerFormController의 레거시 nearby-recovery 설명이 Q 거리 회수로 남음 | 현재 통합 Q는 공간 검사 후 RestoreForm 경로로 거리와 무관하게 회수한다. 레거시 설명을 현재 규칙으로 사용하지 않으며 별도 보호 파일 설명 정비는 허가 범위 확인 필요 |
| F09 | 보호 HMS 기준 씬의 PlatformManager Missing Script1 | native16씬·65프리팹 재검사에서 확인. 파일 `Assets/_Scenes/Tech_HMS/01_PlayerMovementTest.unity`의 해당 객체 원본 수정은 권한 밖이므로 HMS 소유자가 연결된 Script GUID/삭제 이력을 확인하고 복구해야 한다. SYM 소유 Missing은0이며 실제 독립3씬·통합 입력 검증과 분리한다. |
| F10 | 전체 Domain Reload 시 Unity Hierarchy UI Assertion4 | Unity6000.6.0f1에서 startScene/Reload matrix 진행 중 manager!=NULL, Unity.Hierarchy.HierarchyViewModel.SetState stack으로 재현. Cooked/HMS stack 없음. Hook teardown 예외는 수정 후0, matrix 기능은 통과했으나 엔진 창의 전체 오류0으로 주장하지 않는다. 엔진/패키지 변경은 이번 권한 밖이므로 Editor 소유자가 동일 버전·Hierarchy 상태의 엔진 재현을 확인해야 한다. 증거 `output/Tech_SYM/verification/editor-entry-hms-2026-10-06/matrix-console-after-fix.json`. |

## 종결된 항목

EntrySettings 배열 이상 의심은 소유 자산 native 재조회와 실제 Stage2/3 직접 Play의 이전 통과로 종결했다. 이 항목 때문에 종전11개 진입 검증을 새 요구로 재개하지 않는다. 세부 과거 증거는 보관본에 유지한다.

이번에 재현한 DialogueViewModel 취소 관찰자 예외 후 구독 잔존은 finally 정리로 수정했다. 같은 예외에서 잔존 true→false, 대화 회귀14개 통과를 확인했으며 다른 소비자·예외 전달·폐기 후 명령 차단을 유지했다. [이번 검증](2026-10-05-guidance-refresh-검증.md)을 따른다.

F02는 소유 MultiFunc prefab 및 Brokenable/MultiFunc/InGame의 직계 NormalVisual/SmallVisual 조립으로 해결했다. visualRoot non-null만으로 정상으로 판정하던 검증을 보완했고 실제 독립3씬 Q/Q/F·방향 입력 및 native 폼 hierarchy 오류0을 확인했다. 보호 PlayerFormController 코드는 변경하지 않았다. [현재 검증](ready-editor-entry-hms-2026-10-06/테스트 및 검증 항목.md)을 따른다.

F11은 이번 실제 진입에서 확인한 Bootstrap/Actor 출력2·Listener2 문제다. AppComposition이 자기 대기 Camera·Listener를 GameRuntimeService의 세션 출력 준비 통지에 연결하고 Actor 제거 전에 이전 출력을 끄도록 수정했다. 최종 실제 기능7묶음 및9씬 진입에서 제목·오프닝·플레이·retry·엔딩·다음 판의 출력/Listener1, 종료 후 원래 씬·설정 복원을 확인하여 종결했다. 최초 실패는 editor-entry-checks-attempt1.json에 보존하고 새 결과는 functional-checks.json 및 editor-entry-checks.json에 기록한다. [이번 검증](ready-platform-hms-cleanup-2026-10-06/테스트 및 검증 항목.md)을 따른다.
