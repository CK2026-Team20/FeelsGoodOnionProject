using UnityEngine;

/// <summary>
/// 활성화 시 매 프레임 Y축을 회전시키는 컴포넌트입니다.<br/>
/// 제자리에서 돌아가는 연출 목적으로 사용됩니다.<br/>
/// 회전 속도는 deltaTime에 영향을 받습니다.
/// </summary>
public sealed class StunEffectOrbit : MonoBehaviour
{
    [SerializeField] private Vector3 rotationAxis = Vector3.up;

    [SerializeField, Min(0)]
    private float rotationSpeed = 180f;

    private void Update()
    {
        if (rotationAxis.sqrMagnitude <= 0f)
            return;

        transform.Rotate(rotationAxis.normalized, rotationSpeed * Time.deltaTime, Space.Self);
    }
}
