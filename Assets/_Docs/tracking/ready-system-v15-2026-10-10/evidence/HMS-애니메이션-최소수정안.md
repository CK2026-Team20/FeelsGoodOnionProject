# V1.5 애니메이션 우선순위의 최소 수정안

현재 소스에는 적용하지 않은 검토용 제안이다. 기존 HMS 수정 허가는 이전 작업 1회용이므로 이번 수정 권한으로 사용하지 않는다.

V1.5 P0201은 `사망 > 피격 > 장치 상호작용 > 눈물 > 크기 전환 > 착지`를 요구한다. 현재 `PlayerAnimationPlayback.GetPriority`는 장치 상호작용과 크기 전환을 모두 2, 눈물을 3으로 반환한다. 따라서 눈물 재생 중 장치 상호작용 표현 요청이 거절된다. 공개 `TryRequestInteract`로 이 private 우선순위를 바꿀 수 없다. SYM에 별도 Animator 우회 경로를 만들면 표현 소유자가 둘이 된다.

| 파일 | 현재 | 최소 수정 | 이유 |
|---|---|---|---|
| `Assets/_Code/Core/Tech_HMS/Features/PlayerAnimationPlayback.cs` | Interact=2, Tear=3, Form=2, Land=1 | Interact만 4로 분리. 클래스·메서드 설명도 같은 순서로 교정 | 기존 선택기 하나에서 V1.5 우선순위를 처리 |
| `Assets/_Code/Core/Tech_HMS/Features/PlayerAnimationController.cs` | TryRequestInteract 설명이 “형태 전환과 같은 우선순위, 눈물보다 낮음” | 설명을 “눈물보다 높고 피격·사망보다 낮음”으로 교정 | 실제 동작과 공개 API 설명 일치. 메서드 동작 변경 없음 |

검토할 동작 diff는 아래와 같다. enum 번호, RequestId, 종료 결과, 입력·스킬 발동, 이동·점프, 기존 피해·사망 처리에는 변경을 추가하지 않는다.

```diff
 case PlayerAnimationId.Land: return 1;
 case PlayerAnimationId.Shrink:
-case PlayerAnimationId.Interact:
 case PlayerAnimationId.RestoreForm: return 2;
 case PlayerAnimationId.Tear: return 3;
+case PlayerAnimationId.Interact: return 4;
```

승인 후 필요한 검증은 실제 표현 선택기에서 Tear→Interact 교체, Interact 중 Tear 거절, Interact 완료 뒤 최신 기본 표현 복귀, 사망·피격 우선 유지, 중단된 일회성 표현 재개 없음이다. Play Mode는 에이전트가 실행하지 않는다. 현재 이 검증도 실행하지 않았다.
