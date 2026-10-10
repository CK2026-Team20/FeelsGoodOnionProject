using System;
using System.Collections.Generic;
using Cooked.Authoring;
using UnityEngine;
namespace Cooked.Contracts
{
    /// <summary>원본 JSON 자산과 그 안의 Code 선택. 테이블 행을 별도 자산에 복제하지 않는다.</summary>
    [Serializable]
    public sealed class DialogueSelection
    {
        [SerializeField, InspectorLabel("대화 표"), Tooltip("Code를 선택할 원본 JSON TextAsset를 연결하세요.")]
        private TextAsset table;
        [SerializeField, HideInInspector] private string code;
        public TextAsset Table => table;
        public string Code => code;
        public DialogueSelection() { }

        public DialogueSelection(TextAsset table, string code)
        {
            if (table == null) throw new ArgumentNullException(nameof(table));
            if (string.IsNullOrWhiteSpace(code) || code != code.Trim()) throw new ArgumentException("대화 Code 선택이 필요합니다.", nameof(code));
            this.table = table; this.code = code;
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
    public interface IDialogueService : IDisposable
    {
        bool IsActive { get; }
        bool TryStart(string dialogueCode);
        void Cancel();
        void ResetRound();
    }
}
