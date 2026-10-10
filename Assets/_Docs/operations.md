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

Unity Hub에서 위 프로젝트를 지정한 버전으로 열고 import·컴파일이 끝날 때까지 기다린다. 자동 Builder는 삭제했으며 실행하지 않는다. 기존 소유 씬·프리팹을 확인하고 사람의 새 리소스/프리팹으로 교체한다. 문서 검토 작업에서 이 절차를 실행했다고 간주하지 않는다.

## 사람이 확인할 플레이

사람이 Assets/_Scenes/Tech_SYM/Architecture/01_Bootstrapper.unity를 열고 Play한다. 타이틀 → 새 게임 → 오프닝 → 응집형 Prototype Stage → 포포 대화 완료 또는 전체 Skip → 엔딩 → 타이틀의 한 판 흐름을 확인한다. Stage 씬을 선택하여 직접 Play하는 경우 Editor 진입 라우팅이 적용되며 오프닝이 생략된다. 에이전트는 Play 버튼·PlayMode API/CLI/pipeline·batch PlayMode 테스트·직접 플레이를 실행하지 않는다. 저장되지 않은 타인 씬을 자동 저장하지 않는다.

Architecture의 Bootstrapper·Title·Core·Prototype/1_Stage·Stages/03_2_Stage·Stages/03_3_Stage 여섯 경로에만 Bootstrap 시작을 적용한다. 선택한 Stage2/3는 해당 Stage로 도착한다. 그 밖의 저장 씬은 현재 활성 씬에서 직접 시작한다. 역사적 Stages/03_1_Stage는 플레이어를 포함하지 않는 level-only 씬이어서 독립 Play만으로 통합 세션을 만들지는 않는다. 프로그래머가 대상을 바꿀 때 BootstrapEntryRoute의 정확한 경로 목록·설치의 SceneAsset Stage 목록·직접 진입 설정·빌드 생성 경로와 Catalog·실제 진입 검증 목록을 함께 맞춘다.

ArchitectureInstallation.asset에는 씬 자산을, IntegrationEntrySettings.asset에는 직접 진입할 Stage 씬과 초기 조각·해금 설정을 연결한다. 초기 Pose는 해당 Stage의 실제 초기 Marker에서 조회한다. 유효 CP 접촉 뒤 저장 payload는 현재 ActorBody의 유한 월드 Position, Fragments 0..5, UnlockedAbilities 마스크 0/1/2/3의 세 항목뿐이다. StageId/CheckpointId·HP·폼·시간·대화 완료를 저장하지 않는다. 첫 저장 전에는 초기 진입 구성을 사용한다. Core1회 뒤 등록 Stage3개를 최초 additive 로드하고 retry·Title·다음 판에 씬을 유지한다. Title 씬 자체는 NewGame 때 언로드할 수 있다. 한 판 ReleaseSession/재연결과 씬 수명을 구분한다. 현재 Prototype 한 판에서 Stage1→2→3 자동 전환은 추가하지 않는다.

## 자동 기능 검증

검증 전에 해당 프로젝트의 live Editor 연결, 컴파일 완료, Edit Mode와 모든 loaded scene의 path/isDirty를 확인한다. dirty가 하나라도 있으면 해당 씬을 덮어쓰거나 강제 종료하지 않고 저장·보존 상태가 정해질 때까지 씬 이관/build를 보류한다. 원래 scene setup·active scene·playModeStartScene과 보호 파일 해시를 기록한다. 씬 연결 검사는 기존 SYM 씬·프리팹을 사용하며 사람 소유의 prefab 삭제·재생성·Apply와 자동 Builder 실행을 대신하지 않는다.

```powershell
unity status --project-path 'F:\CK2026_Project\Project\FeelsGoodOnionProject' --format json
unity pipeline list --format json
```

실제 Editor 조회가 성공한 뒤 필요한 검증을 실행한다. 2026-10-06 정리로 기존 테스트 전용 클래스와 Cooked/Verification 등의 시험 메뉴를 제거했다. 자동 배치 생성/Repair 도구12개는 제거했다. Cooked/Integration/Build Windows Player와 실제 씬 진입·편집 검증은 유지한다. 과거 검증 메뉴 이름을 현재 실행 명령으로 사용하지 않는다.

일회성 C# 검증은 output/Tech_SYM/ 아래 기능·날짜별 검증 폴더에 두고 Unity의 run_script로 메모리 컴파일하여 Edit Mode에서 실행한다. 실행 스크립트도 Play 중지 조건을 검사한다. 컴파일 진단과 Main의 실제 완료 상태·관찰값·실패를 확인한다. 순수 규칙, 실제 Editor 직렬화/엔진 검사, 사람의 전체 동선·애니메이션 확인은 결과를 구분한다. 비동기 검사는 요청 수락과 완료를 구분하고 시간 제한·취소·finally 정리를 제공한다. 필요한 검증만 작성·실행하며 일회성 테스트 소스를 Assets 안에 남기지 않는다.

끝난 뒤 검증이 소유한 임시 오브젝트·이벤트·정지 차단을 정리한다. Play를 시작하거나 Stop할 필요가 없는 비 Play 검사로 수행한다. 원래 scene setup와 startScene 복원, dirty 상태, 컴파일, Console와 보호 파일 diff를 확인한다. 자동 저장 변경은 원인·전후 diff를 기록하여 커밋에 포함한다. 명령·실행 시각·결과 JSON·케이스·소스 해시·실패 원인은 보존한다.

최신 시스템의 실제 비 Play 검사와 사람 확인 목록은 tracking/ready-system-v15-2026-10-10/테스트 및 검증 항목.md에서 추적한다. ready-platform-hms-cleanup의 결과는 이전 실행 이력이다. 제어된 서비스 요청·런타임 위치 이동 검사, 실제 InputSystem 전체 동선, Windows 빌드, 팀 실제 실행과 FunQA를 따로 판정한다. 이전 editor-entry-hms 및 T9 결과와 캡처는 당시 실행 이력이다.

## 팀 검증 인계

QA 인계를 요청받은 경우 프로그래머·에이전트는 기능 테스트 엑셀과 Cooked! FunQA DOCX 한 개를 제공한다. 현재 V1.5 실행에서는 요청 없이 QA/FunQA 문서를 새로 작성하거나 평가하지 않는다. 기존 원본·당시 인계 기록은 보존한다. 기능 케이스 작성·유지는 프로그래머 책임이며 QA는 제공된 원본 서식의 FunQA를 직접 작성한다. 현재 제공 파일과 문서 검증은 [QA 인계 명세](tracking/2026-10-05-functional-qa-spec.md)를 참조한다.

빌드 ID·소스/명세 버전·실행 파일 경로·해상도·입력 장치·처음부터 진입 또는 체크포인트 조건·알려진 제한을 확정해 전달한다. 현재 존재하는 과거 exe를 새 소스 빌드로 간주하지 않는다. 긴 진행 경로가 필요한 케이스에는 도달 순서와 대상명을 제공하고, 테스트용 진입 기능이 없으면 그 제약을 기록한다. 임의의 새 검증 씬이나 보호 자산 변경을 인계 문서 작성 과정에서 수행하지 않는다.

새 빌드 결과는 이전 엑셀의 실행 환경과 결과를 보존한 사본으로 기록한다. P/F/B/N은 실제 해당 빌드에서 실행한 판정이고 자동 검증 이력과 혼합하지 않는다. QA 재미 평가·전체 플레이 확인 전 상태는 구현/자동 검증 완료 및 팀 검증 대기로 표현한다.

## Windows 빌드

안정된 Edit Mode에서 Cooked/Integration/Build Windows Player를 실행한다. ArchitectureInstallation의 여섯 씬을 명시적으로 빌드한다. ProjectSettings/EditorBuildSettings.asset의 일반 목록을 수정할 필요가 없다.

결과 경로는 output/Tech_SYM/integration/build/Windows/CookedPrototype.exe이고 같은 폴더에 build-evidence.json이 남는다. result가 Succeeded인지, errors가 0인지 확인한다. changedProjectSettings는 빌드 전후 실제 해시 차이이며 자동 변경이 있으면 원인·diff를 감사하고 사용자 지시에 따라 보존·커밋한다. 엔진 빌드 성공과 설정 감사 결과를 분리한다. 빌드 성공과 실행 파일의 두 판 반복 동작 검증은 별개다.

기존 BuildWindowsPlayer의 디스크 보호 검사는 엔진 빌드 성공 뒤 ProjectSettings 자동 저장을 감지하면 명령을 실패시킬 수 있다. 이 경우 엔진 보고서와 명령 예외를 각각 기록한다. 자동 저장의 diff·원래 해시·생성 원인을 감사하고 승인된 변경을 유지한 채 필요한 기능 확인과 빌드를 다시 수행한다. 이전 실패 기록은 남기며 보호 기준점이나 자동 변경을 조용히 원복하지 않는다. 최종 명령 완료·전후 설정 차이·씬 setup/startScene 복원·실행 파일 해시를 확인한다.

현재 서버 배포·네트워크 환경 변수·운영 계정은 필요하지 않다. 음량은 Cooked.TechSYM.UI.v1. 접두사의 Master·Music·Sfx 로컬 설정으로 저장한다. 사용자 설정 초기화를 테스트의 자동 전제로 삼지 않는다.

## 새 목표 검증 범위와 준비 상태

최신 자동 검증은 tracking/ready-system-v11-refactor-2026-10-06/테스트 및 검증 항목.md와 output/Tech_SYM/verification/system-v11-refactor-2026-10-07/T10-final-verified-snapshot.json에서 명령·시각·소스/빌드 해시·관찰 범위를 확인한다. 입력과 물리 시간을 사용해도 위치 준비·서비스 요청·복제 fixture가 포함된 결과는 원본 전체 수동 동선 판정으로 승계하지 않는다.

### Windows 빌드 설정 감사 주의 (2026-10-05 T9의 과거 관찰)
명시 여섯 씬 BuildPipeline.BuildPlayer는 성공했으나 최초 실행에서 Unity 자체가 GraphicsSettings 형식을 정규화하고 InputSystem preloadedAssets 및 UnityConnect m_Enabled를 디스크에 기록했다. 엔진 결과 Succeeded와 ProjectSettings 불변 gate 실패를 분리한다. baseline의 GraphicsSettings 복사본을 native load/save한 바이트는 현재 파일과 동일하므로 형식 변환으로 재현됐다. PlayerSettings.preloadedAssets와 Connect.m_Enabled의 본 검사 부수 변경은 원래 값으로만 native API 복원했다. raw 파일 해시는 줄바꿈/형식 차이 때문에 baseline과 달라 raw 불변 PASS가 아니다. 근거 t9-windows-first-build.json, t9-build-settings-restoration.json.
Unity singleton wrapper를 SaveToSerializedFileAndForget 또는 SaveAssetIfDirty에 넘긴 첫 시도는 디스크 복원이 되지 않았다. 실제 복사 파일을 InternalEditorUtility.LoadSerializedFileAndForget로 읽은 객체에 SerializedObject로 해당 필드만 원복하고 SaveToSerializedFileAndForget로 저장한 뒤 재조회했다. 전역 SaveAssets/SaveOpenScenes나 원본 씬 저장을 사용하지 않았다. 외부 ProBuilder 변경은 복원 대상에서 제외한다.

마지막 카메라 수정 뒤 전체36시나리오·9진입/종료·원본 합성 입력2판/자연 사망4회와 새 지정6씬 Windows 빌드를 다시 실행했다. 원본 동선의 합성 입력 검증과 위치 준비/복제 fixture 기능 검사는 각각 기록한다. Windows 수동 그래픽·UI/UX·FunQA는 별도 수락이며 활성 DryForge 세 문서는 사용자 결과 수락 전 유지한다.
