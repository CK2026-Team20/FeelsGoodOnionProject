using UnityEditor;
using UnityEngine;

namespace Cooked.Level.Editor
{
    [CustomEditor(typeof(StageTrigger)), CanEditMultipleObjects]
    public sealed class StageTriggerEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            var kind = serializedObject.FindProperty("kind");
            EditorGUILayout.PropertyField(kind);
            bool mixed = kind.hasMultipleDifferentValues;
            var request = (StageTriggerKind)kind.intValue;
            Draw("stage");
            if (mixed || request == StageTriggerKind.Checkpoint) Draw("checkpoint");
            if (mixed || request == StageTriggerKind.UnlockForm || request == StageTriggerKind.UnlockTear ||
                request == StageTriggerKind.Dialogue || request == StageTriggerKind.Escape) Draw("dialogue");
            if (mixed || request == StageTriggerKind.Fall)
            {
                Draw("independentHost");
                EditorGUILayout.HelpBox("데드존은 현재 플레이어와 그 플레이어의 소유 껍질을 각각 한 번 수락합니다. 독립 씬에서는 Host만 연결하세요.", MessageType.Info);
            }
            if (mixed || request != StageTriggerKind.Fall) Draw("once");
            serializedObject.ApplyModifiedProperties();
        }
        private void Draw(string field) => EditorGUILayout.PropertyField(serializedObject.FindProperty(field), true);
    }
}
