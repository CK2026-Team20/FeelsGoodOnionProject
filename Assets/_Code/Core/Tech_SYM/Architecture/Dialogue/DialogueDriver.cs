using System;
using UnityEngine;
namespace Cooked.Dialogue
{
    /// <summary>Lifetime guard owned by InGameCore composition; DOTween owns delay updates. Service ownership stays with composition.</summary>
    public sealed class DialogueDriver : MonoBehaviour
    {
        private DialogueService service;
        public void Initialize(DialogueService dialogueService)
        {
            if (service != null && !ReferenceEquals(service, dialogueService)) throw new InvalidOperationException("DialogueDriver is already initialized.");
            service = dialogueService ?? throw new ArgumentNullException(nameof(dialogueService));
        }
        private void OnDisable() { service?.Cancel(); }
        public void Detach() { service?.Cancel(); service = null; }
    }
}
