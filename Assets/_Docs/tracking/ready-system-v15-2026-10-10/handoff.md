# V1.5 구현 인계

사용자가 승인한 V1.5와 후속 직접 선택을 구현했다. 현재 결과와 실제 실행 증거는 [현재 상태](../status.md), [검증 기록](테스트%20및%20검증%20항목.md), [코드 리뷰](코드%20리뷰.md)에 있다. 준비 단계의 문서를 제품 구현 완료 증거로 사용하지 않는다.

## 먼저 볼 문서

1. [씬 흐름과 수동 프리팹 제작 가이드](씬%20흐름과%20수동%20프리팹%20제작%20가이드.md): 전체 씬 흐름, 제작 순서, 기능별 Hierarchy·컴포넌트·필수 설정·드래그할 참조와 실제 물리 예외.
2. [Inspector 연결 전환 명세](Inspector%20연결%20전환%20명세.md): 최종199개 작성 필드의 실제 타입·한국어 이름·Tooltip과 기존 prefab 소비/새 연결 위치.
3. [스크립트 필요성 및 삭제 계획](스크립트%20필요성%20및%20삭제%20계획.md): 유지109개, 제거22개, 리소스 대기1개의 필요성·책임·삭제 근거. Locator와 Composition을 유지하는 이유.
4. [이전 Tech_HMS 변경 기록](이전%20Tech_HMS%20변경%20기록.md): 이전2commit10고유소스와 이번3파일 최소 변경의 추가·삭제·변경 이유 및 추후 PR에 포함할 의도.
5. [현재 구조와 V1.5 차이](현재%20구조와%20V1.5%20차이.md): 이전 문제, 실제 변경, 현재 소유자와 남은 확인 범위.

정본은 `Assets/_Docs/tracking/ready-system-v15-2026-10-10/`의 handoff/spec/plan 및 상세 문서다. `.dryforge/`는 실행 도구용 사본, `output/Tech_SYM/Planning/`은 같은 준비/인계 사본이고 `output/Tech_SYM/Execution/system-v15-2026-10-10/`은 실제 명령·Source hash·엔진 관찰·BuildReport 증거다. 기획 원본은 `Assets/_Docs/Cooked_시스템_기획서_V.1.5.docx`, SHA256은 `d5698cbc3e50bcb1a4ea8c4f31eae325b43af7221bbcc97a150fdb8b28aa8ebe`다.

## 결과의 범위

현재 흐름은 Bootstrap→Title→Opening→응집형 Prototype Stage→포포 Escape(7) 대화 완료/전체 Skip→Ending→Title이다. Stage1→2→3 자동 연결은 추가하지 않았고 등록된 다른 Stage와 기존 독립 기능 씬의 진입을 보존했다. 일반 대화 Completed/Skipped 장부는 유지한다. 포포 최종 대화만 재접촉·재시도 후 새 회차를 허용하며 현재 Stage/Trigger/선택 객체/Table/Code/Actor 세대/Round/Session을 검증한다. 취소·낡은 callback·같은 프레임 사망/포획은 Ending을 열지 않는다.

위치·경로·끝점·CP·카메라 Zone·장치·리소스는 실물 참조다. SceneAsset/대화 Code 선택의 내부 경로와 실행 중 snapshot Vector3는 시스템 경계가 생성·검증한다. 속도·시간·높이·방향은 목적이 분명한 조절 값으로 유지한다. 씬 관리자·카메라·Canvas·월드 오브젝트는 독립 루트다. 기능 prefab의 부모/자식과 ActorBody/Shell 물리 계약은 제작 가이드의 예외를 따른다.

CP는 현재 ActorBody 월드 위치·조각·해금 마스크 정확히3항목을 원자 저장한다. 첫 저장 전/같은 물리 Marker/추격 직전 저장을 구분하며 사망은 같은 Stage 초기 재준비 후 복원한다. 주기 발판은 초기5/2/3초를 조절할 수 있고 실제 몸체 세로 침투<1/4는 착지 보정, ≥1/4는 몸체를 위로 분리하며 사망한다. 껍질은 따로 분리하고 데드존에서 같은 소유 껍질을 현재 Actor로 반환한다. Tear는 수집 Root·Collider가 함께 이동한다.

자동 제작기12개와 중복 런타임10개, 자기 meta를 실제 소비 이관 후 제거했다. HudIconGraphic은 실제 리소스가 연결될 때까지 기존 prototype의 모든 하트·Q/E·조각 소비와 함께 유지한다. 전체23개 삭제 완료가 아니다. 새 prefab에는 Image/Sprite를 연결하며 모든 기존 Graphic 소비를 이관한 뒤 구형 분기와 Source를 제거한다.

## 현재 최종 센서 수정과 검증

체크포인트와 포포 도착은 '한 시도에 한 번' 값과 관계없이 재접촉 규칙을 따른다. 같은 체류에서 수락은 한 번이며 완전히 나간 뒤 다시 요청한다. CP 저장 서비스는 마지막 Marker 재접촉을 거절하고 A→B→A에서는 새 현재 위치·조각·해금을 저장한다. 포포 최종 대화만 일반 Completed/Skipped 장부를 보존한 새 회차를 허용한다. 데드존은 실제 몸체별 중복을 막고 다른 종류는 기존 once 설정을 따른다.

Source a192d2a7/로컬 반영8659744b의 전체 SDK158/158/11·현재 Player DLL204·현재 Native201 평가 실패0, 실제6씬 Windows 빌드 오류0/경고48을 확인했다. 이전 T13의51경고 빌드와 DLL은 당시 증거로 보존한다. Native Completed 정상 시계·자연 접지·실물 표시·사람 Play 대기를 포함한 정확한 범위는 검증 기록에 있다. 최종 수정 diff와 문서의 독립 재검토는1b7069f1까지 approved/남은 차단0이며 코드 리뷰에 원본·검토 SHA를 기록했다. T14 기술 인계와 사용자 최종 수락은 완료했다. T15·사람 리소스/Play는 대기다.

## 권한과 검증 경계

Tech_SYM 소유 소스·최소 자산 참조 이관과 프로젝트 Markdown만 기본 수정 범위다. 이번 HMS 예외는 Playback Interact2→4/설명, Controller 관련 설명, CharacterMovement의 실제 접지각 readonly getter/설명이다. 기존 필드·기본값·Range·접지 동작은 그대로다. 이전9파일 허가는 종료됐다. 보호 원본/meta/import, _Art, Packages/ProjectSettings의 수동 수정 권한은 없다.

에이전트는 Editor Play 버튼·API/CLI/pipeline/batch PlayMode·직접 플레이·Player 실행을 하지 않았다. 실제 비 Play Source 컴파일·직접 DLL 공개 결과·격리 엔진/직렬화·빌드는 평가한 assertion과 명령/exit/Source식별을 기록한다. helper compile-only, 포트, 예약 수락, 파일 존재, NOT_EVALUATED는 통과가 아니다. 실제 자연 접촉·clip 재생·전체 동선·화면 품질·빌드 UI/UX는 사람이 확인한다. QA/FunQA 평가 문서는 이번에 만들지 않는다.

기존 prefab 삭제·재생성·Apply·리소스 연동은 사람이 한다. 새 prefab을 먼저 제작하고 소비표의 설치/설정/중첩 참조를 이관한 뒤 기존 것을 제거한다. 자동 Builder·placeholder로 대체하지 않는다. 미저장 Scene/Prefab을 덮거나 Editor를 강제 종료하지 않는다.

현재 로컬 feature 결과는 승인된 범위의 commit과 자동 저장 감사로 보존한다. 원격 push·PR·main 병합은 이번에 수행하지 않았다. 이후 PR에는 위 변경 의도, 제거된 책임의 실제 소유자, HMS 세 파일의 정확한 최소 범위와 미실행 사람 확인을 적는다. 2026-10-11 사용자가 최종 결과와 인계를 승인하고 로컬 `feature/core/systems` 병합을 지시했다. 활성 실행 사본3개는 `.dryforge/002/`로 보관하며 이 폴더의 정본3개는 계속 유지한다. 실제 병합 식별은 Git 이력과 아래 실행 증거에 기록한다.
