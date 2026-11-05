#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
namespace Cooked.DebugTesting
{
    /// <summary>Formats unsupported log characters without creating or modifying font assets.</summary>
    public sealed class DebugLogText
    {
        private readonly TMP_FontAsset Asset;
        private readonly Dictionary<uint,bool> supported = new Dictionary<uint,bool>();
        public DebugLogText(TMP_FontAsset font) { Asset = font != null ? font : throw new ArgumentNullException(nameof(font)); }
        public string ForDisplay(string text)
        {
            if (Asset == null) throw new ObjectDisposedException(nameof(DebugLogText));
            var result = new StringBuilder(text?.Length ?? 0);
            if (text == null) return string.Empty;
            for (int i = 0; i < text.Length; i++)
            {
                int length = char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]) ? 2 : 1;
                uint code = length == 2 ? (uint)char.ConvertToUtf32(text[i], text[i + 1]) : text[i];
                if (code == '\n' || code == '\r' || code == '\t') result.Append(text[i]);
                else
                {
                    if (!supported.TryGetValue(code, out bool available))
                    {
                        available = code <= char.MaxValue && Asset.HasCharacter((char)code, true, false);
                        supported.Add(code, available);
                    }
                    // TMP 누락 경고가 다시 로그에 들어오는 반복을 막고 원래 코드 포인트를 남깁니다.
                    if (available) result.Append(text, i, length);
                    else result.Append("[U+").Append(code.ToString("X4")).Append(']');
                }
                i += length - 1;
            }
            return result.ToString();
        }

    }
}
#endif
