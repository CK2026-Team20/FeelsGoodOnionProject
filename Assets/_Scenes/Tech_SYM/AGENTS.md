# Tech_SYM 씬과 자산

Tech_SYM 소유 씬·프리팹·데이터·파생 시각 자산을 관리한다. 다른 작업자 씬, _Art 원본, 공용 _Prefabs 원본은 수정하지 않는다.

참조는 공개 기능을 자기 소유 배치에 컴포넌트로 추가·설정·연결하고 해당 배치 override를 저장하는 조립을 포함한다. 보호 원본에 Apply하거나 코드·자산·.meta·import 설정을 직접 수정·이동·삭제·GUID 변경하는 것은 별도 명시적 파일 허가 없이는 금지한다.

Architecture의 설치 자산이 Bootstrap·Title·Core·Stage 1~3 경로를 소유한다. 일반 Build Settings와 동일하지 않다. BootstrapEntryRoute의 고정 여섯 경로가 Editor 부트 진입 대상을 정하며 설치 자산도 이 경로와 일치해야 한다. 기존 03_1_Stage·03_InGame·개별 테스트 씬은 독립 Play하며 이름 일부가 같다는 이유로 대상에 넣지 않는다. 등록 경로 변경은 설치 자산·BootstrapEntryRoute 고정 목록·실제 진입 검증을 함께 확인한다.

새 완성 오브젝트는 빈 루트 아래 Visual·Collider·VFX·SFX·CameraAnchor를 역할별로 구성한다. 현재 PlayerRoot의 ActorBody에는 Facade·물리 컴포넌트가 함께 있어 센서의 attachedRigidbody 계약을 만족한다. 이 기존 배치를 일반 예시만으로 변경하지 않는다. Visual 원본을 수정하기보다 Tech_SYM 소유 배치에서 참조한다.

PlayerRoot의 저장 상태는 비활성이다. Scene의 체크포인트 Pose는 ActorBody 중심이다. 복원 높이를 지면 자체로 바꾸면 충돌이 겹칠 수 있으므로 캡슐 크기·바닥 높이를 함께 확인한다.

게임플레이 카메라는 PlayerRoot의 CameraRig에 HMS Mode/Visual, 출력 Camera·Brain·PlayerCameraController와 네 CinemachineCamera를 첫 Awake 전에 연결한다. Core에는 LevelCameraService만 남긴다. 독립 발판/InGame 배치는 HMS 입력 리더·PlayerController 한 경로를 사용하며 LayoutOverview는 비활성 배치 참고 카메라다. 기존 primitive Player의 Facade·물리 계약은 빈 wrapper의 자식 몸체에 유지하고 Renderer만 직계 VisualRoot로 옮긴다. 플레이어가 없는 역사적 03_1_Stage는 level-only 씬이며 독립 Play가 완성 게임 흐름을 자동 생성하지 않는다.

자산 생성 도구는 이미 저장된 프리팹·씬을 다시 작성할 수 있다. 읽기 검증과 생성·수정 메뉴를 구분한다. 변경 후 Missing Script·GUID 참조·필수 직렬화 필드·실제 기존 씬 동작·저장 상태를 확인한다. .meta를 다른 사람 파일에서 복사하거나 새 GUID로 원본 참조를 끊지 않는다.

기존 씬 동작의 자동 기능 검증과 실제 빌드의 팀 검증 인계는 Assets/_Docs/standards.md의 기능 테스트와 QA 인계를 따른다. 이 확인 규칙은 에이전트의 Computer Use 직접 플레이를 상시 의무로 두지 않는다. 요청된 실제 씬·빌드와 검증 범위를 명시하고 팀 확인 대기인 항목을 통과로 기록하지 않는다.
