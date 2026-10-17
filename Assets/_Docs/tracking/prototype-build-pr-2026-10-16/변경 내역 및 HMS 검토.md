# 누적 로컬 변경과 HMS 검토

기준: d79a670 → 이번 작업트리. 과거 커밋의 HMS 확장을 새 변경으로 재집계하지 않는다. 최신 코드 diff와 10/14 메시 발판 작업 기록을 대조했다. 이번 빌드·PR 단계에서 HMS 코드를 새로 수정하지 않았다.

## HMS: CharacterMovement.cs

경로: Assets/_Code/Core/Tech_HMS/Features/CharacterMovement.cs. 순변경 +11/-0줄.

- 파일 상단에 ICharacterGroundContactFilter 인터페이스를 추가했다. AllowsGroundContact에 실제 surface, 플레이어 capsule, 월드 point/normal, HMS maximumAngle을 전달한다.
- RefreshGround에서 Collider 활성/Trigger 및 충돌 무시 검사 다음에 필터를 조회한다. attachedRigidbody가 있으면 그 몸체에서, 없으면 Collider 객체에서 GetComponent한다. 필터가 있고 접촉을 거절한 경우만 continue한다.
- 목적은 플랫폼 측 실제 메시 지지면·플레이어 아래 반구 조건과 HMS 접지 판정을 일치시키는 것이다. 소비 구현은 SYM Platforms/PlatformContacts.cs의 AllowsGroundContact다. 플레이어가 구체 SYM 클래스를 참조하지 않는다.
- 필터가 없는 일반 표면은 기존 처리를 유지한다. 기존 최대 경사각, 발판 대비 상승 속도, 이전 지지면 우선, GroundCollider/GroundNormal/IsGrounded 최종 결정, 점프는 HMS에 남는다.
- 입력·이동 축 변환, 카메라 조작, 점프 공식 또는 기본 속도 변경은 이 파일 diff에 없다.

## HMS: OilSurface.cs

경로: Assets/_Code/Core/Tech_HMS/Features/OilSurface.cs. 순변경 +22/-32줄.

- RequireComponent(BoxCollider) 및 Awake/Reset의 강제 isTrigger=true를 없애고 명시 Collider oilTrigger를 받는다. 지원 형태를 Box 전용에서 유효한 같은 객체의 Collider로 확장한다. MeshCollider는 Convex와 sharedMesh가 있어야 한다.
- OnEnable은 잘못 연결된 Trigger를 오류로 알린다. 비어 있으면 효과를 적용하지 않는다. oilTrigger 및 효과 시간에 한국어 InspectorLabel/Tooltip을 제공한다.
- TryApplyEffect는 자신의 유효성·활성, 상대 Collider의 존재/활성/비Trigger, IgnoreCollision, IgnoreLayerCollision을 검사한다. ComputePenetration으로 명시 기름 영역의 실제 겹침을 확인한 뒤 상대 attachedRigidbody의 CharacterSlipperyEffect.Apply를 호출한다.
- 목적은 Pan의 지지면과 Oil 효과 영역을 분리하고 같은 Rigidbody의 다른 영역에서 전달된 콜백을 오인하지 않는 것이다. Enter/Stay 갱신과 기본3초 효과는 유지한다.
- 호환 주의: 기존 Box 자동 생성/Trigger 강제 변경을 기대하던 프리팹은 oilTrigger를 명시 연결해야 한다. 기존 메시 발판 자산 이관 기록에서 해당 연결을 확인했으며 사용자 새 배치에도 같은 계약을 적용한다.

## 나머지 코드·자산 변경을 읽는 순서

| 사용자 문제 | 주요 변경 파일/영역 | 결과와 이유 |
|---|---|---|
| 체크포인트/시점 설정 중복 | StageService, StageTrigger, LevelCameraService, StageTriggerEditor | 시작/끝 쌍과 구간 표현을 Stage가 소유, Trigger는 접촉 요청. Zone/Marker/위치 경계 제거 |
| Retry/Title 수명 혼동 | GameFlowService, SceneService, GameSessionRuntime, CoreWorldBinding, SessionActorHost | Retry는 같은 Stage의 스냅샷 복원, Title은 세션·Stage/Core 해제. 등장/복귀는 Capsule 중심 |
| 같은 음악 재시작 | AudioFlowBinding, AudioService, IAudioPlaybackDriver/UnityAudioPlaybackDriver | 실제 Clip 요청과 동일 Clip 유지 |
| 메시 발판 접지/운반 | PlatformContacts, KinematicPlatformMotion, FloatingPlatform | 접촉쌍·다중 지지면·몸체별 운반과 실제 착지점 사용 |
| 주기/피해 판정 혼용 | PeriodicPlatform, MeshCapsuleGeometry, DamageFloorPlatform, DamageTriggerRelay | 실제 메시/캡슐 교차, 지지면과 피해 Trigger 분리 |
| 플레이어 표현/수치 작성 | PlayerPresentationReferences, PlayerVfxPresenter, PlayerTuningEditors | 기존 상태/애니메이션/VFX 연결, 표시 담당의 자기 수명 관리, 조절 수치의 의미 표시 |
| 버튼 전체 하강 | PressurePlatePlatform 및 버튼 prefab | Cap 동적 몸체/충돌면, Body 고정 몸체 분리 |
| 작성 대상/실행 대상 혼동 | _Prefabs 역할별 경로, 01_Stage_Prototype, 설치/Entry | 기존 기능 프리팹 이관, 새 목업/6구간/12CP·Memo 연결, 실제 실행 Stage 한 개 |
| 외관 | Lighting/PostProcessing, LookProfiles, Core RenderSettings, Bootstrap Camera override | 실시간 조명·Bloom·색보정·옅은 Fog. 연출 코드 추가 없음 |
| 빌드 재현 | Cooked_Prototype_Windows Build Profile | 지정한4씬·Windows64 PC 빌드 |

## 이동·삭제·기존 편집 구분

삭제 메타36개를 신규 메타와 대조했을 때29개는 동일 GUID의 새 경로가 있다. PlayerDamagePresentation → PlayerPresentationReferences도 동일 GUID를 사용하는 표현 책임 교체다. 삭제 표기만으로 전체 기능 삭제로 해석하지 않는다. 같은 신규 GUID 목적지가 없는 항목은 CameraZoneTrigger, CheckpointMarker, 옛 Samples/Bootstrap/Title/InGame 씬4개, 기존 ShellRoot prefab이다. 현재 껍질은 기존 Onion_Shell을 재사용한다. retired GUID의 남은 자산 참조는 보존용 Architecture/Prototype/1_Stage의 CheckpointMarker이며, 기존 Missing2 기록에 대응한다. 이번4씬 빌드에는 포함하지 않는다.

기존 로컬 아트/import 순변경은 pf_gratan/pf_pot_body의 preBakeTriangleCollisionMesh 0→1, MAT_Ring_Soft의 TRITONE 키워드다. ProBuilder 모드/도형/팔레트 설정과 Palette, TMP 동적 폰트 직렬화도 포함한다. 이번 단계에서 보호 원본을 새로 편집하지 않았으며 각 실제 작성 주체는 미확정이다. 이전 상태의 형식/줄바꿈만 다른 파일은 Git 정규화 후 순변경에 없을 수 있다.

사용자 미저장 Plane/ProBuilder 레벨 편집은 외관 단계에서 사본으로 보존했다. 이번 현재 상태 빌드·커밋 요청에 따라 추가 사본을 남긴 뒤 Stage를 저장했다. 이전 외관 작업의 '원본 저장 대기'는 당시 상태이며 이번 저장 이후 상태와 구별한다.

## 검증 근거와 한계

10/14 메시 검증은 비Play63통과/1미평가 기록이며 당시 자연 물리 QA와 다르다. 10/15~16 실제 Editor 입력·물리 통합55+보완5 기록, 12CP 동선, 이후 외관 Play의 점프/대표 발판/Retry/Title을 관련 검증 문서로 연결한다. 이 수치들을 Windows Player 전체 통과율로 합산하지 않는다. Patrol 배치 오류, 백페이스, Placeholder Memo, E 기절 관측과 사람 QA는 별도 미완료다. 이번 빌드 결과는 테스트 및 검증 항목.md에 기록한다.
