using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterMovement))]
[DefaultExecutionOrder(-100)]
public sealed class CharacterSlipperyEffect : MonoBehaviour
{
    [Header("Slippery Movement")]
    [SerializeField, Range(0.01f, 1f)]
    private float accelerationMultiplier = 0.2f;

    [SerializeField, Range(0.01f, 1f)]
    private float decelerationMultiplier = 0.04f;

    private CharacterMovement movement;
    private float expirationTime;

    public bool IsActive { get; private set; }

    public float RemainingTime => IsActive
        ? Mathf.Max(0f, expirationTime - Time.time)
        : 0f;

    private void Awake()
    {
        movement = GetComponent<CharacterMovement>();
    }

    private void FixedUpdate()
    {
        if (!IsActive)
        {
            return;
        }

        if (Time.time >= expirationTime)
        {
            ClearEffect();
            return;
        }

        // 실행 중 Inspector에서 수치를 바꿔도 반영한다.
        ApplyMovementMultipliers();
    }

    private void OnDisable()
    {
        ClearEffect();
    }

    public void Apply(float duration)
    {
        if (!isActiveAndEnabled || duration <= 0f)
        {
            return;
        }

        // 남은 시간에 더하지 않고 현재 시점부터 duration초로 갱신.
        expirationTime = Time.time + duration;
        IsActive = true;

        ApplyMovementMultipliers();
    }

    public void ClearEffect()
    {
        bool wasActive = IsActive;

        IsActive = false;
        expirationTime = 0f;

        if (wasActive && movement != null)
        {
            movement.SetTractionMultipliers(1f, 1f);
        }
    }

    private void ApplyMovementMultipliers()
    {
        movement.SetTractionMultipliers(
            accelerationMultiplier,
            decelerationMultiplier);
    }
}