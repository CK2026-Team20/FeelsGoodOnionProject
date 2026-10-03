using UnityEngine;

public sealed class StunEffectOrbit : MonoBehaviour
{
    [SerializeField] private Vector3 rotationAxis = Vector3.up;

    [SerializeField, Min(0)]
    private float rotationSpeed = 120f;

    private void Update()
    {
        if (rotationAxis.sqrMagnitude <= 0f)
            return;

        transform.Rotate(rotationAxis.normalized, rotationSpeed * Time.deltaTime, Space.Self);
    }
}
