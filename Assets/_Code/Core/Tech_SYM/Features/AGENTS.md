# 붕괴·수집 기능

## 책임과 경계

ShatteredPlatform·PlatformPresentation, HeavyState와 기존 TearFragment를 소유한다. Architecture 전용 Level 수집 정책이나 플레이어 폼 원본을 소유하지 않는다.

## 유지할 계약

붕괴 표시 자식에는 Collider를 두지 않고 현재 지원 Collider 계약을 유지한다. ShatteredPlatform은 접촉 판정·붕괴 단계·자기 코루틴을, PlatformPresentation은 자기 경고 Tween·Visual Animator·외형 복원을 소유한다. 경고 완료 후 지원 Collider를 끄고 실제 Collapse 클립의 완료를 기다린다. 클립 미연결 시 한 실행에 경고 한 번을 남기고 외형을 숨겨 물리 붕괴와 후처리를 유지한다. 비활성화·파괴는 자기 작업을 취소하고 빌린 Animator를 임의 수정하지 않는다.

Controller는 Ready·Collapse·Completed 상태와 Collapse Trigger를 사용한다. Ready/Completed의 Motion은 비우며 실제 자산 클립은 Collapse에 연결한다. 연결 절차는 Assets/_Docs/tracking/ready-platform-hms-cleanup-2026-10-06/붕괴 애니메이션 연결 가이드.md를 따른다. 수명 정리와 스테이지 재준비를 붕괴 후 자동 복구로 해석하지 않는다. 신규 Prototype 발판은 canRegenerate=false 계약을 유지하며 기존 독립 자산의 재생성 설정을 일괄 덮어쓰지 않는다. 기존 TearFragment는 획득 시 해당 아이템 pickupRoot만 비활성화한다.

같은 오브젝트 Collider 요구는 기존 구현의 배치 예외다. 일반 루트 분리 원칙만으로 옮기지 않는다. HeavyState를 임의의 전역 플레이어 상태로 바꾸지 않는다.

## 검증

무게·접촉 방향, 중복 붕괴, 재생성·파괴, 중간 비활성화, 풀 재활성화, OnTriggerStay 수집, 인벤토리 상한에서도 기존 소비 동작을 확인한다.
