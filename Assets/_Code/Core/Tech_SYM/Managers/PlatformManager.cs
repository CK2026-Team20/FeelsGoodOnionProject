using System.Collections.Generic;
using FeelsGoodOnion.TechSYM.Features;
using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Managers
{
    [DisallowMultipleComponent]
    public sealed class PlatformManager : MonoBehaviour, IPlatformStateSource
    {
        public static PlatformManager Instance { get; private set; }

        [Tooltip("플랫폼 검증 대상. 실제 캐릭터 연결 시 이 더미 조회 부분만 어댑터로 교체한다.")]
        [SerializeField] private List<HeavyState> trackedCharacters = new List<HeavyState>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("씬에 PlatformManager가 중복되어 중복 컴포넌트를 비활성화합니다.", this);
                enabled = false;
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Register(HeavyState character)
        {
            if (character != null && !trackedCharacters.Contains(character))
                trackedCharacters.Add(character);
        }

        public void Unregister(HeavyState character) => trackedCharacters.Remove(character);

        public bool IsHeavy(Collider actorCollider)
        {
            if (actorCollider == null) return false;
            HeavyState state = actorCollider.GetComponentInParent<HeavyState>();
            return state != null && trackedCharacters.Contains(state)
                && state.isActiveAndEnabled && state.IsEntered;
        }

        public void CollectHeavyColliders(List<Collider> results)
        {
            results.Clear();
            foreach (HeavyState state in trackedCharacters)
            {
                if (state == null || !state.isActiveAndEnabled || !state.IsEntered) continue;
                foreach (Collider candidate in state.GetComponentsInChildren<Collider>())
                    if (candidate.enabled && !candidate.isTrigger) results.Add(candidate);
            }
        }
    }
}
