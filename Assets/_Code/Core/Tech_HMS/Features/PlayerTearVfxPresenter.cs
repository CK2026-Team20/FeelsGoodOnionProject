using UnityEngine;

/// <summary>눈물 스킬의 실제 발동에 맞춰 플레이어 소유 이펙트를 재생합니다.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(PlayerFacade))]
public sealed class PlayerTearVfxPresenter : MonoBehaviour
{
    [SerializeField] private GameObject effectPrefab;
    [SerializeField] private Vector3 localOffset;
    [SerializeField, Min(0.01f)] private float effectScale = 1f;

    private PlayerFacade actor;
    private GameObject effect;
    private ParticleSystem[] particles;

    private void Awake() => actor = GetComponent<PlayerFacade>();

    private void OnEnable()
    {
        actor.SkillActivated += OnSkillActivated;
        actor.StateChanged += OnStateChanged;
    }

    private void OnSkillActivated(PlayerSkillId skill)
    {
        if (skill != PlayerSkillId.Tear || actor.IsDead || effectPrefab == null) return;
        ClearEffect();
        effect = Instantiate(effectPrefab, transform);
        effect.transform.localPosition = localOffset;
        effect.transform.localRotation = Quaternion.identity;
        effect.transform.localScale = effectPrefab.transform.localScale * effectScale;
        particles = effect.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var system in particles)
        {
            system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = false;
            main.playOnAwake = false;
            main.stopAction = ParticleSystemStopAction.None;
        }
        effect.SetActive(true);
        foreach (var system in particles)
            if (system.gameObject.activeInHierarchy) system.Play(false);
    }

    private void LateUpdate()
    {
        if (effect == null) return;
        foreach (var system in particles)
            if (system != null && system.IsAlive(false)) return;
        ClearEffect();
    }

    private void OnStateChanged(PlayerState previous, PlayerState current)
    {
        if (current == PlayerState.Dead) ClearEffect();
    }

    private void ClearEffect()
    {
        if (effect != null)
        {
            effect.SetActive(false);
            Destroy(effect);
        }
        effect = null;
        particles = null;
    }

    private void OnDisable()
    {
        actor.SkillActivated -= OnSkillActivated;
        actor.StateChanged -= OnStateChanged;
        ClearEffect();
    }
}
