# 물리 발판

현재 시스템 기준은 Assets/_Docs/planning-index.md의 V1.8이며 이전 사용자 기능 명세와 기존 구현보다 우선한다. 구현·검증 완료는 tracking/status.md와 실제 증거로 확인한다. 보호 원본·메타·아트의 쓰기 권한은 최신 파일별 허용 범위로 한정한다.

KinematicPlatformMotion은 물리 이동의 단일 작성자, PlatformContacts는 실제 윗면 콜백/점유, 종류별 컨트롤러는 진행 목적/상태를 소유한다. 신규 구조는 Root 로직과 명시적으로 주입받는 PhysicsBody/Contacts 자식이다. 현재 GetComponent/RequireComponent 및 콜백 위치를 실제 코드와 함께 이관하며 같은 물체에 HMS 이동자를 중복 조립하지 않는다.

LinearShuttlePlatform은 실제 startPoint/endPoint Transform과 월드m/s, 양 종점0.5초 대기다. 이동량0은 정지이며 부모 변환은 끝점에 반영한다. FloatingPlatform은 한 착지당 눌림/반동이고 PhysicsBody의 지속 bob은 제거한다. Periodic은 조절 가능한 초기5/2/3초·생성 중 비충돌·개별 시작 위상이다. 압력은 기본PC/껍질만, 해제 복귀·재압력 반전은 현재 진행을 사용한다. DamageFloorPlatform은 Inspector의 정수 피해량(기본1, 0은 무피해)을 전달한다. PotDamagePlatform·GratangDamagePlatform·BurnerDamagePlatform은 실제 피해원 타입이며 플랫폼은 애니메이션을 선택하지 않는다. 플레이어의 무적 판정이 피해량보다 우선하므로 무적 중9999도 무효다. 수락된 피해로 HP가0이 되면 기존 사망 연출·재시도를 사용한다. 데드존의 별도 재시작 경로는 유지한다.

ManualUpdate 트윈은 물리 단계에서 진행하고 비활성화/제목/Stage 제거 시 자기 작업·점유를 해제한다. 발판/장치/적 상태는 체크포인트에 저장하지 않는다. retry는 Stage 초기 재준비다.

검증은 왕복·탑승·일반 물체 운반·점프/이탈 관성·정지/복귀·중간 재입력의 결합 기능으로 묶는다. 끝점 이동만으로 전체 기능 통과를 쓰지 않는다. 실제 물리 시간/모든 phase, 부모 변환·잘못된 값·재활성화·콜백 누락을 확인한다.

현행 지침: Inspector 위치/대상은 실물 참조, 수치는 목적·단위가 명확한 값이다. 씬 월드 객체를 관리자 자식에 묶지 않는다. 에이전트 Play 금지는 철회됐으며 공식 Unity 도구로 승인된 검증을 실행한다. 기존 프리팹 삭제/재생성/Apply·자동 Builder 실행은 해당 작업의 명시 허용 범위를 따른다. 이전 실행 관찰 수는 당시 증거이고 이번 통과가 아니다.

주기 발판은 실제 Collider 교차와 현재 몸체 세로 침투 비율로 판단한다. <1/4는 윗면 착지 보정, 정확히1/4 이상은 실제 ActorBody/Rigidbody/Collider 위 이동과 Dead 완료를 한 번 조정한다. 껍질은 독립 분리하고 데드존에서 같은 소유 객체를 반환한다. ShatteredPlatform의 물리 없는 Visual은 Animator가 없어도 경고 Shake로 움직이므로 고정 Marker를 하위에 두지 않는다.


2026-10-14 사용자 승인 이관: PlatformContacts는 같은 Rigidbody의 명시 Collider 목록과 실제 접점/법선을 소유하며 Box 필수 조건을 제거했다. 플레이어 아래 반구와 MaximumGroundAngle로 필터링하고 HMS는 ICharacterGroundContactFilter 계약을 소비한다. 주기는 실제 메시 교차의 전체 캡슐 높이 비율을 사용하며, 얕은 벽·급경사는 Bounds 중심 방향의 수평 분리 뒤 중력으로 착지한다. DamageFloor는 명시 Trigger 목록, OilSurface는 같은 객체의 Convex Trigger를 사용한다. 이번 승인 대상 프리팹은 Unity API로 수정하며 원본 아트와 씬은 저장하지 않는다. 상세 명세/증거는 tracking/mesh-platforms-2026-10-14를 따른다.

버튼은 빈 기능 Root 아래 Cap Rigidbody·접촉면·Visual만 움직이고 Body는 고정 충돌체·Visual을 유지한다. 눌림 깊이는 실제 이동 몸체의 고정 부모 로컬 -Y 기준이며 비균일 배율을 반영한다. 원본 메시의 장식 기둥을 cap 지지 hull에 포함해 Body와 겹치게 하지 않는다. 비활성 선택형과 실제 센서는 이름만으로 제거하지 않는다.

뒷면 메시를 생략한 아트는 구도·추적·전환 중간에서도 뒷면을 노출하지 않도록 배치한다. 최신 사용자 정정으로 반대편 카메라 이동은 철회했고 기존 방향을 유지한다. 사용자가 오브젝트 배치를 회전하는 작업은 에이전트가 완료한 것으로 기록하지 않는다. 보호 아트의 임의 수정·양면화나 HMS 입력 변환으로 우회하지 않으며 현재 백페이스 노출은 미해결 시각 항목이다.

## 2026-10-30 묶음 프리팹 작성 의도

묶음 프리팹에서 비어 있는 Collider/Rigidbody 참조는 사용자가 사용할 메시를 선택한 뒤 직접 드래그하는 작성 슬롯이다. 자동으로 첫 번째 자식이나 프로토타입 선택값을 채우지 않는다. 메시/몸체 선택과 씬 참조를 공통 설정으로 일괄 Apply하지 않는다. DamageablePlatforms 루트에는 빈 피해 컴포넌트를 두지 않고 개별 Pot/Burner/Gratan에서 구체 피해 타입과 자기 감지영역을 소유한다. Gratan은 기존 묶음 내부에 유지한다. 이번 직접 승인에 따른 지정 프리팹 수정은 아트·기존 스케일·배치 수정 권한을 포함하지 않는다.
