# 열린 문제와 검증 제한

2026-10-08. 아래는 현재 제한과 과거 발견의 처리 상태다. 과거 발견·재현·종결 전체 원문은 [보관본](archive/2026-10-05-before-guidance-refresh/findings.md)에 있다. 보관본은 내용과 판정을 유지하고 이동에 따른 상대 링크만 보정했다.

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


| F13 | 실제 animation13항목 미연결, 전체 수동 두 판·빌드UI/UX·FunQA 대기 | 자동36시나리오584관찰·새빌드·headless기동 통과로 수동/실제clip 판정을 바꾸지 않음. 12clip+controller1 제공 뒤 실제 재생 검증 필요 |
| F14 | 이전 빌드 경고38건/수정 후 새 빌드57건과 첫 빌드의 자동InputActions preload 보존 실패 | 첫 엔진빌드 성공/명령실패 원문 보존. 정확한 자동1개 추가를 분류하고 재빌드는 추가변경0·명령성공. 최신57건은 Pipeline1/Demigiant52/기존 호환 필드CS0414 4이며 상세는 T10-build-warning-classification-after-camera.json이다. postprocess 제거가 디스크에 반영되지 않은 이유는 미확정. 보호 플러그인/패키지/설정 수동 정리는 범위 밖 |

## 종결된 항목

F12의 필요한 HMS9개 공개 확장은 이번 한정 직접 허용으로 구현했고 `5d1770db`와 이후 exactT1 blob 감사로 보존을 확인했다. 새 조립·실제 카메라/입력/사망 검증을 통과했다. 추가13개 HMS 내부 정리 후보는 허용하지 않았으며 이를 전체 보호원본 수정 권한으로 승계하지 않는다.

이번 씬 유지 초기화에서 발생한 옵션 사망 대기, 초기Quarter 준비 대기, 플랫폼 Transform/Rigidbody 잔존 위치, 다음판 Stage 활성화 순서, Stop 중 Core 해제 순서와 Dispose 뒤 Flow 게시 문제는 수정 전 실패와 수정 후 소스/실제 회귀를 보존했다. 최종 Play/Stop9경로·반복 사망·다음판 검사에서 새 오류가 없다. 실제 실행 방법과 범위는 최신 시스템 검증 기록을 따른다.

EntrySettings 배열 이상 의심은 소유 자산 native 재조회와 실제 Stage2/3 직접 Play의 이전 통과로 종결했다. 이 항목 때문에 종전11개 진입 검증을 새 요구로 재개하지 않는다. 세부 과거 증거는 보관본에 유지한다.

이번에 재현한 DialogueViewModel 취소 관찰자 예외 후 구독 잔존은 finally 정리로 수정했다. 같은 예외에서 잔존 true→false, 대화 회귀14개 통과를 확인했으며 다른 소비자·예외 전달·폐기 후 명령 차단을 유지했다. [이번 검증](2026-10-05-guidance-refresh-검증.md)을 따른다.

F02는 소유 MultiFunc prefab 및 Brokenable/MultiFunc/InGame의 직계 NormalVisual/SmallVisual 조립으로 해결했다. visualRoot non-null만으로 정상으로 판정하던 검증을 보완했고 실제 독립3씬 Q/Q/F·방향 입력 및 native 폼 hierarchy 오류0을 확인했다. 보호 PlayerFormController 코드는 변경하지 않았다. [현재 검증](ready-editor-entry-hms-2026-10-06/테스트 및 검증 항목.md)을 따른다.

F11은 이번 실제 진입에서 확인한 Bootstrap/Actor 출력2·Listener2 문제다. AppComposition이 자기 대기 Camera·Listener를 GameRuntimeService의 세션 출력 준비 통지에 연결하고 Actor 제거 전에 이전 출력을 끄도록 수정했다. 최종 실제 기능7묶음 및9씬 진입에서 제목·오프닝·플레이·retry·엔딩·다음 판의 출력/Listener1, 종료 후 원래 씬·설정 복원을 확인하여 종결했다. 최초 실패는 editor-entry-checks-attempt1.json에 보존하고 새 결과는 functional-checks.json 및 editor-entry-checks.json에 기록한다. [이번 검증](ready-platform-hms-cleanup-2026-10-06/테스트 및 검증 항목.md)을 따른다.

## F15 — 원본 중첩 카메라 구간 반복 요청: 구현·자동 실행 종결

원본 CorridorB/QuarterB 겹침에서 기존 Stay가 2초 동안 전환206회를 재요청해 전환을 취소하고 입력을 반복 차단한 product RED를 확인했다. SYM CameraZoneTrigger 한 파일에서 Collider 접촉 집합과 체류당 한 번 제출로 보정했다. 같은 참조 Bind는 접촉을 유지하고 바뀐 Bind/Retry/Disable/Destroy는 자기 접촉을 해제한다. 기존 부모 Facade 판정·직렬화 설정·Gizmo는 보존하고 동거 PlayerDebris와 무효 센서를 제외한다. 실제 원본 PhysX/InputSystem의 4관찰과 서로 독립된 실제소스 대역31/29관찰을 통과했다. old-loaded-assembly RED와 잘못된 helper 구간ID·낙백 뒤 Z 미추종 경로 실패는 원본 증거를 별도로 보존하고 실제 제품 RED와 구분한다.

카메라 접촉 수정 뒤 실제 Editor36시나리오584관찰·6통합/3직접 Play와Stop을 다시 통과했다. 원본 동선 두 판은 실제 방향키/Space/Q/E/Enter/Esc와 UGUI 이벤트로 Title→Opening→Play→각 낙사·적 접촉 사망→재접촉 해금→END→POPO→Ending→Title까지 완료했다. 자연 사망4회와 143관찰 모두 통과했으며 Core/3Stage·출력·수집물/적 identity는 유지됐다. 전체 동선 검사는 teleport·복제 fixture·직접 ForceDeath/Flow 요청·능력/조각 상태 쓰기를 사용하지 않았다. Editor 합성 입력이며 Windows 실행 파일의 수동 UI/UX·QA39케이스·FunQA와 실제 미연결13 animation 재생은 별도 대기다.

증거: T10-camera-overlap-product-RED.json → T10-camera-overlap-actual-regression.json, T10-whole-input-flow.json, final-review/camera-overlap-patch-review.json. 이 종결은 실제 animation13연결과 팀 QA/FunQA 수락을 대신하지 않는다.
