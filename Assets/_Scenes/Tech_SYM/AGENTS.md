# Tech_SYM 씬과 자산

## 현행 V1.6 작성 기준

현재 기준은 Assets/_Docs/planning-index.md의 V1.6과 최신 직접 지시다. 구현·검증 완료는 tracking/status.md와 실제 증거로 확인한다. 보호 원본·메타·아트의 쓰기 권한은 최신 파일별 허용 범위로 한정한다.

- 체크포인트는 현재 ActorBody 월드 위치·보유 조각·해금 마스크 세 항목만 원자 저장한다. 첫 유효 저장 전 상태를 구별하고 같은 물리 Marker 재접촉은 갱신하지 않는다. 시작 트리거 접촉 진입·퇴로 안전 확인 뒤 추격 시작 직전에 현재 상태를 저장한다. 사망은 같은 Stage 초기 재준비 후 마지막 세 값을 복원하며, 완료/Skipped 대화 장부는 능력 복원 때문에 지우지 않는다.
- 현재 목표는 응집형 Prototype Stage 한 곳에서 전체 게임 흐름 1회다. Stage 1→2→3 자동 연결을 추가하지 않으며 등록 씬과 향후 본편의 독립 진입을 보존한다.
- 위치·끝점·경로·대상은 실제 Transform/컴포넌트/자산을 드래그한다. 속도·시간·높이·방향은 명확한 수치/선택으로 남긴다. 한국어 이름과 Tooltip은 무엇을 연결/조절하는지 짧게 설명하고 필요한 단위·0·좌표계·기즈모만 덧붙인다.
- 씬 관리자·카메라·Canvas·월드 오브젝트는 독립 씬 루트다. 빈 Root/Render/Collider 규칙은 개별 기능 프리팹에 적용한다. HMS ActorBody 동거, Shell 실제 물리 Root, 움직이는 Rigidbody 아래 Render의 예외를 명시한다. 눈물은 수집 Root와 Collider가 함께 움직인다.
- 주기 발판 5/2/3초는 조절 가능한 초기값이다. 등장 및 이동 중 실제 세로 침투 비율이 1/4 미만이면 정상 착지 보정, 1/4 이상이면 실제 몸체를 위로 분리하며 사망한다. 껍질은 별도 분리하며 데드존에서 같은 소유 껍질을 현재 플레이어로 반환한다.
- 에이전트는 Editor Play 버튼·PlayMode API/CLI/pipeline/batch 테스트·직접 플레이를 실행하지 않는다. 비 Play 검증과 실제 사람 플레이를 구분한다. QA/FunQA 평가 문서를 요청 없이 작성하지 않는다.
- 기존 프리팹 삭제·재생성·Apply·리소스 제작은 사람이 한다. 소스 정리는 실제 호출·GUID·중첩 참조 이관 후 수행한다. HudIconGraphic은 하트뿐 아니라 Q/E와 눈물 조각을 포함한 6개 Graphic 소비 전체를 실제 리소스로 이관하고 참조0을 확인한 뒤 삭제한다. 자동 Builder로 자산을 다시 만들지 않는다.
- HMS 원본 소스의 쓰기 범위는 security.md와 최신 파일별 직접 지시를 확인한다. 보호 HMS 씬·프리팹·meta는 해당 소스 허가에 포함되지 않는다.

App 출력 Camera/Brain/Listener 각 하나와 Core Side/Corridor/Quarter 세 리그를 독립 루트로 연결한다. Player prefab은 비활성, 실제 물리 ActorBody와 VisualRoot/CameraAnchor 동거·자식 계약을 보존한다. 같은 씬 Component 참조를 사용하고 설치 자산은 SceneAsset로 작성한다.

씬 작성은 연결된 비 Play Editor로 하며 미저장 씬을 저장하거나 닫아 덮어쓰지 않는다. 기존 월드 pose·배율·Collider·GUID·중첩 override를 대조하고 자산 자체의 일괄 삭제/Unpack을 하지 않는다.
