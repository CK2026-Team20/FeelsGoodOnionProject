using System;
using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Enemies
{
    /// <summary>고정 앵커 사이의 월드 거리로 순찰하며 Body 위치만 작성합니다.</summary>
    public sealed class EnemyMotor
    {
        private readonly EnemyActor owner;
        private readonly Transform movingBody, patrolStart, patrolEnd;
        private readonly bool patrol;
        private Vector3 position;

        public EnemyMotor(EnemyActor owner, BoxCollider body, Transform patrolStart, Transform patrolEnd)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (body == null) throw new ArgumentNullException(nameof(body));
            this.owner = owner;
            movingBody = body.transform;
            this.patrolStart = patrolStart;
            this.patrolEnd = patrolEnd;
            patrol = owner.Definition.Kind == EnemyKind.Patrol;
            position = movingBody.position;
        }

        private static bool Finite(Vector3 value) =>
            float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        /// <summary>XYZ 지점의 유한값만 검사합니다. 같은 지점과 선분 밖 초기 위치도 허용합니다.</summary>
        public static void ValidatePatrol(Vector3 start, Vector3 end, Vector3 origin)
        {
            if (!Finite(start) || !Finite(end) || !Finite(origin) ||
                !Finite(end - start) || !Finite(end - origin) || !Finite(start - origin))
                throw new ArgumentException("순찰 시작점·끝점·몸체 위치와 지점 사이 거리가 유한해야 합니다.");
        }

        /// <summary>월드 거리만큼 첫 목표 End로 이동한 뒤 Start/End를 왕복합니다. 같은 앵커는 현재 위치에 정지합니다.</summary>
        public static Vector3 StepPatrol(Vector3 position, Vector3 start, Vector3 end, float distance, ref bool towardEnd)
        {
            ValidatePatrol(start, end, position);
            if (!float.IsFinite(distance) || distance < 0)
                throw new ArgumentOutOfRangeException(nameof(distance));
            if (distance == 0 || start.Equals(end)) return position;
            Vector3 target = towardEnd ? end : start;
            Vector3 next = Vector3.MoveTowards(position, target, distance);
            if (next.Equals(target)) towardEnd = !towardEnd;
            return next;
        }

        public void Move(float distance, ref bool towardEnd)
        {
            if (!patrol) return;
            if (movingBody == null) throw new InvalidOperationException("이동할 적 몸체가 없습니다.");
            owner.RequirePatrolPoint(patrolStart, "순찰 시작점");
            owner.RequirePatrolPoint(patrolEnd, "순찰 끝점");
            position = StepPatrol(movingBody.position, patrolStart.position, patrolEnd.position, distance, ref towardEnd);
        }

        public void ApplyPosition()
        {
            if (movingBody == null) throw new InvalidOperationException("이동할 적 몸체가 없습니다.");
            if (patrol) movingBody.position = position;
        }
    }
}
