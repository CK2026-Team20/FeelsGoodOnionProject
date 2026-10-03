# 작업 시 주의할 동작

## 빌드 씬과 실행 씬의 차이

일반 Build Settings에는 01_Samples만 등록되어 있지만 Architecture 설치 자산에는 여섯 실행 씬이 있다. 이 상태에서 일반 빌드 설정만 보면 통합 게임이 빠진 것처럼 보인다. 통합 Windows 빌드 메뉴는 여섯 씬을 명시적으로 전달하고 ProjectSettings 해시를 전후 비교한다. 일반 빌드 목록을 자동 수정하는 것으로 해결하지 말고 빌드 결과의 scenes와 changedProjectSettings를 확인한다.

## Editor 진입과 실제 장면

기존 03_InGame 또는 발판 테스트 씬에서 Play를 시작해도 설치 자산과 Editor hook이 Architecture 첫 스테이지로 경로를 바꾼다. 열었던 씬 이름만으로 검증 대상을 판단하지 않는다. 실제 로드된 Bootstrap·Core·Stage와 입력 경로를 확인한다.

## 중복 입력과 물리 참조

Architecture 플레이어에 기존 PlayerController나 CheckInteract를 추가하면 입력이 두 경로로 실행될 수 있다. SessionActorHost는 입력 리더와 라우터가 하나씩인지 검사하고 기존 제어·디버그 입력 컴포넌트를 거절한다. 기존 메모의 E와 새 상호작용의 F가 다른 것은 키 이름 오타가 아니다.

플레이어 루트 기준으로 Collider를 옮기면 attachedRigidbody.gameObject에서 PlayerFacade를 찾는 센서가 깨질 수 있다. 현재 빈 PlayerRoot와 자식 ActorBody 구성을 기준으로 충돌체와 Facade의 위치를 함께 확인한다. 저장된 PlayerRoot는 비활성 상태이며 첫 활성화 전에 의존성을 준비한다.

## 수집 경로의 구분

Features/TearFragment와 Architecture/Level/LevelTearFragment는 가득 찬 인벤토리에 대한 소비 규칙이 다르다. 클래스 이름이 비슷하다는 이유로 교체하지 않는다. 대상 씬의 m_Script GUID와 연결된 컴포넌트를 확인하고 상한 상태에서 실제 아이템 활성 여부를 검사한다.

## 종료와 트윈 검증

Coroutine의 Destroy 요청 직후에는 객체가 같은 프레임에 남아 있을 수 있다. Actor despawn은 한 프레임을 기다리고 종료 오류도 전달한다. 재시도 직후 객체 개수를 검증할 때 정리 완료 시점을 기다린다.

압력판의 트윈은 ManualUpdate로 물리 프레임에서 전진한다. 단순히 끝 위치로 이동시킨 검사는 도중 정지·취소·복귀를 검증하지 못한다. 해당 트윈의 실제 갱신 경로를 사용해 중간 상태와 비활성화 후 정리를 확인한다.

## 기존 메모의 입력 차단 범위

MemoUIController의 bool 차단은 단일 흐름용이다. 여러 요청자의 동시 차단을 지원하는 토큰 방식과 같지 않다. Architecture의 중첩 옵션·대화 흐름에 그대로 끼워 넣지 말고 실제 사용 씬과 차단 소유자를 먼저 확인한다.
