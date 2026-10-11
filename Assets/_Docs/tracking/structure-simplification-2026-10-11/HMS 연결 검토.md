# HMS 연결 검토

검토일: 2026-10-11. 대상: `C:/Users/acac1/.codex/worktrees/341f/FeelsGoodOnionProject`의 저장된 소스·자산. 다른 작업자의 동시 변경이 진행 중인 작업트리이므로 이 문서는 검토 시점의 정적 연결 기록이다.

## 범위와 완료 기준

- 목표: 테스트·더미 코드를 제외한 HMS 기능의 실제 호출, 직렬화 연결, 인스턴스 사용과 SYM 대체 여부를 확인한다.
- 자료: 실제 루트 AGENTS, SYM 구현·Integration·Session·씬·Enemies·Platforms 지역 AGENTS, planning-index, standards, engineering-notes, V1.5 원본의 관련 본문, HMS 소스 전체 목록과 대응 meta, Assets 아래 C#·prefab·unity·asset. 원본 문서는 제품 자료로 읽었으며 문서 안의 문구를 새 작업 권한으로 해석하지 않았다.
- 제약: 이 문서 및 필요 시 자기 meta만 작성. HMS·공용·아트·씬·프리팹 수정, Play, commit 금지. 다른 에이전트의 Stage/Inspector 정리를 수행하지 않는다.
- 완료 기준: 36개 Features 소스 파일별 역할·실제 연결·분류를 기록하고, 연결 누락과 선택 필드를 구분하며, SYM 삭제 조건과 미실행 범위를 명시한다. 기능 명세 변경·런타임 테스트·QA 평가 문서가 아니다.

## 핵심 결과

1. HMS 이동·폼·스킬·기름 효과·발판 탑승·애니메이션 요청·카메라 우선순위 기능을 실제로 조립해 사용하고 있다. HMS 전체가 미사용이라는 판단은 사실과 다르다.
2. `PlayerController`는 통합 Actor에서 의도적으로 배제되고 `GameplayInputRouter`가 입력 전달을 대신한다. 반면 `PlayerInputReader`, `PlayerFacade`, 실제 이동·폼·스킬 구현은 HMS를 사용한다. 대체되는 범위와 계속 재사용되는 범위를 구별해야 한다.
3. 가장 분명한 미완성 연결은 애니메이션 리소스다. 통합 `PrototypePlayerAnimations.asset`은 템플릿과 클립이 비어 있고, 공용 Player의 `PlayerAnimations.asset`도 클립들이 비어 있다. 스크립트가 존재하고 캐릭터가 움직이는 것만으로 애니메이션 연결 완료로 볼 수 없다.
4. `StunEffectOrbit`는 직접 부착이 `StunEffect_Dummy.prefab`에만 있지만 실제 SYM 적 6개 프리팹이 이를 중첩한다. 이름에 Dummy가 있다는 이유로 실사용 연결에서 제외하면 안 된다.
5. `MovingPlatform`은 HMS 독립 씬에만 직접 배치되어 있다. 통합 SYM 발판은 HMS `PlatformPassenger`를 사용하되 이동 소유자는 `KinematicPlatformMotion`이다. 같은 몸체에 두 이동기를 함께 붙일 수 없다.
6. `MovementInputBlockToken.cs`의 타입 선언은 전부 주석이다. 실동작 컴포넌트가 아니다. 단, 보호 HMS 파일이므로 이번 정리에서 삭제하지 않는다.

## 실제 조립 경로

아래 경로는 코드와 YAML에서 확인한 연결이다. 함수 실행·물리·카메라 블렌드의 성공을 관찰했다는 뜻은 아니다.

| 기능 | 확인한 경로 | 판정 |
|---|---|---|
| 통합 Actor 생성 | `SessionActorHost.actorPrefab` → `Architecture/Session/PlayerRoot.prefab` → ActorBody의 HMS 컴포넌트. Host가 비활성 프리팹을 생성한 뒤 활성화하고 Bridge/Router를 연결 | 통합 사용 |
| 입력·행동 | HMS `PlayerInputReader` → SYM `GameplayInputRouter` → `PlayerBridgeService` → HMS `PlayerFacade` → `CharacterMovement` / `PlayerSkillController` / `PlayerFormController` | HMS 입력 판독·행동 재사용, HMS 입력 전달자만 우회 |
| 스킬 생성 | Facade가 `TearSkill`, `ShrinkSkill`, `RestoreFormSkill` 생성 → `PlayerSkillController.Initialize/Equip` → `SkillSlot` | 일반 C# 객체. 씬의 m_Script GUID가 없는 것이 정상 |
| 스킬 데이터 | PlayerRoot Facade → `TearSkill.asset` / `ShrinkSkill.asset` / `RestoreSkill.asset`의 HMS 정의 타입 | 세 GUID 실물 확인 |
| 껍질 | Form의 `debrisPrefab` → `ShellRoot.prefab` → `PlayerDebris` + `PlatformPassenger` + `PlatformRigidbodyMovement` + SYM `HeavyState` | 통합 생성 대상. 모든 껍질처럼 보이는 자산이 이 경로에 쓰이는 것은 아님 |
| 애니메이션 | Facade/Movement/Form → `PlayerAnimationController` → `PlayerAnimationPlayback` → `PlayerAnimatorOutput` → `PlayerAnimationSet` → 현재 Form Animator | 요청 체계 연결, 재생 리소스 미완성 |
| 카메라 | App 출력의 HMS `PlayerCameraController`를 Core가 전달 → `CoreWorldBinding` → `LevelCameraService.Bind` → HMS `Bind(modes, side, quarter)` | 런타임 주입. 분리 리그 Inspector의 기존 5참조 null은 누락 아님 |
| 기름 | `OilSurface.OnTriggerEnter/Stay` → 상대 Rigidbody의 `CharacterSlipperyEffect.Apply` → `CharacterMovement.SetTractionMultipliers` | 직접 Inspector 대상 참조 없이 접촉 객체에서 획득 |
| 발판 운반 | SYM `KinematicPlatformMotion` → HMS `PlatformPassenger.ReceivePlatformVelocity` → HMS 플레이어/껍질 이동기 | HMS 물리 수신 계약 재사용 |
| 적 기절 표시 | HMS `TearSkill` → `IStunnable` 구현 SYM `EnemyActor` → `IStunState.StunStateChanged` → HMS `EnemyStunVisual` → 중첩 `StunEffectOrbit` | HMS 상태 표시와 SYM 적 상태가 연결됨 |

## HMS 소스별 판정

소스 기본 경로는 `Assets/_Code/Core/Tech_HMS/Features/`다. `PlayerRoot`는 `Assets/_Scenes/Tech_SYM/Architecture/Session/PlayerRoot.prefab`, `PlayerCharacter`는 `Assets/_Prefabs/Characters/PlayerCharacter.prefab`을 뜻한다. 아래 36행은 Features의 모든 C# 파일을 포함한다. GUID·직접 자산 목록은 후속 근거 부록에 기록한다.

| 소스 | 역할과 실제 소비 | 분류·정리 판단 |
|---|---|---|
| CharacterMovement.cs | Facade 명령을 물리 이동·점프·접지로 실행. PlayerRoot/PlayerCharacter에 부착. SYM Bridge·발판이 공개 물리값을 소비 | 통합 사용. SYM 게임 흐름 조정과 중복 아님 |
| CharacterSlipperyEffect.cs | OilSurface가 Apply, Movement 가감속 배율을 변경하고 만료/비활성 때 복원. 두 Player 프리팹에 부착 | 통합 사용. 직접 대상 필드 없이 같은 객체에서 Movement 획득 |
| EnemyStunVisual.cs | 부모 `IStunState` 구독, 효과 활성·텍스처 변경. SYM 적 6개 프리팹에 직접 부착 | 통합 적 사용. EnemyActor 상태 소유를 대체하지 않음 |
| MovementInputBlockToken.cs | using 외 타입·계약 선언이 주석 처리됨. Facade의 관련 구형 API도 주석 | 미사용 잔존 파일. 보호 원본 삭제 권한 없음 |
| MovingPlatform.cs | 월드 offset 왕복, 끝점 대기, 탑승 속도 전달. HMS `01_PlayerMovementTest.unity`에 배치 | 독립 씬 전용 구현. 통합에는 SYM 이동기 사용. 무조건 대체 불가 |
| OilSurface.cs | Trigger 접촉 Rigidbody에서 SlipperyEffect를 찾아 효과 적용 | 통합 Stage/SessionOil/EnemyOil 및 독립 씬 사용 |
| PlatformPassenger.cs | 지지면과 프레임별 발판 속도를 수신·조회 | PlayerRoot/ShellRoot 사용. SYM 발판이 직접 공개 API 호출 |
| PlatformRigidbodyMovement.cs | 껍질 등 Box 몸체의 중력·접지·탑승 이동 | ShellRoot 및 독립 껍질/발판 자산 사용. CharacterMovement 동거 금지 |
| PlayerAnimationController.cs | Facade 행동·폼·이동 이벤트를 재생 요청으로 변환 | 통합 사용. Bridge 사망 완료, Interaction 상호작용도 소비 |
| PlayerAnimationPlayback.cs | 기본/일회성 재생 우선순위와 완료·중단 상태 | Controller가 소유하는 일반 C# 객체. GUID 부착 없음이 정상 |
| PlayerAnimationSet.cs | 템플릿·클립·대체 클립·재생속도 데이터 | 두 AnimationSet 자산이 사용. 실제 클립 누락은 별도 결함 |
| PlayerAnimationTypes.cs | AnimationId/Request/EndReason 등 값 계약 | Controller/Playback/Output 및 SYM Bridge/Interaction에서 타입별 소비. 파일명 검색만 하면 누락됨 |
| PlayerAnimatorOutput.cs | 현재 폼 Animator에 런타임 컨트롤러를 구성하고 재생 결과 통지 | 두 Player 프리팹에 부착. 리소스 미완성 때문에 실제 출력 완료 아님 |
| PlayerCameraController.cs | Cinemachine 우선순위 및 분리 모드 Preview/Commit/Unbind | Bootstrap 통합 출력에서 주입 사용. 독립 씬 네 카메라 모드도 별도 사용 |
| PlayerController.cs | Reader 입력을 Facade로 전달, Reader 비활성 이벤트 구독 | PlayerCharacter 및 이를 쓰는 HMS 독립 씬 사용. 통합 Host는 존재 자체를 거절; Router로 우회 |
| PlayerDebris.cs | 껍질 Owner·초기 pose/속도·충돌 무시·제거 | Form이 ShellRoot를 생성해 Initialize. 통합 사용 |
| PlayerFacade.cs | 모델·FSM·스킬 소유, 이동/피해/폼/조각 공개 API | PlayerRoot 핵심. SYM Bridge·수집·충돌·세션이 공개 계약 소비 |
| PlayerFormController.cs | Normal/Small, Capsule 크기, 점프 설정, 껍질 수명 | PlayerRoot, ShellRoot 데이터 연결. SYM은 폼 상태·OwnedDebris 소비 |
| PlayerInputReader.cs | InputAction 생성·활성·해제·읽기 | 통합 재사용, Host가 눈물 키 E 설정. 독립 기본 F. Recover 입력은 상수 false인 호환 항목 |
| PlayerModel.cs | HP·보유 조각과 변경 통지, IReadOnlyPlayerModel | Facade 생성, TearSkill 소비, Bridge가 Model 통지 관찰. 모델 중복 생성 필요 없음 |
| PlayerMovementModeController.cs | Side/Quarter/Back/BackFixed별 이동축·속도 | PlayerRoot 부착. LevelCameraService/HMS Camera가 사용. 통합은 Side/Quarter |
| PlayerSkill.cs | 스킬 실행·취소·발동·정의의 추상 계약 | 세 실제 스킬·SkillSlot·Controller·Bridge가 소비. 컴포넌트 아님 |
| PlayerSkillController.cs | 슬롯 초기화·장착·쿨다운·Tick·CancelAll | Facade가 초기화, PlayerRoot 부착. 쿨다운을 SYM에 복제하면 안 됨 |
| PlayerSkillPresentation.cs | 같은 프레임 스킬 재생 요청 우선순위 | Facade가 생성·LateUpdate 소비. 일반 C# 객체 |
| PlayerSkillTypes.cs | SkillId/Phase/BlockReason 계약 | Facade·스킬·Controller·Slot·SYM Bridge에서 타입별 소비 |
| PlayerStateMachine.cs | 현재 물리·생존·제어 잠금에서 행동 상태 전이 | Facade 생성·Refresh, AnimationController가 Facade 상태 소비 |
| PlayerVisualController.cs | 이동 방향을 받아 외형 회전 | PlayerRoot/PlayerCharacter의 실제 VisualRoot 연결. 다른 코드의 타입 호출 없이 Unity 콜백으로 사용 |
| RestoreFormSkill.cs | 준비 후 Form.RestoreForm 요청 및 발동 통지 | Facade 생성·폼 슬롯 교체. 통합 사용 |
| RestoreFormSkillDefinition.cs | 복귀 준비시간·기본 SkillDefinition 데이터 | 통합 RestoreSkill.asset과 독립 TestRestoreFormSkillDefinition.asset이 실제 소비 |
| ShrinkSkill.cs | 준비 후 작은 폼 전환 요청 | Facade 생성·폼 슬롯 교체. 통합 사용 |
| ShrinkSkillDefinition.cs | 축소 준비시간·기본 스킬 설정 | 통합 ShrinkSkill.asset 및 독립 정의 자산 사용 |
| SkillDefinition.cs | 아이콘·초기/발동 쿨다운 공통 데이터 | 세 정의가 상속. 직접 m_Script 자산 없다고 미사용 아님 |
| SkillSlot.cs | 장착한 스킬과 쿨다운 수명·이벤트 | PlayerSkillController가 생성. 통합 사용 |
| StunEffectOrbit.cs | 기절 효과의 회전 | Dummy 이름의 효과 프리팹에 부착되지만 SYM 적 6개가 중첩하여 실사용 |
| TearSkill.cs | 조각 소비·준비·범위 후보·IStunnable 기절 | Facade 생성. Bridge가 실제 적 HurtCollider/차폐 검증을 SetTargetValidation으로 주입 |
| TearSkillDefinition.cs | 필요 조각·반경·기절시간·준비시간 | 통합 TearSkill.asset 및 독립 정의 자산 사용 |

### 제외한 코드와 제외하지 않은 자산

`Temporary/TestPlayerFunc.cs`, `TestPlayerMovementMode.cs`, `TestStunnableTarget.cs`, `PlayerSkillHUD.cs`는 임시 입력·확인·디버그 표시 목적이므로 제품 기능 평가에서 제외했다. `Editor/PlayerAnimationSetupEditor.cs`는 편집기 제작 도구라 런타임 컴포넌트 평가에서 제외했다. 존재/소비 검색은 포함했다. 이름만으로 삭제를 제안하지 않는다.

반면 `Temporary/Test*Skill*.asset`은 PlayerCharacter가 실제 참조한다. `Temporary/PlayerDebris.prefab`도 그 Player의 debrisPrefab이다. `StunEffect_Dummy.prefab`은 통합 적이 중첩한다. 테스트/더미 **스크립트** 제외를, 실사용 **자산 연결** 조사 제외로 확대하지 않았다.

## 빈 필드·미완성 연결의 구체적 자산

| 자산·필드 | 코드상의 조건과 영향 | 판단 |
|---|---|---|
| `Assets/_Scenes/Tech_SYM/Architecture/Session/PrototypePlayerAnimations.asset`: controllerTemplate 및 idle/walk/run/jump/fall/knockback/dead/land/tear/shrink/restoreForm가 fileID 0; interact 직렬화 항목도 없음 | PlayerRoot Output.animationSet은 이 자산 GUID `5b74b6518b2468a42842f84daa7e2adb`를 참조. Output.BindAnimator는 템플릿 null에서 반환. 실제 애니메이션 출력 계약에 필요한 연결 미완성 | 제거할 죽은 필드가 아니라 제작 리소스 연결 대상. Knockback 길이 0, 사망 Missing 결과 경로와 함께 확인 필요 |
| `Assets/_Code/Core/Tech_HMS/Animation/PlayerAnimations.asset`: 템플릿은 연결, 위 클립들은 모두 fileID 0, interact 미직렬화 | PlayerCharacter Output이 GUID `70b2fb2b48554e4d87a651ad8d7961fa` 참조. 템플릿 존재만으로 실제 클립 연결을 충족하지 않음 | 독립 Player 또한 클립 연결 완료 아님. HMS 원본 수정 금지 |
| PlayerRoot/PlayerCharacter `PlayerAnimatorOutput.animatorOverride` = 0 | null이면 Form.CurrentAnimator를 사용하도록 명시됨. Form은 NormalVisual/SmallVisual 아래 GetComponentInChildren로 검색 | 선택 필드. 그 자체는 오류 아님. 중첩 FBX의 실제 Animator 생성 결과는 Editor에서 별도 확인 필요 |
| `Assets/_Scenes/Tech_SYM/Architecture/01_Bootstrapper.unity` HMS Camera의 movementModeController/side/quarter/back/backFixed = 0 | useSeparatedRig=1. LevelCameraService가 Bind하는 boundMovementMode/boundSide/boundQuarter를 사용 | 분리 리그에서 정상적으로 빈 구형 Inspector 필드. HMS 전체 필드 삭제는 독립 네 리그 사용을 깨뜨림 |
| Session의 TearSkill/ShrinkSkill/RestoreSkill.asset `icon` = 0 | SkillDefinition.Icon→PlayerSkill.Icon은 제공하지만 현재 SYM HUD는 이 값을 표시 경로로 사용하지 않음. 임시 HMS HUD 소비와 구분 | 현재 통합 스킬 실행의 필수 누락 아님. 별도 아이콘 UI 채택 시 연결 필요 |
| EnemyStunVisual의 stunEffect/targetRenderer/normalTexture/stunnedTexture | SYM 적 6개 프리팹에서 네 필드에 nonzero 연결 확인. Awake는 모두 요구하며 부모 IStunState도 검사 | 연결 미완성으로 분류하지 않음. 셰이더 속성·실제 표시 성공은 미실행 |

PlayerRoot의 NormalVisual/SmallVisual은 `ch_onion.fbx` GUID `1caff1c2ad0949042a543d36adc994ca`를 각각 중첩한다. FBX meta의 animation import 설정은 확인했지만, 텍스트 YAML만으로 Unity가 생성한 모델 내부 Animator/clip subasset을 완전히 검증할 수 없다. 위 표의 확정 누락은 직접 저장된 AnimationSet의 null 값에 근거한다.

## SYM 대체·삭제 조건

| 비교 대상 | 현재 실제 차이 | 삭제 또는 통합 전에 충족할 조건 |
|---|---|---|
| GameplayInputRouter ↔ HMS PlayerController | 둘 다 이동·점프·스킬을 전달하지만 Router는 해금/모달 제한을 적용하는 Bridge, F 상호작용·메모 닫기, 같은 프레임 입력 억제, 사망 표현 Tick을 연결 | HMS Controller를 그대로 활성화하는 것으로 대체 불가. 이 추가 책임의 실제 소비자를 정하고 입력 한 번 한 호출, Reader disable 입력 비우기, 제한 해제 프레임, 사망 진행을 보존한 뒤 Router/Host 검사·자산을 함께 이관해야 함 |
| PlayerBridgeService ↔ HMS Facade | Bridge는 현재 Actor 세대·세션 해금·저장 복원·사망 재시도·차폐 검증을 연결. HP/폼/이동/스킬 실행 원본은 이미 HMS | 단순 공개 API 전달 부분의 응집 가능성은 있으나 Bridge 전체 즉시 삭제 불가. 세션 책임과 observer 해제·실패 처리를 실제 수신자에 이관해야 함 |
| KinematicPlatformMotion/LinearShuttlePlatform ↔ HMS MovingPlatform | HMS는 월드 travelOffset·끝점 대기·왕복 및 운반이 한 객체. SYM은 다양한 발판 드라이버·실물 기준점·재준비·운반 정책을 지원하고 HMS Passenger를 사용 | 해당 기능의 이동/정지/초기화/운반 계약이 모두 같아진 경우에만 대체. 같은 Rigidbody의 이중 writer 금지. 지금 이름만 보고 SYM 삭제 불가 |
| LevelCameraService ↔ HMS PlayerCameraController | HMS는 우선순위/모드 Preview·Commit, SYM은 Follow/LookAt·정렬·Brain 완료·제어 제한 연결 | 트리거로 앵커 소유를 옮기는 것은 가능하나 정렬/완료 책임의 실제 이관 없이 Service 전체 삭제 불가. HMS 우선순위 구현은 재사용해야 함 |
| SYM EnemyActor ↔ HMS EnemyStunVisual/StunEffectOrbit | 적의 상태/이동/피해와 그 상태의 표시가 다름. IStunState로 이미 연결 | 같은 기능의 대체 관계 아님. 표시 컴포넌트로 EnemyActor를 대체할 수 없음 |
| PlayerDamagePresentation ↔ HMS 애니메이션 체계 | SYM 피해 표시와 HMS 행동/클립 출력은 소비 이벤트가 겹칠 수 있어도 표현 목적이 다름 | 실제 깜빡임/표시 책임의 이전 없이 제거 금지. 애니메이션 리소스 누락을 SYM 표시 유지의 영구 근거로 삼지 않음 |
| SYM HeavyState ↔ HMS Form/Debris | HMS 보호 코드가 HeavyState를 Require/GetComponent하며 Normal·Small·껍질 무게를 토글 | HMS가 직접 사용하는 협업 계약. 중복 추정 삭제 금지 |

이번 감사로 즉시 삭제가 입증된 SYM 기능 컴포넌트는 없다. 이는 현재 분할 구조 전체가 적절하다는 뜻이 아니라, 삭제하려면 추가 책임과 직렬화 소비를 실제로 이관해야 한다는 뜻이다. 구조 단순화 담당 작업이 삭제 단위를 확정할 때 이 표를 사용한다.

HMS 내부의 별도 정리 후보는 주석 전용 MovementInputBlockToken, 항상 false인 RecoverDebrisPressedThisFrame 호환 입력 경로, 현재 읽기·쓰기 소비가 발견되지 않은 PlayerModel.SkillCooldown/FormChangeCooldown이다. HMS 소유자 검토 사항이며 이 감사에서는 수정·삭제하지 않는다. public API 제거는 저장소 검색 외 외부 소비 가능성도 소유자가 확인해야 한다.

## 검증 범위와 한계

- Features C# 36개, 별도 Temporary 4개와 Editor 1개를 목록화했다. MonoBehaviour 이외의 모델·enum·추상 클래스는 코드 생성/타입 소비를 확인했다.
- Assets의 `.prefab/.unity/.asset`에서 Features GUID를 가진 직렬화 블록 78개를 찾아 최상위 오브젝트 참조 필드를 검사했다. nonzero 외부 GUID의 meta 존재 및 로컬 fileID 블록 존재 검사에서 미해결 0건이었다.
- 위 0건은 null 필수 필드의 정상 판정이나 전체 Missing Script 검사 결과가 아니다. 모든 prefab override의 최종 합성값, FBX subasset, 배열/중첩 구조 전체, shader/material 유효성, 동적 로드, Unity import 결과를 검증한 수치도 아니다.
- 중첩 PlayerCharacter→HMS 독립 씬, StunEffect→SYM 적, PlayerRoot→모델과 스킬/껍질 자산 연결을 별도로 추적했다. 각 근거 목록의 직접 GUID 출현은 실제 활성 인스턴스 수와 다르다.
- Play/Player/Unity 네이티브 검사/컴파일/물리/카메라/리소스 재생은 실행하지 않았다. 정적 연결을 기능 테스트 통과로 표기하지 않는다.
- 감사 시작 시 이미 수정된 `CookedKorean SDF.asset`이 있었다. 이 문서 작업에서 수정하지 않았다. 병행 작업자의 변경은 이 감사의 산출물로 계산하지 않는다.

## 소스별 GUID·자산·코드 소비 근거

자동 텍스트 교차검색 부록. 코드 소비는 파일 내 선언 타입 이름의 다른 C# 출현(주석 제외)이며 호출·생성·상속·RequireComponent·금지 검사 등도 포함한다. 상단의 사람이 판독한 역할 표와 함께 해석한다. 자산 목록은 직접 GUID 출현이며 중첩 인스턴스는 본문에서 구분했다.

### CharacterMovement.cs

- GUID: `66bae9877abbb7844ae6648b61f3db93`; SHA-256: `11d289c17354ebc8d01e5c251adb2ebafd48003087bf33956d2e083f22abcd57`.
- 선언 타입: CharacterMovement
- 직접 자산: `Assets/_Prefabs/Characters/PlayerCharacter.prefab`; `Assets/_Scenes/Tech_SYM/Architecture/Session/PlayerRoot.prefab`; `Assets/_Scenes/Tech_SYM/Prefabs/MultiFuncPlatforms.prefab`; `Assets/_Scenes/Tech_SYM/01_BrokenablePlatformTest.unity`
- 코드 소비: `Assets/_Code/Core/Tech_HMS/Features/CharacterSlipperyEffect.cs`; `Assets/_Code/Core/Tech_HMS/Features/PlatformRigidbodyMovement.cs`; `Assets/_Code/Core/Tech_HMS/Features/PlayerAnimationController.cs`; `Assets/_Code/Core/Tech_HMS/Features/PlayerDebris.cs`; `Assets/_Code/Core/Tech_HMS/Features/PlayerFacade.cs`; `Assets/_Code/Core/Tech_HMS/Features/PlayerFormController.cs`; `Assets/_Code/Core/Tech_HMS/Features/PlayerVisualController.cs`; `Assets/_Code/Core/Tech_HMS/Temporary/TestPlayerMovementMode.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Session/PlayerBridgeService.cs`; `Assets/_Code/Core/Tech_SYM/Platforms/KinematicPlatformMotion.cs`; `Assets/_Code/Core/Tech_SYM/Platforms/PeriodicPlatform.cs`

### CharacterSlipperyEffect.cs

- GUID: `bb1934b8925db144db25b67d6605ed36`; SHA-256: `5bc4b253703d60faa3de78d6ef6881c8f9ac468ad0fab34ff23353c08c7b66b1`.
- 선언 타입: CharacterSlipperyEffect
- 직접 자산: `Assets/_Prefabs/Characters/PlayerCharacter.prefab`; `Assets/_Scenes/Tech_SYM/Architecture/Session/PlayerRoot.prefab`; `Assets/_Scenes/Tech_SYM/03_InGame.unity`
- 코드 소비: `Assets/_Code/Core/Tech_HMS/Features/OilSurface.cs`

### EnemyStunVisual.cs

- GUID: `17f8da3663076ae40a103148a4f13594`; SHA-256: `a8cb75d7f108014a77d7c566011091a582704cf01ec57b06d317dd9dcbd9756b`.
- 선언 타입: EnemyStunVisual
- 직접 자산: `Assets/_Prefabs/Characters/Enemy/Enemy_Dummy.prefab`; `Assets/_Scenes/Tech_SYM/Enemies/Prefabs/IdleEnemy.prefab`; `Assets/_Scenes/Tech_SYM/Enemies/Prefabs/OilPatrolEnemy.prefab`; `Assets/_Scenes/Tech_SYM/Enemies/Prefabs/PatrolEnemy.prefab`; `Assets/_Scenes/Tech_SYM/Enemies/Prefabs/PatrolXEnemy.prefab`; `Assets/_Scenes/Tech_SYM/Enemies/Prefabs/PatrolYEnemy.prefab`; `Assets/_Scenes/Tech_SYM/Enemies/Prefabs/StationaryEnemy.prefab`
- 코드 소비: 다른 C#에서 발견되지 않음. Unity 콜백/직렬화 사용은 별도.

### MovementInputBlockToken.cs

- GUID: `c6e09c61e8487934d867b40cf4bc6283`; SHA-256: `9908c41aeda7f071e60984e6f6d7d332ffade7cefe1929b9b806db2efbf2b4e3`.
- 선언 타입: 없음(주석 전용)
- 직접 자산: 없음
- 코드 소비: 다른 C#에서 발견되지 않음. Unity 콜백/직렬화 사용은 별도.

### MovingPlatform.cs

- GUID: `eb72f778acd6ebf4baa42adbbaa8a45d`; SHA-256: `6a7c08e3b58dca0b3b2a54abcc9c87ef6292b1da49323af3f645f556c1c6bf4b`.
- 선언 타입: MovingPlatform
- 직접 자산: `Assets/_Scenes/Tech_HMS/01_PlayerMovementTest.unity`
- 코드 소비: `Assets/_Code/Core/Tech_SYM/Platforms/KinematicPlatformMotion.cs`

### OilSurface.cs

- GUID: `381a1fee175d9904a907c8cb78211471`; SHA-256: `7cc1d89905ccf0627bd7ec28466a4e646675bf00135af819832982597bebf7c1`.
- 선언 타입: OilSurface
- 직접 자산: `Assets/_Scenes/Tech_HMS/01_PlayerMovementTest.unity`; `Assets/_Scenes/Tech_SYM/Architecture/Prototype/1_Stage.unity`; `Assets/_Scenes/Tech_SYM/Architecture/Session/SessionOil.prefab`; `Assets/_Scenes/Tech_SYM/Enemies/Prefabs/EnemyOil.prefab`
- 코드 소비: 다른 C#에서 발견되지 않음. Unity 콜백/직렬화 사용은 별도.

### PlatformPassenger.cs

- GUID: `cb9b0185e676caa419523794b16ce4c4`; SHA-256: `cac87f43735e9795e61f84b73e18f7e7ba72acc56b93305942301fae8075bf1a`.
- 선언 타입: PlatformPassenger, PlatformMotion
- 직접 자산: `Assets/_Code/Core/Tech_HMS/Temporary/PlayerDebris.prefab`; `Assets/_Prefabs/Characters/PlayerCharacter.prefab`; `Assets/_Scenes/Tech_SYM/Architecture/Session/PlayerRoot.prefab`; `Assets/_Scenes/Tech_SYM/Architecture/Session/ShellRoot.prefab`; `Assets/_Scenes/Tech_SYM/Prefabs/MultiFuncPlatforms.prefab`; `Assets/_Scenes/Tech_SYM/Prefabs/OnionShell_Heavy.prefab`; `Assets/_Scenes/Tech_SYM/01_BrokenablePlatformTest.unity`
- 코드 소비: `Assets/_Code/Core/Tech_HMS/Features/CharacterMovement.cs`; `Assets/_Code/Core/Tech_HMS/Features/MovingPlatform.cs`; `Assets/_Code/Core/Tech_HMS/Features/PlatformRigidbodyMovement.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Session/PlayerBridgeService.cs`; `Assets/_Code/Core/Tech_SYM/Platforms/KinematicPlatformMotion.cs`

### PlatformRigidbodyMovement.cs

- GUID: `3f822b7584e4b904eb02f212fc1dd958`; SHA-256: `a9c3f44ad6700b3be532063b4b234948dce6d707895c4c01d65dfe44e2b2d99c`.
- 선언 타입: PlatformRigidbodyMovement
- 직접 자산: `Assets/_Scenes/Tech_SYM/Architecture/Session/ShellRoot.prefab`; `Assets/_Scenes/Tech_SYM/Prefabs/MultiFuncPlatforms.prefab`; `Assets/_Scenes/Tech_SYM/Prefabs/OnionShell_Heavy.prefab`
- 코드 소비: `Assets/_Code/Core/Tech_SYM/Architecture/Session/PlayerBridgeService.cs`; `Assets/_Code/Core/Tech_SYM/Platforms/KinematicPlatformMotion.cs`; `Assets/_Code/Core/Tech_SYM/Platforms/PeriodicPlatform.cs`

### PlayerAnimationController.cs

- GUID: `dcd3b42d5a7f42aab76d5470b77c0326`; SHA-256: `a0324127e51b69509e770df0d2da10ac9f73463e8c1ecffcbe02eb2e98b95e50`.
- 선언 타입: PlayerAnimationController
- 직접 자산: `Assets/_Prefabs/Characters/PlayerCharacter.prefab`; `Assets/_Scenes/Tech_SYM/Architecture/Session/PlayerRoot.prefab`
- 코드 소비: `Assets/_Code/Core/Tech_HMS/Features/PlayerAnimatorOutput.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Session/PlayerBridgeService.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Session/PlayerInteractionService.cs`

### PlayerAnimationPlayback.cs

- GUID: `d6757d4cf5c4489e932795e43eda11ce`; SHA-256: `9483609cf244e8af32c21e4030228e24e0894191287bd582ea40cf685ebecff5`.
- 선언 타입: PlayerAnimationPlayback
- 직접 자산: 없음
- 코드 소비: `Assets/_Code/Core/Tech_HMS/Features/PlayerAnimationController.cs`

### PlayerAnimationSet.cs

- GUID: `d73b75e14bfe417e82a327a728880005`; SHA-256: `a1f22f9336279a5b80b1cd7b61bedfbbdc98f75c3e602dadadd715e8eea84ad3`.
- 선언 타입: PlayerAnimationSet
- 직접 자산: `Assets/_Code/Core/Tech_HMS/Animation/PlayerAnimations.asset`; `Assets/_Scenes/Tech_SYM/Architecture/Session/PrototypePlayerAnimations.asset`
- 코드 소비: `Assets/_Code/Core/Tech_HMS/Editor/PlayerAnimationSetupEditor.cs`; `Assets/_Code/Core/Tech_HMS/Features/PlayerAnimatorOutput.cs`

### PlayerAnimationTypes.cs

- GUID: `85243017f8a04e748edb3ddab33062a7`; SHA-256: `dca0829d3470f04daf6d109554b828943438ee93667608a00ce87caee4bd7d8f`.
- 선언 타입: PlayerAnimationId, PlayerAnimationEndReason, PlayerAnimationRequest
- 직접 자산: 없음
- 코드 소비: `Assets/_Code/Core/Tech_HMS/Features/PlayerAnimationController.cs`; `Assets/_Code/Core/Tech_HMS/Features/PlayerAnimationPlayback.cs`; `Assets/_Code/Core/Tech_HMS/Features/PlayerAnimationSet.cs`; `Assets/_Code/Core/Tech_HMS/Features/PlayerAnimatorOutput.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Session/PlayerBridgeService.cs`

### PlayerAnimatorOutput.cs

- GUID: `500136630c1f4664a8446ac9ccee8130`; SHA-256: `964c6c30aaa3d43ba1d635c5b8d3140c250864181bc46c71566b8e66be150aea`.
- 선언 타입: PlayerAnimatorOutput
- 직접 자산: `Assets/_Prefabs/Characters/PlayerCharacter.prefab`; `Assets/_Scenes/Tech_SYM/Architecture/Session/PlayerRoot.prefab`
- 코드 소비: `Assets/_Code/Core/Tech_HMS/Editor/PlayerAnimationSetupEditor.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Session/PlayerBridgeService.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Session/SessionActorHost.cs`

### PlayerCameraController.cs

- GUID: `07b2370774df0f748abe295ed7e0e2bf`; SHA-256: `6e0e2953384a2b3aa0bdbaeeeee780dec6d88a6f28de36a109cbb0108b5f2410`.
- 선언 타입: PlayerCameraController
- 직접 자산: `Assets/_Scenes/Tech_HMS/01_PlayerMovementTest.unity`; `Assets/_Scenes/Tech_SYM/Architecture/01_Bootstrapper.unity`; `Assets/_Scenes/Tech_SYM/Prefabs/MultiFuncPlatforms.prefab`; `Assets/_Scenes/Tech_SYM/01_BrokenablePlatformTest.unity`
- 코드 소비: `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/AppComposition.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/CoreComposition.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/CoreWorldBinding.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Level/LevelCameraService.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Session/SessionActorHost.cs`

### PlayerController.cs

- GUID: `7530312256ebc5846a7f81b12578fc36`; SHA-256: `8aadb5868492ce8e1850d2444fbcae16bd9447fa4cddc0c0ed7ec0e6c1bacaa7`.
- 선언 타입: PlayerController
- 직접 자산: `Assets/_Prefabs/Characters/PlayerCharacter.prefab`
- 코드 소비: `Assets/_Code/Core/Tech_SYM/Architecture/Session/SessionActorHost.cs`

### PlayerDebris.cs

- GUID: `0f826893d1275d2409de2c3fa13ceb11`; SHA-256: `94b32d179926a58db304a8a17685cd8b4a4e1f5247921a563c8fa2dd57ce47e4`.
- 선언 타입: PlayerDebris
- 직접 자산: `Assets/_Code/Core/Tech_HMS/Temporary/PlayerDebris.prefab`; `Assets/_Scenes/Tech_SYM/Architecture/Session/ShellRoot.prefab`
- 코드 소비: `Assets/_Code/Core/Tech_HMS/Features/PlayerFormController.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Contracts/SessionContracts.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/SceneComponentLookup.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Level/CameraZoneTrigger.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Level/StageService.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Level/StageTrigger.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Session/PlayerBridgeService.cs`; `Assets/_Code/Core/Tech_SYM/Features/ShatteredPlatform.cs`; `Assets/_Code/Core/Tech_SYM/Platforms/PeriodicPlatform.cs`; `Assets/_Code/Core/Tech_SYM/Platforms/PressurePlatePlatform.cs`

### PlayerFacade.cs

- GUID: `50300de8796b58c4eb5b7c11b292338d`; SHA-256: `3bbf29ae501987ea98c37f14dc922965cb26418785ffc57394fd251d1be2f12f`.
- 선언 타입: PlayerFacade
- 직접 자산: `Assets/_Prefabs/Characters/PlayerCharacter.prefab`; `Assets/_Scenes/Tech_HMS/01_PlayerMovementTest.unity`; `Assets/_Scenes/Tech_SYM/Architecture/Session/PlayerRoot.prefab`; `Assets/_Scenes/Tech_SYM/Prefabs/MultiFuncPlatforms.prefab`; `Assets/_Scenes/Tech_SYM/01_BrokenablePlatformTest.unity`; `Assets/_Scenes/Tech_SYM/03_InGame.unity`
- 코드 소비: `Assets/_Code/Core/Tech_HMS/Editor/PlayerAnimationSetupEditor.cs`; `Assets/_Code/Core/Tech_HMS/Features/PlayerAnimationController.cs`; `Assets/_Code/Core/Tech_HMS/Features/PlayerController.cs`; `Assets/_Code/Core/Tech_HMS/Features/PlayerMovementModeController.cs`; `Assets/_Code/Core/Tech_HMS/Temporary/PlayerSkillHUD.cs`; `Assets/_Code/Core/Tech_HMS/Temporary/TestPlayerFunc.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Chase/ChaseRuntimeDriver.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Contracts/SessionContracts.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/GameSessionRuntime.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/SceneComponentLookup.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Level/CameraZoneTrigger.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Level/ChaseRetreatGate.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Level/LevelCameraService.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Level/StageService.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Level/StageTrigger.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Session/PlayerBridgeService.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Session/PlayerDamagePresentation.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Session/SessionActorHost.cs`; `Assets/_Code/Core/Tech_SYM/Enemies/Combat/EnemyContactSensor.cs`; `Assets/_Code/Core/Tech_SYM/Enemies/Runtime/EnemyActor.cs`; `Assets/_Code/Core/Tech_SYM/Features/ShatteredPlatform.cs`; `Assets/_Code/Core/Tech_SYM/Features/TearFragment.cs`; `Assets/_Code/Core/Tech_SYM/Platforms/DamageFloorPlatform.cs`; `Assets/_Code/Core/Tech_SYM/Platforms/FloatingPlatform.cs`; `Assets/_Code/Core/Tech_SYM/Platforms/PeriodicPlatform.cs`; `Assets/_Code/Core/Tech_SYM/Platforms/PressurePlatePlatform.cs`

### PlayerFormController.cs

- GUID: `2c79c9fdfbefde0449bd0292f0b8aabc`; SHA-256: `5698c879a64e235348c58114559db757e750ccf1bacf41259089816acb6bf68f`.
- 선언 타입: PlayerForm, PlayerFormController
- 직접 자산: `Assets/_Prefabs/Characters/PlayerCharacter.prefab`; `Assets/_Scenes/Tech_SYM/Architecture/Session/PlayerRoot.prefab`; `Assets/_Scenes/Tech_SYM/Prefabs/MultiFuncPlatforms.prefab`; `Assets/_Scenes/Tech_SYM/01_BrokenablePlatformTest.unity`
- 코드 소비: `Assets/_Code/Core/Tech_HMS/Features/PlayerAnimationController.cs`; `Assets/_Code/Core/Tech_HMS/Features/PlayerAnimatorOutput.cs`; `Assets/_Code/Core/Tech_HMS/Features/PlayerDebris.cs`; `Assets/_Code/Core/Tech_HMS/Features/PlayerFacade.cs`; `Assets/_Code/Core/Tech_HMS/Features/RestoreFormSkill.cs`; `Assets/_Code/Core/Tech_HMS/Features/ShrinkSkill.cs`; `Assets/_Code/Core/Tech_HMS/Temporary/PlayerSkillHUD.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Level/StageService.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Level/StageTrigger.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Session/PlayerBridgeService.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Session/SessionActorHost.cs`; `Assets/_Code/Core/Tech_SYM/Features/ShatteredPlatform.cs`; `Assets/_Code/Core/Tech_SYM/Platforms/PressurePlatePlatform.cs`

### PlayerInputReader.cs

- GUID: `58ea903c9c1b36a47a3c46615d2afdc6`; SHA-256: `e5e12685e9b8233220030e2f40bf111702e941defba9ff988739dba3067e3896`.
- 선언 타입: PlayerInputReader
- 직접 자산: `Assets/_Prefabs/Characters/PlayerCharacter.prefab`; `Assets/_Scenes/Tech_SYM/Architecture/Session/PlayerRoot.prefab`; `Assets/_Scenes/Tech_SYM/Prefabs/MultiFuncPlatforms.prefab`; `Assets/_Scenes/Tech_SYM/01_BrokenablePlatformTest.unity`
- 코드 소비: `Assets/_Code/Core/Tech_HMS/Features/PlayerController.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Session/GameplayInputRouter.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Session/SessionActorHost.cs`

### PlayerModel.cs

- GUID: `f254d91ac622b5b4799316409e03026c`; SHA-256: `e0feefecd5188b0a5acd7cb96fcc698e96d49e1118ece0296a57bab785300e7f`.
- 선언 타입: IReadOnlyPlayerModel, PlayerModel
- 직접 자산: 없음
- 코드 소비: `Assets/_Code/Core/Tech_HMS/Features/PlayerFacade.cs`; `Assets/_Code/Core/Tech_HMS/Features/TearSkill.cs`; `Assets/_Code/Core/Tech_HMS/Temporary/PlayerSkillHUD.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Session/PlayerBridgeService.cs`

### PlayerMovementModeController.cs

- GUID: `fab50331435effe48b45692b6126069c`; SHA-256: `1286311741bcafaa8038a5298b5ddf2f1efc6601119e584f75046433fbdafe1f`.
- 선언 타입: PlayerMovementMode, PlayerMovementModeController
- 직접 자산: `Assets/_Prefabs/Characters/PlayerCharacter.prefab`; `Assets/_Scenes/Tech_HMS/01_PlayerMovementTest.unity`; `Assets/_Scenes/Tech_SYM/Architecture/Session/PlayerRoot.prefab`; `Assets/_Scenes/Tech_SYM/Prefabs/MultiFuncPlatforms.prefab`; `Assets/_Scenes/Tech_SYM/01_BrokenablePlatformTest.unity`
- 코드 소비: `Assets/_Code/Core/Tech_HMS/Features/PlayerAnimationController.cs`; `Assets/_Code/Core/Tech_HMS/Features/PlayerCameraController.cs`; `Assets/_Code/Core/Tech_HMS/Temporary/TestPlayerMovementMode.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/CoreWorldBinding.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Level/LevelCameraService.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Session/PlayerBridgeService.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Session/SessionActorHost.cs`

### PlayerSkill.cs

- GUID: `abf4dc2c98368b24ba589fe2b6e0ad7a`; SHA-256: `90072904ca1a31e64d246491cab4994075a9da03c133406b0f08009b094ebf3c`.
- 선언 타입: PlayerSkill
- 직접 자산: 없음
- 코드 소비: `Assets/_Code/Core/Tech_HMS/Features/PlayerFacade.cs`; `Assets/_Code/Core/Tech_HMS/Features/PlayerSkillController.cs`; `Assets/_Code/Core/Tech_HMS/Features/RestoreFormSkill.cs`; `Assets/_Code/Core/Tech_HMS/Features/ShrinkSkill.cs`; `Assets/_Code/Core/Tech_HMS/Features/SkillSlot.cs`; `Assets/_Code/Core/Tech_HMS/Features/TearSkill.cs`; `Assets/_Code/Core/Tech_HMS/Temporary/PlayerSkillHUD.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Session/PlayerBridgeService.cs`

### PlayerSkillController.cs

- GUID: `2c9f8830d7c4c28429b72d36b782e8ab`; SHA-256: `2887c291978f06e4d1d9503b7f569ed4b8878e1f07aa803e1c75b77b9f88d8fe`.
- 선언 타입: PlayerSkillController
- 직접 자산: `Assets/_Prefabs/Characters/PlayerCharacter.prefab`; `Assets/_Scenes/Tech_SYM/Architecture/Session/PlayerRoot.prefab`; `Assets/_Scenes/Tech_SYM/Prefabs/MultiFuncPlatforms.prefab`; `Assets/_Scenes/Tech_SYM/01_BrokenablePlatformTest.unity`
- 코드 소비: `Assets/_Code/Core/Tech_HMS/Features/PlayerFacade.cs`; `Assets/_Code/Core/Tech_HMS/Temporary/PlayerSkillHUD.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Session/PlayerBridgeService.cs`

### PlayerSkillPresentation.cs

- GUID: `e7ff012368234197b6dc75bb6b9a03ec`; SHA-256: `36a965cbfa0ccfb97d2e2cdbfdb30a8b28219485d2629117dd58326e6177cff1`.
- 선언 타입: PlayerSkillPresentation
- 직접 자산: 없음
- 코드 소비: `Assets/_Code/Core/Tech_HMS/Features/PlayerFacade.cs`

### PlayerSkillTypes.cs

- GUID: `607e713466ad9c34293cb98c49315d32`; SHA-256: `f84a460d0f3904075d9101681ce255ea03a7821eeb0eba53edc5bbc567c46120`.
- 선언 타입: PlayerSkillId, PlayerSkillPhase, PlayerSkillBlockReason
- 직접 자산: 없음
- 코드 소비: `Assets/_Code/Core/Tech_HMS/Features/PlayerAnimationController.cs`; `Assets/_Code/Core/Tech_HMS/Features/PlayerFacade.cs`; `Assets/_Code/Core/Tech_HMS/Features/PlayerFormController.cs`; `Assets/_Code/Core/Tech_HMS/Features/PlayerSkill.cs`; `Assets/_Code/Core/Tech_HMS/Features/PlayerSkillController.cs`; `Assets/_Code/Core/Tech_HMS/Features/PlayerSkillPresentation.cs`; `Assets/_Code/Core/Tech_HMS/Features/RestoreFormSkill.cs`; `Assets/_Code/Core/Tech_HMS/Features/RestoreFormSkillDefinition.cs`; `Assets/_Code/Core/Tech_HMS/Features/ShrinkSkill.cs`; `Assets/_Code/Core/Tech_HMS/Features/ShrinkSkillDefinition.cs`; `Assets/_Code/Core/Tech_HMS/Features/SkillDefinition.cs`; `Assets/_Code/Core/Tech_HMS/Features/SkillSlot.cs`; `Assets/_Code/Core/Tech_HMS/Features/TearSkill.cs`; `Assets/_Code/Core/Tech_HMS/Features/TearSkillDefinition.cs`; `Assets/_Code/Core/Tech_HMS/Temporary/PlayerSkillHUD.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/CoreWorldBinding.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Session/PlayerBridgeService.cs`

### PlayerStateMachine.cs

- GUID: `091ce41155b5469e876472c080f11673`; SHA-256: `98c51393fda397b8439cb2a4f7f2eab0a2d2b7bd0d520c93a1c2888b88e4e4a6`.
- 선언 타입: PlayerState, PlayerStateMachine
- 직접 자산: 없음
- 코드 소비: `Assets/_Code/Core/Tech_HMS/Features/PlayerAnimationController.cs`; `Assets/_Code/Core/Tech_HMS/Features/PlayerAnimationPlayback.cs`; `Assets/_Code/Core/Tech_HMS/Features/PlayerFacade.cs`; `Assets/_Code/Core/Tech_HMS/Temporary/TestPlayerFunc.cs`

### PlayerVisualController.cs

- GUID: `5e748b3691e789a4a9079e92d67750ef`; SHA-256: `e87df5c20e525b2085c4012d5514ab7547499a235e94e848dc869e80c9a1fd35`.
- 선언 타입: PlayerVisualController
- 직접 자산: `Assets/_Prefabs/Characters/PlayerCharacter.prefab`; `Assets/_Scenes/Tech_SYM/Architecture/Session/PlayerRoot.prefab`; `Assets/_Scenes/Tech_SYM/Prefabs/MultiFuncPlatforms.prefab`; `Assets/_Scenes/Tech_SYM/01_BrokenablePlatformTest.unity`
- 코드 소비: 다른 C#에서 발견되지 않음. Unity 콜백/직렬화 사용은 별도.

### RestoreFormSkill.cs

- GUID: `9b30bc8ac1622574db76cbe14367f367`; SHA-256: `493b42c3701c56de3af9e92715c67ec9b2dd40782cdfe09c419384934b5aa847`.
- 선언 타입: RestoreFormSkill
- 직접 자산: 없음
- 코드 소비: `Assets/_Code/Core/Tech_HMS/Features/PlayerFacade.cs`

### RestoreFormSkillDefinition.cs

- GUID: `e18de49b614945a439eca458f0e74c0a`; SHA-256: `ffb3278c30635ca1c25dc2a53189a31f1b14c68b8258461ac4d7b251248cc189`.
- 선언 타입: RestoreFormSkillDefinition
- 직접 자산: `Assets/_Code/Core/Tech_HMS/Temporary/TestRestoreFormSkillDefinition.asset`; `Assets/_Scenes/Tech_SYM/Architecture/Session/RestoreSkill.asset`
- 코드 소비: `Assets/_Code/Core/Tech_HMS/Features/PlayerFacade.cs`; `Assets/_Code/Core/Tech_HMS/Features/RestoreFormSkill.cs`

### ShrinkSkill.cs

- GUID: `2654f13d67cf5f8409700b67e4e47e6d`; SHA-256: `ac1998933786334a852ffbf37781d87009e97600e02147570914c8c86a258ab1`.
- 선언 타입: ShrinkSkill
- 직접 자산: 없음
- 코드 소비: `Assets/_Code/Core/Tech_HMS/Features/PlayerFacade.cs`

### ShrinkSkillDefinition.cs

- GUID: `b0909ba18ea8ff74da72b37793e68f65`; SHA-256: `8b3afd4da6addac1c2768b17d5616df00afa02eb1adb455458c9e1dfd02c9b2f`.
- 선언 타입: ShrinkSkillDefinition
- 직접 자산: `Assets/_Code/Core/Tech_HMS/Temporary/TestShrinkSkillDifinition.asset`; `Assets/_Scenes/Tech_SYM/Architecture/Session/ShrinkSkill.asset`
- 코드 소비: `Assets/_Code/Core/Tech_HMS/Features/PlayerFacade.cs`; `Assets/_Code/Core/Tech_HMS/Features/ShrinkSkill.cs`

### SkillDefinition.cs

- GUID: `98842c3749c180b408e6d8604e649f12`; SHA-256: `6fb0e5337626e23fe5afea113dc1c34bccb846648fe5a430b01562736b554a5e`.
- 선언 타입: SkillDefinition
- 직접 자산: 없음
- 코드 소비: `Assets/_Code/Core/Tech_HMS/Features/PlayerSkill.cs`; `Assets/_Code/Core/Tech_HMS/Features/RestoreFormSkillDefinition.cs`; `Assets/_Code/Core/Tech_HMS/Features/ShrinkSkillDefinition.cs`; `Assets/_Code/Core/Tech_HMS/Features/TearSkillDefinition.cs`

### SkillSlot.cs

- GUID: `927e893a87c918247986256d6aff3e9a`; SHA-256: `c5d9c2f87c82932c6c5cc2156cd0fc5763b1fe7786da7a8e7983c4d4dfcdc577`.
- 선언 타입: SkillSlot
- 직접 자산: 없음
- 코드 소비: `Assets/_Code/Core/Tech_HMS/Features/PlayerSkillController.cs`

### StunEffectOrbit.cs

- GUID: `95e2681448a92664aac532f6bf335f7d`; SHA-256: `9b1f6ebf645b3cdf86f67859b94561ee8b9f394fbb74670c46e499a97b2673a0`.
- 선언 타입: StunEffectOrbit
- 직접 자산: `Assets/_Prefabs/Characters/Enemy/StunEffect_Dummy.prefab`
- 코드 소비: `Assets/_Code/Core/Tech_HMS/Features/EnemyStunVisual.cs`

### TearSkill.cs

- GUID: `764dbedc08f5c394f8cc8d7f9a4cbb34`; SHA-256: `32ea7a41f9a53e0f623dd1cef0c57ea5995d71b991d56408e0a9d214624122e7`.
- 선언 타입: TearSkill
- 직접 자산: 없음
- 코드 소비: `Assets/_Code/Core/Tech_HMS/Features/PlayerFacade.cs`; `Assets/_Code/Core/Tech_HMS/Features/PlayerInputReader.cs`; `Assets/_Code/Core/Tech_HMS/Temporary/PlayerSkillHUD.cs`; `Assets/_Code/Core/Tech_SYM/Architecture/Session/PlayerBridgeService.cs`

### TearSkillDefinition.cs

- GUID: `368dfbb29c0138f4abf76032049cfabe`; SHA-256: `1d8e59edfa776e140edf0a3ce5bdb7d3c275165f8fe937d7c23e96a5c4d87a35`.
- 선언 타입: TearSkillDefinition
- 직접 자산: `Assets/_Code/Core/Tech_HMS/Temporary/TestTearSkillDifinition.asset`; `Assets/_Scenes/Tech_SYM/Architecture/Session/TearSkill.asset`
- 코드 소비: `Assets/_Code/Core/Tech_HMS/Features/PlayerFacade.cs`; `Assets/_Code/Core/Tech_HMS/Features/TearSkill.cs`
