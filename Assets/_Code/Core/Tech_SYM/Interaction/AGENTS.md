# 기존 메모·프롬프트

## 책임과 경계

CheckInteract 대상 선택, 월드 프롬프트, 메모 Model·ViewModel·View와 생성·해제를 소유한다. Architecture 입력 라우터나 스테이지 안전 대화를 소유하지 않는다.

## 유지할 계약

거리·수평각·차폐를 거쳐 대상을 선택하고 E를 한 번 전달한다. 메모는 E로만 열고 닫으며 마우스 종료 버튼은 없다. 열린 메모의 닫기를 먼저 처리한다. MemoID는 양수이며 다른 MemoObject와 중복하지 않는다.

MemoUIController는 주입된 제어 서비스에서 자기 입력·월드 토큰을 소유한다. Architecture는 CoreWorldBinding이 공유 서비스를 주입하며 독립 씬은 CheckInteract가 별도 서비스를 조립·해제한다. 독립 조립은 PlayerInputReader의 소유 비활성화를 사용하고 Facade bool 차단으로 되돌아가지 않는다. View는 상태 표시와 입력 전달만 맡고 자기 구독만 해제한다.

## 검증

가려진 대상·동거리 대상, 중복 ID·누락 Sprite, 열린 상태 E 닫기, 외부 비활성화·파괴, 재바인딩 중복 및 입력 복원을 확인한다.
