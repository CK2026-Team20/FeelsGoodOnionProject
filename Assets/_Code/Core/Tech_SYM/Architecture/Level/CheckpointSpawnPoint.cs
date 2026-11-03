using System;
using Cooked.Authoring;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cooked.Level
{
    [DisallowMultipleComponent]
    public sealed class CheckpointSpawnPoint : MonoBehaviour
    {
        [SerializeField, InspectorLabel("복귀용 Normal Capsule"), Tooltip("실제 복귀 플레이어 프리팹의 Normal 폼 Capsule을 연결하세요. 에디터 배치 시에만 공간 검사에 사용합니다.")]
        private CapsuleCollider normalCapsule;
        public Pose FeetPose => new Pose(transform.position, transform.rotation);

#if UNITY_EDITOR
        public bool CanPlace(out string reason)
        {
            if (!Cooked.Integration.SceneComponentLookup.IsFixedPoint(this, transform))
            { reason = "스폰 지점은 같은 씬의 고정 오브젝트여야 합니다."; return false; }
            return SpawnPlacement.Check(normalCapsule, FeetPose, gameObject.scene.GetPhysicsScene(),
                normalCapsule != null ? normalCapsule.transform : null, out reason);
        }
        private void OnDrawGizmos()
        {
            if (Application.isPlaying) return;
            bool valid = CanPlace(out string reason);
            Color previous = Gizmos.color;
            Gizmos.color = valid ? Color.blue : Color.red;
            Gizmos.DrawWireSphere(transform.position, .08f);
            Gizmos.DrawLine(transform.position, transform.position + transform.forward * .5f);
            try
            {
                SpawnPlacement.Geometry(normalCapsule, transform.rotation, out float radius, out float height);
                Vector3 bottom = transform.position + Vector3.up * radius, top = transform.position + Vector3.up * (height-radius);
                Gizmos.DrawWireSphere(bottom, radius); Gizmos.DrawWireSphere(top, radius);
                foreach (var direction in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                    Gizmos.DrawLine(bottom + direction * radius, top + direction * radius);
            }
            catch (InvalidOperationException) { /* Missing/invalid shape remains visibly red. */ }
            UnityEditor.Handles.Label(transform.position, valid ? "스폰 가능 (발바닥)" : "스폰 불가: " + reason);
            Gizmos.color = previous;
        }
#endif
    }
}
