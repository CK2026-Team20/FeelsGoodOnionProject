# 열린 문제와 검증 제한

## V1.6 준비에서 확인한 현재 차이

- Prototype에 서랍·적·발판의 외부 기준점16개가 남았다. 새 명세는 완성 기능 객체 내부로 이관한다. 현재는 발견/계획 단계다.
- 현재 카메라는 Side/Quarter 두 구도이며 V1.6의 통로 XZ 조작·세 구도·전환 중 중복 거절과 차이가 있다. 세 시점과 두 DOMove 통로 차단은 새 실행 대상이다.
- V1.6 3.2의 전신 통과/경로 거리 포획은 기존 접촉·직선·실제 몸체 포획을 유지하는 직접 답변과 다르므로 원본 수정 요청으로 남긴다. 원본 DOCX는 편집하지 않는다.
- RealToon 가림 구현은 이번 제외, 실제 하트/애니메이션 자원과 사람 플레이는 별도다. HudIconGraphic은 하트 연결까지 유지한다.
- 아래 ‘현재 V1.5’와 외부Marker 필수 설명은 과거 구현 상태이며 V1.6 목표나 이번 완료 판정이 아니다. 최신 [명세](ready-system-v16-2026-10-11/spec.md)와 [검증 항목](ready-system-v16-2026-10-11/테스트%20및%20검증%20항목.md)을 따른다.


## 2026-10-11 구조 정리에서 확인한 잔여 사항

- HMS 생산36개 감사에서 통합 PrototypePlayerAnimations 자산의 템플릿/클립, 공용 PlayerAnimations의 실제 클립 미연결을 확인했다. 이동 정상과 실제 애니메이션 출력 완료는 별개다. 사람의 리소스 제공/연결 및 보호 원본 경계를 지키므로 이번에 임의 생성하지 않는다.
- 2026-10-11 HMS 기본 입력 이관: PlayerController를 조립하고 GameplayInputRouter를 삭제했다. Host는 F/사망 진행, Bridge는 모달/해금/공통 폼 쿨다운/피드백을 연결한다. HMS 카메라 필드 null은 런타임 Bind 및 독립 모드 용도와 구분한다.
- Editor 첫 import/씬 저장 중 URP GlobalSettings.m_RuntimeSettings가 빈 목록으로 자동 저장됐다. 수동 변경은 없으며 기존 자동 변경 보존 지침에 따라 diff를 보존했다. 실제 빌드 렌더링을 이번 작업에서 평가하지 않았다.
- 직선 추격의 실제 포획/일시정지/재시도/포포 경합은 Play 금지에 따라 미실행이다. 전체 Unity 컴파일·native 배치/참조 검사는 통과했다. 상세는 [검증 기록](structure-simplification-2026-10-11/테스트%20및%20검증%20항목.md).


## 현재 V1.5의 남은 조건

| 항목 | 실제 영향 | 담당/종료 조건 |
|---|---|---|
| 실제 리소스/HudIconGraphic | 구형Graphic은Native12개/rawGUID6곳의하트·Q/E·조각소비에남음 | 사람이실제Sprite/Image와새표시를연결하고모든저장소비를이관한뒤HudViewfallback/Source/meta제거·재검증.현재전체23삭제완료아님 |
| 새prefab의Scene지점 | 순찰4template의시작/끝8개,Linear2개,OvenTray2개는외부배치필수 | 새prefab을Scene에놓은instance에서실제Marker드래그.Stationary2template의4순찰빈슬롯은미사용.현재배치Stage의필수누락과구별 |
| 실제Play/리소스표시 | 자연물리callback·접지/착지·실제clip/VFX·전체한판/다음판·빌드화면품질미평가 | 사람이검증기록의기능목록으로Play.에이전트NoPlay를우회하지않음.요청없이QA/FunQA작성하지않음 |
| 보호HMSInspector | 이번전체한국어라벨정비는SYM소유필드에적용.HMS나머지영문필드원본은보호범위 | 해당소유자가별도명시범위로정비.현재제작가이드에는실제HMS필드명/연결을명시.이번3최소파일허가를전면정비로확대하지않음 |

현재 구현·최종 비 Play assertion·빌드·독립 검토는 [status](status.md)와 [검증 기록](ready-system-v15-2026-10-10/테스트%20및%20검증%20항목.md)을 따른다. 미실행과 실제 실패를 섞어 통과로 표시하지 않는다.

## 현재 실제 비 Play 검증 경계

Native elapsed Tween 실패는 설치 DLL initialized=false/Kill 조기 반환, selection 교체1fixture 실패는 inline 객체 전제가 충족되지 않은 결과로 원문을 보존한다. 두 결과를 미실행이나 통과로 바꾸지 않는다. 실제 Owner/callback8항목·다른UI4사례, 공개PlayerDLL59와 실제기하13통과는 별도다. Native Completed 정상 update 시계·자연contact/Grounded/Landed·실제소유Shell accepted반환·DialogueView Native입력/재Bind·실제리소스표시는 미평가다. 구체적 방법·Source/DLL·원문 경로는 [검증 기록](ready-system-v15-2026-10-10/테스트%20및%20검증%20항목.md)에 있다. 실제Core/Stage 바인딩에서 발견한Floor참조4건은수리·73자산sweep/5Bind/보존검사로해소했다.

## 아래는 과거 발견과 당시 제한의 보존 기록

아래 수치·경로·‘이번/현재’ 표현은 해당 과거 작업의 시점이다. 이전 성공/실패를 V1.5의 새 실행 결과로 주장하지 않는다. 보호 원본의 당시 Missing/Tooltip 등은 이번에 직접 고칠 권한이 없으며 현재 재평가하지 않은 수치를 현행 전수로 일반화하지 않는다.


2026-10-10. V1.5 실행 상태는 status.md를 따른다. 아래는 과거 발견의 증거와 아직 남은 보호/리소스 제한이다. 과거 성공은 이번 성공으로 승계하지 않는다. 과거 발견·재현·종결 전체 원문은 [보관본](archive/2026-10-05-before-guidance-refresh/findings.md)에 있다. 보관본은 내용과 판정을 유지하고 이동에 따른 상대 링크만 보정했다.

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

## V1.5 실행 대기 조건

HudIconGraphic은 실제 하트·Q/E·조각 Sprite/Image 리소스로 현재12소비를 모두 이관하고 사람의 HUD 조립이 끝난 뒤 삭제한다. 연결 전 기존 표시를 유지한다. 실제 clip 재생·전체 수동 플레이는 사람이 확인하며 에이전트는 Play를 실행하지 않는다. 이번 승인 HMS3파일은 Playback 우선순위/설명, Controller 설명, CharacterMovement readonly 접지각 getter/설명만이다. HMS Tooltip 전수 보완·보호 씬 Missing Script를 이 최소 허가로 수정하지 않는다.
