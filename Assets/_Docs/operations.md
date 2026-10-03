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

ArchitectureInstallation.asset이 여섯 씬의 경로를, IntegrationEntrySettings.asset이 직접 진입 시 체크포인트·조각·해금을 제공한다. 각 Stage가 체크포인트의 실제 Pose를 제공한다. 이 세 설정을 혼동해 초기 좌표를 여러 곳에 중복 저장하지 않는다.

## 기존 검증 진입점

아래 메뉴는 코드에 존재하는 진입점이며 이번 문서 작업에서 실행 통과를 확인한 것은 아니다. 실행 전 안정된 Edit Mode, 컴파일 완료, 소유 씬 변경 검토·저장을 확인한다.

| 목적 | Unity 메뉴 | 주의 |
|---|---|---|
| 대화 순수 규칙 | Cooked/Dialogue/Run Pure Regression Tests | 순수 규칙과 실제 View 동작을 구분 |
| UI 규칙 | Cooked/UI/Verify logic | 프리팹 생성 메뉴와 다름 |
| 세션·저장 프리팹 | Cooked/Session/Verify Domain and Authored Prefabs | Play 검증 전체를 대체하지 않음 |
| 두 판 연속 흐름 | Cooked/Verification/Run Two Games Through Input | 실제 씬을 열고 Play 전환, 종료·재시작 확인 |
| Editor 진입 11개 | Cooked/Verification/Run All Eleven Editor Entries | 씬을 순차 교체하고 Play, 원래 활성 씬 복귀 처리 |

결과는 output/Tech_SYM/의 기능별 경로와 Console에서 확인한다. 검증 메뉴의 이름만으로 통과를 기록하지 말고 해당 실행 결과·시각·오류를 남긴다. 기존 증거가 없으면 미실행으로 구분한다.

## Windows 빌드

안정된 Edit Mode에서 Cooked/Integration/Build Windows Player를 실행한다. ArchitectureInstallation의 여섯 씬을 명시적으로 빌드한다. ProjectSettings/EditorBuildSettings.asset의 일반 목록을 수정할 필요가 없다.

결과 경로는 output/Tech_SYM/integration/build/Windows/CookedPrototype.exe이고 같은 폴더에 build-evidence.json이 남는다. result가 Succeeded인지, errors와 changedProjectSettings가 없는지 확인한다. 빌드 성공과 실행 파일의 두 판 반복 동작 검증은 별개다.

현재 서버 배포·네트워크 환경 변수·운영 계정은 필요하지 않다. 음량은 Cooked.TechSYM.UI.v1. 접두사의 Master·Music·Sfx 로컬 설정으로 저장한다. 사용자 설정 초기화를 테스트의 자동 전제로 삼지 않는다.
