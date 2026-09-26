using System;
using UnityEngine;
namespace FeelsGoodOnion.TechSYM.Interaction
{
    /// <summary>공용 말풍선의 현재 대상만 소유한다. View 참조를 가지지 않는다.</summary>
    public sealed class InteractionPromptViewModel
    {
        private InteractionPromptAnchor target;
        public event Action Changed;
        public bool IsVisible => target != null && target.CanInteract;
        public Vector3 WorldPosition => IsVisible ? target.WorldPosition : Vector3.zero;
        public void SetTarget(InteractionPromptAnchor value)
        {
            if (ReferenceEquals(target, value)) return;
            target = value;
            Changed?.Invoke();
        }
    }
}
