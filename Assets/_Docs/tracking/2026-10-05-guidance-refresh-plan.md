# 실행 계획

1. docs: 기존 미커밋 파일 해시·문서 원문을 보관하고 승인된 문서 정비를 메인이 직접 수행한다. 이전 이력은 보존하며 현재 지침을 갱신한다.
2. reconcile: 기능별 코드·자산 대조로 최신 명세와 다른 부분을 식별하고 허용 범위의 확인된 결함을 수정한다. 단일 Unity Editor 조작은 구현 담당 한 명만 소유하며 컴파일·영향 검증과 보호 영역의 차이를 기록한다.
3. verify_review: 독립 최종 검토, 파일·링크·GUID·diff 감사 후 최신 상태를 갱신한다.

```yaml
{
  "tasks": [
    {"id": "docs", "risk": "MECHANICAL", "depends": []},
    {"id": "reconcile", "risk": "RISKY", "depends": ["docs"]},
    {"id": "verify_review", "risk": "RISKY", "depends": ["reconcile"]}
  ],
  "regen_barriers": []
}
```

기존 공유 체크아웃의 누적 변경이 이 작업의 입력이므로 main/작업 브랜치 재설정·자동 커밋을 수행하지 않는다. 사용자 소유권·미저장 작업 경계가 스킬 기본 Git 조작보다 우선한다. 위 그래프는 이 승인 작업의 의존 순서를 명시하며 구현 내부 순서와 그래프 변경을 구분한다.
