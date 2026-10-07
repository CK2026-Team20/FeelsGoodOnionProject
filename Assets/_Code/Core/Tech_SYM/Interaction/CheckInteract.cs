using System;
using UnityEngine;
namespace FeelsGoodOnion.TechSYM.Interaction
{
    /// <summary>T9 native consumer migration/deletion pending. F selection belongs to PlayerInteractionService.</summary>
    [Obsolete("T9: remove this retired component after transferring native references.")]
    public sealed class CheckInteract : MonoBehaviour
    {
        public InteractionPromptAnchor CurrentTarget => null;
        private void Awake() { enabled = false; }
    }
}
