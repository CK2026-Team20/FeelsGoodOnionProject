using System;
using System.Collections.Generic;
using Cooked.Authoring;
using UnityEngine;
namespace Cooked.Contracts
{
    /// <summary>Bootstrapper의 중앙 대화 테이블에서 선택한 키. 트리거는 JSON을 소유하지 않는다.</summary>
    [Serializable]
    public sealed class DialogueSelection
    {
        [SerializeField, HideInInspector] private string code;
        public string Code => code;
        public DialogueSelection() { }

        public DialogueSelection(string code)
        {
            if (string.IsNullOrWhiteSpace(code) || code != code.Trim()) throw new ArgumentException("대화 Code 선택이 필요합니다.", nameof(code));
            this.code = code;
        }
    }

    public sealed class DialogueRow
    {
        public DialogueRow(string dialogueCode, int sequence, string name, string context, float contextRevealDuration)
        {
            if (string.IsNullOrWhiteSpace(dialogueCode) || dialogueCode != dialogueCode.Trim()) throw new ArgumentException("Dialogue code without surrounding whitespace required.", nameof(dialogueCode));
            if (sequence < 1) throw new ArgumentOutOfRangeException(nameof(sequence));
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Name required.", nameof(name));
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
        IReadOnlyList<string> Codes { get; }
        void LoadJson(string json);
        bool TryGetRows(string dialogueCode, out IReadOnlyList<DialogueRow> rows);
        string RequireCode(string dialogueCode);
        string RequireSelection(DialogueSelection selection);
    }
}
