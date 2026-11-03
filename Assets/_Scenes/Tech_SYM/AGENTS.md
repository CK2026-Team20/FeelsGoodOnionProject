## 2026-11-02 튜토리얼 UI·중앙 BGM·첫 체크포인트 무음 등록

최신 사용자 승인(22~24): 튜토리얼 UI는 World Space 프리팹으로 제공하고 사용자가 배치한다. 스프라이트 교체 키는 AnimationClip의 0·40·80…프레임(Samples 60), 최초 접촉만 확대 등장하며 재시도에도 표시/기록을 유지하고 새 게임에서 초기화한다. _Art 원본은 보존한다.

BGM 설정은 Bootstrapper의 AudioCueCatalog에서 SceneAsset별로 연결한다. StageService/StageTrigger는 음악 설정·선택·재생을 소유하지 않는다. 일반/추격/타이틀 Loop, 오프닝/엔딩 One Shot, 이전 곡 페이드아웃 후 다음 곡 시작, 옵션 Pause/Resume. 첫 체크포인트는 초기 위치로 유지하며 시작 시 무음 등록한다. 최초 스폰 리스폰음 없음, 사망 후 Playing 복귀 시 리스폰음 1회. 새 체크포인트만 저장음, 구간 기준 정책은 시작 재생·종료 무음이다.

사운드 원본 30행과 적용 범위는 Assets/_Docs/Audio/사운드 리소스 상세 명세.md, 구현·검증·배치 안내는 Assets/_Docs/tracking/tutorial-audio-2026-11-02/를 따른다. 아래 과거 구간별 음악/옵션 중 음악 계속 재생/최초 장부 미등록 설명은 이번 범위에서 대체한다. 전체 SFX 정합화와 사용자 씬 배치·전체 플레이·빌드 검수는 완료로 승계하지 않는다.

## 2026-11-02 SYM 자산 분리·프로토타입 정리

사용자 직접 지시 및 후속 정정으로 SYM 전용 미사용 프로토타입/테스트 자산을 제거했다. 본편 데이터는 Assets/_Data/Tech_SYM, 관리 씬3개는 Assets/_Scenes/Tech_SYM/Runtime으로 이관했다. 타 작업자 씬과 의존 자산은 원위치/원내용으로 보존하므로 공유 스킬·적·메모 모델7개와 일부 테스트 프리팹은 예외로 남는다. 루트 Tutorial/Stage 및 공용 _Prefabs는 변경하지 않는다. 아래의 이전 Prototype 실행 경로·보존 문구보다 이번 자산 분류가 우선한다. 현재 실행 등록은 ArchitectureInstallation과 IntegrationEntrySettings의 SceneAsset 참조를 따른다. 상세: Assets/_Docs/tracking/asset-organization-2026-11-02/자산 분류 목록.md.

# Tech_SYM 씬과 자산

## 현행 V1.6 작성 기준

현재 시스템 기준은 Assets/_Docs/planning-index.md의 V1.8이며 이전 사용자 기능 명세와 기존 구현보다 우선한다. 구현·검증 완료는 tracking/status.md와 실제 증거로 확인한다. 보호 원본·메타·아트의 쓰기 권한은 최신 파일별 허용 범위로 한정한다.

- StageService가 같은 씬의 논리 구간, 첫 구간, 현재 구간, 활성화한 체크포인트 집합, 마지막 스냅샷과 그 복귀 구간을 소유한다. 구간에는 서로 다른 시작·끝 StageTrigger, 시점 종류, 실제 음악 Clip·음량·전환 시간, 진입 보정·카메라 전환 시간을 연결한다. 이름·배열 순번은 저장 키가 아니다. Trigger는 접촉을 전달하며 시점·음악·저장 장부를 별도로 편집하지 않는다. CheckpointSnapshot은 접촉한 체크포인트 Transform의 유한 월드 Position, 접촉 순간 Fragments 정수0..5, UnlockedAbilities 마스크0/1/2/3의 정확히 세 값이다. StageService가 유효한 최초 접촉에서 스냅샷과 활성화 이력을 콜백 전에 원자 확정한다. 수락 전 실패는 이전 저장을 유지하고, 수락 후 표현 취소·사망은 이미 수락한 저장과 소비 이력을 유지한다. CapsuleCollider 월드 중심을 Position에 맞추며 HP·폼·껍질·음악·시점·세계 상태는 payload에 추가하지 않는다. 첫 저장 전에는 첫 구간 시작과 Entry 초기 값을 사용하고, 추격 직전 예외만 당시 플레이어 Capsule 중심을 저장한다.
- 현재 목표는 응집형 Prototype Stage 한 곳에서 전체 게임 흐름 1회다. Stage 1→2→3 자동 연결을 추가하지 않으며 등록 씬과 향후 본편의 독립 진입을 보존한다.
- 위치·끝점·경로·대상은 실제 Transform/컴포넌트/자산을 드래그한다. 속도·시간·높이·방향은 명확한 수치/선택으로 남긴다. 한국어 이름과 Tooltip은 무엇을 연결/조절하는지 짧게 설명하고 필요한 단위·0·좌표계·기즈모만 덧붙인다.
- 씬 관리자·카메라·Canvas·월드 오브젝트는 독립 씬 루트다. 빈 Root/Render/Collider 규칙은 개별 기능 프리팹에 적용한다. HMS ActorBody 동거, Shell 실제 물리 Root, 움직이는 Rigidbody 아래 Render의 예외를 명시한다. 눈물은 수집 Root와 Collider가 함께 움직인다.
- 주기 발판 5/2/3초는 조절 가능한 초기값이다. 등장 및 이동 중 실제 세로 침투 비율이 1/4 미만이면 정상 착지 보정, 1/4 이상이면 실제 몸체를 위로 분리하며 사망한다. 껍질은 별도 분리하며 데드존에서 같은 소유 껍질을 현재 플레이어로 반환한다.
- 2026-10-15 직접 지시로 에이전트 Play/Player 금지와 과거 검증·QA 역할 고정을 철회했다. 승인된 기능의 실제 Editor Play를 공식 Unity 플러그인으로 수행하고 실행 근거대로 판정한다. API 위치 준비, 실제 InputSystem 합성 입력, 사람 직접 조작, 빌드·Player 실행, 팀 평가를 구분한다. 미저장 작업·파일 소유권·Git 승인 경계는 유지하며 실행하지 않은 항목을 통과로 기록하지 않는다.
- 기존 프리팹 삭제·재생성·Apply·리소스 제작은 사람이 한다. 소스 정리는 실제 호출·GUID·중첩 참조 이관 후 수행한다. HudIconGraphic은 하트뿐 아니라 Q/E와 눈물 조각을 포함한 6개 Graphic 소비 전체를 실제 리소스로 이관하고 참조0을 확인한 뒤 삭제한다. 자동 Builder로 자산을 다시 만들지 않는다.
- HMS 원본 소스의 쓰기 범위는 security.md와 최신 파일별 직접 지시를 확인한다. 보호 HMS 씬·프리팹·meta는 해당 소스 허가에 포함되지 않는다.

App 게임 출력 Camera/Brain/Listener 각 하나와 Core Side/Corridor/Quarter 세 리그를 연결한다. Title은 별도 Camera/Listener를 갖고 TitleScenePresentation이 게임 출력과 번갈아 활성화한다. Player prefab은 비활성, 실제 물리 ActorBody와 VisualRoot/CameraAnchor 동거·자식 계약을 보존한다. 같은 씬 Component 참조를 사용하고 설치 자산은 SceneAsset로 작성한다.

씬 작성은 연결된 비 Play Editor로 하며 미저장 씬을 저장하거나 닫아 덮어쓰지 않는다. 기존 월드 pose·배율·Collider·GUID·중첩 override를 대조하고 자산 자체의 일괄 삭제/Unpack을 하지 않는다.

구간 시작·끝은 고정 참조로 연결하며 센서 중심/크기와 복귀 Transform을 구별한다. 현재 제작 씬은 01_Stage_Prototype이다. 복구용 Assets/_Recovery 씬들은 사용자 지시로 제거했으며 보존용 Architecture/Prototype/1_Stage를 같은 삭제 대상으로 취급하지 않는다.

뒷면 메시를 생략한 아트는 구도·추적·전환 중간에서도 뒷면을 노출하지 않도록 배치한다. 최신 사용자 정정으로 반대편 카메라 이동은 철회했고 기존 방향을 유지한다. 사용자가 오브젝트 배치를 회전하는 작업은 에이전트가 완료한 것으로 기록하지 않는다. 보호 아트의 임의 수정·양면화나 HMS 입력 변환으로 우회하지 않으며 현재 백페이스 노출은 미해결 시각 항목이다.
## 2026-11-02 후속 자산 위치 정정

프리팹은 공용 Assets/_Prefabs 아래 기능별 폴더에 두며 작업자 이름을 추가하지 않는다. _Data에는 순수 모델/설정 데이터만 둔다. 튜토리얼 프리팹은 Assets/_Prefabs/UI/Tutorial, 상호작용 안내는 Assets/_Prefabs/UI/Interaction에 있다. 튜토리얼 애니메이션은 Assets/_Animations/Tech_SYM/Tutorial에서 _Art/UI/Tutorials의 원본 Sprite를 직접 참조한다. PNG 사본을 만들지 않는다. 이 정정은 아래 과거 공용 _Prefabs 이동 제외 기록보다 우선한다.

