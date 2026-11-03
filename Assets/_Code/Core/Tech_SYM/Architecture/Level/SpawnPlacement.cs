using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cooked.Level
{
    /// <summary>Foot-position geometry for runtime; collision validation for Editor authoring only.</summary>
    public static class SpawnPlacement
    {
        public const float Skin = .002f;
        public const float GroundTolerance = .05f;
        public static void Geometry(CapsuleCollider capsule, Quaternion rotation, out float radius, out float height)
        {
            if (capsule == null || capsule.isTrigger || capsule.direction != 1 ||
                !CameraProfileValidation.IsFinite(capsule.center) ||
                !float.IsFinite(rotation.x) || !float.IsFinite(rotation.y) || !float.IsFinite(rotation.z) || !float.IsFinite(rotation.w) ||
                Mathf.Abs(Quaternion.Dot(rotation, rotation) - 1f) > .01f || Vector3.Dot(rotation * Vector3.up, Vector3.up) < .999f)
                throw new InvalidOperationException("스폰에는 수직 Y축 플레이어 Capsule과 유효한 수평 회전이 필요합니다.");
            Vector3 scale = capsule.transform.lossyScale;
            if (!CameraProfileValidation.IsFinite(scale) || scale.x <= 0 || scale.y <= 0 || scale.z <= 0)
                throw new InvalidOperationException("플레이어 Capsule 배율은 유한한 양수여야 합니다.");
            radius = capsule.radius * Mathf.Max(scale.x, scale.z);
            height = Mathf.Max(capsule.height * scale.y, radius * 2);
            if (!float.IsFinite(radius) || !float.IsFinite(height) || radius <= Skin)
                throw new InvalidOperationException("플레이어 Capsule 크기가 유효하지 않습니다.");
        }
        public static Vector3 Center(CapsuleCollider capsule, Pose feet)
        {
            if (!CameraProfileValidation.IsFinite(feet.position)) throw new InvalidOperationException("스폰 발바닥 위치가 유한하지 않습니다.");
            Geometry(capsule, feet.rotation, out _, out float height);
            return feet.position + Vector3.up * (height * .5f);
        }
        public static Vector3 Feet(CapsuleCollider capsule)
        {
            Geometry(capsule, capsule.transform.rotation, out _, out float height);
            return capsule.transform.TransformPoint(capsule.center) - Vector3.up * (height * .5f);
        }
#if UNITY_EDITOR
        public static bool Check(CapsuleCollider capsule, Pose feet, PhysicsScene world, Transform ignoredRoot, out string reason)
        {
            try
            {
                Center(capsule, feet);
                Geometry(capsule, feet.rotation, out float radius, out float height);
                if (!world.IsValid()) throw new InvalidOperationException("스폰 검사 물리 씬이 유효하지 않습니다.");
                Physics.SyncTransforms();
                bool Obstacle(Collider c) => c != null && c != capsule &&
                    (ignoredRoot == null || !c.transform.IsChildOf(ignoredRoot)) &&
                    !Physics.GetIgnoreLayerCollision(capsule.gameObject.layer, c.gameObject.layer) &&
                    !Physics.GetIgnoreCollision(capsule, c);
                var hits = new Collider[128];
                int count = world.OverlapCapsule(feet.position + Vector3.up * radius,
                    feet.position + Vector3.up * (height - radius), radius - Skin, hits, ~0, QueryTriggerInteraction.Ignore);
                if (count == hits.Length) throw new InvalidOperationException("스폰 겹침 검사 용량을 초과했습니다.");
                for (int i = 0; i < count; i++)
                    if (Obstacle(hits[i])) throw new InvalidOperationException("스폰 공간이 '" + hits[i].name + "' Collider와 겹칩니다.");
                var ground = new RaycastHit[128];
                count = world.Raycast(feet.position + Vector3.up * GroundTolerance, Vector3.down, ground,
                    GroundTolerance * 2, ~0, QueryTriggerInteraction.Ignore);
                if (count == ground.Length) throw new InvalidOperationException("스폰 지면 검사 용량을 초과했습니다.");
                float angle = capsule.GetComponent<CharacterMovement>()?.MaximumGroundAngle ?? 45f;
                for (int i = 0; i < count; i++)
                    if (Obstacle(ground[i].collider) && Vector3.Angle(ground[i].normal, Vector3.up) <= angle)
                    { reason = null; return true; }
                throw new InvalidOperationException("발바닥 ±0.05m에 서 있을 수 있는 지면이 없습니다.");
            }
            catch (InvalidOperationException error) { reason = error.Message; return false; }
        }
#endif
    }
}
