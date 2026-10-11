# 현재 요구와 검증 상태

2026-10-11. 현행은 V1.5 활성 본문과 사용자 후속 직접 선택이다. 승인된 [spec](ready-system-v15-2026-10-10/spec.md)과 [plan](ready-system-v15-2026-10-10/plan.md)을 구현하고 실제 소스·소유 참조를 이관했다. 이전 버전의 Play·빌드·QA 수치는 아래 당시 이력으로만 보존하며 이번 통과로 승계하지 않는다.

## 2026-10-11 기능 객체 중심 구조 정리

이번 직접 지시에 따른 [명세](structure-simplification-2026-10-11/기능%20명세서.md), [코드 리뷰](structure-simplification-2026-10-11/코드%20리뷰.md), [검증](structure-simplification-2026-10-11/테스트%20및%20검증%20항목.md)를 적용했다. 추격은 접촉 트리거와 +X 직선 이동/전면 Box 포획으로 바뀌었고 경로·전신 통과 및 중간5소스를 제거했다. Stage 작성필드는 초기 CP/음악2개다. 퇴로는 시작 트리거가, 카메라/CP 앵커는 소유 객체의 자식이 된다. 기존 Enter4개·경로점16개를 제거하고22개 기준점 월드 좌표를 보존했다. HUD 미사용 필드와 Host Inspector를 정리하고 HMS36소스를 감사했다.

해당 작업트리 Unity 전체 컴파일과4Stage/Core 비 Play native 연결 검사는 통과했다. 실제 포획·retry·한 판 Play/빌드는 미실행이다. 애니메이션 리소스 누락과 HMS 입력 우회 경계는 [감사](structure-simplification-2026-10-11/HMS%20연결%20검토.md)에 기록했다. LFS 원본 복원 및 Editor URP 자동 저장 diff를 검토했고 기존 사용자 폰트 변경을 보존했다. Git 커밋/원격 작업은 하지 않았다. 아래 이전 구현·검증은 당시 이력이며 새 추격의 Play 성공으로 승계하지 않는다.

## 2026-10-11 조립 시작·종료 오류 후속 수정

제품 Source는 `6dd87e4fad53f9af523498f47292def3ba3a117e`이며 로컬 `feature/core/systems`에 반영했다. 앞선 `c2c3b6b`에서 App 조립을 Awake에서 Start로 옮긴 뒤, 사람이 실행한 Console을 읽어 장치·적의 로드 중 참조 거절과 종료 시 Stage 재검색 예외를 확인했다. `Flow revision 1: LoadingGame, busy=True` 자체는 정상 상태 Log다.

같은 씬의 실물 참조·고정 지점 검사는 Awake/OnEnable에서도 가능하게 했다. 씬 전체 검색은 로드 완료 조건을 유지한다. Bootstrap은 Flow 준비 → 옵션·페이드·오디오 연결 → Boot 순서로 실행하며, Core 해제는 연결 당시 보관한 카메라 구간·주기 발판만 해제한다. Inspector 값·씬·프리팹·GUID·HMS 원본을 수동 변경하지 않았다. 파일별 원인·의도·전후는 [코드 리뷰](ready-system-v15-2026-10-10/코드%20리뷰.md)에 있다.

현재 Source 전체 컴파일은 Runtime158/Player158/Editor11 모두 성공했다. 멈춘 Editor의 실제 제품 DLL 검사38개와 등록 Stage3곳의 지점 검사21개가 통과했다. 실제 기동·Play 종료 및 자연 충돌은 사람이 재확인해야 한다. 이번 수정 뒤 새 Windows 빌드를 만들거나 Play/Player를 실행하지 않았다. 아래의 이전 204/201 검사와 Windows 빌드는 V1.5 수락 당시 Source의 증거이며 이 후속 수정의 실행 증거로 승계하지 않는다.

TMP 폰트의 앞선 자동 glyph/atlas 변경은 diff를 보존·검토했다. 후속 사람 실행 종료 후 실제 파일은 기존 Git 원문 SHA `5637afc73bd49fc8568ea1a92e2b789775a54ca2ba7e69e7f0317d1abfbc040d`와 같아 현재 변경이 없다. 에이전트가 폰트 reset/restore/수동 편집을 실행하지 않았으며 빈 자동 변경 커밋을 만들지 않았다.

## V1.5 수락 당시 구현

| 영역 | 실제 변경 | 남은 사람 작업/확인 |
|---|---|---|
| 씬 흐름 | Bootstrap→Title→Opening→Prototype한번→최종포포대화→Ending→Title. 등록Stage독립진입·기존standaloneHost유지 | 실제 전체한판/다음판과 각독립Stage Play |
| Inspector | 위치/끝점/대상/Scene/대화/음원 실물참조·선택. SYM109source/표시199필드 이름/Tooltip·최종연결표 | 사람의 새prefab/실제자원연결. 보호HMS전체Inspector이름변경은이번허용밖 |
| 체크포인트 | Position/Fragments/해금마스크3항목원자저장.첫저장전/같은Marker/추격직전저장·같은Stage초기재준비복원 | 실제센서접촉/원본레벨동선 |
| 물리/기믹 | Tear전체Root/Collider이동.주기시간조절·실제세로침투<1/4착지·≥1/4몸체위분리/사망.껍질독립분리·데드존같은객체반환 | 자연collision/착지·실제clip·좁은레벨조건 |
| 씬루트/참조 | 관리자하위145루트이관.기존1670객체pose/물리/GUID·1877enabled보존,219참조검사와후속실제Floor4참조수리/5Bind검사 | 새리소스prefab의배치instance필수지점연결 |
| 대화/UI | Skip한명령경로·Options/Log/Auto/수명보존.최종Escape7만장부보존한새회차,전체context검증·동시죽음우선 | 실제버튼/폰트·스프라이트·전체동선 |
| 소스정리 | 자동Builder12+중복runtime10및자기meta실제제거,유지109필요성/수명/대체근거 | HudIconGraphic조건부1개:모든하트/Q/E/조각Graphic소비이관후삭제 |
| HMS공개계약 | PlaybackInteract2→4/설명,Controller설명,CharacterMovement실제접지각readonlygetter/설명만 | 이전허가종료.이후PR에정확한추가/삭제/변경의도명시 |

## 실제 검증과 인계

현재 최종 센서 수정 Source는 a192d2a7, 부모 로컬 반영은8659744b다. 전체 SDK Runtime158/Player158/Editor11 exit0, 현재 Player DLL204와 Native201(새 센서46 포함) 평가 실패0, 실제 Windows6씬 빌드 오류0·경고48, 설정29개/자동 URP-TMP/추가 자산 변화0을 확인했다. CP A→B→A와 최종 포포 재접촉의 영구 once 문제를 StageTrigger 한 파일에서 고치고 기존 CP8/Escape2의 once1과 참조를 보존했다.73자산/1670기존객체/145루트 보존 검사는 앞선 T13의 실제 자산 이력이며 이번 Source-only 수정으로 배치를 바꾸지 않았다. 원래 새 Native r2의3사례/4행 실패와 이전 Memo·selection fixture 실패, Completed 정상 Native 시계·자연 접지/표시 미평가는 [검증 기록](ready-system-v15-2026-10-10/테스트%20및%20검증%20항목.md)에 보존한다. 첫 독립 검토의 차단3/비차단2 중 Quarter 안내/포인터·역사 링크3건은 문서 재검토에서 해결 판정을 받았다. 수정된 Source/실제 증거/제작 문서 전체의 최종 독립 재검토는1b7069f1까지 approved/남은 차단0이다. 첫 issues 보고서는 보존한다. T14 기술 인계와 사용자 최종 수락을 마쳤다. T15와 사람 Play·리소스는 대기다. 실제 판정은 [코드 리뷰](ready-system-v15-2026-10-10/코드%20리뷰.md)에 기록한다.

[수동 제작 가이드](ready-system-v15-2026-10-10/씬%20흐름과%20수동%20프리팹%20제작%20가이드.md)에 컴포넌트부착/설정자산/Hierarchy·물리예외/실제드래그대상/기존GUID소비교체순서를 적었다. [HMS변경기록](ready-system-v15-2026-10-10/이전%20Tech_HMS%20변경%20기록.md)은 이전2commit10고유소스와이번3파일최소변경을분리한다.

에이전트 Play 버튼·API/CLI/pipeline/batch PlayMode·직접 플레이·Player 실행은 하지 않았다. QA/FunQA 문서를 요청 없이 새로 작성/평가하지 않았다. 기존 AI prefab 삭제·재생성·Apply·리소스 제작도 하지 않았다. 최종비Play결과와사람리소스/플레이완료를구별한다. 사용자가 최종 결과를 수락하고 로컬 `feature/core/systems` 병합을 지시했다. 원격 push·PR·main 병합은 수행하지 않았다.

## 최종 수락과 문서 보관

2026-10-11 사용자가 결과·인계 문서의 검토 종결과 활성3문서 보관을 승인하고 Systems 브랜치 병합을 지시했다. 실제 브랜치명은 `feature/core/systems`다. 앞선 `.dryforge/001/` 보관본을 보존하고 이번 실행용 handoff/spec/plan을 `.dryforge/002/`로 보관했다. 실행 루트에 활성3문서를 남기지 않고 기존 `initialized: true` 표시는 유지한다. 이 수락은 T15·사람 리소스 제작·실제 Play·미평가 Native 항목의 완료 판정이 아니다.

승인·보관·병합 식별과 보존 검사는 `output/Tech_SYM/Execution/system-v15-2026-10-10/evidence/systems-merge-completion-r1.json`에 기록한다. 검토된 Source와 씬·프리팹·설정에 추가 변경이 없으면 기존 실제 컴파일·DLL 평가·빌드 결과를 해당 범위에 재사용한다.

## 이전 V1.1 실행 기록 (현재 명세/이번 통과 아님)


2026-10-08. 최신 직접 지시와 최종 선택을 구현했고 소스·저장 자산·자동 기능·지정 씬 진입 및 새 Windows 빌드를 확인했다. 원본 정책 SHA256은 `65e1d74a01071972436764b9697f0f68b56085ef56f019c95374aaa4bca9ac8a`다. #11의 원본 기획 문서 정리는 제외하고 HUD 안내 규칙은 적용했다. HMS 최소9소스의 한정 확장 외 보호 원본 수동 수정 권한은 없다.

| 영역 | 현재 확인 | 남은 확인·작업 |
|---|---|---|
| 저장·재시도 | Position/Fragments 정확히2값, 실제 START/END만 저장, 현재Stage 능력/안내 재잠금·트리거 재접촉, 이전Stage 능력 보존 | 원본 전체 동선의 수동 평가 |
| 씬·서비스 수명 | Core1회 뒤 Stage3개 최초 additive 로드, 재시도/Title/다음 판에 같은 씬 유지, App locator·한 판 서비스 구분, 소비 조각 동일 객체 복구 | 씬 추가 시 동일 계약 검증 필요 |
| 카메라 | App 출력1/Core리그2, 초기Quarter ready, 종료guard와 겹친 구간 체류1회 제출·실제4관찰 | 실제 빌드 UI/UX·픽셀 평가 |
| 입력·상호작용 | 방향키/Space/Q/E/F, Q공간 거절·1초, 직접몸체 E/무환급, F거리/각도/차폐/동거리·메모 정지 | 모든 원본 배치의 수동 동선 |
| 적·발판·장치 | 실제 PhysX10시나리오238관찰, 적 Idle/X/Y·재기절, 붕괴1초/비복구, 주기5/2/3·운반·압력반전·장치F | 실제 미연결 표현 clip 재생 |
| 위험·사망 | 실제 피해35/얕은기름26/깊은기름28/강제사망32, 통합 사망·옵션 두경로각10 | 깊은 기름은 fall-plane fixture; 원본 배치수락은 별도 |
| 추격·포포·대화 | 전신 시작선·새저장없음·포획, 포포 Completed→Ending→Title·취소/사망 우선, Enter/Auto/로그·메모 자기토큰 | 준비 위치·서비스 요청 포함; 수동 원본 동선 별도 |
| 오프닝·엔딩 | 기존 원화8장/각4컷×4초와 승인 임시 문구, 실제8PNG·옵션/Skip 입력 | 최종 스토리 원고·실제 animation 재생 |
| 소스·자산 정리 | 지정32소스/소유meta삭제,7호환필드/11자산재저장,12씬/61프리팹 삭제참조0/Missing0/invalid0 | 사용자 기존 prefab 일괄제거는 수행하지 않음 |
| 작성 도구 | 실제2회/37관찰/64출력·GUID 유지, Temp정리 뒤1991기준점 변화0 | 기존 사용자 자산의 전체 재생성을 수행하지 않음 |
| 최종 자동 검사 | 현재174소스의36시나리오584관찰,6통합+3직접 Play/Stop, Stage2/3 이전능력각6, 실제 컴파일 실패 없음 | teleport·PositionPrepare·복제/격리fixture를 포함한 범위 |
| Windows 빌드 | 2026-10-07T22:45:58.9107793Z~2026-10-07T22:46:14.9169095Z 지정6씬 Succeeded/errors0/warnings57, 설정추가변경0/setup동일, 새8초 headless기동 | Windows 수동 그래픽·UI/UX·FunQA 대기 |
| 보호·자동 변경 | 432기준점/HMS9 exactT1/원본 DOCX·정책 보존, 예상외0. URP EOL과 정확한InputActions preload1개만 자동분류 | 수동 보호원본 수정으로 확대하지 않음 |
| QA 인계 | Excel 자동근거13행과 마지막 수정3행 추가·39케이스 유지, 스타일·FunQA원본hash 보존 | 수동39결과 미실행/FunQA 평가 대기 |
| 애니메이션 | 12AnimationClip+controllerTemplate1 참조가 null인 상태와 누락완료대체를 명시 | 13항목 제공·연결 후 실제 재생 필요 |
| 최종 독립 검토 | approved·차단0·열린 권고0. 2026-10-08 사용자 지시로 검토 종결 | 애니메이션13·수동QA39·FunQA는 미완료로 유지. [종결 기록](<ready-system-v11-refactor-2026-10-06/코드 리뷰.md>) |

현재 명세는 ready-system-v11-refactor-2026-10-06의 기능 명세서·확정안 적용 명세를 따른다. 코드 리뷰와 테스트 및 검증 항목에서 기능별 실제 관찰·남은 조건을 구분한다. 원본 DOCX/PDF를 정리하거나 지우지 않았다. source/API·자동 Editor·엔진 빌드·headless 초기기동·팀 수동 플레이·FunQA·실제 clip 재생은 서로 다른 판정이다.

최종 바이트 식별과 실행 근거는 `output/Tech_SYM/verification/system-v11-refactor-2026-10-07/T10-final-verified-snapshot.json`이다. 최초 빌드의 설정 보존 실패와 후속 성공은 각각 보존한다. 기존 사용자/Unity 자동 변경을 포함한 로컬 커밋은 승인 범위이며 push/PR/원격 병합은 별도다.

2026-10-08 사용자가 이번 작업의 검토 마무리를 지시했다. 구현·자산 연결·자동 검증 결과의 검토를 종결하고 활성 명세·계획·인계 3문서를 `.dryforge/001/`로 보관했다. 문서 바이트와 기존 `initialized: true` 표시는 보존했으며 `.dryforge/` 루트에 활성 3문서를 남기지 않았다. 검토 종결은 대기 항목의 완료 판정이 아니다. 애니메이션 12개 클립·컨트롤러 템플릿 1개 연결 및 실제 재생, Windows 수동 QA39건과 FunQA 평가는 미완료다. 기존 실행 근거는 보존하며 이번 종결에서는 코드·게임 자산을 변경하거나 기능 검증·빌드를 다시 실행하지 않았다.

원본 전체 입력 검증: 카메라 접촉 수정 뒤 실제 Editor36시나리오584관찰·6통합/3직접 Play와Stop을 다시 통과했다. 원본 동선 두 판은 실제 방향키/Space/Q/E/Enter/Esc와 UGUI 이벤트로 Title→Opening→Play→각 낙사·적 접촉 사망→재접촉 해금→END→POPO→Ending→Title까지 완료했다. 자연 사망4회와 143관찰 모두 통과했으며 Core/3Stage·출력·수집물/적 identity는 유지됐다. 전체 동선 검사는 teleport·복제 fixture·직접 ForceDeath/Flow 요청·능력/조각 상태 쓰기를 사용하지 않았다. Editor 합성 입력이며 Windows 실행 파일의 수동 UI/UX·QA39케이스·FunQA와 실제 미연결13 animation 재생은 별도 대기다.

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

## 2026-10-11 HMS 기본 입력 이관

HMS PlayerController 조립, Host의 F/사망 진행, Bridge의 모달/슬롯 정책/피드백 연결과 Router 삭제를 완료했다. 비 Play 컴파일·프리팹2/씬4 참조·API9 검사가 통과했다. 실제 Play는 미실행이다. [검증 기록](structure-simplification-2026-10-11/HMS%20입력%20조립%20검증.md)을 따른다.
