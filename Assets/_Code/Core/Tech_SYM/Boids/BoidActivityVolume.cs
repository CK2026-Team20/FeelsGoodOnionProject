using UnityEngine;
using Cooked.Authoring;

namespace Cooked.Boids
{
    [DisallowMultipleComponent]
    public sealed class BoidActivityVolume : MonoBehaviour
    {
        [SerializeField, InspectorLabel("활동 박스"), Tooltip("활동 범위를 정의하는 BoxCollider Trigger를 연결하세요. 청록색은 경계입니다.")]
        BoxCollider box;
        public BoxCollider Box => box;
        public void Configure(BoxCollider value)
        {
            box = value;
        }

        public bool Contains(Vector3 world, float radius)
        {
            if (!box)
                return false;
            Vector3 p = box.transform.InverseTransformPoint(world) - box.center;
            Vector3 scale = box.transform.lossyScale;
            Vector3 e = box.size * .5f;
            return Mathf.Abs(p.x) + radius / Mathf.Abs(scale.x) <= e.x
                && Mathf.Abs(p.y) + radius / Mathf.Abs(scale.y) <= e.y
                && Mathf.Abs(p.z) + radius / Mathf.Abs(scale.z) <= e.z;
        }

        public Vector3 Inward(Vector3 world)
        {
            return (box.transform.TransformPoint(box.center) - world).normalized;
        }

        public void Validate(float radius)
        {
            if (!box || !box.isTrigger)
                throw new System.InvalidOperationException("활동 박스 Trigger가 필요합니다.");
            var s = box.transform.lossyScale;
            var size = Vector3.Scale(box.size, s);
            if (Mathf.Min(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)) <= radius * 2)
                throw new System.InvalidOperationException("활동 박스가 개체 몸보다 작습니다.");
        }

        void OnDrawGizmos()
        {
            if (!box)
                return;
            Gizmos.color = Color.cyan;
            var old = Gizmos.matrix;
            Gizmos.matrix = box.transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, box.size);
            Gizmos.matrix = old;
        }
    }
}
