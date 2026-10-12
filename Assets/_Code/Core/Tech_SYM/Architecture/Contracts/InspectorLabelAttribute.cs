using System;
using UnityEngine;

namespace Cooked.Authoring
{
    /// <summary>SYM 필드의 표시 이름만 바꾼다. 직렬화 이름과 Tooltip, 값의 소유권은 그대로 유지한다.</summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public sealed class InspectorLabelAttribute : PropertyAttribute
    {
        public string Text { get; }

        // 컬렉션 전체에 적용하므로 배열 제목을 바꾸고 Element 0 등의 이름은 유지한다.
        public InspectorLabelAttribute(string text) : base(true)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("표시 이름이 필요합니다.", nameof(text));
            Text = text;
            order = -1000;
        }
    }
}
