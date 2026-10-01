using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider))]
public sealed class OilSurface : MonoBehaviour
{
    [SerializeField, Min(0.1f)]
    private float effectDuration = 3f;

    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        TryApplyEffect(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryApplyEffect(other);
    }

    private void TryApplyEffect(Collider other)
    {
        if (!isActiveAndEnabled || other.isTrigger)
        {
            return;
        }

        // 자식 Collider가 접촉해도 물리 본체에서 효과 컴포넌트를 찾는다.
        Rigidbody characterBody = other.attachedRigidbody;

        if (characterBody == null)
        {
            return;
        }

        if (characterBody.TryGetComponent(
                out CharacterSlipperyEffect slipperyEffect))
        {
            slipperyEffect.Apply(effectDuration);
        }
    }
}