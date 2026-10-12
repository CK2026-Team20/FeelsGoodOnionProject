# 현재 요구와 검증 상태

## 2026-10-12 V1.6 구현·소유 자산 이관

현행 원본은 [V1.6](../Cooked_시스템_기획서_V.1.6.docx)이며 최신 직접 답변이 우선한다. [실행 명세](ready-system-v16-2026-10-11/spec.md)에 따라 SYM 104개 전수 검수, 앱/한 판 종료 통합, UI·오디오 수명 정리, 고정 Root/내부 앵커/이동 Body, 세 시점 및 두 DOMove 통로 차단을 구현했다. Bootstrapper와 SessionShutdownOperation을 통합 삭제하여 최종 SYM 소스는102개다. HudIconGraphic은 실제 자원 연결 전 예외 유지한다.

HMS 누적12파일의 변경57개를 소비 근거로 재검토해 불필요 분리 카메라 확장·getter 등을 제거했다. PR 기준 PlayerCameraController는 원본과 같으며 필요한 피해·사망·회수·애니메이션·스킬 연결은 유지했다. 보호 HMS 기준 씬과 _Art·Packages·ProjectSettings의 수동 수정 권한을 확대하지 않았다.

| 결과 | 현재 판정 |
|---|---|
| 소스·Unity 컴파일 | 전체 Runtime150/Editor12 exit0, 실제 Editor compile failure 없음 |
| 소유 자산 | 12씬·61프리팹 직렬화 오류0/Missing0, 기존 물리·렌더1,030개 보존, 붕괴 연결7개 통과 |
| 카메라·통로 비 Play | 실제 DOMove/물리 검사85 assertions 통과, 전체 판정 PARTIAL. 다음 실제 프레임 입력 복귀·public Kill 비활성 전이 미평가 |
| 실제 게임·리소스 | Play/Player/새 빌드 미실행. 실제 애니메이션·Sprite·최종 구도·전체 한 판/재시도는 사람 확인 |
| Git/PR | 최종 반영 대상은 feature/core/systems와 기존 초안 PR #26이다. 원격 반영 결과는 해당 PR의 최신 head를 확인한다 |

[코드 리뷰](ready-system-v16-2026-10-11/코드%20리뷰.md), [전수 판정](ready-system-v16-2026-10-11/전수%20검수%20대상.md), [구조 및 연결 안내](ready-system-v16-2026-10-11/구조%20및%20연결%20가이드.md), [HMS 변경 근거](ready-system-v16-2026-10-11/HMS%20변경%20검토.md), [카메라 검토](ready-system-v16-2026-10-11/카메라%20검토.md), [실행 검사·사람 확인](ready-system-v16-2026-10-11/테스트%20및%20검증%20항목.md)을 따른다.

최신 미해결/협의는 [findings](findings.md)에 있다. 이전 상태 전체는 [변경 전 보관본](archive/2026-10-12-before-v16-status.md)에 보존했다. 과거 Play·빌드 성공은 이번 소스의 실행 성공이 아니다.

최종 소스·자산·문서 독립 검토에서 차단 사항은 없었고 과거 검증 링크 권고는 최신 경로로 수정했다. 구조 이관 커밋은 855781a이며 실제 검증과 사람 확인의 경계는 위 문서를 따른다.
