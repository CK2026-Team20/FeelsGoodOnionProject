using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Enemies
{
    /// <summary>적과 독립된 기름 수명. 플랫폼 스케일은 기름 크기에 전달하지 않는다.</summary>
    public sealed class EnemyOilLifetime : MonoBehaviour
    {
        private Transform surface;
        private Vector3 anchor;
        private Quaternion rotation;
        private float remaining;
        private bool expires;
        public void Initialize(Transform surface, float seconds)
        {
            this.surface = surface;
            anchor = surface.InverseTransformPoint(transform.position);
            rotation = Quaternion.Inverse(surface.rotation) * transform.rotation;
            remaining = seconds; expires = seconds > 0;
        }
        private void FixedUpdate()
        {
            if (surface != null) transform.SetPositionAndRotation(surface.TransformPoint(anchor), surface.rotation * rotation);
            if (!expires) return;
            remaining -= Time.fixedDeltaTime;
            if (remaining <= 0) Destroy(gameObject);
        }
    }
}
