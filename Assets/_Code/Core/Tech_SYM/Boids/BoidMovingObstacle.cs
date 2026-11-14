using UnityEngine;
using Cooked.Authoring;

namespace Cooked.Boids
{
    [DefaultExecutionOrder(-200)]
    public sealed class BoidMovingObstacle : MonoBehaviour
    {
        [SerializeField, InspectorLabel("이동 몸체"), Tooltip("회전하지 않고 직선 이동하는 BoxCollider를 연결하세요.")]
        BoxCollider body;
        [SerializeField, InspectorLabel("출발점"), Tooltip("몸체 밖에 고정된 출발 Transform입니다.")]
        Transform start;
        [SerializeField, InspectorLabel("도착점"), Tooltip("몸체 밖에 고정된 도착 Transform입니다.")]
        Transform end;
        [SerializeField, InspectorLabel("이동 속력"), Tooltip("전방 접근 시험 구조물의 속력입니다(m/s)."), Min(.1f)]
        float speed = 2;
        public BoxCollider Body => body;
        public Vector3 Velocity { get; private set; }

        public Vector3 LastDisplacement { get; private set; }

        public float StopTime => isActiveAndEnabled
            && body
            && end ? Vector3.Distance(body.transform.position,
             end.position) / Mathf.Max(.001f,
             speed) : 0;
        public Vector3 PredictedOffset(float time)
        {
            if (!isActiveAndEnabled || !body || !end)
                return Vector3.zero;
            return Vector3.ClampMagnitude(end.position - body.transform.position, Mathf.Max(0, time) * speed);
        }

        public void Configure(BoxCollider b, Transform a, Transform z, float value)
        {
            body = b;
            start = a;
            end = z;
            speed = value;
            ResetMotion();
        }

        public void ResetMotion()
        {
            if (body && start)
            {
                body.transform.position = start.position;
                LastDisplacement = Vector3.zero;
                Velocity = end ? (end.position - start.position).normalized * speed : Vector3.zero;
            }
        }

        void OnDisable()
        {
            Velocity = Vector3.zero;
            LastDisplacement = Vector3.zero;
        }

        void FixedUpdate()
        {
            if (!body || !start || !end)
                return;
            Vector3 delta = end.position - body.transform.position;
            LastDisplacement = Vector3.ClampMagnitude(delta, speed * Time.fixedDeltaTime);
            body.transform.position += LastDisplacement;
            delta = end.position - body.transform.position;
            Velocity = delta.sqrMagnitude > .000001f ? delta.normalized * speed : Vector3.zero;
        }
    }
}
