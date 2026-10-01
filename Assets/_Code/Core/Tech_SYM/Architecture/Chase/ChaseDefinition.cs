using UnityEngine;

namespace Cooked.Chase
{
    [CreateAssetMenu(menuName = "Cooked/Chase Definition")]
    public sealed class ChaseDefinition : ScriptableObject
    {
        [SerializeField, Min(.01f)] private float speed = 4f;
        [SerializeField, Min(0)] private float captureGap = .8f;
        public ChaseSettings CreateSettings() => new ChaseSettings(speed, captureGap);
    }
}
