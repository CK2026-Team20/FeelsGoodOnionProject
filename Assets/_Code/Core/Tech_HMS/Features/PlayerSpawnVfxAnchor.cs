using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>스폰 이펙트만 시작 위치에 고정하고, 형태 복귀 시에는 기존 플레이어 자식으로 재생합니다.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(PlayerFacade), typeof(PlayerFormController))]
[DefaultExecutionOrder(300)]
public sealed class PlayerSpawnVfxAnchor : MonoBehaviour
{
    [SerializeField, Tooltip("같은 몸체의 PlayerVfxPresenter에 연결된 등장·회수 파티클을 지정합니다.")]
    private ParticleSystem spawnBurst;

    private PlayerFacade actor;
    private PlayerFormController forms;
    private Transform originalParent;
    private Vector3 originalPosition;
    private Quaternion originalRotation;
    private Vector3 originalScale;
    private GameObject worldAnchor;
    private bool bound;

    private void Awake()
    {
        actor = GetComponent<PlayerFacade>();
        forms = GetComponent<PlayerFormController>();
        if (!spawnBurst || !spawnBurst.transform.IsChildOf(transform))
        {
            Debug.LogError("플레이어 자식의 등장 파티클을 연결하세요.", this);
            enabled = false;
            return;
        }
        originalParent = spawnBurst.transform.parent;
        originalPosition = spawnBurst.transform.localPosition;
        originalRotation = spawnBurst.transform.localRotation;
        originalScale = spawnBurst.transform.localScale;
    }

    private void OnEnable()
    {
        if (!spawnBurst || !originalParent || bound) return;
        forms.FormChanged += OnFormChanged;
        bound = true;
        if (!actor.IsDead) AnchorAtPlayer();
    }

    private void OnFormChanged(PlayerForm form)
    {
        if (form != PlayerForm.Normal || actor.IsDead || !worldAnchor) return;
        RestoreParent();
        // 스폰 도중 복귀한 경우에도 이전 월드 위치에서 시작한 입자를 정리합니다.
        spawnBurst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        spawnBurst.Play(true);
    }

    private void AnchorAtPlayer()
    {
        if (!spawnBurst || !originalParent) return;
        if (!worldAnchor)
        {
            worldAnchor = new GameObject("Player Spawn VFX Anchor");
            SceneManager.MoveGameObjectToScene(worldAnchor, gameObject.scene);
        }
        worldAnchor.transform.SetPositionAndRotation(
            originalParent.TransformPoint(originalPosition), originalParent.rotation * originalRotation);
        spawnBurst.transform.SetParent(worldAnchor.transform, false);
        spawnBurst.transform.localPosition = Vector3.zero;
        spawnBurst.transform.localRotation = Quaternion.identity;
        spawnBurst.transform.localScale = Vector3.Scale(originalParent.lossyScale, originalScale);
        // 기존 Presenter가 이전 월드 위치에서 시작한 입자도 새 발동 위치로 맞춥니다.
        spawnBurst.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        spawnBurst.Play(true);
    }

    private void LateUpdate()
    {
        if (worldAnchor && spawnBurst && !spawnBurst.IsAlive(true)) RestoreParent();
    }

    private void RestoreParent()
    {
        if (spawnBurst && originalParent)
        {
            spawnBurst.transform.SetParent(originalParent, false);
            spawnBurst.transform.localPosition = originalPosition;
            spawnBurst.transform.localRotation = originalRotation;
            spawnBurst.transform.localScale = originalScale;
        }
        if (worldAnchor) Destroy(worldAnchor);
        worldAnchor = null;
    }

    private void OnDisable()
    {
        if (bound && forms) forms.FormChanged -= OnFormChanged;
        bound = false;
        RestoreParent();
    }
}
