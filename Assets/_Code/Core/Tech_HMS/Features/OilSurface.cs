using Cooked.Authoring;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class OilSurface : MonoBehaviour
{
    [SerializeField, InspectorLabel("기름 감지 영역"), Tooltip("이 컴포넌트와 같은 오브젝트의 기름 Trigger를 연결하세요. MeshCollider는 Convex를 켜세요. Pan 지지면과 분리하며 비워 두면 효과를 적용하지 않습니다.")]
    private Collider oilTrigger;

    [SerializeField, Min(0.1f), InspectorLabel("미끄러짐 지속 시간"), Tooltip("기름 접촉으로 갱신되는 미끄러짐 효과의 지속 시간입니다(초).")]
    private float effectDuration = 3f;

    private bool HasValidTrigger => oilTrigger != null && oilTrigger.gameObject == gameObject && oilTrigger.isTrigger &&
        (!(oilTrigger is MeshCollider mesh) || (mesh.convex && mesh.sharedMesh != null));

    private void OnEnable()
    {
        if (oilTrigger != null && !HasValidTrigger)
            Debug.LogError("기름 감지 영역에 같은 오브젝트의 Trigger를 연결하세요. MeshCollider는 메시와 Convex 설정이 필요합니다.", this);
    }

    private void OnTriggerEnter(Collider other) => TryApplyEffect(other);
    private void OnTriggerStay(Collider other) => TryApplyEffect(other);

    private void TryApplyEffect(Collider other)
    {
        if (!isActiveAndEnabled || !HasValidTrigger || !oilTrigger.enabled || !oilTrigger.gameObject.activeInHierarchy ||
            other == null || other.isTrigger || !other.enabled || !other.gameObject.activeInHierarchy ||
            Physics.GetIgnoreCollision(oilTrigger, other) ||
            Physics.GetIgnoreLayerCollision(oilTrigger.gameObject.layer, other.gameObject.layer)) return;

        // 같은 Rigidbody의 다른 감지 영역 콜백이 기름 효과를 일으키지 않도록 한다.
        if (!Physics.ComputePenetration(oilTrigger, oilTrigger.transform.position, oilTrigger.transform.rotation,
            other, other.transform.position, other.transform.rotation, out _, out _)) return;

        // 자식 Collider가 접촉해도 물리 본체에서 효과 컴포넌트를 찾는다.
        Rigidbody characterBody = other.attachedRigidbody;
        if (characterBody != null && characterBody.TryGetComponent(out CharacterSlipperyEffect slipperyEffect))
            slipperyEffect.Apply(effectDuration);
    }
}
