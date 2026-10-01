using UnityEngine;

/// <summary>
/// 플레이어의 논리적인 정면 방향에 맞춰 외형만 회전시킴.<br/>
/// 루트와 물리 Collider는 회전시키지 않음
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-100)]
[RequireComponent(typeof(CharacterMovement))]
public sealed class PlayerVisualController : MonoBehaviour
{
    [Tooltip("모델의 정면이 로컬 +Z를 향하도록 구성한 외형 루트.")]
    [SerializeField]
    private Transform playerVisualRoot;

    private CharacterMovement movement;

    private void Awake()
    {
        movement = GetComponent<CharacterMovement>();

        if (playerVisualRoot == null)
        {
            playerVisualRoot = transform.Find("VisualRoot");
        }

        if (playerVisualRoot == null || playerVisualRoot.parent != transform)
        {
            Debug.LogError("플레이어 루트의 직계 자식인 외형 루트를 연결해 주세요.", this);
            enabled = false;
        }
    }

    private void Start()
    {
        ApplyFacingRotation();
    }

    private void LateUpdate()
    {
        ApplyFacingRotation();
    }

    /// <summary>
    /// 외형의 로컬 +Z가 플레이어의 수평 정면 방향을 향하도록 합니다
    /// </summary>
    private void ApplyFacingRotation()
    {
        Vector3 direction = movement.FacingDirection;
        direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        playerVisualRoot.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }
}