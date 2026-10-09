# V1.5 구현 인계 — 승인 대기

현재 작업은 DryForge READY다. 최신 V1.5·직접 답변과 현재 소스를 조사하여 실행 명세/계획/수동 제작 계약을 작성했다. 제품 구현·source 삭제·씬/prefab 변경·Unity실행·commit/push/PR은 아직 수행하지 않았다. 이 인계를 승인한 뒤 같은 대화에서 `dryforge:go`로 실행한다.

## 문서 역할과 위치

아래 위치는 프로젝트 루트 기준이며 portable이다. 실행 정본은 `.dryforge/`3문서, 사람이 검토할 동일 바이트 사본과 상세 설명은 `output/Tech_SYM/Planning/ready-system-v15-2026-10-10/`다. 이전3문서는 그 폴더의 `archive-before-ready/`에 보존했다.

| 문서 | 위치 | 역할 |
|---|---|---|
| handoff | `.dryforge/handoff.md` | 권한·하드게이트·읽기 순서·실행 범위 |
| spec | `.dryforge/spec.md` | 만들 동작/데이터/불변식·검증의 기준 |
| plan | `.dryforge/plan.md` | 작업 대상/순서/공유작성/gate·Execution Graph |
| 현재 차이 | `output/Tech_SYM/Planning/ready-system-v15-2026-10-10/현재 구조와 V1.5 차이.md` | 현재 소스 근거와 V1.5 차이. 목표를 완료로 가장하지 않음 |
| 제작 guide | `output/Tech_SYM/Planning/ready-system-v15-2026-10-10/씬 흐름과 수동 프리팹 제작 가이드.md` | 사용자에게 먼저 보여줄 초보 제작 순서·컴포넌트·연결·물리 예외 |
| Inspector | `output/Tech_SYM/Planning/ready-system-v15-2026-10-10/Inspector 연결 전환 명세.md` | 현재201개 작성 필드와 새 참조/수치 분류 |
| source 판단 | `output/Tech_SYM/Planning/ready-system-v15-2026-10-10/스크립트 필요성 및 삭제 계획.md` | 174source/23삭제후보의 필요성·책임이관·현재GUID/소비 |
| HMS 기록 | `output/Tech_SYM/Planning/ready-system-v15-2026-10-10/이전 Tech_HMS 변경 기록.md` | 이전2commit10고유source의 추가·삭제·변경 이유와 이번 최소 허용 |
| 검수 | `output/Tech_SYM/Planning/ready-system-v15-2026-10-10/코드 리뷰.md`, `테스트 및 검증 항목.md` | READY 결과와 후속 구현·사람 검증을 구별 |

행동 충돌은 spec이 plan/설명문서보다 우선한다. 계획 변경으로 spec 동작/권한/데이터를 조용히 바꾸지 않는다. 최신 직접 수정이 들어오면 해당 의도와 영향을 문서에 반영한다. 현재 `Assets/_Docs`와 지역 지침에는 V1.1 전용 규칙이 남아 있으므로 T01에서 일치시키기 전에는 이번 spec과 최신 직접 지시를 우선한다. 과거 `.dryforge`나 dated 실행 기록을 현재 목표로 다시 읽지 않는다.

## 작업의 큰 모양

현재는 **한 응집형 Prototype Stage에서 게임 진행1회**를 보여준다. Stage 1→2→3 자동 연결과 본편 레벨 제작은 이번 범위 밖이다. 이후 본편 연결에서도 각 씬 독립 진입을 유지한다. 등록된 다른 Stage를 곧 삭제하거나 새게임이 자동으로 방문한다고 일반화하지 않는다.

처음에는 지침·참조 authoring와 snapshot 원본을 정리한다. 기능별 수정 후 한 작성자가 App/Core/Stage/Actor 연결을 이관한다. 그 뒤 관리자 하위 월드 배치와 자기 자산 참조를 실제로 바꾸고, 참조가 사라진 source를 삭제한다. 비 Play 검증과 최종 제작 guide를 전달한다. 실제 하트Sprite가 제공된 뒤 HudIconGraphic과 임시구형HUD소비를 제거하는 마지막 외부자료 조건이 있다.

## 반드시 지킬 실행 경계

1. **에이전트 Play 금지.** Editor 버튼/PlayMode API·CLI·pipeline/batchPlayMode/직접플레이를 실행하지 않는다. 사람이 플레이한다. 정적/EditMode/import/compile/build를 실제로 실행한 범위만 기록한다.
2. **사람 prefab 소유.** 기존 AI prefab 삭제·재생성·Apply·아트연결은 사람이 한다. 먼저 guide와 기존GUID 소비표를 보여준다. source 수정에 필요한 자기 소유 참조의 최소 migration은 가능하나 보호 원본·중첩 원본을 건드리지 않는다. 무해소참조는삭제대기다.
3. **명시된 HMS2파일만 예외.** `Assets/_Code/Core/Tech_HMS/Features/PlayerAnimationPlayback.cs`의 Interact2→4와 관련 설명, `PlayerAnimationController.cs`의 관련 설명만 허용한다. 다른 HMS·타 작업자·공용·아트·보호 meta/import/Packages/ProjectSettings는 수동 수정하지 않는다. 이전 HMS9파일 허가는 재사용하지 않는다.
4. **씬과 개별 prefab을 구별.** 월드 오브젝트를 manager 자식으로 배치하지 않는다. 개별 Actor/Shell/Platform의 실제 물리 동거 계약은 보존하고 예외를 설명한다. 계층만 펼치고 자식 탐색·해제·재준비를 방치하지 않는다.
5. **저장 항목3개만.** CP는 유한 Position/0..5 Fragments/Form·Tear 마스크다. WorldSave/StageID/CheckpointID/HP/폼/시간을 저장하지 않는다. 대화 완료는 한 판 런타임 장부이며 능력 rollback으로 지우지 않는다.
6. **상태와 작성자는 하나.** HP/폼/운동은 HMS, CP/해금은 Session, 기믹은 자기 소유자, 화면은 VM 상태·명령을 사용한다. 빌린 Actor를 Dispose하거나 모든 listener를 RemoveAll하지 않는다. Render만 움직여 충돌체를 남기지 않는다.
7. **미저장 작업·Git 보호.** READY는 Git을 변경하지 않는다. 실행 시에도 검토 범위와 기존 승인 경계를 지킨다. 자동 저장 diff는 원인·전후·소유를 검토한다. 미저장 Editor를 강제 종료하거나 검증 때문에 씬을 덮어쓰지 않는다.
8. **완료의 의미를 구별.** READY3문서 검수, 실제 source 삭제, 비 Play compile/build, 실제 clip 재생·수동 전체 플레이는 서로 다른 상태다. QA/FunQA 평가 문서를 임의 작성하지 않는다. 하트 리소스 대기나 컴파일 미실행을 통과로 쓰지 않는다.

## 구현 방법을 선택한 이유

사용자가 배치한 지점을 그대로 drag/drop해야 하므로 경로·도착 위치를 작성좌표에서 Transform 참조로 옮긴다. 실행 중 저장 Vector3와 계산 결과는 필요한 값이므로 유지한다. 씬 관리자 부모 배치는 사용자 제작 방식과 맞지 않으므로 씬 소속 조회와 실제 참조로 옮긴다.

App/Core 조립자는 실제 생성·해제/재연결 수명이 있어서 유지하고, 같은 수명에서 Bind만 중복하는 UIroot/표시객체·상태전달을 흡수한다. Locator를 구체 서비스 생성/씬 배치까지 아는 거대 객체로 바꾸지 않는다. 자동 prefab 제작기를 없애는 이유는 사람이 리소스 병합과 제작을 맡기 때문이다.

깊은 끼임은 몸체 위 이동+Dead, 껍질은 분리만 수행하고 데드존에서는 같은 소유 껍질을 현재 Player로 반환한다. 껍질 회수/Q 성장과 같은 뜻으로 사용하지 않는다. Root/Render/Collider 제작 예시보다 실제 HMS 물리 동거·Remove 수명 계약을 우선하여 제작 예외를 명시한다.

## 증거와 한계

`evidence/source-v15.json`에는 문단 색/취소선/본문, `reference-inventory.json`에는174개 source의 보수적 코드 언급/GUID, `inspector-inventory.json`에는 자동 추출 필드, `scene-hierarchy-inventory.json`에는12개 소유 씬의 배치가 있다. 원본은 `sources/Cooked_시스템_기획서_V.1.5.docx`, 실제 Git patch는 `evidence/hms-6f2a386.patch`/`hms-5d1770d.patch`다. 이 경로들의 접두사는 상세 문서 폴더다.

정규식/텍스트 GUID 검색은 정확한 compiler 호출 그래프나 바이너리·외부 소비까지 증명하지 않는다. 실행 시 실제 SerializedObject/import/compile로 대조한다.128개 SYM 중 삭제 후보는23개(22개 선행 이관 후,1개 실제 리소스 후)다. 필요한 편집 어댑터가 추가될 수 있으므로 최종 파일 수를 성과로 고정하지 않는다.

이번은 이미 harness가 있는 delta 작업이다. 프로젝트 기반 설계를 다시 설정하거나 처음부터 Foundation을 요구하지 않는다. 기존 프로젝트 관례와 이번 상위 명세를 따르고 아래 승인 게이트 뒤 실행한다.

## 실행 전 사용자 확인

세 문서와 제작 guide를 검토한 뒤 이 계획대로 진행한다고 확인한다. 승인 뒤 같은 대화에서 `dryforge:go`를 호출한다. HMS 두 파일의 제한 허용과 깊은끼임·껍질 규칙·현재 프로토타입 범위는 이미 직접 결정됐으므로 다시 묻지 않는다. 새로운 미결 제품 선택은 없다. 리소스 제공/사람 Play가 아직 없는 경우 해당 작업 상태를 남겨 둔다.
