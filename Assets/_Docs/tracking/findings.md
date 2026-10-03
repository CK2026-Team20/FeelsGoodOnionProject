# 확인이 남은 사항

## 직접 진입 해금 배열의 직렬화 값

조건: Assets/_Scenes/Tech_SYM/Architecture/Configuration/IntegrationEntrySettings.asset의 Stage 2·3 entries를 텍스트로 읽으면 abilities 값이 000000000200000001000000i로 끝난다. enum 배열의 예상 숫자열 밖에 문자 i가 포함되어 있다.

영향: Unity가 이 값을 어떻게 읽는지에 따라 직접 진입의 능력 해금이 기대와 다를 수 있다. 텍스트 이상은 확인했지만 Editor 역직렬화 실패나 런타임 고장을 재현한 것은 아니다.

현재 처리하지 않는 이유: 이번 작업은 문서 작성이며 씬·자산 수정과 실행 복구를 포함하지 않는다. 의도된 해금 구성을 추측해 문자열을 고치지 않는다.

다음 확인: Unity Inspector와 IntegrationEntrySettings.Require 결과를 읽고, Stage 2·3 진입 시 능력 해금과 11개 Editor 진입 검증을 확인한다. 결함이 확인되면 Tech_SYM 소유 자산만 승인된 변경 범위에서 복구한다.
