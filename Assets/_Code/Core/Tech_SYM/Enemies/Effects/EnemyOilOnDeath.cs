using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Enemies
{
    [DisallowMultipleComponent, RequireComponent(typeof(EnemyActor))]
    public sealed class EnemyOilOnDeath : MonoBehaviour
    {
        [Header("Oil")]
        [Tooltip("사망 시 발밑 지면에 기름을 한 번 생성합니다.")]
        [SerializeField] private bool spawnOilOnDeath;
        [Tooltip("기존 OilSurface를 사용하는 독립 기름 프리팹입니다.")]
        [SerializeField] private GameObject oilPrefab;
        [Tooltip("기름이 놓일 지면 레이어입니다.")]
        [SerializeField] private LayerMask groundMask = 1;
        [Tooltip("사망 위치 아래 지면 탐색 거리(m)입니다.")]
        [SerializeField, Min(.01f)] private float searchDistance = 10f;
        [Tooltip("생성할 기름의 가로/깊이 크기(m)입니다.")]
        [SerializeField] private Vector2 size = new Vector2(2, 1.5f);
        [Tooltip("기름 유지 시간(초)입니다. 0이면 씬 종료까지 유지합니다.")]
        [SerializeField, Min(0)] private float lifetime;
        private EnemyActor owner;
        private void Awake() => owner = GetComponent<EnemyActor>();
        private void OnEnable() => owner.Died += OnDied;
        private void OnDisable() => owner.Died -= OnDied;
        private void OnDied(EnemyActor actor)
        {
            if (!spawnOilOnDeath) return;
            if (oilPrefab == null || oilPrefab.GetComponent<OilSurface>() == null || oilPrefab.GetComponent<EnemyOilLifetime>() == null ||
                !float.IsFinite(searchDistance) || searchDistance <= 0 ||
                !float.IsFinite(size.x) || !float.IsFinite(size.y) || size.x <= 0 || size.y <= 0 ||
                !float.IsFinite(lifetime) || lifetime < 0)
            { Debug.LogError("기름 프리팹과 생성 설정을 확인하세요.", this); return; }
            RaycastHit hit = default;
            float nearest = float.PositiveInfinity;
            foreach (var candidate in Physics.RaycastAll(actor.BodyPosition, Vector3.down, searchDistance, groundMask, QueryTriggerInteraction.Ignore))
                if (actor.IsTerrainCollider(candidate.collider) && candidate.distance < nearest)
                { hit = candidate; nearest = candidate.distance; }
            if (float.IsPositiveInfinity(nearest)) return;
            var oil = Instantiate(oilPrefab, hit.point + hit.normal * .015f,
                Quaternion.FromToRotation(Vector3.up, hit.normal));
            oil.name = "Enemy Oil";
            oil.transform.localScale = new Vector3(size.x, 1, size.y);
            oil.GetComponent<EnemyOilLifetime>().Initialize(hit.collider.transform, lifetime);
        }
    }
}
