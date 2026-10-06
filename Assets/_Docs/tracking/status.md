# 현재 기능과 검증 상태

기준: 2026-10-06. 최신 승인 작업은 플랫폼 설정·HMS 조립·소스 정리다. 구현·새 컴파일·자산 검사, 실제 기능7묶음 및9씬 진입, 새 Windows 빌드는 완료했다. 최종 독립 검토·기능별 커밋/push 결과는 이번 검증 기록에 추가한다. 사용자 수락·팀 실제 빌드 플레이·FunQA는 별도 상태다.

## 현재 플랫폼·정리 작업

| 영역 | 현재 확인 | 남은 항목 |
|---|---|---|
| 부모 기준 3차원 선형 발판 | 오프셋+속도, 기존26배치 끝점 보존, 실제 왕복·HMS 탑승·점프·정지·비정상 설정 통과 | 기존 씬에서 제어한 기능 검사이며 팀 동선 평가는 별도 |
| 트리거 로컬 정렬 | 로컬 설정/월드 요청 분리·파란 OnDrawGizmos, 트리거6/체크포인트10 기준점 보존, 실제 Brain·물리·취소/중첩/정지 통과 | 공유 기즈모 계산·소스 확인, 파란 픽셀 캡처 미검증 |
| 붕괴 Animator 준비 | Controller·소유7배치 Animator 연결, 실제 접촉·경고·Collider 해제·숨김·복구/제거·재사용 통과 | Motion 비움. 실제 붕괴 클립은 자산 제공 후 연결·재생 확인 |
| 더미·중복 정리 | 테스트/더미39+회전 중복1 소스/메타, 빈2폴더 삭제; GUID 참조0·새 컴파일·결합 기능/빌드 통과 | 실제 작성·Play 진입·빌드 도구 및 역사 자산 보존 |
| Inspector·지침·QA | native Inspector250필드 누락0, 쉬운 설명·조립/삭제·자동 변경 정책 반영,39케이스와 이번 자동 증거 갱신 | 수동39판정·FunQA 서식/평가 보존. 보호 HMS62필드는 F01 |
| 저장 자산 검사 | 소유59프리팹·12씬, 42이관 기준점 일치·Missing Script0 | 실제 클립 재생 성공을 뜻하지 않음 |
| Bootstrap 출력·실제 진입 | 세션 출력 준비 시 앱 대기 Camera/Listener 해제. 실제9진입·두 세션·retry·Opening/Ending에서 출력/Listener1 | 실제 입력 전체 동선·새 Reload 조합 전수·픽셀 품질과 구분 |
| 이번 Windows 빌드 | Windows-platform-20261006-034111Z, 명시적6씬, Succeeded/errors0/warnings43, 전후 설정 차이0 | 팀 실행 파일 플레이·UI/UX·FunQA 미실행 |
| 보호·자동 저장 | 원래1,025개 기준점 보존, 승인 자동 저장5건만 변화, 나머지0; 남은 기존 .meta 변화0 | 자동 저장도 원인/diff 감사하여 커밋에 포함 |
| 저장소 반영 | 앞선250항목 포함과 자동 변경 커밋/push 승인 | 최종 기능별 커밋·실제 push·PR 예정 |

현재 근거는 [플랫폼 검증 기록](ready-platform-hms-cleanup-2026-10-06/테스트 및 검증 항목.md)과 같은 폴더의 코드 리뷰·자산 연결 가이드다. 기존 씬에서 실행할 제어된 검증과 실제 입력 전체 동선·팀 빌드 판정을 혼합하지 않는다.

## 앞선 Editor 진입·HMS 실행 이력

아래는 플랫폼 설정을 바꾸기 전 완료한 Editor 진입·HMS 작업이다. 당시 구현 및 자동 검증과 독립 검토 차단0을 기록하며 새 플랫폼 동작·새 빌드의 통과로 재사용하지 않는다.

| 영역 | 최신 확인된 결과 | 남은 제한·근거 |
|---|---|---|
| 활성 씬 Editor Play | 정확한 통합6씬은 Bootstrap 경유 후 선택 Stage/Title로 도착, 비대상4씬은 직접 Play. 실제 진입10·start/Reload 조합6·취소1 통과, 설정/씬 setup/일회성 route 복원 | [현재 검증](ready-editor-entry-hms-2026-10-06/테스트 및 검증 항목.md), 순수 route78·Hook callback97은 실제 Play와 구분 |
| HMS 컴포넌트·Cinemachine | 기준 01_PlayerMovementTest의 공개 계약으로 소유 Actor/독립 씬을 조립. native16씬·65프리팹에서 SYM Missing/필수 null/폼 hierarchy 오류0. 독립3씬 Q/Q/F·방향 입력, 네 모드의 실제 Brain/축 확인 | 보호 HMS PlatformManager Missing1은 F09. 실제 모델/클립/착지·픽셀 품질은 제외 |
| 카메라·세션 결합 | 실제 trigger·Brain 완료·평면 정렬·축 확정, 실제 Esc 정지/재개, 두 게임/게임당2 retry에서 새 Actor·UI·one output/listener 수명 통과. 물리 결합 검사 통과 | 제어된 공개 요청/런타임 접촉 검사는 전체 입력 동선과 구분. reload 중 엔진 Hierarchy UI Assertion4는 F10 |
| 통합 게임 입력 동선 | 기존 Prototype에서 실제 InputSystem으로 두 판 Title→Opening→Play→실패/Retry→Ending→Title 완료, fullLoopVerified=true, 캡처16장 | 새 Windows 빌드·실행 파일 검증을 뜻하지 않음 |
| 원본·작업 상태 보존 | 기존2004파일 baseline 기준 보호/기존meta변경0, 기존 staged diff 보존. 사용자 지시에 따라 미저장 Prototype을 마지막 저장 상태로 reload 후 검증, 종료 후 같은 씬 clean·start/Reload baseline 복원 | 과거 미저장 메모리 확인불가 F07과 별개. 무관한 TMP atlas 변경은 이번 실행 전 소유 파일의 정확한 바이트로 복원 |
| 기능 테스트·FunQA 인계 | 기존36 QA 결과/서식을 보존하며 7대분류·26기능·39케이스로 갱신. Editor 자동 증거13행을 별도 시트에 기록. 읽기 전용 Excel PDF14쪽 검수, FunQA DOCX 해시 불변 | [인계 명세](2026-10-05-functional-qa-spec.md), [케이스 정본](2026-10-05-functional-qa-cases.md). 팀 빌드 결과·FunQA 평가는 미실행 |

현재 구현의 요구사항별 변경과 독립 리뷰는 [현재 코드 리뷰](ready-editor-entry-hms-2026-10-06/코드 리뷰.md), 프로그래머의 진입 목록/Stage 확장은 [수정 가이드](ready-editor-entry-hms-2026-10-06/프로그래머 수정 가이드.md)를 따른다. 자동 검증 및 독립 검토는 사용자 최종 수락이나 보호 파일 수정 허가가 아니다. 승인 명세·계획·인계는 수락 전까지 활성 상태로 유지한다.

## 이전 검증 이력

2026-10-05 지침 정비·원본 대조·대화 취소 정리와14검사는 [당시 검증](2026-10-05-guidance-refresh-검증.md), Tooltip178/254필드 및 메모 E전용 확인은 [후속 검증](2026-10-05-inspector-ux-검증.md)에 보존한다. HMS Tooltip12파일62필드는 미승인이다.

이전 Windows000106의 빌드 Succeeded/errors0/warnings73·입력 두 판 및 UI/물리43검사 등은 [T9 최종 검증](2026-10-05-T9-최종검증.md)과 [T9 실행기록](2026-10-05-T9-실행기록.md)의 당시 증거다. 이번 소스의 새 빌드로 주장하지 않는다. 이전 상태 전체 원문은 [보관본](archive/2026-10-05-before-guidance-refresh/status.md)에 있다.

게임은 기능 응집 Prototype/1_Stage를 사용하며 Stage2/3 직접 진입 계약을 유지한다. 앱 재시작 후 진행 저장·서버·네트워크·본편 추가 배치는 현재 요구로 확정하지 않았다. 열린 문제는 [findings.md](findings.md), 권한은 [security.md](../security.md)를 확인한다.
