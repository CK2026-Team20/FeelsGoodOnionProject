using System;
using Cooked.Authoring;
using UnityEngine;

namespace Cooked.Chase
{
    [CreateAssetMenu(menuName = "Cooked/Chase Definition")]
    public sealed class ChaseDefinition : ScriptableObject
    {
        [SerializeField, InspectorLabel("추격 속도"), Min(.01f)]
        [Tooltip("무리가 월드 +X 방향으로 이동하는 속도입니다(m/s, 0보다 큰 값).")]
        private float speed = 4f;

        public float Speed => float.IsFinite(speed) && speed > 0
            ? speed : throw new InvalidOperationException("추격 속도는 유한한 양수여야 합니다.");
    }
}
