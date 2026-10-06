# 실행 및 검증 절차

## 준비

Windows에서 Unity 6000.6.0f1과 Windows 빌드 지원을 설치하고 저장소를 준비한다. 프로젝트 경로는 아래 예시를 실제 체크아웃으로 바꾼다. 패키지는 Packages/manifest.json과 packages-lock.json에 맞춰 Unity가 복원한다. 문서 확인만을 위해 업그레이드하거나 자산 생성 메뉴를 실행하지 않는다.

```powershell
Set-Location 'F:\CK2026_Project\Project\FeelsGoodOnionProject'
git status --short
Get-Content ProjectSettings/ProjectVersion.txt
Get-Content Packages/manifest.json
git lfs version
git lfs pull
```

LFS 복원은 Git LFS 설치와 해당 원격 저장소 읽기 권한이 필요하다. 오류가 나면 원인을 해결한 뒤 Unity를 연다. 포인터 파일을 모델·이미지 원본으로 사용하지 않는다.

Unity Hub에서 위 프로젝트를 지정한 버전으로 열고 import·컴파일이 끝날 때까지 기다린다. 처음부터 Builder를 실행하면 저장된 자산을 다시 만들 수 있으므로 기존 씬·프리팹을 우선 사용한다. 문서 검토 작업에서 이 절차를 실행했다고 간주하지 않는다.

## 플레이

Assets/_Scenes/Tech_SYM/Architecture/01_Bootstrapper.unity를 열고 Play한다. 제목 → 새 게임 → 오프닝 → Stage 1 진입을 확인한다. Stage 씬에서 직접 Play하는 경우 Editor 진입 라우팅이 적용되며 오프닝이 생략된다. 저장되지 않은 타인 씬을 자동 저장하지 않는다.

Architecture의 Bootstrapper·Title·Core·Prototype/1_Stage·Stages/03_2_Stage·Stages/03_3_Stage 여섯 경로에만 Bootstrap 시작을 적용한다. 선택한 Stage2/3는 해당 Stage로 도착한다. 그 밖의 저장 씬은 현재 활성 씬에서 직접 시작한다. 역사적 Stages/03_1_Stage는 플레이어를 포함하지 않는 level-only 씬이어서 독립 Play만으로 통합 세션을 만들지는 않는다. 프로그래머가 대상을 바꿀 때 BootstrapEntryRoute의 정확한 경로 목록·설치의 Stage Paths·StageId/직접 진입 설정·생성 코드의 Catalog·실제 진입 검증 목록을 함께 맞춘다.

ArchitectureInstallation.asset이 여섯 씬의 경로를, IntegrationEntrySettings.asset이 직접 진입 시 체크포인트·조각·해금을 제공한다. 각 Stage가 체크포인트의 실제 Pose를 제공한다. 이 세 설정을 혼동해 초기 좌표를 여러 곳에 중복 저장하지 않는다.

## 자동 기능 검증

검증 전에 해당 프로젝트의 live Editor 연결, 컴파일 완료, Edit Mode와 모든 loaded scene의 path/isDirty를 확인한다. dirty가 하나라도 있으면 먼저 현재 작업의 저장·보존 여부를 확인하고 Play/build를 보류한다. 원래 scene setup·active scene·playModeStartScene과 보호 파일 해시를 기록한다. 씬 연결 검사는 기존 SYM 씬·프리팹을 사용한다.

```powershell
unity status --project-path 'F:\CK2026_Project\Project\FeelsGoodOnionProject' --format json
unity pipeline list --format json
```

실제 Editor 조회가 성공한 뒤 필요한 검증을 실행한다. 2026-10-06 정리로 기존 테스트 전용 클래스와 Cooked/Verification 등의 시험 메뉴를 제거했다. 현재 배치 생성 도구와 Cooked/Integration/Build Windows Player는 제품 작업 도구로 유지한다. 과거 검증 메뉴 이름을 현재 실행 명령으로 사용하지 않는다.

일회성 C# 검증은 output/Tech_SYM/verification/<기능·날짜>/에 두고 Unity의 run_script로 메모리 컴파일해 기존 Play 환경에서 실행한다. 먼저 dry_run으로 컴파일 진단을 확인하고 Main 실행 결과의 완료 상태·관찰값·실패를 조회한다. 비동기 검사는 요청 수락과 완료를 구분하고 시간 제한·취소·finally 정리를 제공한다. 필요할 때만 작성·실행하며 테스트 소스를 Assets 안에 다시 남기지 않는다.

끝난 뒤 임시 입력·오브젝트·이벤트·정지 차단을 정리하고 Stop한다. 안정된 Edit Mode에서 원래 scene setup와 startScene 복원, clean 상태, 새 컴파일, Console와 보호 파일 diff를 확인한다. 자동 저장 변경은 원인·전후 diff를 기록하여 커밋에 포함한다. 일회성 스크립트는 제거하고 명령·실행 시각·결과 JSON·케이스·소스 해시·실패 원인은 보존한다.

현재 기능 케이스와 실행 결과는 tracking/ready-platform-hms-cleanup-2026-10-06/테스트 및 검증 항목.md에 기록한다. 제어된 서비스 요청·런타임 위치 이동 검사, 실제 InputSystem 전체 동선, Windows 빌드, 팀 실제 실행과 FunQA를 따로 판정한다. 이전 editor-entry-hms 및 T9 결과와 캡처는 당시 실행 이력이다.

## 팀 검증 인계

프로그래머·에이전트는 기능 테스트 엑셀과 Cooked! FunQA DOCX 한 개를 제공한다. 기능 케이스 작성·유지는 프로그래머 책임이며 QA는 제공된 원본 서식의 FunQA를 직접 작성한다. 현재 제공 파일과 문서 검증은 [QA 인계 명세](tracking/2026-10-05-functional-qa-spec.md)를 참조한다.

빌드 ID·소스/명세 버전·실행 파일 경로·해상도·입력 장치·처음부터 진입 또는 체크포인트 조건·알려진 제한을 확정해 전달한다. 현재 존재하는 과거 exe를 새 소스 빌드로 간주하지 않는다. 긴 진행 경로가 필요한 케이스에는 도달 순서와 대상명을 제공하고, 테스트용 진입 기능이 없으면 그 제약을 기록한다. 임의의 새 검증 씬이나 보호 자산 변경을 인계 문서 작성 과정에서 수행하지 않는다.

새 빌드 결과는 이전 엑셀의 실행 환경과 결과를 보존한 사본으로 기록한다. P/F/B/N은 실제 해당 빌드에서 실행한 판정이고 자동 검증 이력과 혼합하지 않는다. QA 재미 평가·전체 플레이 확인 전 상태는 구현/자동 검증 완료 및 팀 검증 대기로 표현한다.

## Windows 빌드

안정된 Edit Mode에서 Cooked/Integration/Build Windows Player를 실행한다. ArchitectureInstallation의 여섯 씬을 명시적으로 빌드한다. ProjectSettings/EditorBuildSettings.asset의 일반 목록을 수정할 필요가 없다.

결과 경로는 output/Tech_SYM/integration/build/Windows/CookedPrototype.exe이고 같은 폴더에 build-evidence.json이 남는다. result가 Succeeded인지, errors가 0인지 확인한다. changedProjectSettings는 빌드 전후 실제 해시 차이이며 자동 변경이 있으면 원인·diff를 감사하고 사용자 지시에 따라 보존·커밋한다. 엔진 빌드 성공과 설정 감사 결과를 분리한다. 빌드 성공과 실행 파일의 두 판 반복 동작 검증은 별개다.

기존 BuildWindowsPlayer의 디스크 보호 검사는 엔진 빌드 성공 뒤 ProjectSettings 자동 저장을 감지하면 명령을 실패시킬 수 있다. 이 경우 엔진 보고서와 명령 예외를 각각 기록한다. 자동 저장의 diff·원래 해시·생성 원인을 감사하고 승인된 변경을 유지한 채 필요한 기능 확인과 빌드를 다시 수행한다. 이전 실패 기록은 남기며 보호 기준점이나 자동 변경을 조용히 원복하지 않는다. 최종 명령 완료·전후 설정 차이·씬 setup/startScene 복원·실행 파일 해시를 확인한다.

현재 서버 배포·네트워크 환경 변수·운영 계정은 필요하지 않다. 음량은 Cooked.TechSYM.UI.v1. 접두사의 Master·Music·Sfx 로컬 설정으로 저장한다. 사용자 설정 초기화를 테스트의 자동 전제로 삼지 않는다.

## 현재 승인 검증 범위

새 Prototype/1_Stage 실제씬에서 InputSystem 입력 동선을 검증한다. 서비스 직접 호출·teleport·EditMode API 검사는 별도 근거이며 전체 사용자 동선 통과를 대체하지 않는다. 시작 전 모든 로드 씬의 path/isDirty/isLoaded/rootcount와 active/setup/startScene을 기록하고 dirty가 하나라도 있으면 Play/build를 보류한다. 미저장 씬을 임의 저장·폐기하지 않고 원래 setup과 startScene을 복원한다. Unity·Editor 자동 저장은 감사하여 보존·커밋한다. 기존 03_1_Stage를 재생성하지 않는다.


### Windows 빌드 설정 감사 주의 (2026-10-05 T9의 과거 관찰)
명시 여섯 씬 BuildPipeline.BuildPlayer는 성공했으나 최초 실행에서 Unity 자체가 GraphicsSettings 형식을 정규화하고 InputSystem preloadedAssets 및 UnityConnect m_Enabled를 디스크에 기록했다. 엔진 결과 Succeeded와 ProjectSettings 불변 gate 실패를 분리한다. baseline의 GraphicsSettings 복사본을 native load/save한 바이트는 현재 파일과 동일하므로 형식 변환으로 재현됐다. PlayerSettings.preloadedAssets와 Connect.m_Enabled의 본 검사 부수 변경은 원래 값으로만 native API 복원했다. raw 파일 해시는 줄바꿈/형식 차이 때문에 baseline과 달라 raw 불변 PASS가 아니다. 근거 t9-windows-first-build.json, t9-build-settings-restoration.json.
Unity singleton wrapper를 SaveToSerializedFileAndForget 또는 SaveAssetIfDirty에 넘긴 첫 시도는 디스크 복원이 되지 않았다. 실제 복사 파일을 InternalEditorUtility.LoadSerializedFileAndForget로 읽은 객체에 SerializedObject로 해당 필드만 원복하고 SaveToSerializedFileAndForget로 저장한 뒤 재조회했다. 전역 SaveAssets/SaveOpenScenes나 원본 씬 저장을 사용하지 않았다. 외부 ProBuilder 변경은 복원 대상에서 제외한다.
