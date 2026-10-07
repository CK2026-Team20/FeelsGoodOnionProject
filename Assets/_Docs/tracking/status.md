# 현재 요구와 검증 상태

2026-10-07 최신 직접 지시와 사용자 최종 선택을 적용해 구현 중이다. 확정안 원문은 `C:/Users/acac1/Desktop/시스템 정책 검토 요약.md`이며 SHA-256은 `65e1d74a01071972436764b9697f0f68b56085ef56f019c95374aaa4bca9ac8a`다. #11 원본 기획 문서 정리만 제외하고 HUD 안내 확정안은 적용한다. 이번 명세의 최소 HMS 공개 확장만 1회 허가됐으며 보호 원본 전체 수정 권한은 아니다.

| 항목 | 현재 확인 | 아직 남은 검증/구현 |
|---|---|---|
| 명세/권한 | 최종 선택·두 값 저장·현재 Stage 능력 재잠금·전체 Stage 최초 로드/이후 유지 반영 | 원본 DOCX/PDF 편집 없음 |
| T1 HMS 최소 확장 | 9개 .cs, 커밋 `5d1770d`; 실제 Editor 컴파일 오류0 및 공개 API/순수 계약10건 통과 | 실제 Interact/Dead/Knockback clip 재생·물리·Brain 완료 증거와 구분 |
| T2 수명/저장/입력 | 43ada64 통합·독립 검토 승인·메인 Editor 오류0·실제 두 값/원자성/재잠금 계약 PASS | 실제 Stage/접촉/Core callback 연결은 T9; 통합 Play/물리/Brain/빌드는 T10 |
| T3 카메라 | `cd85a52` 통합·독립 검토 승인·메인 Editor 오류0; 최신 요청/재진입/취소 소스 관찰17개 통과 | 실제 Brain·Side 정렬·초기 위치 모드와 자산 연결은 T9/T10 |
| T4 발판/장치/조각 | `915e5e9` 통합·독립 검토 승인·메인 Editor 오류0; 실제 소스 관찰82개 통과 | 실제 PhysX 접촉/운반·주기 침범·조각 재등장과 Stage 연결은 T9/T10 |
| T5 일반 적 | `14e2160` 통합·독립 검토 승인·메인 Editor 오류0; 소스 관찰11개 통과 | 실제 Collider 접촉·순찰 지형·기절 표시와 자산 연결은 T9/T10 |
| T6 상호작용/화면 | `31e8f0b` 통합·독립 검토 승인·메인 Editor 오류0; 소스135관찰/새 판 수명11관찰, 실제 Editor 대화 JSON 경계20관찰 통과 | 실제 UGUI/InputSystem/메모 강제 취소·Stage/Core 안내 연결은 T9/T10 |
| T7 연출 | `6e7829e` 통합·독립 검토 승인·메인 Editor 오류0; 단일 컷 소스17관찰/API 컴파일 완료 | 실제 원화/UI/InputSystem과 자산 연결은 T9/T10 |
| T8 작성 도구 | `55ee8ec`/`6613350`/`d5b1de6` 통합·독립 승인·Editor 오류0; 실제 Unity 두 번 작성37관찰/6씬/64출력/GUID 유지·Missing0 | 임시 작성 자산 검사이며 기존 자산 이관·통합 Play·새 빌드는 T9/T10 |
| T9 연결/정리 | T8 완료 후 격리 worktree의 단일 source writer와 main-only native 작성으로 실행 중 | 기존 자산 참조 이관·32개 지정 소스 삭제·수명 통합 검증 |
| PhysX 경계 | 실제 Unity ComputePenetration 8관찰 통과, preview 씬 닫힘·기존 setup/dirty 상태 동일 | Collider 접촉 콜백·Tween·Stage retry 전체 실행과 구분 |
| Unity 자산 읽기 | 통합6씬과 전체 소유12씬·58프리팹 Missing Script0, 앞선 통합 직렬화682기록; 원래 씬 setup 복구 | 새 구현 Play/Windows 빌드 통과를 뜻하지 않음 |
| 실제 클립/원화 | 기존 Opening/Ending 원화8장 확인; 플레이어 animation clip 슬롯은 미연결 | 임의 FBX Take를 피격/사망 클립으로 지정하지 않음; 제공/연결 후 실제 재생 검증 필요 |
| QA | 최신 확정안39케이스/V01..V34 전체 연결, 읽기 전용 Excel PDF18쪽 텍스트·배치 검수; 이전 원본 백업/FunQA 원본·작성양식 hash 보존 | 실제 빌드 판정39개·새 통합 기능 실행·새 빌드·팀 플레이·FunQA 평가 미실행 |

[기능 명세서](ready-system-v11-refactor-2026-10-06/기능 명세서.md) · [확정안 적용 명세](ready-system-v11-refactor-2026-10-06/확정안 적용 명세.md) · [실행 계획](ready-system-v11-refactor-2026-10-06/plan.md) · [검증 항목](ready-system-v11-refactor-2026-10-06/테스트 및 검증 항목.md). 씬 reload 여부의 두 문제와 대책은 확정안 적용 명세에서 비교한다. checkpoint payload는 Position/Fragments뿐이며 Stage/능력/필드 상태는 저장하지 않는다.

이전 미커밋 PlayerRoot/Ending Timeline/ProjectSettings 변경과 사용자 prefab 삭제를 보존하고 최초 Git diff로 추적한다. 새 실행의 자동 저장도 원인과 diff를 분류한 후 기존 커밋 지시에 포함하며 보호 원본 수동 수정 범위를 확대하지 않는다.

## 아래는 새 원본 적용 전 실행 이력

다음 원문은 이전 플랫폼/Editor 진입 작업의 당시 결과다. 아래의 ‘현재/최신’ 표현은 그 작업 시점에 한정된다. 새 E/F·월드 진행·추적 적·위치/조각 두 값 저장·분리 카메라 요구의 완료를 뜻하지 않는다.

## 현재 기능과 검증 상태

기준: 2026-10-06. 최신 승인 작업은 플랫폼 설정·HMS 조립·소스 정리다. 구현·새 컴파일·자산 검사, 실제 기능7묶음 및9씬 진입, 새 Windows 빌드는 완료했다. 최종 독립 검토는 차단·열린 권고0으로 승인됐고 기능별10커밋을 실제 push하여 Draft PR26을 생성했다. 사용자 수락·팀 실제 빌드 플레이·FunQA는 별도 상태다.

### 현재 플랫폼·정리 작업

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
| 저장소 반영 | 앞선250항목과 이번 변경을 기능별10커밋으로 push, 첫 원격 HEAD 543790c747 일치, [Draft PR26](https://github.com/CK2026-Team20/FeelsGoodOnionProject/pull/26) 생성·chat 연결 | 완료 기록은 별도 docs커밋으로 추가 반영. 최종 HEAD/clean 확인은 git-publication.json |

현재 근거는 [플랫폼 검증 기록](ready-platform-hms-cleanup-2026-10-06/테스트 및 검증 항목.md)과 같은 폴더의 코드 리뷰·자산 연결 가이드다. 기존 씬에서 실행할 제어된 검증과 실제 입력 전체 동선·팀 빌드 판정을 혼합하지 않는다.

### 앞선 Editor 진입·HMS 실행 이력

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

### 이전 검증 이력

2026-10-05 지침 정비·원본 대조·대화 취소 정리와14검사는 [당시 검증](2026-10-05-guidance-refresh-검증.md), Tooltip178/254필드 및 메모 E전용 확인은 [후속 검증](2026-10-05-inspector-ux-검증.md)에 보존한다. HMS Tooltip12파일62필드는 미승인이다.

이전 Windows000106의 빌드 Succeeded/errors0/warnings73·입력 두 판 및 UI/물리43검사 등은 [T9 최종 검증](2026-10-05-T9-최종검증.md)과 [T9 실행기록](2026-10-05-T9-실행기록.md)의 당시 증거다. 이번 소스의 새 빌드로 주장하지 않는다. 이전 상태 전체 원문은 [보관본](archive/2026-10-05-before-guidance-refresh/status.md)에 있다.

게임은 기능 응집 Prototype/1_Stage를 사용하며 Stage2/3 직접 진입 계약을 유지한다. 앱 재시작 후 진행 저장·서버·네트워크·본편 추가 배치는 현재 요구로 확정하지 않았다. 열린 문제는 [findings.md](findings.md), 권한은 [security.md](../security.md)를 확인한다.
