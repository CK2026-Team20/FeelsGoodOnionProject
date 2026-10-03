# Tech_SYM 구현 영역

기존 FeelsGoodOnion.TechSYM 기능과 Cooked 통합 구조가 공존한다. 클래스가 비슷하다는 이유로 통일하지 않는다. 사용자는 현재 구현을 의도된 동작으로 확인했으며 변경할 부분은 따로 지정한다.

수정은 승인된 Tech_SYM 경로로 제한한다. Tech_HMS·art_*·QA_*와 _Art는 참조 전용이다. 공용 Interfaces나 ProjectSettings를 함께 고치는 권한은 없다.

Architecture는 앱·세션·스테이지 수명을 분리한다. 구성 지점이 구체 의존성을 조립하고 View는 주입된 ViewModel에 바인딩한다. 원본 플레이어 상태는 HMS PlayerModel이 소유하고 SYM은 공개 API와 스냅샷을 소비한다.

Background/Editor는 주방 배경 자산 작성 도구다. 독립 런타임 모듈로 확장하지 않으며 실행하면 Tech_SYM의 배경 자산을 다시 만들 수 있다. 문서 확인을 위해 Builder를 실행하지 않는다.

코드 변경은 입력 단일 처리, 상태·수명 소유권, 예외·취소, 재바인딩·폐기 후 콜백, 기존 자산 연결을 확인한다. 단순 문서 수정으로 코드·씬·패키지를 정리하지 않는다.
