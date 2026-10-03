# Tech_SYM 씬과 자산

Tech_SYM 소유 씬·프리팹·데이터·파생 시각 자산을 관리한다. 다른 작업자 씬, _Art 원본, 공용 _Prefabs 원본은 수정하지 않는다.

Architecture의 설치 자산이 Bootstrap·Title·Core·Stage 1~3 경로를 소유한다. 일반 Build Settings와 동일하지 않다. 기존 씬은 Editor 진입 변환에 포함되므로 임의로 삭제하거나 경로를 변경하지 않는다.

새 완성 오브젝트는 빈 루트 아래 Visual·Collider·VFX·SFX·CameraAnchor를 역할별로 구성한다. 현재 PlayerRoot의 ActorBody에는 Facade·물리 컴포넌트가 함께 있어 센서의 attachedRigidbody 계약을 만족한다. 이 기존 배치를 일반 예시만으로 변경하지 않는다. Visual 원본을 수정하기보다 Tech_SYM 소유 배치에서 참조한다.

PlayerRoot의 저장 상태는 비활성이다. Scene의 체크포인트 Pose는 ActorBody 중심이다. 복원 높이를 지면 자체로 바꾸면 충돌이 겹칠 수 있으므로 캡슐 크기·바닥 높이를 함께 확인한다.

자산 생성 도구는 이미 저장된 프리팹·씬을 다시 작성할 수 있다. 읽기 검증과 생성·수정 메뉴를 구분한다. 변경 후 Missing Script·GUID 참조·필수 직렬화 필드·실제 기존 씬 동작·저장 상태를 확인한다. .meta를 다른 사람 파일에서 복사하거나 새 GUID로 원본 참조를 끊지 않는다.
