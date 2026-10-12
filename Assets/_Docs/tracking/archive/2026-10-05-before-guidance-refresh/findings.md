# 확인이 남은 사항

## 현재 제한 사항 — T9 최종 제한 포함 승인

메인 에이전트가 전달한 Parfit의 최종 독립 리뷰 판정: 제품 코드·실행 근거는 제한 포함 approved이며 추가 제품 P1/P2는 없다. 리뷰어는 기존 코드·문서·실행 증거를 읽기 전용으로 대조했으며 직접 Unity 실행이나 재빌드를 수행한 결과가 아니다.

승인에는 raw settings 보존 gate 미충족, 전체 diffcheck exit2(m_Name 후행공백14곳), ProBuilder 추가2키 원인미확정, 강제 domain reload 미실행, legacy VisualRoot 예외, 이전 dirty 메모리 보존 확인불가, 실제 클립 재생 제외가 포함된다. 필수 모두 PASS·권한 외 변경0·설정 완전불변으로 해석하지 않는다.

- Editor 231946 및 Windows230609 두 판 입력 동선 통과. 강화 actualCaught/복원 관찰 독립 승인. 두 비율 UI 캡처와 타이틀 팀표기 수정 확인.
- 독립 physics 리뷰 P2 세 건 및 추가 negative: 233621 실측 Passed 후 approved로 해결. 플레이어 R16/R17/빨간 표현도 wrapper 종료 계약 수정 후 43검사 Succeeded. 플레이어43 및 wrapper 독립 리뷰 approved로 완료.
- 기존 비대상 독립 Play 표본에서 PlayerFormController 직계 VisualRoot 구성 예외. 진입 자체 성공과 게임 정상 여부를 구분하며 공유/타인 원본 무단 변경 금지.
- 실제 ExitingEditMode 취소 및 설정/setup 복원 통과. 최종 제품 반영 Windows000106 두판/종료0 확인. Parfit 전체 독립 최종 리뷰는 제한 포함 approved로 완료.
- TimeManager/GraphicsSettings native 정규화로 raw baseline 불변 미충족. 빌드가 InputSystem preloadedAssets/Connect 및 URP prefilter/runtime 목록을 기록한 부수 효과를 공식API 원복·감사한다. 외부 ProBuilder 보존. Unity prefab 빈 m_Name 후행공백으로 전체 diffcheck exit2, C#/Markdown 부분 exit0.

아래 배열 항목은 변경 전 정적 의심 기록이다. T68 이후 실제 EntrySettings/기존 Stage2·3 직접 Play 결과를 우선하며 최종 자산 감사와 함께 종결 여부를 기록한다.


## 직접 진입 해금 배열의 직렬화 값

조건: Assets/_Scenes/Tech_SYM/Architecture/Configuration/IntegrationEntrySettings.asset의 Stage 2·3 entries를 텍스트로 읽으면 abilities 값이 000000000200000001000000i로 끝난다. enum 배열의 예상 숫자열 밖에 문자 i가 포함되어 있다.

영향: Unity가 이 값을 어떻게 읽는지에 따라 직접 진입의 능력 해금이 기대와 다를 수 있다. 텍스트 이상은 확인했지만 Editor 역직렬화 실패나 런타임 고장을 재현한 것은 아니다.

현재 종결: T68에서 소유 EntrySettings를 Unity로 적용·재조회했고 PrototypeRegression 및 실제 Stage2/3 직접 Play가 통과했다. 이 아래 발견 설명은 변경 전 이력이다.

다음 확인: Unity Inspector와 IntegrationEntrySettings.Require 결과를 읽고, Stage 2·3 진입 시 능력 해금과 11개 Editor 진입 검증을 확인한다. 결함이 확인되면 Tech_SYM 소유 자산만 승인된 변경 범위에서 복구한다.


### 최종 ProBuilder 보존 감사 보정
기존 사용자 전달 외부변경 2키(autoUnwrap=true, ResetSettings=false)는 유지했다. 최종 파일은 T68의1f6ca5c 해시가 아니라244e4c85이며 experimental.enabled=false, editor.stripProBuilderScriptsOnBuild=true 두 키가 추가되어 있다. 설치 패키지 Experimental.cs11/UnityScenePostProcessor.cs20의 기본값과 일치하고 파일mtime23:06:31이지만 개별 저장 호출 주체를 확정할 근거는 없다. 따라서 전체 ProBuilder 파일이 T68 이후 raw불변이라고 주장하지 않으며 추가 두키는 원인미확정으로 분리 기록·보존한다. 외부기존키를 삭제하거나 임의원복하지 않는다. t9-final-settings-restoration.json 참조.
