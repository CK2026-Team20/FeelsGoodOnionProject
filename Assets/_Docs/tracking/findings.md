# 열린 문제와 검증 제한

2026-10-16 빌드 인계 갱신: 사용자 현재 상태 빌드·PR 요청으로 Stage 사본 보존 후 원본 저장을 완료했다. 아래 외관 작업의 저장 대기는 당시 상태다. Windows64 빌드는 Succeeded(오류0/경고52)이며 Player 실행 QA는 미실행이다. Sphere Read/Write·메시34개 collision pre-bake·기존 플러그인 메타/API/셰이더 경고의 상세는 prototype-build-pr-2026-10-16/테스트 및 검증 항목.md에 기록한다. 해당 경고 해결을 위해 보호 원본을 임의 수정하지 않았다.

## 2026-10-16 외관 작업에서 확인한 항목

- **기존 Patrol 배치 오류:** 조명 변경 전 baseline부터 EnemyActor.OnEnable의 초기 위치/순찰 끝점 안전 높이 검사가 실패한다. 새 실행·Retry에서도 재현. 외관 변경 범위 밖이므로 레벨·게임 코드 수정 없이 기록한다. output/Tech_SYM/prototype-look-2026-10-16/console.json 참조.
- **NativeArray 수명 경고:** 외관 Play 검증 중 JobTempAlloc 수명 경고를 관측했다. 원인 확정/해결을 주장하지 않는다.
- **Stage 원본 저장 대기:** 시작 시 미저장 Plane/ProBuilder 편집이 있어 기존 편집 및 추가 Lighting/PostProcessing을 모두 dirty 상태로 유지했다. 별도 native 사본 보존 후 원본 저장 확인 필요.
- **색감과 성능 범위:** 현재 실제 재질은 목재여서 라벤더 모델 자체의 최종 외관 검증은 별개다. A/C/D/F 비교 화면과 E Quarter 화면을 제공하며 사용자 미감 승인은 대기. Editor 1080p 제한 표본을 전체 경로60fps 보장으로 확대하지 않는다.

## 2026-10-16 현재 남은 항목

| 항목 | 확인한 사실·영향 | 후속 범위 |
|---|---|---|
| 백페이스 노출 | 반대편 카메라 이동은 사용자 철회로 복원. 원본 아트는 뒷면 메시가 없어 기존 구도에 노출 구간이 남음 | 사용자가 배치를 회전하는 방향. 보호 아트/HMS 임의 수정 없음; 완료 주장 금지 |
| 메모 콘텐츠 | 실제 MemoUI/Canvas 연결·열기/닫기·정지/옵션 중첩 통과. 원본은 PlaceholderMemo_1 | 실제 본문 이미지 제작/배치 후 콘텐츠 검토. 연결 결함과 구분 |
| 입력 동선 범위 | 12CP/F끝까지 실제 Keyboard 입력·사망/Retry 포함. 변경부는 별도 저장 후 회귀 | 최종 저장판 무사망 단일 완주·모든 경사/종횡비/Small 왕복 검증은 없음 |
| E 기절 관측 | 조각5→0 소비 확인, 해당 시도에서 적 IsStunned 성공은 미관측 | 실제 유효 표적/가림 조건에서 별도 기절 검증 필요. 실패 원인을 추정하지 않음 |
| 보존 씬 Missing2 | Architecture/Prototype/1_Stage의 P_START/P_END 기존 Missing Script2는 이관 전후 동일 | 보존 자료 소유자의 후속 처리. 현재 실행 01_Stage_Prototype의 missing0과 구분 |
| 빌드·사람 평가 | Editor Play·서비스/API·합성 Keyboard 검사만 수행 | Windows 빌드·Player 실행·사람의 체감/팀 평가는 미실행 |
| 사용자 미저장 배치 | StationaryPlatforms root(8,0,0), dirty=true가 보완 Play에 포함됨 | native copy 보존, 원본 자동저장/원복 없음. 저장본 SHA와 실행 편집 상태 구분 |

기존 Actor 생성 실패, 체크포인트 시점 누락, 별도 Zone 부재를 보완해야 한다는 과거 설계, 버튼 몸통 동반 하강, Small 크기 역전은 이번 구조와 검증 결과로 대체됐다. 단일 StageTrigger+StageService 설계에는 별도 Zone을 추가하지 않는다. 실제55+5 통합 검사와 물리·동선 범위는 stage-flow-unification-2026-10-15의 검증 문서에 기록한다.

2026-10-16 최종 문서 대조 중 Stage 디스크의 추가 변경을 발견했다. 00:01 native 미저장 복사본과 비교하면 FloorPlatforms (11)의 루트 Y 회전0→180도만 다르며, 이전 미저장 StationaryPlatforms=(8,0,0)도 현재 디스크에 포함돼 있다. 이번 문서 작업은 씬을 저장하거나 회전시키지 않았고 변경 주체를 단정하지 않는다. 추가 배치를 보존하며 회전 이후의 충돌·화면 상태는 앞선 Play PASS로 확대하지 않는다. 근거: output/Tech_SYM/stage-flow-unification-2026-10-15/t7/stage-concurrent-diff.txt. 다른 동결 제품6개와 보호789개는 T6 이후 동일하다.

## 이전 발견과 당시 검증 제한 보존

아래는 과거 시점의 기록이며 Play 금지·Zone/SafeAnchor 필요성·당시 연결 미완성을 현행 지침으로 적용하지 않는다. 이번 범위 밖 기존 리소스·보호 씬 문제는 해당 근거를 보존한다.

2026-10-12. 최신 [상태](status.md)와 [검증 근거](ready-system-v16-2026-10-11/테스트%20및%20검증%20항목.md)를 따른다. 과거 발견·당시 실패·종결 기록 전체는 [보관본](archive/2026-10-12-before-v16-findings.md)에 보존했다.

| 항목 | 영향·현재 근거 | 처리/종료 조건 |
|---|---|---|
| V1.6 추격 문구 | 3.2의 전신 통과·경로 거리 포획과 직접 답변 불일치 | 기획 원본은 접촉·직선·실제 몸체 포획으로 수정 요청. 구현은 직접 답변 유지. 원본 DOCX는 편집하지 않음. |
| 카메라 최종 배치 | 초기 화면 비율·경계·대표 지상 안전 앵커를 연결했으나 전체 동선/공중/종횡비/화면 연출 미평가 | 사람이 각도·거리·감쇠·시간·경계·안전 앵커 확정. 화면 끝/낙하/복귀 차단 검증. |
| 자연 실행 | 비 Play 동기 검사에서는 다음 frame 입력 복귀와 DOTween Kill 비활성 전이 미평가. 자연 피해 결합·전체 한 판·실제 옵션 UI도 미실행 | 사람 Play에서 이동/카메라/문 중 옵션과 사망, 취소 후 물리 복원·피해, Retry·Title 복귀·최종 대화 경합 확인. 에이전트 Play 금지 유지. |
| 실제 리소스 | Player/붕괴 실제 클립·Sprite 미연결은 이번 코드 정리로 해결되지 않음 | 사람이 자원 연결·재생 확인. HudIconGraphic은 하트3/Q/E/눈물 총6소비 이관 후 제거. 임시 아트·Builder 생성하지 않음. |
| RealToon 가림 | Assets/RealToon 지정, 이번 감지·반투명 구현 명시 제외 | 후속 별도 명세/호환 검토·연결 작업. 현재 구현 완료로 표시하지 않음. |
| 보호 HMS 기준 씬 | 01_PlayerMovementTest의 PlatformManager Missing Script1 및 기존 Shattered 접촉 미연결 확인. 보호 씬 물리40개는 보존 | HMS 소유자가 GUID a039ffdaaa75e494d89aedbd9dee2e73 삭제 이력과 해당 물리 연결을 복구. SYM의 Missing0과 구별. |

외부 월드 앵커, 두 리그만 있는 통합 카메라, 중첩 Shattered 접촉 오연결은 이번 이관·재검사로 해결했다. 메모 실물 연결은 정상이며 EditorJsonUtility 표기만으로 누락 판정하지 않는다. 선택적 참조 null은 해당 모드/템플릿 계약에 따라 남긴다.

## 2026-10-15 실제 Play 후속 확인

Play 금지는 최신 직접 지시로 철회됐다. ActorBody 비활성·Quarter 접촉 미반영·선형 발판 상승 접지 이탈을 수정하고 실제 Play에서 확인했다. [결과](play-flow-verification-2026-10-15/테스트%20및%20검증%20항목.md). CameraZoneTrigger0개에 따른 통로 차단 연출과 Small/Normal 충돌체 크기 역전은 별도 미해결 범위다. 자연 키보드 전체 동선/빌드 QA는 미검증. 과거 금지·미실행 서술은 당시 범위다.

## 2026-10-15 현재 Stage 기존 연결 미완성

StageManager 추가 후에도 기존 StageTriggerPoint6개 소속/Trigger 미설정·시작 음악 누락 때문에 InitialPose 검증이 차단된다. 기존 Player/Camera의 통합 중복 위험 및 미작성 흐름을 포함한 대상·영향·미수정 이유는 [검수 기록](prototype-core-audit-2026-10-15/코드%20리뷰.md)을 따른다. 사용자 지시의 기존 객체 수정 금지로 보존했다.


## 2026-10-15 수동 변경 이후 재점검

앞선 Stage 참조/Trigger/시작 음악/Memo 및 중복 Actor/Camera 항목은 현재 저장본에서 해소 확인. 현재 미해결은 구간 CameraZoneTrigger0개와 Quarter 복귀 기본 시점에 의한 최초/재시도 모드 불일치, Small 충돌체가 Normal보다 큰 설정이다. [재점검 근거와 영향](flow-diagnosis-2026-10-15/코드%20리뷰.md). 읽기 전용 검수이므로 제품 수정은 하지 않았다.

