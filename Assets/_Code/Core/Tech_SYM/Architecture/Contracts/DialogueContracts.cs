using System;
using System.Collections.Generic;
namespace Cooked.Contracts
{
    public sealed class DialogueRow
    {
        public DialogueRow(string dialogueCode, int sequence, string name, string context, float contextRevealDuration)
        {
            if (string.IsNullOrWhiteSpace(dialogueCode)) throw new ArgumentException("Dialogue code required.", nameof(dialogueCode));
            if (sequence < 1) throw new ArgumentOutOfRangeException(nameof(sequence));
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (string.IsNullOrWhiteSpace(context)) throw new ArgumentException("Context required.", nameof(context));
            if (float.IsNaN(contextRevealDuration) || float.IsInfinity(contextRevealDuration) || contextRevealDuration < 0) throw new ArgumentOutOfRangeException(nameof(contextRevealDuration));
            Dialogue_Code = dialogueCode; Sequence = sequence; Name = name; Context = context; ContextRevealDuration = contextRevealDuration;
        }
        public string Dialogue_Code { get; }
        public int Sequence { get; }
        public string Name { get; }
        public string Context { get; }
        public float ContextRevealDuration { get; }
    }
    public interface IDialogueTableService
    {
        void LoadJson(string json);
        bool TryGetRows(string dialogueCode, out IReadOnlyList<DialogueRow> rows);
    }
    public interface IDialogueService : IDisposable
    {
        bool IsActive { get; }
        bool TryStart(string dialogueCode);
        void Cancel();
    }
}
