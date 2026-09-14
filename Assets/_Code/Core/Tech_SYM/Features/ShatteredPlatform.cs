using System.Collections;
using System.Collections.Generic;
using FeelsGoodOnion.TechSYM.Managers;
using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Features
{
    [DisallowMultipleComponent]
    public sealed class ShatteredPlatform : MonoBehaviour, IBrokenable
    {
        public enum CollapsePhase { Ready, Wiggling, Collapsed, Regenerating }
        public enum CollisionPolicy { HeavyCharactersOnly, Everyone }

        [Header("References")]
        [Tooltip("IPlatformStateSource 구현 컴포넌트. 비어 있으면 PlatformManager.Instance 사용.")]
        [SerializeField] private MonoBehaviour stateSource;
        [Tooltip("컨트롤러의 자식인 Plane 표시 오브젝트. 충돌체를 포함하지 않아야 한다.")]
        [SerializeField] private Transform visual;
        [SerializeField] private Collider supportCollider;
        [SerializeField] private BoxCollider detectionTrigger;

        [Header("Hand-written tweens")]
        [SerializeField, Min(0.01f)] private float wiggleDuration = 1f;
        [SerializeField, Min(0f)] private float wiggleAngle = 4f;
        [SerializeField, Min(0.1f)] private float wiggleFrequency = 8f;
        [SerializeField, Min(0.01f)] private float regenerationDuration = 3f;
        [SerializeField, Min(0.01f)] private float blinkInterval = 0.15f;
        [SerializeField] private CollisionPolicy collisionPolicy = CollisionPolicy.HeavyCharactersOnly;

        public CollapsePhase Phase { get; private set; } = CollapsePhase.Ready;
        public CollisionPolicy Policy => collisionPolicy;

        private readonly List<Collider> heavyColliders = new List<Collider>();
        private readonly HashSet<Collider> ignoredColliders = new HashSet<Collider>();
        private Renderer[] renderers;
        private bool[] rendererDefaults;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private bool initialized;
        private Coroutine cycle;

        private IPlatformStateSource StateSource => stateSource != null
            ? stateSource as IPlatformStateSource : PlatformManager.Instance;

        private void Awake()
        {
            if (visual == null || visual == transform || !visual.IsChildOf(transform)
                || supportCollider == null || supportCollider.isTrigger
                || detectionTrigger == null || !detectionTrigger.isTrigger
                || supportCollider == detectionTrigger
                || visual.GetComponentsInChildren<Collider>(true).Length != 0
                || (stateSource != null && !(stateSource is IPlatformStateSource)))
            {
                Debug.LogError("플랫폼에 독립된 Plane 표시, 지지 Collider, 감지 Trigger와 올바른 상태 제공자가 필요합니다.", this);
                enabled = false;
                return;
            }
            restPosition = visual.localPosition;
            restRotation = visual.localRotation;
            renderers = visual.GetComponentsInChildren<Renderer>(true);
            rendererDefaults = new bool[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) rendererDefaults[i] = renderers[i].enabled;
            initialized = true;
        }

        private void OnTriggerEnter(Collider other) => TryStartFrom(other);
        // 위에 머문 뒤 HeavyState가 활성화된 경우도 감지한다.
        private void OnTriggerStay(Collider other) => TryStartFrom(other);

        private void TryStartFrom(Collider other)
        {
            if (Phase != CollapsePhase.Ready || other.isTrigger || StateSource == null
                || !StateSource.IsHeavy(other)) return;

            // 감지 박스를 옆이나 아래에서 스친 경우는 제외한다. 수평 플랫폼 기준.
            Bounds actor = other.bounds;
            Bounds surface = supportCollider.bounds;
            const float tolerance = 0.08f;
            if (actor.min.y < surface.max.y - tolerance || actor.center.y <= surface.max.y
                || actor.max.x <= surface.min.x || actor.min.x >= surface.max.x
                || actor.max.z <= surface.min.z || actor.min.z >= surface.max.z) return;
            Collapses();
        }

        public void Collapses()
        {
            if (!initialized || !isActiveAndEnabled || Phase != CollapsePhase.Ready) return;
            // 즉시 단계를 바꿔 같은 물리 프레임의 복수 충돌 요청을 막는다.
            Phase = CollapsePhase.Wiggling;
            cycle = StartCoroutine(CollapseCycle());
        }

        private IEnumerator CollapseCycle()
        {
            float elapsed = 0f;
            while (elapsed < wiggleDuration)
            {
                float phase = elapsed * wiggleFrequency * Mathf.PI * 2f;
                float envelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(elapsed / wiggleDuration));
                visual.localRotation = restRotation * Quaternion.Euler(
                    Mathf.Sin(phase) * wiggleAngle * envelope, 0f,
                    Mathf.Sin(phase * 1.25f) * wiggleAngle * envelope);
                elapsed += Time.deltaTime;
                yield return null;
            }

            ResetPose();
            Phase = CollapsePhase.Collapsed;
            detectionTrigger.enabled = false;
            if (collisionPolicy == CollisionPolicy.Everyone) supportCollider.enabled = false;
            else IgnoreHeavyCharacters();

            // 애니메이션 대신 표시 오브젝트를 비활성화한다.
            // 코루틴을 실행하는 루트는 활성 상태로 유지한다.
            visual.gameObject.SetActive(false);
            yield return null;

            // 현재 대체 애니메이션은 한 프레임에 끝나므로 즉시 재생성을 시작한다.
            Phase = CollapsePhase.Regenerating;
            visual.gameObject.SetActive(true);
            elapsed = 0f;
            while (elapsed < regenerationDuration)
            {
                if (collisionPolicy == CollisionPolicy.HeavyCharactersOnly) IgnoreHeavyCharacters();
                SetVisible(Mathf.FloorToInt(elapsed / blinkInterval) % 2 != 0);
                elapsed += Time.deltaTime;
                yield return null;
            }
            Restore();
            cycle = null;
        }

        private void IgnoreHeavyCharacters()
        {
            if (StateSource == null) return;
            StateSource.CollectHeavyColliders(heavyColliders);
            foreach (Collider actor in heavyColliders)
            {
                if (actor == null || actor == supportCollider || ignoredColliders.Contains(actor)) continue;
                // 다른 시스템이 이미 무시한 충돌 쌍의 소유권을 빼앗지 않는다.
                if (Physics.GetIgnoreCollision(supportCollider, actor)) continue;
                Physics.IgnoreCollision(supportCollider, actor, true);
                ignoredColliders.Add(actor);
            }
        }

        private void SetVisible(bool visible)
        {
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null) renderers[i].enabled = visible && rendererDefaults[i];
        }

        private void ResetPose()
        {
            if (visual == null) return;
            visual.localPosition = restPosition;
            visual.localRotation = restRotation;
        }

        private void Restore()
        {
            foreach (Collider actor in ignoredColliders)
                if (actor != null && supportCollider != null)
                    Physics.IgnoreCollision(supportCollider, actor, false);
            ignoredColliders.Clear();
            ResetPose();
            if (visual != null) visual.gameObject.SetActive(true);
            SetVisible(true);
            if (supportCollider != null) supportCollider.enabled = true;
            if (detectionTrigger != null) detectionTrigger.enabled = true;
            Phase = CollapsePhase.Ready;
        }

        private void OnDisable()
        {
            if (cycle != null) StopCoroutine(cycle);
            cycle = null;
            if (initialized) Restore();
        }
    }
}
