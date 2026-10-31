#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace Cooked.DebugTesting
{
    /// <summary>로그 표시용 글리프와 임시 아틀라스 수명. 원본 로그와 폰트 자산은 변경하지 않습니다.</summary>
    public sealed class DebugLogFont : IDisposable
    {
        public TMP_FontAsset Asset { get; private set; }
        private readonly Dictionary<uint, bool> supported = new Dictionary<uint, bool>();

        public DebugLogFont()
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>("Assets/_Scenes/Tech_SYM/Architecture/UI/Fonts/NotoSansCJKkr-Regular.otf");
            if (source == null) throw new InvalidOperationException("로그용 Noto Sans CJK KR 원본 폰트가 없습니다.");
            Asset = TMP_FontAsset.CreateFontAsset(source, 48, 5, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            if (Asset == null) throw new InvalidOperationException("로그용 임시 폰트를 생성하지 못했습니다.");
            Asset.name = "Debug Log Font (Session)";
            Asset.hideFlags = HideFlags.HideAndDontSave;
            Asset.material.hideFlags = HideFlags.HideAndDontSave;
        }

        public string ForDisplay(string text)
        {
            if (Asset == null) throw new ObjectDisposedException(nameof(DebugLogFont));
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
                        available = Asset.HasCharacter((int)code) || Asset.TryAddCharacters(new[] { code }, out uint[] missing);
                        supported.Add(code, available);
                    }
                    // TMP 누락 경고가 다시 로그에 들어오는 반복을 막고 원래 코드 포인트를 남깁니다.
                    if (available) result.Append(text, i, length);
                    else result.Append("[U+").Append(code.ToString("X4")).Append(']');
                }
                i += length - 1;
            }
            foreach (var texture in Asset.atlasTextures) if (texture != null) texture.hideFlags = HideFlags.HideAndDontSave;
            return result.ToString();
        }

        public void Dispose()
        {
            if (Asset == null) return;
            foreach (var texture in Asset.atlasTextures) Release(texture);
            Release(Asset.material);
            Release(Asset);
            Asset = null;
            supported.Clear();
        }

        private static void Release(UnityEngine.Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
#endif
