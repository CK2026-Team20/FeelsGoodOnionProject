# 인스펙터 직렬화 필드 감사

Assets 전체 C# 328개를 분류했다. 자체 코드 242개(SYM 196, HMS 41, 공용 인터페이스 5), Nova 아트/vendor 55, DOTween vendor 17, RealToon vendor 12, Unity 템플릿 TutorialInfo 2다. 외부 플러그인과 아트 원본은 자동 설명 추가에서 제외하며 TutorialInfo는 게임 기능이 아닌 템플릿 안내다. Unity 기본 컴포넌트 필드는 자체 코드 감사와 구분한다.

실제 SerializedObject.NextVisible로 노출 경로 397개를 수집하고 선언 경로+선언 타입+필드 기준으로 355개를 집계했다. SYM 254개 중 누락 0, HMS 101개 중 누락 62다. 배열 컨테이너/원소 노출은 별도로 유지하며 중복 선언으로 세지 않는다. 배열 길이만 SerializedPropertyType.ArraySize로 제외하고 EnemyOilOnDeath.size(Vector2)는 포함한다.

기존 SYM 누락 179개 중 178개에 Tooltip을 추가하고 메모 closeButton 1개를 삭제했다. 함께 선언된 21필드는 이름·형식·순서·초기값을 유지하며 개별 선언으로 분리했다. 기존 오안내 2개(bodyOffset, MemoUIController.player)도 소비 코드에 맞춰 고쳤다. 캐릭터 설정·SO·부모 필드·중첩 Audio/Entry/CameraZone/SlotView를 포함한다. SkillDefinition의 선언 경로와 각 파생 노출 타입을 구분한다.

SerializeReference, field:SerializeField, 자체 PropertyDrawer는 현재 소스 검색에서 없다. HMS PlayerAnimatorOutputEditor는 DrawDefaultInspector로 기본 속성을 표시한다. 일반 DTO 공개 필드와 enum value__는 Inspector 설정으로 세지 않는다. 초기 반사 후보422/전체반사482는 실제 조절 필드 수가 아니다. Editor 전용 PrototypePlayerStunProbe는 AddComponent 생성이 거부됐고 선언 직렬화 필드가 0이므로 명시 제외했다. 실패한 최초 수집 기록을 보존하며 최종 제외 후 수집 오류는 0이다.

| 선언 경로 | 선언 타입·필드 | 분류 | 실제 Tooltip |
|---|---|---|---|
| `Assets/_Code/Core/Tech_HMS/Features/CharacterMovement.cs` | `CharacterMovement.airAcceleration` | setting-or-container | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/CharacterMovement.cs` | `CharacterMovement.airDeceleration` | setting-or-container | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/CharacterMovement.cs` | `CharacterMovement.allowDepthMovement` | setting-or-container | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/CharacterMovement.cs` | `CharacterMovement.allowHorizontalMovement` | setting-or-container | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/CharacterMovement.cs` | `CharacterMovement.externalDeceleration` | setting-or-container | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/CharacterMovement.cs` | `CharacterMovement.fallGravityMultiplier` | setting-or-container | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/CharacterMovement.cs` | `CharacterMovement.gravity` | setting-or-container | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/CharacterMovement.cs` | `CharacterMovement.groundAcceleration` | setting-or-container | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/CharacterMovement.cs` | `CharacterMovement.groundDeceleration` | setting-or-container | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/CharacterMovement.cs` | `CharacterMovement.groundStickSpeed` | setting-or-container | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/CharacterMovement.cs` | `CharacterMovement.jumpHeight` | setting-or-container | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/CharacterMovement.cs` | `CharacterMovement.maximumFallSpeed` | setting-or-container | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/CharacterMovement.cs` | `CharacterMovement.maximumGroundAngle` | setting-or-container | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/CharacterMovement.cs` | `CharacterMovement.moveSpeed` | setting-or-container | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/CharacterSlipperyEffect.cs` | `CharacterSlipperyEffect.accelerationMultiplier` | setting-or-container | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/CharacterSlipperyEffect.cs` | `CharacterSlipperyEffect.decelerationMultiplier` | setting-or-container | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/EnemyStunVisual.cs` | `EnemyStunVisual.additionalTexturePropertyNames` | setting-or-container | 같은 텍스처로 함께 변경할 속성입니다. 별도 음영 텍스처를 사용하는 셰이더에 설정합니다. |
| `Assets/_Code/Core/Tech_HMS/Features/EnemyStunVisual.cs` | `EnemyStunVisual.materialIndex` | setting-or-container | Texture를 변경할 Material Slot입니다. |
| `Assets/_Code/Core/Tech_HMS/Features/EnemyStunVisual.cs` | `EnemyStunVisual.normalTexture` | reference | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/EnemyStunVisual.cs` | `EnemyStunVisual.stunEffect` | reference | Stun 여부에 따라 활성화/비활성화되는 비주얼 목적 오브젝트입니다. (StunEffectOrbit 컴포넌트의 부착은 필수가 아닙니다.) |
| `Assets/_Code/Core/Tech_HMS/Features/EnemyStunVisual.cs` | `EnemyStunVisual.stunnedTexture` | reference | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/EnemyStunVisual.cs` | `EnemyStunVisual.targetRenderer` | reference | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/EnemyStunVisual.cs` | `EnemyStunVisual.texturePropertyName` | setting-or-container | 기본 텍스처 속성입니다. URP Lit은 _BaseMap, Unity Toon은 _MainTex를 사용합니다. |
| `Assets/_Code/Core/Tech_HMS/Features/MovingPlatform.cs` | `MovingPlatform.endpointWaitTime` | setting-or-container | 각 끝점에 도착한 뒤 기다릴 시간(초). 시간은 물리 스텝 단위로 처리됩니다. |
| `Assets/_Code/Core/Tech_HMS/Features/MovingPlatform.cs` | `MovingPlatform.maximumSupportAngle` | setting-or-container | 위쪽 지지 접촉으로 인정할 최대 각도. 평평한 Cube에서는 기본값을 유지합니다. |
| `Assets/_Code/Core/Tech_HMS/Features/MovingPlatform.cs` | `MovingPlatform.moveSpeed` | setting-or-container | 플랫폼 이동 속도(m/s). 0이면 현재 위치에서 정지합니다. |
| `Assets/_Code/Core/Tech_HMS/Features/MovingPlatform.cs` | `MovingPlatform.travelOffset` | setting-or-container | 시작 위치에서 끝 위치까지의 월드 이동량. Cube 크기와 무관합니다. |
| `Assets/_Code/Core/Tech_HMS/Features/OilSurface.cs` | `OilSurface.effectDuration` | setting-or-container | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/PlatformRigidbodyMovement.cs` | `PlatformRigidbodyMovement.gravity` | setting-or-container | 직접 적용하는 아래쪽 중력 가속도(m/s²). Rigidbody의 Use Gravity는 꺼집니다. |
| `Assets/_Code/Core/Tech_HMS/Features/PlatformRigidbodyMovement.cs` | `PlatformRigidbodyMovement.groundStickSpeed` | setting-or-container | 지지면 접촉 유지를 위한 작은 아래쪽 상대 속도(m/s). |
| `Assets/_Code/Core/Tech_HMS/Features/PlatformRigidbodyMovement.cs` | `PlatformRigidbodyMovement.maximumFallSpeed` | setting-or-container | 공중에서의 최대 낙하 속도(m/s). |
| `Assets/_Code/Core/Tech_HMS/Features/PlatformRigidbodyMovement.cs` | `PlatformRigidbodyMovement.maximumGroundAngle` | setting-or-container | 바닥으로 인정할 최대 각도. 평평한 Cube 테스트에서는 기본값을 유지합니다. |
| `Assets/_Code/Core/Tech_HMS/Features/PlatformRigidbodyMovement.cs` | `PlatformRigidbodyMovement.outOfWorldHeight` | setting-or-container | 이 월드 Y 아래로 떨어지면 FellOutOfWorld 이벤트만 발생시킵니다. 자동 삭제하지 않습니다. |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerAnimationController.cs` | `PlayerAnimationController.logPlaybackRequests` | setting-or-container | 실제 클립 연결 전 재생 요청 변경을 Console에서 확인합니다. |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerAnimationController.cs` | `PlayerAnimationController.walkMode` | setting-or-container | 걷기 애니메이션을 사용할 이동 모드입니다. 나머지 모드는 달리기를 사용합니다. (실제 이동 속도와는 관련 X) |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerAnimationSet.cs` | `PlayerAnimationSet.blendDuration` | setting-or-container | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerAnimationSet.cs` | `PlayerAnimationSet.controllerTemplate` | reference | 제공된 PlayerAnimationTemplate을 연결합니다. |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerAnimationSet.cs` | `PlayerAnimationSet.dead` | reference | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerAnimationSet.cs` | `PlayerAnimationSet.fall` | reference | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerAnimationSet.cs` | `PlayerAnimationSet.idle` | reference | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerAnimationSet.cs` | `PlayerAnimationSet.jump` | reference | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerAnimationSet.cs` | `PlayerAnimationSet.knockback` | reference | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerAnimationSet.cs` | `PlayerAnimationSet.land` | reference | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerAnimationSet.cs` | `PlayerAnimationSet.restoreForm` | reference | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerAnimationSet.cs` | `PlayerAnimationSet.run` | reference | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerAnimationSet.cs` | `PlayerAnimationSet.runReferenceSpeed` | setting-or-container | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerAnimationSet.cs` | `PlayerAnimationSet.shrink` | reference | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerAnimationSet.cs` | `PlayerAnimationSet.tear` | reference | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerAnimationSet.cs` | `PlayerAnimationSet.walk` | reference | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerAnimationSet.cs` | `PlayerAnimationSet.walkReferenceSpeed` | setting-or-container | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerAnimatorOutput.cs` | `PlayerAnimatorOutput.animationSet` | reference | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerAnimatorOutput.cs` | `PlayerAnimatorOutput.animatorOverride` | reference | 비워두면 PlayerFormController의 현재 형태 Animator를 사용합니다. 외형 하나만 쓰는 구성에서는 직접 지정할 수 있습니다. |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerCameraController.cs` | `PlayerCameraController.backCamera` | reference | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerCameraController.cs` | `PlayerCameraController.backFixedCamera` | reference | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerCameraController.cs` | `PlayerCameraController.movementModeController` | reference | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerCameraController.cs` | `PlayerCameraController.quarterCamera` | reference | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerCameraController.cs` | `PlayerCameraController.selectedPriority` | setting-or-container | 현재 모드에 해당하는 카메라의 우선순위. |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerCameraController.cs` | `PlayerCameraController.sideCamera` | reference | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerCameraController.cs` | `PlayerCameraController.standbyPriority` | setting-or-container | 다른 모드의 카메라에 적용할 우선순위. |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerFacade.cs` | `PlayerFacade.initialHP` | setting-or-container | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerFacade.cs` | `PlayerFacade.initialSkillFragment` | setting-or-container | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerFacade.cs` | `PlayerFacade.invincible` | setting-or-container | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerFacade.cs` | `PlayerFacade.maxHP` | setting-or-container | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerFacade.cs` | `PlayerFacade.restoreFormSkillDefinition` | reference | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerFacade.cs` | `PlayerFacade.shrinkSkillDefinition` | reference | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerFacade.cs` | `PlayerFacade.tearSkillDefinition` | reference | 사전 생성된 ScriptableObject를 할당 |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerFormController.cs` | `PlayerFormController.debrisPrefab` | reference | PlayerDebris와 플랫폼 이동 처리가 붙은 활성 프리팹입니다. |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerFormController.cs` | `PlayerFormController.debrisRecoveryDistance` | setting-or-container | 플레이어와 껍질의 루트 위치 사이 회수 가능 거리입니다. 월드 단위입니다. |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerFormController.cs` | `PlayerFormController.normalJumpHeight` | setting-or-container | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerFormController.cs` | `PlayerFormController.restoreCheckTolerance` | setting-or-container | 바닥과의 미세한 겹침 허용 거리입니다. 월드 단위입니다. |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerFormController.cs` | `PlayerFormController.smallHeight` | setting-or-container | 작은 형태의 캡슐 전체 높이입니다. 지름 이상이어야 합니다. |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerFormController.cs` | `PlayerFormController.smallJumpHeight` | setting-or-container | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerFormController.cs` | `PlayerFormController.smallRadius` | setting-or-container | 작은 형태의 캡슐 반경입니다. 루트 로컬 단위입니다. |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerFormController.cs` | `PlayerFormController.visualRoot` | reference | 비워두면 VisualRoot라는 직계 자식을 찾습니다. |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerMovementModeController.cs` | `PlayerMovementModeController.backMoveSpeed` | setting-or-container | 백뷰 모드의 최대 자체 이동 속도(m/s). |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerMovementModeController.cs` | `PlayerMovementModeController.initialMode` | setting-or-container | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerMovementModeController.cs` | `PlayerMovementModeController.quarterMoveSpeed` | setting-or-container | 쿼터 모드의 최대 자체 이동 속도(m/s). 사이드 속도 이하로 제한합니다. |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerMovementModeController.cs` | `PlayerMovementModeController.sideMoveSpeed` | setting-or-container | 사이드 모드의 최대 자체 이동 속도(m/s). |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerVisualController.cs` | `PlayerVisualController.playerVisualRoot` | reference | 모델의 정면이 로컬 +Z를 향하도록 구성한 외형 루트. |
| `Assets/_Code/Core/Tech_HMS/Features/RestoreFormSkillDefinition.cs` | `RestoreFormSkillDefinition.preparationTime` | setting-or-container | 사용 요청 수락 후 실제 복귀까지 걸리는 시간입니다. (단위: 초) |
| `Assets/_Code/Core/Tech_HMS/Features/ShrinkSkillDefinition.cs` | `ShrinkSkillDefinition.preparationTime` | setting-or-container | 사용 요청 수락 후 실제 형태 전환까지 걸리는 시간입니다. (단위: 초) |
| `Assets/_Code/Core/Tech_HMS/Features/SkillDefinition.cs` | `SkillDefinition.cooldown` | setting-or-container | 스킬이 실제로 발동한 뒤 적용할 쿨다운입니다. (단위: 초) |
| `Assets/_Code/Core/Tech_HMS/Features/SkillDefinition.cs` | `SkillDefinition.icon` | reference | 슬롯 UI에 표시할 스킬 이미지입니다. |
| `Assets/_Code/Core/Tech_HMS/Features/SkillDefinition.cs` | `SkillDefinition.initCooldown` | setting-or-container | 장착 직후에만 적용할 쿨다운입니다. 음수이면 기본 쿨다운을 동일하게 사용합니다. (단위: 초) |
| `Assets/_Code/Core/Tech_HMS/Features/StunEffectOrbit.cs` | `StunEffectOrbit.rotationAxis` | setting-or-container | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/StunEffectOrbit.cs` | `StunEffectOrbit.rotationSpeed` | setting-or-container | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Features/TearSkillDefinition.cs` | `TearSkillDefinition.effectRadius` | setting-or-container | 효과 중심을 기준으로 스턴 대상을 검사할 반경입니다. (단위: Unity 월드 단위) |
| `Assets/_Code/Core/Tech_HMS/Features/TearSkillDefinition.cs` | `TearSkillDefinition.preparationTime` | setting-or-container | 사용 요청 수락 후 실제 발동까지 걸리는 시간입니다. (단위: 초) |
| `Assets/_Code/Core/Tech_HMS/Features/TearSkillDefinition.cs` | `TearSkillDefinition.requiredFragments` | setting-or-container | 스킬을 한 번 발동할 때 소비하는 조각 수입니다. |
| `Assets/_Code/Core/Tech_HMS/Features/TearSkillDefinition.cs` | `TearSkillDefinition.stunDuration` | setting-or-container | 대상에게 적용할 스턴 지속 시간입니다. (단위: 초) |
| `Assets/_Code/Core/Tech_HMS/Temporary/PlayerSkillHUD.cs` | `PlayerSkillHUD.debrisDarkOverlay` | reference | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Temporary/PlayerSkillHUD.cs` | `PlayerSkillHUD.debrisPanel` | reference | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Temporary/PlayerSkillHUD.cs` | `PlayerSkillHUD.fragmentCountText` | reference | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Temporary/PlayerSkillHUD.cs` | `PlayerSkillHUD.player` | reference | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Temporary/PlayerSkillHUD.cs` | `PlayerSkillHUD.slotViews` | setting-or-container | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Temporary/PlayerSkillHUD.cs` | `PlayerSkillHUD.tearPanel` | reference | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Temporary/PlayerSkillHUD.cs` | `PlayerSkillHUD+SlotView.cooldownOverlay` | reference | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Temporary/PlayerSkillHUD.cs` | `PlayerSkillHUD+SlotView.cooldownText` | reference | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Temporary/PlayerSkillHUD.cs` | `PlayerSkillHUD+SlotView.icon` | reference | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Temporary/PlayerSkillHUD.cs` | `PlayerSkillHUD+SlotView.root` | reference | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_HMS/Temporary/PlayerSkillHUD.cs` | `PlayerSkillHUD+SlotView.slotIndex` | setting-or-container | 누락: 보호 영역 허용 대기 |
| `Assets/_Code/Core/Tech_SYM/Architecture/Audio/AudioCueCatalog.cs` | `Cooked.Audio.AudioCueCatalog.entries` | setting-or-container | 게임에서 사용할 소리 목록입니다. 각 항목에 고유 ID와 재생 파일을 연결하세요. 빈 목록이면 재생할 소리가 없습니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Audio/AudioCueCatalog.cs` | `Cooked.Audio.AudioCueCatalog+Entry.clip` | reference | 이 항목에서 재생할 AudioClip 파일입니다. 비워 두면 소리 목록 등록에 실패합니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Audio/AudioCueCatalog.cs` | `Cooked.Audio.AudioCueCatalog+Entry.gain` | setting-or-container | 이 소리의 기본 음량 배율입니다(0~1). 0은 무음, 1은 최대 기본 음량입니다. 사용자 음량 설정이 추가로 적용됩니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Audio/AudioCueCatalog.cs` | `Cooked.Audio.AudioCueCatalog+Entry.id` | setting-or-container | 소리를 요청할 때 사용하는 고유 ID입니다. 호출 코드의 이름과 정확히 일치해야 하며 중복하면 등록에 실패합니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Audio/AudioCueCatalog.cs` | `Cooked.Audio.AudioCueCatalog+Entry.kind` | setting-or-container | 음악 또는 효과음 분류입니다. 선택한 분류의 음량 설정이 이 소리에 적용됩니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Audio/AudioRuntimeHost.cs` | `Cooked.Audio.AudioRuntimeHost.catalog` | reference | 소리 ID와 실제 AudioClip을 연결한 목록 자산입니다. 게임에서 요청하는 ID가 이 목록에 있어야 재생됩니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Audio/AudioRuntimeHost.cs` | `Cooked.Audio.AudioRuntimeHost.sources` | reference | 소리를 재생할 AudioSource 목록입니다. 서로 다른 AudioSource를 정확히 10개 연결해야 합니다. 음악·효과음 재생에 사용하며 null이나 중복 참조는 허용하지 않습니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Chase/ChaseDefinition.cs` | `Cooked.Chase.ChaseDefinition.captureGap` | setting-or-container | 추격 시작을 허용할 때 경로 끝에 남겨 두는 거리입니다(월드 단위, 0 이상 8 미만). 높이면 경로 끝에 더 많은 여유가 필요합니다. 현재 포획은 앞쪽 트리거의 실제 접촉으로 판정하며 이 값만으로 포획하지 않습니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Chase/ChaseDefinition.cs` | `Cooked.Chase.ChaseDefinition.speed` | setting-or-container | 추격 무리가 경로를 따라 이동하는 속도입니다(월드 단위/초, 0보다 커야 함). 높이면 플레이어에게 더 빨리 접근합니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Chase/ChaseFrontContact.cs` | `Cooked.Chase.ChaseFrontContact.driver` | reference | 이 앞쪽 접촉 트리거의 포획을 처리하는 추격 담당입니다. 동일 추격 무리의 ChaseRuntimeDriver를 연결하세요. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Chase/ChaseRuntimeDriver.cs` | `Cooked.Chase.ChaseRuntimeDriver.definition` | reference | 추격 속도와 시작 거리 조건을 제공하는 설정 자산입니다. 무리의 밸런스는 이 자산에서 조절하세요. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Chase/ChaseRuntimeDriver.cs` | `Cooked.Chase.ChaseRuntimeDriver.presentation` | reference | 추격 무리의 등장·상하 움직임·퇴장 외형을 표현할 컴포넌트입니다. 포획 판정 자체는 바꾸지 않습니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Chase/ChaseSwarmPresentation.cs` | `Cooked.Chase.ChaseSwarmPresentation.appearSeconds` | setting-or-container | 등장 크기 변화 시간입니다(초, 최소 0.01). 높이면 더 천천히 나타납니다. 월드 정지 중에는 멈춥니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Chase/ChaseSwarmPresentation.cs` | `Cooked.Chase.ChaseSwarmPresentation.bobHalfPeriod` | setting-or-container | 상하 흔들림의 편도 시간입니다(초, 최소 0.01). 높이면 더 느리게 흔들리며 왕복은 이 시간의 두 배입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Chase/ChaseSwarmPresentation.cs` | `Cooked.Chase.ChaseSwarmPresentation.bobHeight` | setting-or-container | 기본 외형 위치에서 위로 흔들리는 거리입니다(로컬 단위, 0 이상). 0이면 상하 흔들림을 만들지 않습니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Chase/ChaseSwarmPresentation.cs` | `Cooked.Chase.ChaseSwarmPresentation.exitSeconds` | setting-or-container | 퇴장 크기 변화 시간입니다(초, 최소 0.01). 높이면 더 천천히 사라집니다. 월드 정지와 별개로 진행하지만 옵션 중에는 멈춥니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Chase/ChaseSwarmPresentation.cs` | `Cooked.Chase.ChaseSwarmPresentation.visual` | reference | 등장·퇴장 크기 변화와 상하 흔들림을 적용할 외형 자식입니다. 논리·접촉 트리거 루트 대신 외형만 연결하세요. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Cinematics/CinematicArtworkClip.cs` | `Cooked.Cinematics.CinematicArtworkClip.artwork` | reference | 이 타임라인 구간에 전체 화면으로 표시할 원화 텍스처입니다. 원화는 지정 순서대로 한 장씩 표시됩니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Cinematics/CinematicArtworkClip.cs` | `Cooked.Cinematics.CinematicArtworkClip.endScale` | setting-or-container | 이전 자산 호환용 값입니다(1~1.1). 현재 컷신은 항상 배율 1로 표시하므로 변경해도 확대·트윈 효과가 생기지 않습니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Cinematics/CinematicCatalog.cs` | `Cooked.Cinematics.CinematicCatalog.ending` | reference | 탈출 완료 후 재생할 엔딩 Timeline 자산입니다. 완료 후 타이틀로 돌아갑니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Cinematics/CinematicCatalog.cs` | `Cooked.Cinematics.CinematicCatalog.opening` | reference | 새 게임 시작 시 재생할 오프닝 Timeline 자산입니다. 원화의 순서와 표시 시간은 이 자산에서 편집하세요. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Cinematics/CinematicHost.cs` | `Cooked.Cinematics.CinematicHost.catalog` | reference | 오프닝·엔딩 Timeline을 지정한 목록 자산입니다. 게임 흐름에 사용할 두 연출을 이 자산에서 선택합니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Cinematics/CinematicHost.cs` | `Cooked.Cinematics.CinematicHost.director` | reference | 오프닝·엔딩 Timeline을 재생하는 PlayableDirector입니다. 현재 컷신 오브젝트의 재생 담당을 연결하세요. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Cinematics/CinematicHost.cs` | `Cooked.Cinematics.CinematicHost.frameBridge` | reference | 타임라인의 원화를 화면에 전달하는 연결 컴포넌트입니다. 같은 컷신의 CinematicFrameBridge를 연결하세요. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Cinematics/CinematicHost.cs` | `Cooked.Cinematics.CinematicHost.view` | reference | 전체 화면 원화와 스킵 버튼을 표시하는 화면입니다. 이 컷신과 함께 사용할 CinematicView를 연결하세요. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Cinematics/CinematicView.cs` | `Cooked.Cinematics.CinematicView.panelArtwork` | reference | 원화 텍스처를 표시할 RawImage 목록입니다. 현재 첫 번째 이미지만 사용하며 나머지는 숨깁니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Cinematics/CinematicView.cs` | `Cooked.Cinematics.CinematicView.panelViewports` | reference | 원화를 표시할 화면 영역 목록입니다. 현재 단일 원화 방식에서는 첫 번째 영역을 전체 화면으로 사용합니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Cinematics/CinematicView.cs` | `Cooked.Cinematics.CinematicView.skipButton` | reference | 현재 컷신을 완료 처리하고 건너뛰는 버튼입니다. 메모 닫기 버튼과는 별도 기능입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Cinematics/CinematicView.cs` | `Cooked.Cinematics.CinematicView.visibility` | reference | 컷신 화면 전체의 표시와 입력 허용을 제어하는 CanvasGroup입니다. 원화와 스킵 버튼을 포함한 그룹을 연결하세요. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Dialogue/DialogueView.cs` | `Cooked.Dialogue.DialogueView.autoButton` | reference | 대화 자동 진행을 켜고 끄는 버튼입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Dialogue/DialogueView.cs` | `Cooked.Dialogue.DialogueView.autoLabel` | reference | 자동 진행 상태를 표시할 TMP 텍스트입니다. 자동 버튼의 상태 안내에 사용됩니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Dialogue/DialogueView.cs` | `Cooked.Dialogue.DialogueView.body` | reference | 현재 대화 문장을 순차 표시할 TMP 텍스트입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Dialogue/DialogueView.cs` | `Cooked.Dialogue.DialogueView.history` | reference | 이전에 표시한 문장 로그를 출력할 TMP 텍스트입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Dialogue/DialogueView.cs` | `Cooked.Dialogue.DialogueView.historyButton` | reference | 현재 대화의 지난 문장 로그를 여는 버튼입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Dialogue/DialogueView.cs` | `Cooked.Dialogue.DialogueView.logCloseButton` | reference | 대화 로그만 닫고 본문으로 돌아가는 버튼입니다. 메모 닫기 기능과는 다릅니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Dialogue/DialogueView.cs` | `Cooked.Dialogue.DialogueView.logGroup` | reference | 지난 대화 로그 화면의 표시·입력을 제어할 CanvasGroup입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Dialogue/DialogueView.cs` | `Cooked.Dialogue.DialogueView.logScroll` | reference | 긴 대화 로그를 스크롤할 ScrollRect입니다. 로그 화면의 스크롤 영역을 연결하세요. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Dialogue/DialogueView.cs` | `Cooked.Dialogue.DialogueView.nextMarker` | reference | 문장 표시가 끝나 다음 입력을 기다릴 때 보이는 안내 오브젝트입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Dialogue/DialogueView.cs` | `Cooked.Dialogue.DialogueView.panelButton` | reference | 대화 본문 클릭으로 표시 완료 또는 다음 문장 진행을 요청하는 버튼입니다. 한 입력에 한 번만 진행합니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Dialogue/DialogueView.cs` | `Cooked.Dialogue.DialogueView.rootGroup` | reference | 대화 본문 화면의 표시·입력을 제어할 CanvasGroup입니다. 로그 그룹과 구분해 연결하세요. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Dialogue/DialogueView.cs` | `Cooked.Dialogue.DialogueView.skipButton` | reference | 현재 대화 시퀀스를 건너뛰는 버튼입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Dialogue/DialogueView.cs` | `Cooked.Dialogue.DialogueView.speaker` | reference | 현재 문장의 화자 이름을 표시할 TMP 텍스트입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Foundation/ArchitectureInstallation.cs` | `Cooked.Foundation.ArchitectureInstallation.bootstrapPath` | setting-or-container | 부트스트래퍼 씬의 프로젝트 상대 경로입니다. Assets/부터 .unity까지 입력하며 실제 씬 경로와 일치해야 합니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Foundation/ArchitectureInstallation.cs` | `Cooked.Foundation.ArchitectureInstallation.corePath` | setting-or-container | 플레이어·공통 시스템을 유지할 Core 씬의 프로젝트 상대 경로입니다. 스테이지 씬과 구분하세요. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Foundation/ArchitectureInstallation.cs` | `Cooked.Foundation.ArchitectureInstallation.stagePaths` | setting-or-container | 등록할 스테이지 씬의 프로젝트 상대 경로 목록입니다. 첫 항목이 새 게임의 첫 스테이지이며 실제 씬 경로를 지정해야 합니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Foundation/ArchitectureInstallation.cs` | `Cooked.Foundation.ArchitectureInstallation.titlePath` | setting-or-container | 게임 타이틀 씬의 프로젝트 상대 경로입니다. 시작·엔딩 후 돌아갈 실제 씬 경로를 지정하세요. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Foundation/Bootstrapper.cs` | `Cooked.Foundation.Bootstrapper.dialogueTable` | reference | 대화 ID·화자·문장을 담은 JSON TextAsset입니다. 시작 시 검증하므로 현재 대화 테이블 자산을 연결하세요. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/AppComposition.cs` | `Cooked.Integration.AppComposition.audio` | reference | 앱의 음악·효과음을 재생할 AudioRuntimeHost입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/AppComposition.cs` | `Cooked.Integration.AppComposition.bootstrap` | reference | 앱 시작과 공통 서비스 준비를 담당하는 Bootstrapper입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/AppComposition.cs` | `Cooked.Integration.AppComposition.installation` | reference | 부트·타이틀·Core·스테이지 경로를 지정한 설치 자산입니다. 실제 씬 흐름을 이 자산에서 설정합니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/AppComposition.cs` | `Cooked.Integration.AppComposition.ui` | reference | 앱 수명 동안 유지할 옵션·화면 전환 UI 루트입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/CoreComposition.cs` | `Cooked.Integration.CoreComposition.actor` | reference | 세션 플레이어를 생성·복원·제거할 SessionActorHost입니다. 씬에 직접 배치한 중복 플레이어 대신 이 담당을 사용합니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/CoreComposition.cs` | `Cooked.Integration.CoreComposition.cameraService` | reference | 트리거 구간별 카메라와 이동축 전환을 담당하는 컴포넌트입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/CoreComposition.cs` | `Cooked.Integration.CoreComposition.chase` | reference | 마지막 구간의 추격 무리를 갱신하고 포획을 전달할 담당입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/CoreComposition.cs` | `Cooked.Integration.CoreComposition.cinematic` | reference | 오프닝·엔딩 재생을 담당하는 CinematicHost입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/CoreComposition.cs` | `Cooked.Integration.CoreComposition.dialogueDriver` | reference | 대화 문장 진행을 갱신하는 담당입니다. 해당 Core의 DialogueDriver를 연결하세요. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/CoreComposition.cs` | `Cooked.Integration.CoreComposition.dialogueView` | reference | 현재 대화와 로그를 보여 줄 화면입니다. 해당 Core의 DialogueView를 연결하세요. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/CoreComposition.cs` | `Cooked.Integration.CoreComposition.entries` | reference | 직접 씬 진입 때 사용할 체크포인트·조각·해금 능력 설정 자산입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/CoreComposition.cs` | `Cooked.Integration.CoreComposition.gameplayCamera` | reference | 실제 인게임 화면과 월드 안내를 투영할 카메라입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/CoreComposition.cs` | `Cooked.Integration.CoreComposition.ui` | reference | Core HUD와 월드 안내를 세션 상태에 연결하는 담당입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/CoreUiBinding.cs` | `Cooked.Integration.CoreUiBinding.gameplayCamera` | reference | 월드 안내를 바라보게 할 실제 인게임 카메라입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/CoreUiBinding.cs` | `Cooked.Integration.CoreUiBinding.instructionDistance` | setting-or-container | 플레이어 주변에서 조작 설명을 찾는 최대 거리입니다(월드 단위, 최소 1). 높이면 더 먼 설명도 표시됩니다. 여러 설명 중 가장 가까운 것을 선택합니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/CoreUiBinding.cs` | `Cooked.Integration.CoreUiBinding.instructionView` | reference | 주변 기믹의 조작 설명을 표시하는 별도 안내 화면입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/CoreUiBinding.cs` | `Cooked.Integration.CoreUiBinding.ui` | reference | 체력·조각 HUD와 월드 상호작용 안내를 포함한 GameUiRoot입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/InstructionView.cs` | `Cooked.Integration.InstructionView.group` | reference | 조작 설명의 표시 여부를 제어할 CanvasGroup입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/InstructionView.cs` | `Cooked.Integration.InstructionView.label` | reference | 가장 가까운 기믹의 조작 설명을 출력할 TMP 텍스트입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/IntegrationEntrySettings.cs` | `Cooked.Integration.IntegrationEntrySettings.entries` | setting-or-container | 스테이지 직접 진입용 초기 설정 목록입니다. 각 Stage Id는 중복 없이 한 번만 등록해야 합니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/IntegrationEntrySettings.cs` | `Cooked.Integration.IntegrationEntrySettings+Entry.abilities` | setting-or-container | 직접 진입 시 이미 해금할 능력 목록입니다. 비어 있으면 능력이 잠겨 있으며 이후 체크포인트에서 해금됩니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/IntegrationEntrySettings.cs` | `Cooked.Integration.IntegrationEntrySettings+Entry.checkpointId` | setting-or-container | 직접 진입 시 사용할 체크포인트 ID입니다. 해당 스테이지에 등록된 Checkpoint Id와 일치해야 합니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/IntegrationEntrySettings.cs` | `Cooked.Integration.IntegrationEntrySettings+Entry.fragments` | setting-or-container | 직접 진입 시 보유할 눈물 조각 수입니다(0~5개). 0은 조각 없음이며 5개를 모아야 눈물을 사용할 수 있습니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Integration/Runtime/IntegrationEntrySettings.cs` | `Cooked.Integration.IntegrationEntrySettings+Entry.stageId` | setting-or-container | 진입할 스테이지의 고유 ID입니다. 해당 StageService의 Stage Id와 정확히 일치해야 합니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/CameraZoneTrigger.cs` | `Cooked.Level.CameraZoneTrigger.zone` | setting-or-container | 이 트리거에 들어왔을 때 적용할 카메라 구간 데이터입니다. 이동 모드와 보간 정렬 위치를 함께 설정하세요. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/ChaseRetreatGate.cs` | `Cooked.Level.ChaseRetreatGate.blocker` | reference | 추격 시작 뒤 뒤로 돌아가지 못하도록 활성화할 BoxCollider입니다. 플레이어와 겹치지 않는 위치의 차단 콜라이더를 연결하세요. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/CheckpointMarker.cs` | `Cooked.Level.CheckpointMarker.bodyOffset` | setting-or-container | 체크포인트 표식에서 플레이어 물리 본체 중심까지의 월드 좌표 이동량입니다(월드 단위). XYZ를 표식의 월드 위치에 그대로 더하며 표식 회전으로 돌리지 않습니다. Y를 높이면 더 높은 위치에 복원합니다. 모두 0이면 표식 위치에 복원하므로 바닥과 겹치지 않게 설정하세요. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/CheckpointMarker.cs` | `Cooked.Level.CheckpointMarker.cameraZone` | setting-or-container | 이 체크포인트 복원 시 사용할 카메라 구간 데이터입니다. 체크포인트 위치에 맞는 이동 모드와 카메라 위치를 설정하세요. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/CheckpointMarker.cs` | `Cooked.Level.CheckpointMarker.checkpointId` | setting-or-container | 이 체크포인트의 고유 ID입니다. 같은 스테이지에서 중복하지 않으며 트리거와 초기 진입 설정의 ID가 일치해야 합니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/CheckpointMarker.cs` | `Cooked.Level.CheckpointMarker.unlockForm` | setting-or-container | 켜면 이 체크포인트 도달 시 Q 상태 전환과 자동 껍질 회수 능력을 함께 해금합니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/CheckpointMarker.cs` | `Cooked.Level.CheckpointMarker.unlockTear` | setting-or-container | 켜면 이 체크포인트 도달 시 F 눈물 능력을 해금합니다. 사용에는 눈물 조각 5개가 필요합니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/LevelCameraService.cs` | `Cooked.Level.CameraZoneData.AllowDepth` | setting-or-container | Movement가 Legacy일 때만 사용합니다. 켜면 쿼터뷰(X/Z), 끄면 사이드뷰(X)로 해석합니다. 명시한 이동 모드에는 영향이 없습니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/LevelCameraService.cs` | `Cooked.Level.CameraZoneData.Forward` | setting-or-container | 기존 구간 데이터의 진행 방향입니다. 현재 트리거 기반 전환에서는 이 값으로 카메라 방향을 자동 선택하지 않습니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/LevelCameraService.cs` | `Cooked.Level.CameraZoneData.HalfWidth` | setting-or-container | 기존 구간 데이터의 반폭입니다(월드 단위). 현재 트리거 기반 전환의 접촉 폭은 Collider로 조절하며 이 값은 접촉 폭을 바꾸지 않습니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/LevelCameraService.cs` | `Cooked.Level.CameraZoneData.Id` | setting-or-container | 카메라 구간의 고유 이름입니다. 같은 이름의 구간으로 다시 진입하면 전환을 반복하지 않으므로 서로 다른 구간은 이름을 구분하세요. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/LevelCameraService.cs` | `Cooked.Level.CameraZoneData.LookOffset` | setting-or-container | 추적 대상에서 카메라가 바라볼 지점까지의 월드 위치 차이입니다. 카메라 위치가 아니라 시선 목표를 조절합니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/LevelCameraService.cs` | `Cooked.Level.CameraZoneData.MaximumProgress` | setting-or-container | 기존 구간의 최대 진행값입니다. 현재 트리거 기반 전환에서는 자동 구간 탐색에 사용하지 않습니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/LevelCameraService.cs` | `Cooked.Level.CameraZoneData.MinimumProgress` | setting-or-container | 기존 구간의 최소 진행값입니다. 현재 트리거 기반 전환에서는 자동 구간 탐색에 사용하지 않습니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/LevelCameraService.cs` | `Cooked.Level.CameraZoneData.Movement` | setting-or-container | 이 구간의 이동 방식입니다. Side는 월드 X, Corridor는 월드 Z, Quarter는 X/Z 이동입니다. Legacy는 Allow Depth를 따릅니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/LevelCameraService.cs` | `Cooked.Level.CameraZoneData.Offset` | setting-or-container | 추적 대상에서 카메라까지의 월드 위치 차이입니다. X/Y/Z를 바꾸면 카메라의 좌우·높이·앞뒤 위치가 달라집니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/LevelCameraService.cs` | `Cooked.Level.CameraZoneData.Origin` | setting-or-container | 구간 기준 월드 위치입니다. 보간 전환 시 사이드뷰는 이 위치의 Z, 통로는 X로 플레이어를 정렬합니다. 즉시 적용(snap)에서는 플레이어 위치를 정렬하지 않습니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/LevelCameraService.cs` | `Cooked.Level.LevelCameraService.blendSeconds` | setting-or-container | 보간 전환의 카메라 위치·시선과 플레이어 평면 정렬 시간입니다(초, 최소 0.01). 높이면 더 천천히 전환하며 전환 중 이동 입력을 잠급니다. 즉시 적용(snap)에서는 이 시간을 사용하지 않습니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/LevelCameraService.cs` | `Cooked.Level.LevelCameraService.cameraRig` | reference | 위치와 회전을 갱신할 인게임 카메라 Transform입니다. 플레이어 추적 기준점과 구분하세요. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/LevelCameraService.cs` | `Cooked.Level.LevelCameraService.zones` | setting-or-container | 이전 구간 등록 데이터입니다. 현재 전환은 트리거 또는 체크포인트가 전달한 구간을 사용하며 이 목록만 수정해 자동 전환하지 않습니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/LevelInstruction.cs` | `Cooked.Level.LevelInstruction.text` | setting-or-container | 주변 플레이어에게 표시할 기믹 조작 설명입니다. 화면 표시용이며 실제 입력 키나 기능 규칙을 변경하지 않습니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/LevelTearFragment.cs` | `Cooked.Level.LevelTearFragment.pickupRoot` | reference | 조각 획득 후 숨길 오브젝트입니다. 하위 감지 트리거만이 아니라 조각 전체 루트를 연결하세요. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/OvenHandle.cs` | `Cooked.Level.OvenHandle.tray` | reference | 이 손잡이를 E로 조작했을 때 열고 닫을 오븐 트레이입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/ShellPressureLatch.cs` | `Cooked.Level.ShellPressureLatch.crossingProgress` | setting-or-container | 퍼즐 통과로 인정할 진행 위치입니다(경로 월드 거리, 경로 없으면 스테이지 로컬 X). 작아진 플레이어의 진행값이 이 값 이상이고 껍질이 발판을 누르면 문이 열립니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/ShellPressureLatch.cs` | `Cooked.Level.ShellPressureLatch.plate` | reference | 양파껍질의 눌림 상태를 확인할 압력 발판입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/ShellPressureLatch.cs` | `Cooked.Level.ShellPressureLatch.plateSurface` | reference | 실제 껍질이 발판 위에 있는지 확인할 표면 Collider입니다. 발판 외형 대신 충돌 표면을 연결하세요. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/ShellPressureLatch.cs` | `Cooked.Level.ShellPressureLatch.raisedGate` | reference | 껍질로 발판을 누르고 통과 조건을 만족하면 비활성화할 문 오브젝트입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/ShellPressureLatch.cs` | `Cooked.Level.ShellPressureLatch.stage` | reference | 플레이어의 경로 진행값을 계산할 소속 StageService입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/StageService.cs` | `Cooked.Level.StageService.chaseCheckpointId` | setting-or-container | 추격 시작 조건과 복원 위치를 확인할 체크포인트 ID입니다. 이 스테이지의 등록 ID와 일치해야 합니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/StageService.cs` | `Cooked.Level.StageService.chaseRetreatGate` | reference | 추격 시작 후 뒤쪽 통로를 닫을 담당입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/StageService.cs` | `Cooked.Level.StageService.chaseStartProgress` | setting-or-container | 추격을 시작할 최소 진행값입니다(경로 월드 거리, 진행 경로 없으면 스테이지 로컬 X). 높이면 더 나중에 추격이 시작됩니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/StageService.cs` | `Cooked.Level.StageService.chaseWaypoints` | setting-or-container | 추격 무리가 이동할 월드 위치 경로입니다. 두 점 이상을 이동 순서대로 지정합니다. 빈 목록은 추격 경로 없음입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/StageService.cs` | `Cooked.Level.StageService.checkpoints` | reference | 이 스테이지의 체크포인트 목록입니다. null 또는 중복 ID가 있으면 등록에 실패합니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/StageService.cs` | `Cooked.Level.StageService.enemies` | reference | 현재 플레이어를 연결할 스테이지 적 목록입니다. 추격 상태를 추가하는 설정이 아닙니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/StageService.cs` | `Cooked.Level.StageService.initialCheckpointId` | setting-or-container | 새 진입 시 사용할 체크포인트 ID입니다. 아래 Checkpoints 목록에 같은 ID가 반드시 있어야 합니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/StageService.cs` | `Cooked.Level.StageService.progressWaypoints` | setting-or-container | 기믹·추격 시작 판단에 사용할 월드 진행 경로입니다. 두 점 이상이면 경로 거리로 계산하며 없으면 스테이지 로컬 X를 사용합니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/StageService.cs` | `Cooked.Level.StageService.stageId` | setting-or-container | 스테이지의 고유 ID입니다. 진입 설정·저장 체크포인트의 Stage Id와 정확히 일치해야 합니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/StageTrigger.cs` | `Cooked.Level.StageTrigger.kind` | setting-or-container | 플레이어 접촉 시 요청할 기능입니다. 체크포인트·대화 등 선택한 종류에 맞게 Value를 지정하세요. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/StageTrigger.cs` | `Cooked.Level.StageTrigger.once` | setting-or-container | 켜면 요청이 성공한 뒤 다시 발동하지 않습니다. 끄면 플레이어가 완전히 나갔다 다시 들어왔을 때 재요청할 수 있습니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/StageTrigger.cs` | `Cooked.Level.StageTrigger.stage` | reference | 이 트리거의 체크포인트·대화·추격·탈출 요청을 처리할 소속 StageService입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/StageTrigger.cs` | `Cooked.Level.StageTrigger.value` | setting-or-container | 요청 대상 ID입니다. Checkpoint는 체크포인트 ID, Dialogue는 대화 ID를 사용합니다. ID를 읽지 않는 종류에서는 값을 바꿔도 영향이 없습니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/TearStunGate.cs` | `Cooked.Level.TearStunGate.barrier` | reference | 폐기된 눈물 문 연결의 이전 장애물 참조입니다. 현재 초기화에서 장애물을 비활성화하며 새 프로토타입에 배치하지 않습니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Level/TearStunGate.cs` | `Cooked.Level.TearStunGate.enemy` | reference | 폐기된 눈물 문 연결의 이전 적 참조입니다. 현재 초기화에서 이 기믹을 종료하며 적 기절로 문을 열지 않습니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Session/ActorFacingPresentation.cs` | `Cooked.Session.ActorFacingPresentation.movement` | reference | 플레이어의 마지막 이동 방향을 읽을 CharacterMovement입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Session/ActorFacingPresentation.cs` | `Cooked.Session.ActorFacingPresentation.visualRoot` | reference | 플레이어 진행 방향에 따라 회전할 외형 자식입니다. 물리 루트 대신 모델 외형을 연결하세요. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Session/ActorFallGuard.cs` | `Cooked.Session.ActorFallGuard.actor` | reference | 아래 낙사 높이를 검사하고 사망시킬 플레이어입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Session/ActorFallGuard.cs` | `Cooked.Session.ActorFallGuard.killHeight` | setting-or-container | 이 값보다 월드 Y 위치가 낮아지면 사망합니다(월드 단위). 값을 높이면 더 높은 위치에서 낙사합니다. 0은 비활성화가 아니라 월드 Y=0 경계입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Session/PlayerDamagePresentation.cs` | `Cooked.Session.PlayerDamagePresentation.damageColor` | setting-or-container | 피격 시 지정할 색상입니다. 현재 빨간색을 사용하며 체력이나 피해량에는 영향을 주지 않습니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Session/PlayerDamagePresentation.cs` | `Cooked.Session.PlayerDamagePresentation.duration` | setting-or-container | 피격 색상 유지 시간입니다(초, 0 이상). 높이면 더 오래 표시하며 0이면 색상 표현을 건너뜁니다. 연속 피격 시 시간을 다시 시작합니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Session/PlayerDamagePresentation.cs` | `Cooked.Session.PlayerDamagePresentation.player` | reference | 실제 피격 이벤트를 받아 색상을 표시할 플레이어입니다. 체력·무적 판정은 이 설정에서 변경하지 않습니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Session/PlayerDamagePresentation.cs` | `Cooked.Session.PlayerDamagePresentation.targets` | reference | 피격 시 색상을 바꿀 Renderer 목록입니다. _BaseColor 또는 _Color를 지원하는 머테리얼만 표시하며 공유 머테리얼 원본은 수정하지 않습니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Session/SessionActorHost.cs` | `Cooked.Session.SessionActorHost.actorPrefab` | reference | 세션에 생성할 플레이어 루트 프리팹입니다. 병합 PlayerFacade·물리·외형 연결이 준비된 프리팹을 지정하세요. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Session/SessionActorHost.cs` | `Cooked.Session.SessionActorHost.transientScope` | reference | 플레이어 껍질·일회성 객체를 세션 종료 때 정리할 소유 범위입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/Session/SessionTransientLifetime.cs` | `Cooked.Session.SessionTransientLifetime.scope` | reference | 이 객체를 세션 종료 시 함께 정리할 소유 범위입니다. 다른 세션의 범위를 연결하지 마세요. |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Composition/GameUiRoot.cs` | `Cooked.UI.GameUiRoot.cinematicMount` | reference | 전체 화면 컷신을 배치할 화면 UI 부모입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Composition/GameUiRoot.cs` | `Cooked.UI.GameUiRoot.dialogueMount` | reference | 대화 화면을 배치할 화면 UI 부모입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Composition/GameUiRoot.cs` | `Cooked.UI.GameUiRoot.hud` | reference | 체력·조각·해금 상태를 표시할 HUD입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Composition/GameUiRoot.cs` | `Cooked.UI.GameUiRoot.prompt` | reference | 상호작용 가능한 월드 대상 위에 표시할 안내 화면입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Composition/GameUiRoot.cs` | `Cooked.UI.GameUiRoot.worldCanvas` | reference | 월드 공간 안내를 표시할 Canvas입니다. 메모 본문용 스크린 Canvas와 구분하세요. |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Composition/GlobalUiRoot.cs` | `Cooked.UI.GlobalUiRoot.fadeView` | reference | 씬 전환 중 화면을 덮는 페이드 화면입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Composition/GlobalUiRoot.cs` | `Cooked.UI.GlobalUiRoot.optionsView` | reference | 음량·재시도·타이틀 이동을 표시할 옵션 화면입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Composition/TitleUiRoot.cs` | `Cooked.UI.TitleUiRoot.view` | reference | 새 게임·설정·종료 버튼을 포함한 타이틀 화면입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/FadeView.cs` | `Cooked.UI.FadeView.cover` | reference | 씬 전환 시 투명도를 조절할 전체 화면 CanvasGroup입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/HudIconGraphic.cs` | `Cooked.UI.HudIconGraphic.fillAmount` | setting-or-container | 체력 아이콘의 채워진 비율입니다(0~1). 0은 비어 있음, 1은 가득 참입니다. 런타임 HUD가 체력에 맞춰 갱신하며 다른 모양에는 체력 채우기를 적용하지 않습니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/HudIconGraphic.cs` | `Cooked.UI.HudIconGraphic.symbol` | setting-or-container | 이 HUD에 그릴 아이콘 모양입니다. 체력·눈물·상태 전환·회수 중 화면 목적에 맞는 모양을 선택하세요. |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/HudView.cs` | `Cooked.UI.HudView.abilities` | reference | 해금 능력 안내를 표시할 기존 TMP 텍스트입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/HudView.cs` | `Cooked.UI.HudView.checkpoint` | reference | 현재 체크포인트 안내를 표시할 TMP 텍스트입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/HudView.cs` | `Cooked.UI.HudView.formCard` | reference | Q 상태 전환 해금 여부를 표시할 카드 그룹입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/HudView.cs` | `Cooked.UI.HudView.fragmentCount` | reference | 현재 눈물 조각 수와 최대 5개를 표시할 TMP 텍스트입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/HudView.cs` | `Cooked.UI.HudView.fragments` | reference | 조각 수를 표시할 기존 TMP 텍스트입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/HudView.cs` | `Cooked.UI.HudView.group` | reference | 게임 HUD 전체의 표시 여부를 제어할 CanvasGroup입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/HudView.cs` | `Cooked.UI.HudView.health` | reference | 체력을 숫자로 표시할 기존 TMP 텍스트입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/HudView.cs` | `Cooked.UI.HudView.hearts` | reference | 체력 순서대로 채울 하트 아이콘 목록입니다. 목록 길이만큼 표시하며 실제 최대 체력을 변경하지 않습니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/HudView.cs` | `Cooked.UI.HudView.recoverCard` | reference | 이전 회수 카드 참조입니다. 현재 별도 회수키 안내를 숨기며 회수는 Q 상태 전환에 포함됩니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/HudView.cs` | `Cooked.UI.HudView.tearCard` | reference | F 눈물 해금 여부를 표시할 카드 그룹입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/OptionsView.cs` | `Cooked.UI.OptionsView.close` | reference | 옵션을 닫는 버튼입니다. 메모 닫기 버튼이 아닙니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/OptionsView.cs` | `Cooked.UI.OptionsView.error` | reference | 설정 저장 실패 등 옵션 오류를 표시할 TMP 텍스트입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/OptionsView.cs` | `Cooked.UI.OptionsView.master` | reference | 전체 음량을 조절할 Slider입니다. 값 0~1을 사용하며 0은 무음입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/OptionsView.cs` | `Cooked.UI.OptionsView.modal` | reference | 옵션 화면의 표시·입력 허용을 제어할 CanvasGroup입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/OptionsView.cs` | `Cooked.UI.OptionsView.music` | reference | 음악 음량을 조절할 Slider입니다. 값 0~1이며 전체 음량과 함께 적용됩니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/OptionsView.cs` | `Cooked.UI.OptionsView.retry` | reference | 저장 체크포인트부터 다시 시작할 버튼입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/OptionsView.cs` | `Cooked.UI.OptionsView.sfx` | reference | 효과음 음량을 조절할 Slider입니다. 값 0~1이며 전체 음량과 함께 적용됩니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/OptionsView.cs` | `Cooked.UI.OptionsView.title` | reference | 현재 세션을 종료하고 타이틀로 이동할 버튼입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/OptionsView.cs` | `Cooked.UI.OptionsView.values` | reference | 현재 음량 설정값을 표시할 TMP 텍스트입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/TitleView.cs` | `Cooked.UI.TitleView.newGame` | reference | 새 게임을 시작할 버튼입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/TitleView.cs` | `Cooked.UI.TitleView.quit` | reference | 게임을 종료할 버튼입니다. Editor에서는 실행 환경에 따라 종료 동작이 다릅니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/TitleView.cs` | `Cooked.UI.TitleView.settings` | reference | 타이틀에서 옵션 화면을 여는 버튼입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/WorldPromptView.cs` | `Cooked.UI.WorldPromptView.anchor` | reference | 월드 대상 위치로 이동시킬 안내 RectTransform입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/WorldPromptView.cs` | `Cooked.UI.WorldPromptView.group` | reference | 월드 상호작용 안내의 표시 여부를 제어할 CanvasGroup입니다. |
| `Assets/_Code/Core/Tech_SYM/Architecture/UI/Views/WorldPromptView.cs` | `Cooked.UI.WorldPromptView.label` | reference | 상호작용 E 안내를 표시할 TMP 텍스트입니다. |
| `Assets/_Code/Core/Tech_SYM/Enemies/Combat/EnemyContactSensor.cs` | `FeelsGoodOnion.TechSYM.Enemies.EnemyContactSensor.owner` | reference | 이 센서의 생존/전투 상태를 소유한 적입니다. |
| `Assets/_Code/Core/Tech_SYM/Enemies/Data/EnemyDefinition.cs` | `FeelsGoodOnion.TechSYM.Enemies.EnemyDefinition.contactDamage` | setting-or-container | 플레이어에게 접촉당 한 번 가하는 피해입니다. |
| `Assets/_Code/Core/Tech_SYM/Enemies/Data/EnemyDefinition.cs` | `FeelsGoodOnion.TechSYM.Enemies.EnemyDefinition.controlLockMilliseconds` | setting-or-container | 넉백 시 이동·점프 제어 제한 시간(ms). 0이면 새 제한을 요청하지 않습니다. |
| `Assets/_Code/Core/Tech_SYM/Enemies/Data/EnemyDefinition.cs` | `FeelsGoodOnion.TechSYM.Enemies.EnemyDefinition.floatAmplitude` | setting-or-container | 중심에서 위아래로 움직이는 최대 거리(m)입니다. 0이면 부유하지 않습니다. |
| `Assets/_Code/Core/Tech_SYM/Enemies/Data/EnemyDefinition.cs` | `FeelsGoodOnion.TechSYM.Enemies.EnemyDefinition.floatPeriod` | setting-or-container | 위아래 왕복 한 주기의 시간(초)입니다. |
| `Assets/_Code/Core/Tech_SYM/Enemies/Data/EnemyDefinition.cs` | `FeelsGoodOnion.TechSYM.Enemies.EnemyDefinition.health` | setting-or-container | 활성화마다 초기화할 체력입니다. |
| `Assets/_Code/Core/Tech_SYM/Enemies/Data/EnemyDefinition.cs` | `FeelsGoodOnion.TechSYM.Enemies.EnemyDefinition.initialIdle` | setting-or-container | 최초 IDLE 유지 시간(초)입니다. |
| `Assets/_Code/Core/Tech_SYM/Enemies/Data/EnemyDefinition.cs` | `FeelsGoodOnion.TechSYM.Enemies.EnemyDefinition.kind` | setting-or-container | 고정 부유형 또는 XY 왕복 순찰형입니다. |
| `Assets/_Code/Core/Tech_SYM/Enemies/Data/EnemyDefinition.cs` | `FeelsGoodOnion.TechSYM.Enemies.EnemyDefinition.knockbackSpeed` | setting-or-container | 접촉 피해가 적용된 플레이어를 적의 바깥쪽으로 밀어낼 속력(m/s). 0이면 넉백하지 않습니다. |
| `Assets/_Code/Core/Tech_SYM/Enemies/Data/EnemyDefinition.cs` | `FeelsGoodOnion.TechSYM.Enemies.EnemyDefinition.speed` | setting-or-container | 순찰 속도(m/s)입니다. |
| `Assets/_Code/Core/Tech_SYM/Enemies/Effects/EnemyOilOnDeath.cs` | `FeelsGoodOnion.TechSYM.Enemies.EnemyOilOnDeath.groundMask` | setting-or-container | 기름이 놓일 지면 레이어입니다. |
| `Assets/_Code/Core/Tech_SYM/Enemies/Effects/EnemyOilOnDeath.cs` | `FeelsGoodOnion.TechSYM.Enemies.EnemyOilOnDeath.lifetime` | setting-or-container | 기름 유지 시간(초)입니다. 0이면 씬 종료까지 유지합니다. |
| `Assets/_Code/Core/Tech_SYM/Enemies/Effects/EnemyOilOnDeath.cs` | `FeelsGoodOnion.TechSYM.Enemies.EnemyOilOnDeath.oilPrefab` | reference | 기존 OilSurface를 사용하는 독립 기름 프리팹입니다. |
| `Assets/_Code/Core/Tech_SYM/Enemies/Effects/EnemyOilOnDeath.cs` | `FeelsGoodOnion.TechSYM.Enemies.EnemyOilOnDeath.searchDistance` | setting-or-container | 사망 위치 아래 지면 탐색 거리(m)입니다. |
| `Assets/_Code/Core/Tech_SYM/Enemies/Effects/EnemyOilOnDeath.cs` | `FeelsGoodOnion.TechSYM.Enemies.EnemyOilOnDeath.size` | setting-or-container | 생성할 기름의 가로/깊이 크기(m)입니다. |
| `Assets/_Code/Core/Tech_SYM/Enemies/Effects/EnemyOilOnDeath.cs` | `FeelsGoodOnion.TechSYM.Enemies.EnemyOilOnDeath.spawnOilOnDeath` | setting-or-container | 사망 시 발밑 지면에 기름을 한 번 생성합니다. |
| `Assets/_Code/Core/Tech_SYM/Enemies/Runtime/EnemyActor.cs` | `FeelsGoodOnion.TechSYM.Enemies.EnemyActor.body` | reference | 몸통 접촉 피해를 판정하는 실제 몸체 Trigger입니다. |
| `Assets/_Code/Core/Tech_SYM/Enemies/Runtime/EnemyActor.cs` | `FeelsGoodOnion.TechSYM.Enemies.EnemyActor.definition` | reference | 개체별 실행 상태는 공유하지 않는 설정 SO입니다. |
| `Assets/_Code/Core/Tech_SYM/Enemies/Runtime/EnemyActor.cs` | `FeelsGoodOnion.TechSYM.Enemies.EnemyActor.heightSearchDistance` | setting-or-container | 시작 시 위아래 플랫폼 탐색 거리(m)입니다. |
| `Assets/_Code/Core/Tech_SYM/Enemies/Runtime/EnemyActor.cs` | `FeelsGoodOnion.TechSYM.Enemies.EnemyActor.initialDirection` | setting-or-container | 최초 XY 이동 방향입니다. |
| `Assets/_Code/Core/Tech_SYM/Enemies/Runtime/EnemyActor.cs` | `FeelsGoodOnion.TechSYM.Enemies.EnemyActor.initiallyInvincible` | setting-or-container | 활성화 시 적용할 무적 여부입니다. |
| `Assets/_Code/Core/Tech_SYM/Enemies/Runtime/EnemyActor.cs` | `FeelsGoodOnion.TechSYM.Enemies.EnemyActor.manualLowerDistance` | setting-or-container | 아래 플랫폼이 없을 때 시작점 아래로 허용할 거리(m)입니다. |
| `Assets/_Code/Core/Tech_SYM/Enemies/Runtime/EnemyActor.cs` | `FeelsGoodOnion.TechSYM.Enemies.EnemyActor.manualUpperDistance` | setting-or-container | 위 플랫폼이 없을 때 시작점 위로 허용할 거리(m)입니다. |
| `Assets/_Code/Core/Tech_SYM/Enemies/Runtime/EnemyActor.cs` | `FeelsGoodOnion.TechSYM.Enemies.EnemyActor.patrolDistance` | setting-or-container | 최초 위치에서 순찰 끝점까지의 거리(m)입니다. |
| `Assets/_Code/Core/Tech_SYM/Enemies/Runtime/EnemyActor.cs` | `FeelsGoodOnion.TechSYM.Enemies.EnemyActor.surfaceClearance` | setting-or-container | 플랫폼과 몸체 표면 사이 최소 간격(m)입니다. |
| `Assets/_Code/Core/Tech_SYM/Enemies/Runtime/EnemyActor.cs` | `FeelsGoodOnion.TechSYM.Enemies.EnemyActor.target` | reference | 몸통 접촉 전투 대상. 씬에서 명시적으로 연결합니다. |
| `Assets/_Code/Core/Tech_SYM/Enemies/Runtime/EnemyActor.cs` | `FeelsGoodOnion.TechSYM.Enemies.EnemyActor.terrainMask` | setting-or-container | 플랫폼과 벽을 검사할 레이어입니다. Trigger는 제외합니다. |
| `Assets/_Code/Core/Tech_SYM/Features/ObjectRotation.cs` | `FeelsGoodOnion.TechSYM.Features.ObjectRotation.degreesPerSecond` | setting-or-container | Y축 양의 방향 회전 속도(도/초). 다음 활성화 시 적용됩니다. |
| `Assets/_Code/Core/Tech_SYM/Features/ObjectRotation.cs` | `FeelsGoodOnion.TechSYM.Features.ObjectRotation.rotationTarget` | reference | 회전할 자식 Transform. 비워 두면 자신을 회전합니다. |
| `Assets/_Code/Core/Tech_SYM/Features/ShatteredPlatform.cs` | `FeelsGoodOnion.TechSYM.Features.ShatteredPlatform.blinkIntervalSeconds` | setting-or-container | Blink Tween 간격 |
| `Assets/_Code/Core/Tech_SYM/Features/ShatteredPlatform.cs` | `FeelsGoodOnion.TechSYM.Features.ShatteredPlatform.canRegenerate` | setting-or-container | 재생성 가능 여부. 동적으로 작동하지 않습니다. |
| `Assets/_Code/Core/Tech_SYM/Features/ShatteredPlatform.cs` | `FeelsGoodOnion.TechSYM.Features.ShatteredPlatform.collapseEndSeconds` | setting-or-container | 플랫폼 붕괴 트윈 지정 시간 |
| `Assets/_Code/Core/Tech_SYM/Features/ShatteredPlatform.cs` | `FeelsGoodOnion.TechSYM.Features.ShatteredPlatform.collapsedHoldSeconds` | setting-or-container | 붕괴 유지 시간. |
| `Assets/_Code/Core/Tech_SYM/Features/ShatteredPlatform.cs` | `FeelsGoodOnion.TechSYM.Features.ShatteredPlatform.regenerationSeconds` | setting-or-container | 재생성 까지 걸리는 시간. |
| `Assets/_Code/Core/Tech_SYM/Features/ShatteredPlatform.cs` | `FeelsGoodOnion.TechSYM.Features.ShatteredPlatform.shakeAngleDegrees` | setting-or-container | 회전강도. 0이면 흔들림 없음. |
| `Assets/_Code/Core/Tech_SYM/Features/ShatteredPlatform.cs` | `FeelsGoodOnion.TechSYM.Features.ShatteredPlatform.shakeFrequency` | setting-or-container | 초당 흔들림 횟수. |
| `Assets/_Code/Core/Tech_SYM/Features/ShatteredPlatform.cs` | `FeelsGoodOnion.TechSYM.Features.ShatteredPlatform.supportCollider` | reference | 오브젝트 충돌 검증 콜라이더 |
| `Assets/_Code/Core/Tech_SYM/Features/ShatteredPlatform.cs` | `FeelsGoodOnion.TechSYM.Features.ShatteredPlatform.visual` | reference | 콜라이더가 없는 렌더 오브젝트 |
| `Assets/_Code/Core/Tech_SYM/Features/ShatteredPlatform.cs` | `FeelsGoodOnion.TechSYM.Features.ShatteredPlatform.warningEndSeconds` | setting-or-container | 플랫폼 붕괴 경고 트윈 시간 |
| `Assets/_Code/Core/Tech_SYM/Features/TearFragment.cs` | `FeelsGoodOnion.TechSYM.Features.TearFragment.pickupRoot` | reference | 획득 후 비활성화할 이 눈물조각의 Root. 씬 전체 루트가 아닙니다. |
| `Assets/_Code/Core/Tech_SYM/Interaction/CheckInteract.cs` | `FeelsGoodOnion.TechSYM.Interaction.CheckInteract.detectionLayers` | setting-or-container | 상호작용 대상과 차폐할 벽을 포함합니다. 플레이어 Collider는 코드에서 제외합니다. |
| `Assets/_Code/Core/Tech_SYM/Interaction/CheckInteract.cs` | `FeelsGoodOnion.TechSYM.Interaction.CheckInteract.distance` | setting-or-container | 상호작용 대상을 찾는 최대 거리입니다(월드 단위, 최소 0.01). 높이면 더 먼 대상을 E로 조작할 수 있습니다. 각도와 차폐 조건도 만족해야 합니다. |
| `Assets/_Code/Core/Tech_SYM/Interaction/CheckInteract.cs` | `FeelsGoodOnion.TechSYM.Interaction.CheckInteract.horizontalAngle` | setting-or-container | 플레이어 전방 기준 대상 탐색의 전체 수평각입니다(도, 1~360). 좌우에는 절반씩 적용하며 360은 뒤쪽까지 포함합니다. 거리·차폐 조건도 적용됩니다. |
| `Assets/_Code/Core/Tech_SYM/Interaction/CheckInteract.cs` | `FeelsGoodOnion.TechSYM.Interaction.CheckInteract.overlaySource` | reference | IInteractionOverlay를 구현한 화면 UI 담당. 없어도 일반 상호작용은 가능합니다. |
| `Assets/_Code/Core/Tech_SYM/Interaction/CheckInteract.cs` | `FeelsGoodOnion.TechSYM.Interaction.CheckInteract.player` | reference | 상호작용 거리·시선 방향·입력 가능 여부를 읽을 플레이어입니다. |
| `Assets/_Code/Core/Tech_SYM/Interaction/CheckInteract.cs` | `FeelsGoodOnion.TechSYM.Interaction.CheckInteract.promptView` | reference | 선택한 상호작용 대상 위에 표시할 말풍선 안내입니다. |
| `Assets/_Code/Core/Tech_SYM/Interaction/CheckInteract.cs` | `FeelsGoodOnion.TechSYM.Interaction.CheckInteract.viewCamera` | reference | 월드 말풍선을 바라보게 할 인게임 카메라입니다. |
| `Assets/_Code/Core/Tech_SYM/Interaction/InteractionPromptAnchor.cs` | `FeelsGoodOnion.TechSYM.Interaction.InteractionPromptAnchor.interactionLocalOffset` | setting-or-container | 거리·전방 각도·차폐 검사에 사용하는 조작부의 로컬 위치입니다. 손잡이 등에 맞추세요. Scene 뷰의 초록 박스로 표시되며, 박스 크기는 상호작용 범위를 뜻하지 않습니다. |
| `Assets/_Code/Core/Tech_SYM/Interaction/InteractionPromptAnchor.cs` | `FeelsGoodOnion.TechSYM.Interaction.InteractionPromptAnchor.localOffset` | setting-or-container | 말풍선 UI를 표시할 로컬 위치입니다. Scene 뷰의 노란 구로 표시됩니다. |
| `Assets/_Code/Core/Tech_SYM/Interaction/InteractionPromptView.cs` | `FeelsGoodOnion.TechSYM.Interaction.InteractionPromptView.bubble` | reference | 상호작용 대상이 있을 때 표시할 말풍선 외형 오브젝트입니다. |
| `Assets/_Code/Core/Tech_SYM/Interaction/InteractionPromptView.cs` | `FeelsGoodOnion.TechSYM.Interaction.InteractionPromptView.scaleTween` | reference | 말풍선 등장·퇴장 크기 표현을 담당할 UIScaleTween입니다. |
| `Assets/_Code/Core/Tech_SYM/Interaction/MemoModel.cs` | `FeelsGoodOnion.TechSYM.Interaction.MemoModel.memoID` | setting-or-container | 메모의 고유 번호입니다(1 이상의 정수). 서로 다른 메모 오브젝트가 같은 번호를 사용하면 등록에 실패합니다. |
| `Assets/_Code/Core/Tech_SYM/Interaction/MemoModel.cs` | `FeelsGoodOnion.TechSYM.Interaction.MemoModel.memoImage` | reference | 메모 본문으로 보여 줄 Sprite입니다. 비워 두면 메모를 열 수 없습니다. 닫기는 E만 사용합니다. |
| `Assets/_Code/Core/Tech_SYM/Interaction/MemoObject.cs` | `FeelsGoodOnion.TechSYM.Interaction.MemoObject.memoModel` | reference | 이 오브젝트가 독점 사용할 메모 데이터입니다. 다른 메모와 고유 번호를 공유하지 마세요. |
| `Assets/_Code/Core/Tech_SYM/Interaction/MemoObject.cs` | `FeelsGoodOnion.TechSYM.Interaction.MemoObject.memoUI` | reference | E로 메모를 열고 닫으며 월드 정지를 관리할 화면 담당입니다. |
| `Assets/_Code/Core/Tech_SYM/Interaction/MemoUIController.cs` | `FeelsGoodOnion.TechSYM.Interaction.MemoUIController.player` | reference | 메모 열기 전에 입력 가능한 상태인지 확인할 플레이어 참조입니다. 비워 두면 이 추가 검사를 생략합니다. 메모 표시 중 입력·월드 정지는 별도로 주입된 제어 서비스가 관리합니다. |
| `Assets/_Code/Core/Tech_SYM/Interaction/MemoUIController.cs` | `FeelsGoodOnion.TechSYM.Interaction.MemoUIController.screenRoot` | reference | 메모 본문을 생성할 스크린 공간 Canvas 안의 RectTransform입니다. World Space Canvas는 사용할 수 없습니다. |
| `Assets/_Code/Core/Tech_SYM/Interaction/MemoUIController.cs` | `FeelsGoodOnion.TechSYM.Interaction.MemoUIController.viewPrefab` | reference | 메모 본문 화면 프리팹입니다. 이미지·크기 표현을 연결하며 마우스 닫기 버튼 없이 E로만 닫습니다. |
| `Assets/_Code/Core/Tech_SYM/Interaction/MemoView.cs` | `FeelsGoodOnion.TechSYM.Interaction.MemoView.memoImage` | reference | 메모 데이터의 Sprite를 표시할 본문 Image입니다. 클릭으로 메모를 닫지 않습니다. |
| `Assets/_Code/Core/Tech_SYM/Interaction/MemoView.cs` | `FeelsGoodOnion.TechSYM.Interaction.MemoView.scaleTween` | reference | E 열기·닫기 때 크기 표현을 재생할 담당입니다. 옵션 중에는 멈추며 완료 후 화면 상태를 갱신합니다. |
| `Assets/_Code/Core/Tech_SYM/Interaction/UIScaleTween.cs` | `FeelsGoodOnion.TechSYM.Interaction.UIScaleTween.duration` | setting-or-container | UI 열기·닫기 크기 변화 시간입니다(초, 인스펙터 최소 0.01). 높이면 더 천천히 바뀝니다. 월드 정지 중에도 진행하며 옵션이 표시를 정지하면 멈춥니다. |
| `Assets/_Code/Core/Tech_SYM/OvenTray/OvenTrayObject.cs` | `FeelsGoodOnion.TechSYM.OvenTray.OvenTrayObject.moveDuration` | setting-or-container | 트레이의 편도 열기·닫기 시간입니다(초, 최소 0.02). 같은 이동량에서 높이면 더 천천히 움직입니다. 이동 중 E 재조작은 받지 않습니다. |
| `Assets/_Code/Core/Tech_SYM/OvenTray/OvenTrayObject.cs` | `FeelsGoodOnion.TechSYM.OvenTray.OvenTrayObject.travelOffset` | setting-or-container | 닫힌 위치에서 열린 위치까지의 이동량입니다(부모 로컬 좌표). XYZ의 부호로 방향을, 크기로 이동 거리를 정합니다. 모두 0이면 위치가 바뀌지 않습니다. |
| `Assets/_Code/Core/Tech_SYM/Platforms/DamageFloorPlatform.cs` | `FeelsGoodOnion.TechSYM.Platforms.DamageFloorPlatform.controlLockMilliseconds` | setting-or-container | 넉백 시 이동·점프 제어 제한 요청 시간(ms). 0이면 새 제한을 요청하지 않습니다. |
| `Assets/_Code/Core/Tech_SYM/Platforms/DamageFloorPlatform.cs` | `FeelsGoodOnion.TechSYM.Platforms.DamageFloorPlatform.damageInterval` | setting-or-container | 피해 적용 간격(초). 피해량은 1입니다. |
| `Assets/_Code/Core/Tech_SYM/Platforms/DamageFloorPlatform.cs` | `FeelsGoodOnion.TechSYM.Platforms.DamageFloorPlatform.knockbackSpeed` | setting-or-container | 피해가 적용된 플레이어를 진행 반대 방향으로 밀어낼 월드 속력(m/s). 0이면 넉백하지 않습니다. |
| `Assets/_Code/Core/Tech_SYM/Platforms/FloatingMotion.cs` | `FeelsGoodOnion.TechSYM.Platforms.FloatingMotion.ease` | setting-or-container | 부유 이동의 속도 곡선. 부드러운 움직임은 InOutSine을 사용합니다. |
| `Assets/_Code/Core/Tech_SYM/Platforms/FloatingMotion.cs` | `FeelsGoodOnion.TechSYM.Platforms.FloatingMotion.loopType` | setting-or-container | 반복 방식. Yoyo는 시작 높이로 되돌아옵니다. Restart는 시작점으로 즉시 돌아갑니다. |
| `Assets/_Code/Core/Tech_SYM/Platforms/FloatingMotion.cs` | `FeelsGoodOnion.TechSYM.Platforms.FloatingMotion.targetY` | setting-or-container | 도착할 월드 Y 좌표. 시작 높이는 활성화 시점의 위치입니다. |
| `Assets/_Code/Core/Tech_SYM/Platforms/FloatingMotion.cs` | `FeelsGoodOnion.TechSYM.Platforms.FloatingMotion.travelSeconds` | setting-or-container | 목표 높이까지 편도 이동 시간(초). |
| `Assets/_Code/Core/Tech_SYM/Platforms/FloatingPlatform.cs` | `FeelsGoodOnion.TechSYM.Platforms.FloatingPlatform.actionEase` | setting-or-container | 아래로 눌리는 곡선. |
| `Assets/_Code/Core/Tech_SYM/Platforms/FloatingPlatform.cs` | `FeelsGoodOnion.TechSYM.Platforms.FloatingPlatform.actionSeconds` | setting-or-container | 아래로 눌리는 시간(초). |
| `Assets/_Code/Core/Tech_SYM/Platforms/FloatingPlatform.cs` | `FeelsGoodOnion.TechSYM.Platforms.FloatingPlatform.landingDepth` | setting-or-container | 착지할 때 아래로 눌리는 거리(m). |
| `Assets/_Code/Core/Tech_SYM/Platforms/FloatingPlatform.cs` | `FeelsGoodOnion.TechSYM.Platforms.FloatingPlatform.overshootHeight` | setting-or-container | 부유 기준 높이보다 위로 올라갈 목표 거리(m). Back/Elastic 곡선은 이 높이를 더 넘을 수 있습니다. |
| `Assets/_Code/Core/Tech_SYM/Platforms/FloatingPlatform.cs` | `FeelsGoodOnion.TechSYM.Platforms.FloatingPlatform.reactionEase` | setting-or-container | 위쪽 목표 높이로 올라가는 곡선. |
| `Assets/_Code/Core/Tech_SYM/Platforms/FloatingPlatform.cs` | `FeelsGoodOnion.TechSYM.Platforms.FloatingPlatform.reactionSeconds` | setting-or-container | 눌린 위치에서 위쪽 목표 높이까지 올라가는 시간(초). |
| `Assets/_Code/Core/Tech_SYM/Platforms/FloatingPlatform.cs` | `FeelsGoodOnion.TechSYM.Platforms.FloatingPlatform.rearmGap` | setting-or-container | 다시 착지할 수 있도록 판정을 풀어주는 윗면과의 간격(m). |
| `Assets/_Code/Core/Tech_SYM/Platforms/FloatingPlatform.cs` | `FeelsGoodOnion.TechSYM.Platforms.FloatingPlatform.settleEase` | setting-or-container | 반동 이후 기준 높이로 정착하는 곡선. |
| `Assets/_Code/Core/Tech_SYM/Platforms/FloatingPlatform.cs` | `FeelsGoodOnion.TechSYM.Platforms.FloatingPlatform.settleSeconds` | setting-or-container | 위쪽 목표 높이에서 부유 기준 높이로 정착하는 시간(초). |
| `Assets/_Code/Core/Tech_SYM/Platforms/LinearShuttlePlatform.cs` | `FeelsGoodOnion.TechSYM.Platforms.LinearShuttlePlatform.initialDirection` | setting-or-container | 최초 이동 방향. 실행 중에는 변경되지 않습니다. |
| `Assets/_Code/Core/Tech_SYM/Platforms/LinearShuttlePlatform.cs` | `FeelsGoodOnion.TechSYM.Platforms.LinearShuttlePlatform.moveSpeed` | setting-or-container | 이동 속도(m/s). |
| `Assets/_Code/Core/Tech_SYM/Platforms/LinearShuttlePlatform.cs` | `FeelsGoodOnion.TechSYM.Platforms.LinearShuttlePlatform.travelSeconds` | setting-or-container | 편도 이동 시간(초). 같은 경로로 무한 왕복합니다. |
| `Assets/_Code/Core/Tech_SYM/Platforms/PeriodicPlatform.cs` | `FeelsGoodOnion.TechSYM.Platforms.PeriodicPlatform.blinkAcceleration` | setting-or-container | 깜빡임 가속도. 값이 클수록 후반에 빠르게 깜빡입니다. |
| `Assets/_Code/Core/Tech_SYM/Platforms/PeriodicPlatform.cs` | `FeelsGoodOnion.TechSYM.Platforms.PeriodicPlatform.blinkCount` | setting-or-container | 경고 시간 동안 깜빡이는 총 횟수. |
| `Assets/_Code/Core/Tech_SYM/Platforms/PeriodicPlatform.cs` | `FeelsGoodOnion.TechSYM.Platforms.PeriodicPlatform.hiddenSeconds` | setting-or-container | 경고 전에 완전히 숨기는 시간(초). |
| `Assets/_Code/Core/Tech_SYM/Platforms/PeriodicPlatform.cs` | `FeelsGoodOnion.TechSYM.Platforms.PeriodicPlatform.lifetime` | setting-or-container | 밟을 수 있는 시간(초). |
| `Assets/_Code/Core/Tech_SYM/Platforms/PeriodicPlatform.cs` | `FeelsGoodOnion.TechSYM.Platforms.PeriodicPlatform.warningSeconds` | setting-or-container | 재생성 전 깜빡임 시간(초). 이때는 밟을 수 없습니다. |
| `Assets/_Code/Core/Tech_SYM/Platforms/PressureLinkedPlatform.cs` | `FeelsGoodOnion.TechSYM.Platforms.PressureLinkedPlatform.pressurePlate` | reference | 연결할 압력판. |
| `Assets/_Code/Core/Tech_SYM/Platforms/PressureLinkedPlatform.cs` | `FeelsGoodOnion.TechSYM.Platforms.PressureLinkedPlatform.returnSpeed` | setting-or-container | 버튼을 놓았을 때의 복귀 속도(m/s). 버튼 시간과 별개입니다. |
| `Assets/_Code/Core/Tech_SYM/Platforms/PressureLinkedPlatform.cs` | `FeelsGoodOnion.TechSYM.Platforms.PressureLinkedPlatform.travelOffset` | setting-or-container | 시작점에서 끝점까지의 월드 이동량(m). 방향과 거리를 지정합니다. |
| `Assets/_Code/Core/Tech_SYM/Platforms/PressurePlatePlatform.cs` | `FeelsGoodOnion.TechSYM.Platforms.PressurePlatePlatform.linkedPlatform` | reference | 함께 움직이는 발판. 같은 버튼에 연결된 발판이 필요합니다. |
| `Assets/_Code/Core/Tech_SYM/Platforms/PressurePlatePlatform.cs` | `FeelsGoodOnion.TechSYM.Platforms.PressurePlatePlatform.pressDepth` | setting-or-container | 버튼이 내려가는 거리(m). |
| `Assets/_Code/Core/Tech_SYM/Platforms/PressurePlatePlatform.cs` | `FeelsGoodOnion.TechSYM.Platforms.PressurePlatePlatform.travelSeconds` | setting-or-container | 끝까지 누르거나 복귀하는 시간(초). 중간 해제 시 남은 거리만큼 적용합니다. |
