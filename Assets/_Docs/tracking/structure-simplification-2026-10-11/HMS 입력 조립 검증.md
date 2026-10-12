# HMS 입력 조립 코드 리뷰 및 검증

2026-10-11. 사용자 승인: PlayerSkillController.cs 한 파일 수정, HMS PlayerController 기본 조립, Host/Bridge 연결, 씬/프리팹 이관, Router와 불필요한 전달 제거. 최종 반영은 feature/core/systems.

## 구현

- PlayerSkillController에 슬롯 추가 조건 Bind/Unbind 및 TryUse 거절 이벤트를 연결했다. Inspector 필드/세션 참조를 추가하지 않았다.
- Bridge는 해금 원본 조회, 성공한 폼 전환 간 공통 1초, 모달/세션/Host 제한, Actor 세대별 이벤트 연결과 해제를 담당한다. 성공음은 기존 HMS SkillActivated, 공간 부족 실패음은 UseRejected로 연결한다.
- Host 실행 순서 -100을 코드와 Unity MonoImporter로 명시했다. F 닫기/상호작용/대상 갱신은 Host에서 처리하고, 모달 닫힘 및 제어 상태 변경 프레임은 Facade 입력을 억제한다. 죽음 완료는 기존 Host 한 곳에서 진행한다.
- PlayerRoot.prefab, MultiFuncPlatforms.prefab, 01_BrokenablePlatformTest.unity를 Unity API로 수정했다. Reader/Facade와 같은 실제 ActorBody에 PlayerController를 붙였다. 프리팹 재생성/Apply는 하지 않았다.
- GameplayInputRouter.cs와 meta, Bridge.Move/Jump/Tear/ChangeForm을 제거했다. 별도 대체 클래스는 없다.

## 완료된 검증

1. Unity 6000.6.0f1 비 Play 재컴파일: completed, failed=false, errors=[], compilationFailed=false.
2. Unity 네이티브 참조 검증: PlayerRoot/MultiFuncPlatforms 프리팹 2개, 01_BrokenablePlatformTest/02_MultiFuncPlatformTest/03_InGame/Architecture/03_InGameCore 씬 4개. Reader/Controller/Facade 동일 객체·각 1개, Host 입력 순서, 깨진 객체 참조/누락 스크립트 없음.
3. 실제 로드된 Assembly-CSharp에서 Router 타입 및 4개 전달 메서드 없음.
4. 새 HMS API의 비 Play 검사 9개: 미연결 기존 동작, 조회 무피드백, 잠금 실행 거절 1회, 타 슬롯 보존, 중복 조건 소유 거절, 낡은 해제가 새 조건을 지우지 않음, 쿨다운 거절, 슬롯 실패 전달, 구독 해제 후 호출 없음. 테스트 스킬을 이용한 직접 API 호출이며 입력/물리 시뮬레이션이 아니다.
5. 독립 정적 검토: 실제 폼 변경 후 SkillActivated 순서, 자동 껍질 반환과 성공음 분리, F 억제/사망 수명, Actor 세대별 조건·이벤트 해제에서 확정 버그 없음.
6. 전체 Assets의 Router GUID 직렬화 참조 0, 제품 C# 타입/호출 참조 0. 과거 문서에는 당시 기록으로 이름을 보존한다.

세부 기계 증거는 output/Tech_SYM/hms-input-assembly/verification.json, skill-hooks.txt, migration.json, compile-status.json이다. 워크트리 정리 전에 주 작업 폴더 output/Tech_SYM 아래로 보존한다.

## 관찰과 한계

- 첫 Editor 시작은 소스 이관 중간과 겹쳐 이전 Router의 제거된 메서드 호출로 실패했다. 자산 이관 후 Router를 완전히 삭제한 최종 재컴파일은 통과했다.
- MonoImporter 순서 확인에서 기본 0을 관찰하여 SYM Host의 실행 순서를 -100으로 직렬화했고 이후 검증을 통과했다. HMS meta는 변경하지 않았다.
- Unity 저장이 빈 YAML 필드에 붙이는 후행 공백은 기능 변경이 아니다. 코드 및 비공백 오류 검사는 별도로 확인한다.
- Play/PlayMode/Player/실제 게임 입력/물리/클립/음향 검증은 실행하지 않았다. HMS 점프의 물리 접지 타이밍과 모달 동시 키 동작은 사람 플레이 확인 대상이다.
- 기존 미커밋 구조 정리와 Editor 자동 저장 변경을 함께 보존한다. URP RuntimeSettings 목록 초기화 및 TMP 동적 글리프 캐시 변경은 기존 자동 변경으로 분류하며 보호 원본 수동 수정으로 확대하지 않는다. ProjectSettings 상태 표시는 내용 diff 없는 EOL/stat 변화다.
