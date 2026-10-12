# HMS 누적 변경 검토

merge-base `d12e468d2b7685b3c75ebd1018b20a1a7d97c7b9` 대비. 원시 patch와 소비 검색은 output/Tech_SYM/Refactor/system-v16-2026-10-12/T6에 보존한다.

| 파일 | 전 + / - | 최종 + / - | 결론 |
|---|---|---|---|
| CharacterMovement.cs | 2 / 0 | 2 / 0 | 필수 소비 보존; 상세 hunk 판정 참조 |
| PlayerAnimationController.cs | 39 / 1 | 39 / 1 | 필수 소비 보존; 상세 hunk 판정 참조 |
| PlayerAnimationPlayback.cs | 45 / 7 | 45 / 7 | 필수 소비 보존; 상세 hunk 판정 참조 |
| PlayerAnimationSet.cs | 4 / 0 | 4 / 0 | 필수 소비 보존; 상세 hunk 판정 참조 |
| PlayerAnimationTypes.cs | 11 / 1 | 11 / 1 | 필수 소비 보존; 상세 hunk 판정 참조 |
| PlayerAnimatorOutput.cs | 33 / 6 | 33 / 6 | 필수 소비 보존; 상세 hunk 판정 참조 |
| PlayerCameraController.cs | 111 / 3 | 0 / 0 | 분리 확장 제거; 원 4rig 계약 보존 |
| PlayerFacade.cs | 71 / 4 | 70 / 3 | 필수 소비 보존; 상세 hunk 판정 참조 |
| PlayerFormController.cs | 5 / 6 | 5 / 6 | 필수 소비 보존; 상세 hunk 판정 참조 |
| PlayerInputReader.cs | 37 / 24 | 34 / 24 | 필수 소비 보존; 상세 hunk 판정 참조 |
| PlayerSkillController.cs | 43 / 3 | 43 / 3 | 필수 소비 보존; 상세 hunk 판정 참조 |
| TearSkill.cs | 22 / 2 | 22 / 2 | 필수 소비 보존; 상세 hunk 판정 참조 |


## HMS 12파일의 변경 단위별 판정

기준: fetch origin/main `d47253ec8fedab276ee3f863690af3f805451760`, merge-base `d12e468d2b7685b3c75ebd1018b20a1a7d97c7b9`, 시작 `147a9723c8d1261ba4f17e57801796048c22cf4f`.

목표: 최신 inline 계약으로 필요한 HMS 확장만 보존. 최초 목적은 시작 diff와 audit-hms.md에 근거한 역할 설명이며 작성자 의도를 별도로 추측하지 않는다.

제약: 지정 12소스만 허용. Unity/Play/Player/씬/자산/meta/push 실행 안 함. 완료 기준은 전 hunk 판정, 소비/자산 참조 기록, no-overlay 전체 runtime/editor compile, 한국어 커밋.

각 파일의 시작 diff 원문은 before.patch, 최종 PR 순변경은 final-pr.patch에 보존한다. 아래 번호는 git 기본 unified=3 hunk 순서다.

## CharacterMovement.cs

| Hunk | 시작 위치 | 최초 목적·현재 소비·판정 근거 | 판정 |
|---|---|---|---|
| 1 | `@@ -68,6 +68,8 @@ public sealed class CharacterMovement : MonoBehaviour` | 실제 접지각 공개: PeriodicPlatform.cs MaximumGroundAngle 소비. 원 접지 필드/알고리즘 보존. | 유지 |

## PlayerAnimationController.cs

| Hunk | 시작 위치 | 최초 목적·현재 소비·판정 근거 | 판정 |
|---|---|---|---|
| 1 | `@@ -35,6 +35,8 @@ public sealed class PlayerAnimationController : MonoBehaviour` | 종료 통지 공개: PlayerBridgeService.Attach의 PlaybackEnded 구독과 OnPlaybackEnded 사망 대기 종결 소비. | 유지 |
| 2 | `@@ -48,6 +50,7 @@ public sealed class PlayerAnimationController : MonoBehaviour` | Playback 내부 결과를 Controller 이벤트로 전달하는 OnEnable 구독. 외부 Bridge까지 연결되는 필수 경로. | 유지 |
| 3 | `@@ -106,6 +109,34 @@ public sealed class PlayerAnimationController : MonoBehaviour` | 실제 Output의 TryReportResult 소비, InteractionService.TryRequestInteract 소비. 활성/요청 ID 검사 및 관찰자 예외 격리는 결과와 사망 종결 계약. | 유지 |
| 4 | `@@ -122,7 +153,14 @@ public sealed class PlayerAnimationController : MonoBehaviour` | Reset 중 Interrupted 전달 후 finally에서 자기 구독 해제. 재활성화 중복과 끊긴 사망 대기 방지. | 유지 |

## PlayerAnimationPlayback.cs

| Hunk | 시작 위치 | 최초 목적·현재 소비·판정 근거 | 판정 |
|---|---|---|---|
| 1 | `@@ -3,7 +3,7 @@ using System;` | Interact 우선순위 설명을 실제 선택 계약과 일치시킴. 사망/넉백 우선 보존. | 유지 |
| 2 | `@@ -11,6 +11,10 @@ public sealed class PlayerAnimationPlayback` | endedRequestId와 PlaybackEnded: Controller를 거쳐 Bridge가 완료/누락/중단을 구분. | 유지 |
| 3 | `@@ -24,7 +28,7 @@ public sealed class PlayerAnimationPlayback` | 상호작용 중 기본 상태 갱신 설명. 완료 후 현재 이동 표현 복귀. | 유지 |
| 4 | `@@ -52,10 +56,10 @@ public sealed class PlayerAnimationPlayback` | Interact 요청 대상 및 중단/거절 비재개 설명. InteractionService 소비. | 유지 |
| 5 | `@@ -75,8 +79,24 @@ public sealed class PlayerAnimationPlayback` | TryComplete를 결과 경로에 통합하고 stale/중복 결과 거절. Output 및 Controller 소비. | 유지 |
| 6 | `@@ -92,12 +112,29 @@ public sealed class PlayerAnimationPlayback` | 새 요청 선택 후 이전 요청 Interrupted 전달. Bridge의 currentId-1 중단 판정 소비; 재진입 이전 상태 쓰기 방지. | 유지 |
| 7 | `@@ -107,6 +144,7 @@ public sealed class PlayerAnimationPlayback` | Interact 우선순위4: Tear3보다 높고 Dead/Knockback에는 거절. InteractionService 소비. | 유지 |

## PlayerAnimationSet.cs

| Hunk | 시작 위치 | 최초 목적·현재 소비·판정 근거 | 판정 |
|---|---|---|---|
| 1 | `@@ -25,6 +25,8 @@ public sealed class PlayerAnimationSet : ScriptableObject` | Interact 실클립 작성 필드: PlayerAnimatorOutput -> Resolve가 소비. 미연결은 Missing이며 기능 삭제 사유 아님. | 유지 |
| 2 | `@@ -55,6 +57,7 @@ public sealed class PlayerAnimationSet : ScriptableObject` | Interact 누락 시 Idle 대체 금지. Output의 Missing 결과 의미 보존. | 유지 |
| 3 | `@@ -99,6 +102,7 @@ public sealed class PlayerAnimationSet : ScriptableObject` | GetClip의 Interact 분기. 요청 -> 설정 -> 실제 출력 연결. | 유지 |

## PlayerAnimationTypes.cs

| Hunk | 시작 위치 | 최초 목적·현재 소비·판정 근거 | 판정 |
|---|---|---|---|
| 1 | `@@ -22,7 +22,17 @@ public enum PlayerAnimationId` | Interact ID 및 Completed/Interrupted/Missing 값. Controller/Output/Bridge 전체 소비. 기존 enum 숫자 보존. | 유지 |

## PlayerAnimatorOutput.cs

| Hunk | 시작 위치 | 최초 목적·현재 소비·판정 근거 | 판정 |
|---|---|---|---|
| 1 | `@@ -36,6 +36,17 @@ public sealed class PlayerAnimatorOutput : MonoBehaviour` | 실제 Knockback 클립 길이: SessionActorHost 검증과 PlayerBridgeService.GetKnockbackControlLockMilliseconds 소비. | 유지 |
| 2 | `@@ -75,7 +86,7 @@ public sealed class PlayerAnimatorOutput : MonoBehaviour` | Animator/Set 누락 결과를 일반 Dead 요청에도 보고. Bridge 영구 사망 대기 방지. | 유지 |
| 3 | `@@ -110,6 +121,7 @@ public sealed class PlayerAnimatorOutput : MonoBehaviour` | 비반복 Dead 등 클립 종료 Completed 통지. Bridge 사망 표현 종결 소비. | 유지 |
| 4 | `@@ -163,7 +175,7 @@ public sealed class PlayerAnimatorOutput : MonoBehaviour` | 일회성 실클립 누락을 Completed로 오인하지 않게 Missing 통지. | 유지 |
| 5 | `@@ -171,7 +183,7 @@ public sealed class PlayerAnimatorOutput : MonoBehaviour` | Controller 상태 누락 시 일반/일회성 모두 Missing. Bridge 종결 대체 경로. | 유지 |
| 6 | `@@ -185,6 +197,8 @@ public sealed class PlayerAnimatorOutput : MonoBehaviour` | 다른 클립으로 대체한 경우 실제 요청 성공 아님을 Missing으로 보고. | 유지 |
| 7 | `@@ -208,8 +222,21 @@ public sealed class PlayerAnimatorOutput : MonoBehaviour` | Output 해제 시 자기 일회성 중단과 finally Animator 복구. Controller 요청ID 검사 경로 소비. | 유지 |

## PlayerCameraController.cs

| Hunk | 시작 위치 | 최초 목적·현재 소비·판정 근거 | 판정 |
|---|---|---|---|
| 1 | `@@ -9,6 +9,9 @@ using UnityEngine;` | 분리 rig opt-in 직렬 필드. T5 LevelCameraService로 소유권 이관되어 소스 소비0. 제거; 남은 씬값 T7 인계. | 제거 |
| 2 | `@@ -32,10 +35,90 @@ public sealed class PlayerCameraController : MonoBehaviour` | borrowed rig 상태/세대/IsBound/Bind/Unbind/Preview/Commit 및 Awake 우회. 외부 소비0, LevelCameraService가 직접 세 rig와 모드 전환 소유. 해당 확장만 제거. | 제거 |
| 3 | `@@ -46,7 +129,7 @@ public sealed class PlayerCameraController : MonoBehaviour` | 분리모드 Awake 검증 우회 괄호 제거. 독립 네 rig 비null/서로다름 검증 복구. | 제거 |
| 4 | `@@ -70,6 +153,13 @@ public sealed class PlayerCameraController : MonoBehaviour` | 분리 OnEnable 구독 분기 제거. 원 ModeChanged 구독 및 초기/재활성화 적용 보존. | 제거 |
| 5 | `@@ -79,6 +169,11 @@ public sealed class PlayerCameraController : MonoBehaviour` | 분리 OnDisable 분기 제거. 원 자기 ModeChanged 해제 보존. | 제거 |
| 6 | `@@ -87,6 +182,7 @@ public sealed class PlayerCameraController : MonoBehaviour` | 분리 Preview 중 ModeChanged 억제 제거. 독립 모드 변경 반영 보존. | 제거 |
| 7 | `@@ -95,9 +191,21 @@ public sealed class PlayerCameraController : MonoBehaviour` | 분리 두 rig 우선순위 분기와 Unbind 전용 OnDestroy 제거. 원 네 rig 우선순위 적용 보존. EOF 개행은 원 형식 유지. | 제거 |

## PlayerFacade.cs

| Hunk | 시작 위치 | 최초 목적·현재 소비·판정 근거 | 판정 |
|---|---|---|---|
| 1 | `@@ -1,4 +1,4 @@` | BOM 제거는 동작 목적/소비 없음. 원 UTF-8 BOM만 복구하여 불필요 인코딩 순diff 제거. | 제거 |
| 2 | `@@ -12,6 +12,10 @@ public sealed class PlayerFacade : MonoBehaviour, IDamageable, IKnockbackable` | 피격 무적 설정/시간 및 피해 세대. Host 2초 설정; Damage 재진입/강제사망 원자성 소비. | 유지 |
| 3 | `@@ -293,19 +297,76 @@ public sealed class PlayerFacade : MonoBehaviour, IDamageable, IKnockbackable` | 피해 무적/공통 넉백/ForceDeath API. Bridge.Damage/ForceDeath 및 적/발판/데드존/추격 경로 소비. 유한값/세대 검사 보존. | 유지 |
| 4 | `@@ -320,8 +381,8 @@ public sealed class PlayerFacade : MonoBehaviour, IDamageable, IKnockbackable` | 수동과 피격 무적 통합 조회. Facade.Damage의 공통 차단 계약. | 유지 |
| 5 | `@@ -395,9 +456,12 @@ public sealed class PlayerFacade : MonoBehaviour, IDamageable, IKnockbackable` | Revive 시 세대와 피격 무적 해제. 원 공개 TryRevive 재진입 및 독립 부활 계약. | 유지 |
| 6 | `@@ -520,6 +584,7 @@ public sealed class PlayerFacade : MonoBehaviour, IDamageable, IKnockbackable` | Inspector 피격 시간 유한/음수 검증. 새 설정 경계 책임. | 유지 |
| 7 | `@@ -534,6 +599,8 @@ public sealed class PlayerFacade : MonoBehaviour, IDamageable, IKnockbackable` | OnDisable 시 세대 무효화/무적 해제. 피해 콜백 도중 Actor 해제 뒤 늦은 넉백 방지. | 유지 |

## PlayerFormController.cs

| Hunk | 시작 위치 | 최초 목적·현재 소비·판정 근거 | 판정 |
|---|---|---|---|
| 1 | `@@ -356,11 +356,6 @@ public sealed class PlayerFormController : MonoBehaviour` | 회수 전 복귀 거절 제거. RestoreFormSkill.GetBlockReason -> GetRestoreBlockReason 소비; Q 거리무관 복귀 계약. | 유지 |
| 2 | `@@ -370,7 +365,7 @@ public sealed class PlayerFormController : MonoBehaviour` | 공간 성공 뒤 소유 껍질 회수 설명. 같은 공개 RestoreForm 계약. | 유지 |
| 3 | `@@ -379,6 +374,10 @@ public sealed class PlayerFormController : MonoBehaviour` | 공간 성공 뒤에만 OwnedDebris.Remove/null/회수상태 변경. RestoreFormSkill.Execute 소비. 실패 시 껍질 보존. | 유지 |

## PlayerInputReader.cs

| Hunk | 시작 위치 | 최초 목적·현재 소비·판정 근거 | 판정 |
|---|---|---|---|
| 1 | `@@ -7,8 +7,37 @@ public sealed class PlayerInputReader : MonoBehaviour` | 눈물 바인딩 저장/override는 Host 두 경로에서 E 소비; 미조립 Reader F 정책 보존. TearSkillBinding getter만 전체 Assets C# 소비0이므로 제거. 회수 Action 제거는 Q 폼 통합. | 부분 제거/유지 |
| 2 | `@@ -17,8 +46,8 @@ public sealed class PlayerInputReader : MonoBehaviour` | RecoverDebrisPressedThisFrame false는 보호 PlayerController.cs:99 소비. 삭제하면 목록외 소비자 수정 필요하므로 유지. | 유지 |
| 3 | `@@ -30,7 +59,6 @@ public sealed class PlayerInputReader : MonoBehaviour` | 별도 회수 Action 생성 제거: Q 폼 전환과 중복 입력 방지. | 유지 |
| 4 | `@@ -42,7 +70,6 @@ public sealed class PlayerInputReader : MonoBehaviour` | 제거된 회수 Action Enable 제거. 기존 이동/점프/눈물/폼 Action 유지. | 유지 |
| 5 | `@@ -53,9 +80,8 @@ public sealed class PlayerInputReader : MonoBehaviour` | 회수 Action Disable 제거 및 Awake 전 해제 안전한 null 조건. InputDisabled 소비 보존. | 유지 |
| 6 | `@@ -69,23 +95,16 @@ public sealed class PlayerInputReader : MonoBehaviour` | 회수 Action Dispose와 WASD composite 제거. 방향키 정책; 남은 Action 수명 유지. | 유지 |
| 7 | `@@ -102,18 +121,12 @@ public sealed class PlayerInputReader : MonoBehaviour` | 눈물 생성 시 선택 바인딩 소비 및 Q 폼 전환. Host E override/기본 F 구별; 회수 Q/폼 R 이중 정책 제거. | 유지 |

## PlayerSkillController.cs

| Hunk | 시작 위치 | 최초 목적·현재 소비·판정 근거 | 판정 |
|---|---|---|---|
| 1 | `@@ -12,6 +12,28 @@ using UnityEngine;` | Bridge의 BindAdditionalCondition/UnbindAdditionalCondition/UseRejected 소비. 해금/공통 간격/공간부족 피드백, 자기 조건만 해제. | 유지 |
| 2 | `@@ -126,6 +148,10 @@ public class PlayerSkillController : MonoBehaviour` | 기본 공통 조건 후 Bridge 추가조건 실행. 독립 미조립 시 기존 조건 유지. | 유지 |
| 3 | `@@ -139,9 +165,10 @@ public class PlayerSkillController : MonoBehaviour` | TryUse 거절 이벤트 한 번. Bridge 피드백, 조건조회에는 발생 안 함. | 유지 |
| 4 | `@@ -178,6 +205,8 @@ public class PlayerSkillController : MonoBehaviour` | OnDestroy 추가조건/이벤트 참조 해제. Host/Bridge 수명 종료. | 유지 |
| 5 | `@@ -194,6 +223,17 @@ public class PlayerSkillController : MonoBehaviour` | 거절 관찰자별 예외 격리. Bridge 피드백 실패가 다른 관찰자 누락으로 번지지 않음. | 유지 |

## TearSkill.cs

| Hunk | 시작 위치 | 최초 목적·현재 소비·판정 근거 | 판정 |
|---|---|---|---|
| 1 | `@@ -8,6 +8,21 @@ public class TearSkill : PlayerSkill` | Bridge SetTargetValidation(actor.transform, ValidateTearTarget)/해제 소비. 실제 몸체 중심/차폐 정책 주입과 실행 중 교체 거절. | 유지 |
| 2 | `@@ -79,6 +94,7 @@ public class TearSkill : PlayerSkill` | 빌린 effectRoot 파괴/비활성 시 사용 거절. 오래된 Actor 검증 정책 사용 방지. | 유지 |
| 3 | `@@ -96,7 +112,7 @@ public class TearSkill : PlayerSkill` | 발동 중심을 주입받은 실제 ActorBody로 사용. 미주입 owner 기본 동작 보존. | 유지 |
| 4 | `@@ -142,6 +158,7 @@ public class TearSkill : PlayerSkill` | effectRoot 자식 Collider 자기피격 제외. Bridge 실제 ActorBody 루트 계약. | 유지 |
| 5 | `@@ -149,9 +166,12 @@ public class TearSkill : PlayerSkill` | effectRoot 대상 제외 및 정책 검증 후 dedupe. Sensor가 먼저 나와도 유효 Hurt Collider를 잃지 않음; Bridge.ValidateTearTarget 소비. EOF 개행은 코드 종료 형식이며 별도 기능 아님. | 유지 |
