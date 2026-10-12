# 이전 Tech_HMS 변경 기록

현재 브랜치에서 이전 SYM 통합과 직접 관련된 보호 HMS 원본 변경 두 건을 Git patch로 확인했다. 전체 HMS 개발 이력을 대신하는 문서가 아니라 협업 PR에서 **우리 통합 때문에 바뀐 코드의 추가·삭제·변경 이유**를 공유하기 위한 기록이다. V1.5 구현은 로컬 feature branch에 커밋했다. PR·push·원격 병합은 수행하지 않았다.

## 2026-10-06 — 6f2a386e6b9c8196a4bd80918a84a542e2ae79b4

`fix: Q 폼 전환과 F 눈물 입력 경로 통일`. 실제 patch는 두 소스, 추가12줄/삭제30줄이다. 제목 문구보다 아래 실제 동작·diff를 기준으로 검토한다.

| 소스 | 추가/삭제/변경 | 바꾼 이유 | 독립 소비/협업 주의 |
|---|---|---|---|
| PlayerInputReader.cs | WASD composite 삭제, 방향키만 유지 | 지정 입력에 없는 중복 이동 입력 제거 | 독립 구성도 이 변경의 입력 정책을 받음. 옛 WASD 사용을 보존했다고 주장하지 않음 |
| PlayerInputReader.cs | 별도 RecoverDebrisAction 생성·Enable/Disable·Dispose 경로 삭제. RecoverDebrisPressedThisFrame은 false 호환 값 | 독립 회수 입력을 없애고 Q 복귀와 껍질 회수를 한 행동으로 연결 | 호환 프로퍼티가 남아 있는 이유/실제 항상 false 의미를 숨기지 않음 |
| PlayerInputReader.cs | 기존 E 눈물 기본 바인딩을 F로, R 폼을 Q로 변경. Disable null-safe | 당시 독립 입력 기본과 통합 폼 키 연결 | 이후 통합은 Host가 E로 재바인딩함. 기본F와 통합E를 같은 것으로 설명하지 않음 |
| PlayerFormController.cs | 복귀 조건의 DebrisNotRecovered 차단 삭제. HasRestoreSpace 성공 이후 OwnedDebris.Remove/null/debrisRecovered=true 후 Normal 전환 | 공간이 충분한 Q 한 번으로 어디에 있든 껍질 회수+성장. 별도 회수 선행 필요 제거 | 좁은 공간 실패 때 Small/껍질 유지. 거리 기반 공개 회수 API는 다른 독립 소비를 위해 남김. 원본 전면 삭제를 허용한 변경 아님 |

## 2026-10-07 — 5d1770db9b533465d7b71bc7e55fbbc390a00439

`feat: HMS 외부 조립을 위한 선택적 공개 API 확장`. 소스9개, 추가362줄/삭제20줄. 앞선 Reader와 중복하므로 두 commit의 고유 변경 소스는 **10개**다. 당시 허가는 이 작업 1회용이었다. 이번 권한을 자동으로 확대하지 않는다.

| 소스 | 실제 변경 | 추가/변경이 필요한 이유 | 동작·수명 계약 |
|---|---|---|---|
| PlayerInputReader.cs | TearSkillBinding/SetTearSkillBinding과 해당 action 재연결; Awake 이전 설정 보존; 기본F | 보호 입력 구현을 복제하지 않고 통합 Host에서 E 지정 | 활성 action 중 자기 눈물 action만 해제/재생성. 통합과 독립 기본 구별 |
| PlayerFacade.cs | 선택적 hitInvincibilitySeconds, SetHitInvincibilityDuration, 피해 세대 번호와 무적 만료; 기존 Damage(int) 경로 | 통합 피해1/무적2초를 기존 HP 소유자에서 처리 | 기본0으로 독립 기존 정책 보존. HealthChanged 재진입으로 동일 피격 중복 적용 방어 |
| PlayerFacade.cs | Damage(int, Vector3, float ms) overload 및 유한 값 검증 | 수락한 피해에만 넉백/공통 clip 길이 적용 | 치명타도 포함. 콜백 중 최신 사망·부활·해제 발생 시 낡은 넉백 미적용 |
| PlayerFacade.cs | ForceDeath(Vector3, float ms) | 데드존·추격 포획이 무적을 무시하고 HP0으로 진입해야 함 | 기존 Dead/FSM 이벤트 사용. 폼·HeavyState·중력·운동은 초기화하지 않음. 이미Dead/비활성 거절 |
| PlayerFacade.cs | Revive/Disable에서 자동 무적 상태 정리 | 재등장/재바인딩에 이전 Actor 무적이 누출되지 않게 함 | 기존 수동 무적과 선택적 피격 무적을 구별 |
| PlayerCameraController.cs | 명시 Bind/Unbind, 출력/Side·Quarter 리그/Actor anchor 분리 연결, preview/commit 이동 모드, 우선순위 소유 | Actor 제거 때 카메라 출력까지 파괴하지 않고 Core 리그와 연결/해제 | 외부 연결이 없는 독립 기존 fallback 유지. 자기 요청/제한만 해제 |
| TearSkill.cs | SetTargetValidation(effectRoot, 후보검증 함수); Root 중심 사용; 후보 검증→dedupe 순서 | OverlapSphere 후보만으로 벽 너머 적 또는 Sensor를 기절시키지 않도록 실제 몸체 차폐를 검사 | 콜백/Root는 대여. 실행 중 변경 거절. 미연결 독립 기존 범위 처리 보존. Sensor 후보가 먼저 나와도 Body 후보를 소진하지 않음 |
| PlayerAnimationTypes.cs | 기존 PlayerAnimationId 끝에 Interact 추가. PlayerAnimationEndReason=Completed/Interrupted/Missing 신규 enum | 장치 표현과 실제 종료 원인을 공개 계약으로 구별 | 기존 enum 번호 유지. 애니메이션 성공과 스킬 발동 성공을 같은 값으로 합치지 않음 |
| PlayerAnimationPlayback.cs | PlaybackEnded, endedRequestId, TryReportResult. 교체 시 이전 ID에 Interrupted; 완료/누락1회 발행; observer별 예외 보고 | 실제 출력 완료와 낡은 요청을 구별하여 사망 복귀·표현 해제가 지연되거나 중복되지 않도록 함 | TryComplete는 Completed 경로 위임. 이미 끝난/다른 ID 거절. 같은 우선순위 마지막 요청 재시작, 중단 표현 재개 없음 |
| PlayerAnimationPlayback.cs | Interact를 당시 Form과 같은 우선순위2에 포함 | 당시 추가된 장치 표현 선택 | **이 당시 값은 V1.5와 다르다.** 이번 최소 수정에서4로 분리했다. 이전 확장 자체를 최신 정합성 완료로 주장하지 않음 |
| PlayerAnimationController.cs | PlaybackEnded 구독/발행, TryReportResult, TryRequestInteract, Disable finally 구독 해제 | Unity 입력/상태와 실제 표현 선택기의 결과를 공개 연결 | 비활성·낡은 ID 거절. 관찰자 하나의 예외로 다음 관찰자 차단하지 않음. 장치 실행 규칙은 변경하지 않음 |
| PlayerAnimationSet.cs | interact AnimationClip 필드/Resolve 연결 | 실제 장치 상호작용 clip를 사용자가 드래그할 연결 지점 | 비 Legacy clip. 누락이면 기다리지 않음. 실제 clip가 연결·재생됐다는 증거는 별도 |
| PlayerAnimatorOutput.cs | KnockbackControlLockMilliseconds 프로퍼티(실제 Knockback clip만, 누락0) | 임의 시간이나 Idle 대체 clip 길이 대신 공통 피격 표현 길이 사용 | 반환 단위ms. 넉백 속도/피해량과 혼동 금지 |
| PlayerAnimatorOutput.cs | 출력 불가/클립·state 누락은 Missing; base Dead/Knockback 끝은 Completed; Disable은 현재 one-shot Interrupted 후 finally 정리 | 표현 누락 때 행동/사망 진행이 영원히 대기하지 않게 함 | 요청 ID 결과1회, runtime AnimatorOverride 해제 및 원래 Animator 설정 복원 |

원본 위치는 모두 `Assets/_Code/Core/Tech_HMS/Features/`다. 회복/피해 API를 유지한 이유는 당시 최소 additive 허가와 독립 소비 보존이다. 이 문서에 남은 호환 코드가 있다고 새 작업에서 모두 삭제할 권한이 생기지 않는다.

## 이번 V1.5의 세 파일 최소 변경

처음 허용한 두 애니메이션 파일과 이후 실제 접지각 읽기 getter의 허용을 구분한다. 허용 밖 HMS 원본·자산·meta·enum 번호·독립 기본값을 바꾸지 않는다. 정확한 이번 patch는 기본 `cd528ce` 대비 세 파일이며 추가9줄/삭제7줄이다. 대부분 설명 변경이고 실제 동작 변경은 아래 표의 두 계약이다.

| 파일 | 정확히 추가·삭제·변경한 내용 | 필요한 이유와 전후 결과 | 유지한 경계 |
|---|---|---|---|
| PlayerAnimationPlayback.cs | Interact를 Form과 같은 우선순위2 묶음에서 제거하고 별도4로 선택. 관련 우선순위 주석 수정 | 이전에는 Tear(3)가 Interact(2)를 덮었다. V1.5의 장치 상호작용 우선순위에 맞춰 현재 Dead(6)>Knockback(5)>Interact(4)>Tear(3)>Form(2)>Land(1)>기본(0)으로 처리 | 요청ID·완료/중단/누락·중단 one-shot 재개 없음 보존. 이동/점프 실행을 새로 막지 않음 |
| PlayerAnimationController.cs | TryRequestInteract의 우선순위 설명만 변경 | 실제 선택기와 API 문서가 같은 의미를 설명하도록 함 | 실행 코드·이벤트·구독·Disable 정리 변경 없음 |
| CharacterMovement.cs | public readonly getter `MaximumGroundAngle => maximumGroundAngle`와 설명 두 줄 추가 | 생성 발판의 지지 판정이 HMS Inspector에 작성한 실제 최대 접지각을 사용하게 함. 별도 45도 상수·가짜 접지 규칙을 만들지 않음 | 원래 직렬화 필드·기본값·범위·접지 계산·물리 writer 변경 없음 |

CharacterMovement는 로케이터나 새 SYM 접지 상태를 참조하지 않는다. SYM은 현재 Actor에서 기존 이동 컴포넌트의 실제 설정을 읽는다. 이 getter 추가를 다른 HMS 기능 재작성 허가로 해석하지 않는다. 세 파일의 최소 변경은 최종 token/scope 검사로 확인한다.

PlayerFormController의 정상/Small 점프 기본2/3, PlayerMovementModeController의 Quarter 기본3은 보호 소스 재작성 대신 **소유 prefab에서 정상3/Small2/Side5/Quarter5**로 작성했다. Root scale과 기존 Form 물리 크기 전환 계약은 유지했다. 직접 배치·Inspector 설정 변경과 보호 원본 source 변경 권한은 별개다.

현재 보호 Inspector의 나머지 영문 이름·Tooltip을 전면 변경하지 않았다. 사용자 친화화는 SYM 소유 Inspector와 조립 가이드에 반영했고, HMS 필드는 해당 원본 필드명과 연결 대상도 함께 설명한다. 보호 원본 전체 이름 변경이 이번 세 파일 허용에 포함된 것으로 주장하지 않는다.

## 이후 PR에 반드시 붙일 설명과 증거

추가: 어떤 사용자의 입력/상태/외부 연결이 기존 API로 표현되지 않아 새 계약이 필요한지, default/독립 소비/수명 영향, 단위와 결과를 적는다. 삭제: 호출·GUID·nested prefab·Editor/build 의존을 어디로 이관했는지와 제거한 기능이 기획에 없는 이유를 적는다. 변경: 같은 입력에서 전후 행동이 어떻게 달라지는지와 현재 기획 문단을 적는다.

검증에는 실제 사용한 source/commit/빌드 식별자, 실행 명령과 결과, 실행하지 않은 Play/실제 애니메이션/수동 동선을 구별한다. 과거 commit 메시지의 테스트 수는 그 당시 기록이며 이번에 재실행한 결과가 아니다. 이번 구현과 최종 비 Play 검증은 [테스트 및 검증 항목](<테스트 및 검증 항목.md>)의 Source/DLL/명령 근거로 구분한다. 실제 Play·클립 재생·사용자 리소스 표시 결과는 사람이 확인할 범위다.

HMS 협업자가 확인할 질문은 기존 enum/독립 기본값 보존, 대여 참조 해제, API ms/초 변환, 요청ID1회, 재진입 중 최신 상태 보존이다. PR 생성 시 해당 변경표를 최종 diff에 맞춰 다시 작성하고 실제 PR을 이 task에 연결한다. 지금은 PR을 생성하지 않는다.


## 이후 PR 설명에 사용할 변경 의도

현재 PR은 만들지 않았다. 사용자가 이후 PR을 지시하면 최종 diff와 실제 검증 결과에 맞춰 다음 내용을 포함한다.

“최신 V1.5에 맞춰 장치 Interact 표현이 Tear보다 먼저 선택되도록 우선순위를2→4로 분리했다. 선택기와 Controller 설명을 함께 맞췄으며 기존 request ID와 종료 계약, 이동/점프 실행은 유지한다. 생성 발판은 실제 ActorBody Collider의 침투와 지지를 판정하므로 HMS의 실제 최대 접지각을 읽는 readonly getter 한 개를 추가했다. 접지 필드·기본값·계산은 바꾸지 않는다. 점프3/2와 Side/Quarter속도5는 SYM 소유 prefab 값으로 연결했다.”

이전 두 commit의 입력/폼/피해/카메라/눈물/애니메이션 API는 위 역사 표로 별도 설명한다. 현재 PR이 그 이전 API를 새로 추가했다는 표현을 쓰지 않는다. 삭제22개는 파일별 대체 소유자·호출/저장 참조 이관·삭제 후 import/compile 근거, 씬 루트145개는 기존 world pose·물리·GUID 보존 비교를 적는다. 자동 cache 저장은 수동 기능 수정과 구분하고 실제 원인·전후 diff·보호 범위를 기록한다.
