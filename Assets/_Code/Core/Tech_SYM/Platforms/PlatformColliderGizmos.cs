using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Platforms
{
    /// <summary>실제 Collider의 자식 변환을 보존하는 작성용 표시. 경로의 연속 충돌 검증은 아니다.</summary>
    public static class PlatformColliderGizmos
    {
        public static void Draw(PlatformContacts contacts, Matrix4x4 displacement)
        {
            if (contacts == null) return;
            var surfaces = contacts.Surfaces;
            if (surfaces == null || surfaces.Count == 0) Draw(contacts.Surface, displacement);
            else foreach (var surface in surfaces) Draw(surface, displacement);
        }
        public static void Draw(Collider collider, Matrix4x4 displacement)
        {
            if (collider == null) return;
            Matrix4x4 saved = Gizmos.matrix; Color color = Gizmos.color;
            try
            {
                Gizmos.matrix = displacement * collider.transform.localToWorldMatrix;
                Gizmos.color = collider.isTrigger ? new Color(1f, .55f, 0f) : Color.cyan;
                if (collider is MeshCollider mesh && mesh.sharedMesh != null)
                {
                    // 비 Convex는 실제 삼각형, Convex는 입력 메시 참고선이다. cooked hull은 Physics Debugger로 확인한다.
                    if (mesh.convex) Gizmos.color = Color.magenta;
                    Gizmos.DrawWireMesh(mesh.sharedMesh);
                }
                else if (collider is BoxCollider box) Gizmos.DrawWireCube(box.center, box.size);
                else if (collider is SphereCollider sphere) Gizmos.DrawWireSphere(sphere.center, sphere.radius);
            }
            finally { Gizmos.matrix = saved; Gizmos.color = color; }
        }
        public static void DrawAt(KinematicPlatformMotion motion, Vector3 position, Quaternion rotation)
        {
            if (motion == null || motion.PhysicsBody == null) return;
            var body = motion.PhysicsBody.transform;
            Matrix4x4 displacement = Matrix4x4.TRS(position, rotation, Vector3.one) *
                Matrix4x4.TRS(body.position, body.rotation, Vector3.one).inverse;
            Draw(motion.Contacts, displacement);
        }
        public static void DrawHemisphere(Vector3 center, Vector3 up, float radius)
        {
            Gizmos.color = Color.yellow;
            Vector3 x = Vector3.Cross(up, Mathf.Abs(up.y) < .9f ? Vector3.up : Vector3.right).normalized;
            Vector3 z = Vector3.Cross(up, x);
            const int count = 32;
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count, b = (i + 1) * Mathf.PI * 2f / count;
                Gizmos.DrawLine(center + radius * (x * Mathf.Cos(a) + z * Mathf.Sin(a)), center + radius * (x * Mathf.Cos(b) + z * Mathf.Sin(b)));
                float c = i * Mathf.PI / count, d = (i + 1) * Mathf.PI / count;
                foreach (var tangent in new[] { x, z })
                    Gizmos.DrawLine(center + radius * (tangent * Mathf.Cos(c) - up * Mathf.Sin(c)), center + radius * (tangent * Mathf.Cos(d) - up * Mathf.Sin(d)));
            }
        }
    }
}
