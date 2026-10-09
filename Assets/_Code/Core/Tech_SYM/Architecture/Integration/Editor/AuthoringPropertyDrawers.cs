using System;
using System.Linq;
using System.Reflection;
using Cooked.Authoring;
using Cooked.Contracts;
using Cooked.Dialogue;
using UnityEditor;
using UnityEngine;

namespace Cooked.Integration.Editor
{
    /// <summary>Attribute를 지정한 SYM 필드만 그린다. 전체 Inspector를 교체하지 않는다.</summary>
    [CustomPropertyDrawer(typeof(InspectorLabelAttribute))]
    public sealed class InspectorLabelDrawer : PropertyDrawer
    {
        private GUIContent Label(GUIContent original)
        {
            string tooltip = original.tooltip;
            if (string.IsNullOrEmpty(tooltip)) tooltip = fieldInfo?.GetCustomAttribute<TooltipAttribute>()?.tooltip;
            return new GUIContent(((InspectorLabelAttribute)attribute).Text, tooltip);
        }

        // Unity 6 PropertyHandler의 다음 속성/타입 Drawer에 연결한다. Range 등은 표준 표시를 사용한다.
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => EditorGUI.GetPropertyHeight(property, Label(label), true);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
            => EditorGUI.PropertyField(position, property, Label(label), true);
    }

    [CustomPropertyDrawer(typeof(SceneReference))]
    public sealed class SceneReferenceDrawer : PropertyDrawer
    {
        private static string ReadError(SerializedProperty property)
        {
            var asset = property.FindPropertyRelative("scene");
            if (asset.hasMultipleDifferentValues) return null;
            try { SceneCatalog.RequireScenePath(asset.objectReferenceValue as SceneAsset, property.propertyPath); return null; }
            catch (ArgumentException error) { return error.Message; }
            catch (InvalidOperationException error) { return error.Message; }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => EditorGUIUtility.singleLineHeight * (ReadError(property) == null ? 1 : 3) +
                (ReadError(property) == null ? 0 : EditorGUIUtility.standardVerticalSpacing);

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            try
            {
                var asset = property.FindPropertyRelative("scene");
                var row = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.BeginChangeCheck();
                EditorGUI.PropertyField(row, asset, label);
                if (EditorGUI.EndChangeCheck())
                {
                    var scene = asset.objectReferenceValue as SceneAsset;
                    property.FindPropertyRelative("runtimePath").stringValue = scene == null ? string.Empty : SceneCatalog.RequireScenePath(scene, property.propertyPath);
                }
                string error = ReadError(property);
                if (error != null)
                {
                    row.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
                    row.height = EditorGUIUtility.singleLineHeight * 2;
                    EditorGUI.HelpBox(row, error, MessageType.Error);
                }
            }
            finally { EditorGUI.EndProperty(); }
        }
    }

    // SYM 소유 계약 enum의 표시만 담당한다. SessionContracts의 번호/규칙은 바꾸지 않는다.
    [CustomPropertyDrawer(typeof(AbilityId))]
    public sealed class AbilityIdDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            bool mixed = EditorGUI.showMixedValue;
            try
            {
                EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
                int current = property.intValue == 0 ? 1 : property.intValue == 1 ? 2 : 0;
                EditorGUI.BeginChangeCheck();
                int selected = EditorGUI.Popup(position, label, current, new[] { new GUIContent("선택 필요"), new GUIContent("크기 전환"), new GUIContent("눈물") });
                if (EditorGUI.EndChangeCheck()) property.intValue = selected - 1;
            }
            finally { EditorGUI.showMixedValue = mixed; EditorGUI.EndProperty(); }
        }
    }

    [CustomPropertyDrawer(typeof(DialogueSelection))]
    public sealed class DialogueSelectionDrawer : PropertyDrawer
    {
        private static float Line => EditorGUIUtility.singleLineHeight;
        private static float Gap => EditorGUIUtility.standardVerticalSpacing;

        private static string[] ReadChoices(SerializedProperty property, out string error)
        {
            error = null;
            var asset = property.FindPropertyRelative("table");
            if (asset.hasMultipleDifferentValues) { error = "대화 표가 서로 다릅니다. 같은 JSON 자산을 먼저 연결하세요."; return Array.Empty<string>(); }
            var table = asset.objectReferenceValue as TextAsset;
            if (table == null) { error = property.displayName + " / 대화 표: JSON TextAsset를 연결하세요."; return Array.Empty<string>(); }
            try
            {
                var service = new DialogueTableService(table.name);
                service.LoadJson(table.text);
                var code = property.FindPropertyRelative("code");
                if (!code.hasMultipleDifferentValues)
                {
                    try { service.RequireCode(code.stringValue); }
                    catch (FormatException failure) { error = failure.Message; }
                }
                return service.Codes.ToArray();
            }
            catch (FormatException failure)
            {
                error = failure.Message;
                return Array.Empty<string>();
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            ReadChoices(property, out string error);
            return Line * 3 + Gap * 2 + (error == null ? 0 : Line * 3 + Gap);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            int previousIndent = EditorGUI.indentLevel;
            bool previousMixed = EditorGUI.showMixedValue;
            try
            {
                var row = new Rect(position.x, position.y, position.width, Line);
                EditorGUI.LabelField(row, label);
                EditorGUI.indentLevel++;
                row.y += Line + Gap;
                EditorGUI.PropertyField(row, property.FindPropertyRelative("table"), new GUIContent("대화 표", "Code를 선택할 원본 JSON TextAsset를 연결하세요."));
                string[] codes = ReadChoices(property, out string error);
                var selected = property.FindPropertyRelative("code");
                int index = Array.IndexOf(codes, selected.stringValue) + 1;
                string[] choices = new[] { index == 0 && !string.IsNullOrEmpty(selected.stringValue) ? "선택 필요 (" + selected.stringValue + ")" : "대화 선택" }.Concat(codes).ToArray();
                row.y += Line + Gap;
                using (new EditorGUI.DisabledScope(codes.Length == 0))
                {
                    EditorGUI.showMixedValue = selected.hasMultipleDifferentValues;
                    EditorGUI.BeginChangeCheck();
                    int next = EditorGUI.Popup(row, new GUIContent("대화", "연결한 JSON 안의 검증된 Code를 선택하세요."), index, choices.Select(s => new GUIContent(s)).ToArray());
                    if (EditorGUI.EndChangeCheck()) selected.stringValue = next == 0 ? string.Empty : codes[next - 1];
                }
                if (error != null)
                {
                    row.y += Line + Gap; row.height = Line * 3;
                    EditorGUI.HelpBox(row, error, MessageType.Error);
                }
            }
            finally
            {
                EditorGUI.showMixedValue = previousMixed;
                EditorGUI.indentLevel = previousIndent;
                EditorGUI.EndProperty();
            }
        }
    }
}
