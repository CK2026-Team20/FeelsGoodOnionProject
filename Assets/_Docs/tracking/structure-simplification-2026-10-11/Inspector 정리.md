# Inspector 정리 — 2026-10-11

## 목표·참고·제약·완료 기준

- 목표: 전체 Assets 소비를 확인해 HUD의 미사용 표시 경로를 제거하고 SessionActorHost의 모드별 설정과 필수 누락을 명확하게 표시한다.
- 참고: HudView.cs, UI/Runtime/ViewModels.cs의 HudViewModel, BoundView.cs, SessionActorHost.cs, 기존 StageTriggerEditor 및 InspectorLabelDrawer, 전체 Assets 직렬화 GUID/override와 C# 소비 검색.
- 제약: HMS 원본·기존 meta·씬·프리팹을 수정하지 않는다. HudIconGraphic 및 hearts 직렬화 연결을 유지한다. Stage/trigger와 HMS audit 작업은 다른 담당 범위다. Play·자산 제작·Git 변경은 하지 않는다.
- 완료 기준: 제거 필드의 비어 있지 않은 저장 참조와 다른 VM 소비가 없을 것, 기존 하트 GUID가 유지될 것, 모드별 필드 보존·누락 진단·다중 선택 처리가 있을 것, 수정 소스의 컴파일 가능성과 검증 한계를 기록할 것.

## 변경·검토

### HudView / HudViewModel

- health/fragments/abilities TMP 필드와 대응 VM Health/Fragments/Abilities 문자열 속성을 삭제했다. FragmentCount/HeartFill/FormUnlocked/TearUnlocked와 상태 구독/해제는 유지한다.
- 사용되지 않는 heartImages/heartSprite 및 두 표현 방식 전환 분기를 삭제했다. 향후 실물 리소스 연결 시 필요한 실제 표현으로 교체한다. 현 시점에 미래용 빈 필드를 노출하지 않는다.
- hearts 이름·타입은 보존하고 한국어 이름/Tooltip으로 Inspector에서 현재 사용되는 세 하트를 볼 수 있도록 했다. 기존 HudIconGraphic을 생성하거나 변경하지 않았다.
- 필수 group/formCard/tearCard/fragmentCount와 서로 다른 세 hearts를 OnValidate 및 실제 Model 연결/표시 시 검증한다. 잘못된 바인딩은 예외로 드러나고 BoundView의 기존 정리 경로가 자기 구독을 해제한다. Model이 없는 Unbind/파괴 시에는 누락 참조를 역참조하지 않는다.
- ViewModel에 View 참조나 새 구독을 추가하지 않았다. HUD는 입력 Command가 없는 표시 View다.

### SessionActorHostEditor

- 기존 Host 전용 Inspector는 없었다. 프로젝트의 기존 IMGUI/SerializedProperty 방식과 InspectorLabelDrawer를 활용하는 전용 Editor 한 개를 추가했다.
- independentActor는 기존 런타임 모드 선택 기준이므로 항상 표시한다. 새 모드 bool/enum이나 별도 설정 상태를 만들지 않았다.
- 통합 모드: actorPrefab. 독립 모드: independentUi/independentMemos/independentCamera. 공통 넉백 설정은 항상 표시한다. 혼합 다중 선택에서는 두 모드 필드를 표시한다.
- 각 선택 객체별로 필수 프리팹 또는 독립 UI/Camera 누락을 HelpBox 오류로 표시한다. 통합 프리팹 activeSelf도 기존 Spawn 계약에 맞춰 검사한다.
- 숨긴 필드 값은 지우지 않는다. 모드 변경 시 기존 설정을 다시 표시한다. SerializedProperty를 사용하여 Undo/prefab override 편집 관례를 보존한다. 자동 연결이나 fallback은 없다.
- SessionActorHost 런타임 소스는 변경하지 않았다. 기존 Editor 폴더를 재사용하고 새 Editor 소스 meta만 추가했다. 폴더 meta를 잘못 덮어쓴 것을 확인해 기존 GUID c00f4d7f64934f87ad2c722488d04c79 및 중복 없는 원래 3행으로 복원했다.

## 전체 Assets 참조 감사

- HudView script GUID: 94bb7cb54a524d36a64d94a605318777.
- 전체 Assets 검색 결과 직접 직렬화 원본은 Architecture/UI/Prefabs/GameUiRoot.prefab의 컴포넌트 3342649275387857287. 03_InGameCore.unity의 HudView는 해당 프리팹의 stripped 참조다.
- GameUiRoot prefab GUID: fe931de3551058e4e9361036fe5b131f. 이 GUID의 전체 Assets 소비를 함께 조회했다.
- 원본의 health/fragments/abilities는 모두 fileID 0. heartImages는 빈 배열, heartSprite는 fileID 0. 제거 대상 propertyPath override는 전체 Assets의 unity/prefab/asset 검색에서 0건이다.
- HudViewModel 소비는 HudView와 CoreUiBinding 생성 경로. 삭제된 세 속성의 실제 표시 소비는 삭제한 HudView 코드뿐이었다. 필드명 문자열/nameof 사용도 확인했다.
- 유지한 hearts fileID: 7744607675494385696, 7595775525159764677, 9004163343407192870. 모두 같은 prefab 안의 실제 MonoBehaviour로 해석되며 HudIconGraphic GUID c4fabefcfa9a4f98aaf92d01906cdee1과 일치한다.
- group/formCard/tearCard/fragmentCount의 4개 fileID도 같은 prefab에서 해석됨을 확인했다.
- Host script GUID 7e8afdfc2e354f6dbf313a2cf1278310 소비는 통합 Host prefab/Core 씬뿐 아니라 01_BrokenablePlatformTest, 02_MultiFuncPlatformTest, 03_InGame, MultiFuncPlatforms prefab에 존재한다. 따라서 독립 모드 필드를 삭제하지 않았다.
- 새 Editor 소스 GUID는 1066041543e74725a2f4896ff0f6ba03이다. Session/Editor.meta는 신규 파일이 아니며 기존 GUID c00f4d7f64934f87ad2c722488d04c79를 보존한다.
- 기존 자산을 수정하지 않았으므로 YAML에는 제거된 빈 필드 키가 남아 있을 수 있다. 이를 제거하기 위한 프리팹 재저장/Apply는 하지 않았다.

## 수행한 검증과 한계

- 정적 코드/직렬화 참조 감사 완료. 생존 필드와 GUID를 유지했고 다른 객체의 리스너를 지우는 코드를 추가하지 않았다.
- Roslyn 5.9, Unity 6000.6.0f1 실제 참조 DLL, 원본 checkout의 Library/ScriptAssemblies를 이용한 변경 소스 부분 컴파일: 종료 코드 0.
- 컴파일 입력: HudView.cs, 현재 HudViewModel이 있는 ViewModels.cs, 변경하지 않은 BoundView.cs, SessionActorHostEditor.cs.
- 최초 검증 명령의 UnityEditor 중복 DLL 참조로 CS0433이 발생했다. 중복 facade 참조를 제거한 검증 명령으로 재실행해 성공했다. 제품 코드 오류가 아니었다.
- 최종 경고: 기존 Assembly-CSharp와 입력 소스의 BoundView/HudViewModel 중복에 따른 CS0436 2건, Unity 직렬화 private 필드의 CS0649 5건. 변경 소스를 우선 사용하는 부분 컴파일이며 현재 worktree의 전체 Unity 컴파일이나 실제 DLL 기능 실행으로 주장하지 않는다.
- 명령 response 파일과 로그: output/Tech_SYM/validation/inspector-cleanup/compile-fixed.rsp 및 compile-fixed.log. 최초 실패 로그 compile.log도 보존한다.
- Unity import, 실제 Inspector 표시/Undo, SerializedObject 네이티브 실행, Bind/Unbind 동작 실행, Play/빌드는 실행하지 않았다. 해당 항목은 후속 확인 대상으로 남긴다.
