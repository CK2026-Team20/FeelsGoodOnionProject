# 지침 최신화·프로젝트 정합성 검증

2026-10-05. 시작 기준점은 기존 미커밋 변경을 포함한 파일 해시 1,984개다. 기존 Git diff 전체를 이번 변경으로 계산하지 않는다. 증거는 프로젝트 루트의 output/Tech_SYM/guidance-refresh-2026-10-05/에 있다.

## 지침과 문서

- 적 머리 밟기 제거, 정확한 여섯 부트 경로, 최신 기획 우선, 고정 에이전트 체제 철회를 현재 지역 지침과 일치시켰다.
- status/findings의 과거 내용은 archive에 보존했다. 판정·내용은 그대로이며 상대 링크만 이동 위치에 맞춰 보정했다. 변경 전 바이트 사본은 output before와 .dryforge backup에 있다.
- 현재 작업 정본 세 파일과 실행 사본은 바이트 단위로 같다. 루트 AGENTS/CLAUDE도 같다. 현재 문서·보관본의 Markdown 링크 존재를 검사했다.
- 실행 그래프는 JSON-compatible YAML을 실제 파싱해 순환·없는 의존 ID·본문 작업 ID 불일치가 없음을 확인했다. PyYAML 미설치로 최초 검사는 실행하지 못했으며 이후 표준 JSON 파서로 YAML의 JSON 부분집합을 검증했다.
- 기존 공통 지침 28파일의 문자 비교는 document-metrics.json에 기록한다. 새 작업 문서·보관본은 제외하며 실제 토큰 소비 감소율로 해석하지 않는다.

## 제품 코드 수정

DialogueViewModel.Dispose는 Cancel의 외부 관찰자가 예외를 던질 때 구독 정리가 생략됐다. finally로 자기 구독과 표시 이벤트를 해제하며 예외는 전달한다. 변경 파일은 DialogueViewModel.cs와 DialogueRegressionTests.cs 두 개다. this-run.patch는 이번 실제 패치만 기록하며 변경 전 전체 파일 원문을 재구성한 백업이 아니다.

| 검증 | 결과 | 근거 |
|---|---|---|
| Unity 실제 컴파일 | 오류0, failed=false | implementation/compile-result.json |
| 예외 경로 전후 재현 | 폐기 VM 잔존 true→false | implementation/dialogue-before.json, dialogue-after.json |
| 다른 소비자·예외 전달·폐기 후 명령 | 보존 | 같은 재현 결과 및 회귀 검사 |
| 대화 회귀 | 14개 통과 | implementation/dialogue-regressions.json |
| 현재 Core/Prototype 및 소유 프리팹 | Missing Script 0; 프리팹59개 조사 | implementation/assets-live.json |
| 소유 씬·프리팹 설정 대조 | 메모 버튼0, 기존수집8 disabled/새수집8 enabled, 붕괴 canRegenerate=false, 적3종 스턴표현 연결 | implementation/assets-live.json, connections-live.json |
| Editor 상태 | Core(active)+Prototype 두 씬, dirty=false, Edit Mode, 시작씬·Play옵션 유지 | implementation/editor-initial.json, editor-final.json |

대화 검사는 Editor에서 실행한 서비스/VM 검사이며 실제 키 입력 Play나 전체 게임 동선 검사가 아니다. CLI status는 인스턴스 미발견이었으나 pipeline 연결과 실제 MCP 질의 성공으로 Editor 접근을 확인했다. 일시적 eval namespace 오류·재컴파일 직후 timeout은 후속 실행 성공과 구분한다.

## 프로젝트 대조 범위와 한계

implementation/matrix.json에 입력·폼, 눈물/수집/피해, 부트, 메모/모달, 옵션/MVVM, 대화, 컷신, 카메라/체크포인트, 발판, 적/추격, 보호영역을 기록했다. 새 제품 결함은 대화 수명 한 건을 확인·수정했다. 다른 항목은 현재 소스·저장 자산·필요한 native 조회의 정합성 확인이며 전체 실행을 다시 통과시켰다는 뜻이 아니다.

추가 Side 트리거는 첫 통로 뒤쪽 (42,3,-1.7)에서 기존 Side ID를 재사용하는 귀환 경계다. 마지막 QuarterB(105,3,36) 뒤의 여섯 번째 진행 구역이 아니다. 위치·authoring·연결을 대조했으며 실제 역주행 물리 검사는 수행하지 않았다. 방향 전용 필터가 있다고 주장하지 않는다.

HMS Tooltip 12파일62필드와 레거시 설명, 기존 독립 VisualRoot 예외, 과거 설정 raw차이·ProBuilder 원인·domain reload 미실행 제한은 유지한다. 실제 모델/클립 재생·화면 픽셀·전체 Play·빌드는 이번 변경에서 실행하지 않았다. 파일 소유권을 넘는 변경과 씬 저장·전환·Git 쓰기는 수행하지 않았다.

## 최종 감사와 독립 검토

validation.json은 기준점 대비 보호 영역, 기존 meta, 새 GUID 중복·누락, 현재 링크, 정본/사본을 검사한다. history-check.json은 보관 내용과 보정 링크를 검사한다. 독립 최종 검토는 approved/차단0으로 완료됐다. 리뷰어가 기준점1,900파일을 독립 재비교했으며 문서 외 차이는 지정된 C# 두 파일뿐이었다. 리뷰어가 Unity 실행을 새로 수행한 것은 아니다.

경미 제안2건은 메인이 반영했다. 씬 경로 변경은 설치 자산과 BootstrapEntryRoute의 고정 목록을 함께 확인하도록 명시했고, 작업 간 의존 관계의 일방적 재판단 금지·영향 실행 중단·계획 갱신·재검증을 보강했다. 이는 문구 정비이며 제품 코드를 추가 변경하지 않았다. 원래 독립 판정은 review/doc-review.json에 보존한다.

독립 문서 검토 완료 후 .dryforge/status.json에 initialized=true를 기록·재조회한다. 이 값은 프로젝트 문서 초기화만 의미하며 게임 전체 검증이나 사용자 최종 수락을 뜻하지 않는다. 최종 파일·링크·GUID 감사와 문서/C# diffcheck 실행값은 output의 final-summary.json에 기록한다.
