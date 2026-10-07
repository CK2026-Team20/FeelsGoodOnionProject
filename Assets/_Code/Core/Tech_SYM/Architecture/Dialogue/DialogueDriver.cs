using System;
using UnityEngine;
namespace Cooked.Dialogue
{
    /// <summary>T9 removes native consumers; Core lifetime owner must call DialogueService.Cancel directly.</summary>
    [Obsolete("T9: transfer cancellation to Core lifetime owner and remove this retired component.")]
    public sealed class DialogueDriver : MonoBehaviour
    {
        public void Initialize(DialogueService dialogueService)
        {
            if (dialogueService == null) throw new ArgumentNullException(nameof(dialogueService));
            enabled = false;
        }
        public void Detach() { }
    }
}
