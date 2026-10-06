using System;
using UnityEngine;
namespace Cooked.Level
{
    /// <summary>Level-owned physical retreat barrier. Chase rules and start acceptance belong to Chase.</summary>
    public sealed class ChaseRetreatGate : MonoBehaviour
    {
        [Tooltip("추격 시작 뒤 뒤로 돌아가지 못하도록 활성화할 BoxCollider입니다. 플레이어와 겹치지 않는 위치의 차단 콜라이더를 연결하세요.")]
        [SerializeField] private BoxCollider blocker;
        public bool IsClosed { get; private set; }
        public Bounds ClosureBounds
        {
            get
            {
                if (blocker == null) throw new InvalidOperationException("Missing chase retreat blocker.");
                // Collider.bounds is empty while the initially-open blocker GameObject is inactive.
                var t = blocker.transform;
                Vector3 size = blocker.size;
                var x = t.TransformVector(new Vector3(size.x,0,0));
                var y = t.TransformVector(new Vector3(0,size.y,0));
                var z = t.TransformVector(new Vector3(0,0,size.z));
                return new Bounds(t.TransformPoint(blocker.center), Abs(x)+Abs(y)+Abs(z));
            }
        }
        public bool CanClose(PlayerFacade actor)
        {
            if (actor == null || blocker == null) return false;
            Bounds clearance = ClosureBounds;
            clearance.Expand(.1f);
            foreach (var collider in actor.GetComponentsInChildren<Collider>(true))
                if (collider.enabled && !collider.isTrigger && collider.gameObject.activeInHierarchy && clearance.Intersects(collider.bounds)) return false;
            return true;
        }
        // The StageService acceptance boundary calls this only after Chase.Start returned true.
        public bool TryClose(PlayerFacade actor)
        {
            if (IsClosed) return true;
            if (!CanClose(actor)) return false;
            blocker.gameObject.SetActive(true);
            IsClosed = true;
            return true;
        }
        private static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    }
}
