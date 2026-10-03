# 붕괴·수집 기능

## 책임과 경계

ShatteredPlatform·PlatformPresentation, HeavyState와 기존 TearFragment를 소유한다. Architecture 전용 Level 수집 정책이나 플레이어 폼 원본을 소유하지 않는다.

## 유지할 계약

붕괴 표시 자식에는 Collider를 두지 않고 현재 지원 Collider 계약을 유지한다. 비활성화는 자기 Sequence를 취소·복원한다. 기존 TearFragment는 획득 시 해당 아이템 pickupRoot만 비활성화한다.

같은 오브젝트 Collider 요구는 기존 구현의 배치 예외다. 일반 루트 분리 원칙만으로 옮기지 않는다. HeavyState를 임의의 전역 플레이어 상태로 바꾸지 않는다.

현재 작업자는 Tech_SYM이며 변경은 승인된 Tech_SYM 범위에 한정한다. Tech_HMS 등 다른 작업자 폴더, 공용 Interfaces, _Art 원본과 그 .meta는 참조 전용이다. 같은 직군의 별도 명시적 파일 허용이 없는 한 수정하지 않는다.

## 검증

무게·접촉 방향, 중복 붕괴, 재생성·파괴, 중간 비활성화, 풀 재활성화, OnTriggerStay 수집, 인벤토리 상한에서도 기존 소비 동작을 확인한다.

실행하지 않은 검증을 통과로 기록하지 않는다. Editor의 자산 생성 메뉴와 읽기·검증 메뉴를 구분하며 다른 기능의 씬을 재생성하지 않는다.
