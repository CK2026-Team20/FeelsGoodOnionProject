# 인스펙터 UX·메모 E 전용 검증

## 현재 판정
Tech_SYM 구현과 영향 범위 검증을 마쳤다. 메인 전달에 따라 독립 리뷰어 Ampere가 코드·자산·실행 근거·문서를 읽기 전용으로 대조해 승인 완료했으며 추가 차단 사항은 0개다. 이는 사용자 수정 허용을 뜻하지 않으며 HMS 12개 파일의 누락 62개는 사용자 수정 허용 대기다. 전체 요청 완료로 표시하지 않는다. 기능·제약·완료 기준은 [후속 기능 명세](2026-10-05-inspector-ux-기능명세.md), 선언별 문구는 [감사 목록](2026-10-05-inspector-ux-감사.md), 타인 영역 초안은 [보호 영역](2026-10-05-inspector-ux-보호영역.md)을 따른다.

## 조사와 변경
Assets 전체 C# 328개를 분류했다. 자체 _Code는 SYM 196·HMS 41·공용 인터페이스 5개다. 나머지는 Nova 아트/vendor 55·DOTween 17·RealToon 12·Unity 템플릿 안내 2개로 수정 대상에서 제외했다. Unity 기본 컴포넌트 필드는 자체 설정과 구분한다.

실제 SerializedObject의 표시 가능한 propertyPath 397개를 수집했다. 선언 파일·선언 타입·필드 기준으로 고유 필드는 355개이며 SYM 254·HMS 101개다. 상속·중첩·배열 원소 노출 경로를 별도로 유지한다. 초기 반사 후보 422개에는 Inspector에 연결되지 않은 DTO와 enum 내부 값이 포함됐으므로 조절 필드 수로 사용하지 않는다. 실제 EnemyOilOnDeath.size를 포함하며 배열 길이는 이름이 아닌 ArraySize 형식으로만 제외한다.

SYM의 이전 누락 179개 중 Tooltip 178개 추가, 폐기한 closeButton 1개 삭제로 최종 누락 0개다. 그룹 선언 21개는 개별 문구를 위해 분리했으며 기존 필드명·형식·순서·초기값을 유지했다. 기존 bodyOffset·MemoUIController.player의 오안내 2개도 소비 코드와 일치하도록 고쳤다. 새로운 설정값·검증 규칙은 추가하지 않았다. `tooltip-changes.json`에 그룹 선언 21개를 포함한 추가 178개를 기록했다.

HMS 고유 누락은 62개(참조 31, 설정/컨테이너 31)이며 `protected-missing-drafts.json`과 보호 영역 문서에 정확한 선언 파일·필드·한국어 초안을 남겼다. 해당 12개 파일은 수정하지 않았다. 이전 HMS 입력/회수 규칙 허가는 이번 툴팁 변경에 자동 적용하지 않았다.

## 실행 근거
모든 경로는 프로젝트 루트 기준 `output/Tech_SYM/inspector-ux-2026-10-05/`다.

| 항목 | 결과와 범위 | 근거 |
|---|---|---|
| Unity 컴파일 | recompile completed, failed=false, errors=[], compilationFailed=false | 실제 Unity MCP 응답; 최종 문구 보강 뒤 실행 |
| 실제 인스펙터 | SYM 254개 누락 0; 노출 propertyPath·선언 경로·Tooltip 문구 기록 | serialized-property-audit.json, inventory-summary.json |
| 수집 제한 | 최초 Editor 전용 PrototypePlayerStunProbe 생성 실패 1건 보존. 직렬화 필드 0인 검증용 컴포넌트로 명시 제외 후 수집 오류 0 | serialized-property-audit-initial.json, 최종 audit의 exclusions |
| 통합 메모 | Succeeded, 12확인. 실제 프로토타입에서 주입 키보드 E 열기·닫기, 본문/옛 X 위치/화면 밖 클릭 종료 불가, 옵션 중 E 차단, 종료 프레임 소비, 한 번 닫기·정지 복원·재열림·Unbind | memo-integrated-play.json, memo-play-probe.cs |
| 독립 메모 | Succeeded, 7확인. 원본 03_InGame에서 API 열기, CheckInteract의 주입 E 닫기, 실제 서비스 조립·리더 비활성화/복원·비활성화 정리 | memo-standalone-play.json, memo-standalone-probe.cs |
| 모달 계약 | 22확인 통과. 실제 서비스/VM을 호출하는 순수 계약 검사 | modal-lifetime.json, T2ModalLifetimeVerification.Run |
| 입력 자원 복원 | 원 설정 객체·flags·백그라운드·장치 ID 목록·현재 Keyboard/Mouse 동일 | input-restoration.json. 성공한 메모 시나리오를 반복한 검사가 아닌 별도 입력 lease 생성/해제 검사 |
| 저장 프리팹 | Button 0, obsoleteProperty=false, obsoleteSerializedText=false, 이미지·트윈 참조 유효 | memo-prefab-final.json; 컴파일 후 해당 프리팹만 공식 API로 저장 |
| 씬·시작 설정 | 원래 Core+Prototype 편집 구성, active Core, Bootstrap 시작 설정을 공식 API로 명시 복원 | editor-before.json, editor-after.json, editor-start-restoration.json |

통합 검사는 사람이 직접 조작한 수동 검사와 다르다. InputSystem 장치를 통한 실제 입력 처리와 Bind/Unbind·검증 fixture API를 함께 사용했다. 독립 검사는 거리 탐색/E 열기 전체 동선 검증이 아니며 원본 PlayerFormController의 VisualRoot 예외를 별도로 관찰했다. 해당 원본 씬·PC 구조는 변경하지 않았고 전체 독립 게임 통과를 주장하지 않는다. 메모가 종료된 프레임의 입력은 기존 suppress 계약을 유지한다.

입력 자원 복원 probe의 ExitPlaymode 뒤에는 Play 시작 씬이 빈 값이었고, 이를 자동 복원 통과로 기록하지 않았다. 해당 중간 snapshot은 editor-after-input-restoration.json에 보존했다. 이후 시작 전 실제 값 `Assets/_Scenes/Tech_SYM/Architecture/01_Bootstrapper.unity`를 EditorSceneManager.playModeStartScene에 명시 복원하고 재조회한 최종 결과를 editor-after.json에 기록했다. 씬 저장은 수행하지 않았다.

## 보존과 검토 경계
`baseline-hashes.json`은 이번 후속 작업 시작 시점의 Assets/_Code·Assets/_Scenes/Tech_SYM·Assets/_Docs 파일 해시다. 이전 작업의 Git 변경과 이번 변경을 혼동하지 않는다. `baseline-delta.json`·`preservation-summary.json`으로 해당 시작점 대비 차이를 기록한다. 씬/기능 설정 자산은 메모 프리팹을 제외하고 동일하고, 기존 .meta/GUID 및 보호 코드도 동일해야 한다. ProjectSettings·Assets/Settings는 이 시작 해시 목록에 없으므로 이 근거로 새 해시 불변 PASS를 주장하지 않는다. 이번에는 빌드를 실행하거나 그 설정을 수정하지 않았다.

시작 전 코드 텍스트 사본을 확보하지 않았으므로 실행하지 않은 필드 초기값 비교를 만들어 PASS로 기록하지 않는다. 툴팁 속성 추가와 그룹 선언 분리의 필드·형식·초기값 보존은 실제 소스 패치와 독립 정적 리뷰로 확인한다. 메모 View와 두 기존 검증 도구만 입력 기능 제거에 맞춰 변경했다. ViewModel/Controller의 E 닫기 명령과 수명 정리는 유지했다. 메모 제작 코드에 닫기 버튼 생성 경로가 추가로 존재하지 않음을 검색했고 소유 씬의 메모는 런타임 프리팹 인스턴스로 생성한다.

최종 보존 파일을 생성했다. 시작 파일 1,061개 중 변경 62개이며 보호 코드 0개·기존 .meta 0개·기존 .unity 0개 변경이다. 자산 변경은 MemoView.prefab 한 개다. 최종 before/after 편집 구성·active 씬·Play 시작 씬은 동일하다. 문서 추가·갱신을 끝내기 전의 59개 중간 집계와 구분한다.

기존 ready/spec과 과거 X 검증·실행 로그는 보존했다. 현재 business-rules·contracts·지역 Interaction 지침·검증표는 E 전용으로 갱신했다. 과거 동작 이력을 현재 명세로 읽지 않도록 후속 명세에서 대체를 명시한다. Git stage·commit·push·브랜치·worktree 생성은 수행하지 않았다.
