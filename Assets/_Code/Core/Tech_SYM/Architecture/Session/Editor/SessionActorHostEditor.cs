using UnityEditor;
using UnityEngine;

namespace Cooked.Session.Editor
{
    [CustomEditor(typeof(SessionActorHost)), CanEditMultipleObjects]
    public sealed class SessionActorHostEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true)) Draw("m_Script");
            Draw("independentActor");
            var independent = serializedObject.FindProperty("independentActor");
            bool mixed = independent.hasMultipleDifferentValues;
            bool standalone = independent.objectReferenceValue != null;
            EditorGUILayout.HelpBox(mixed
                ? "다중 선택: 각 객체의 독립 플레이어 연결 여부에 따라 실행 모드가 결정됩니다."
                : standalone ? "독립 모드: 씬의 기존 플레이어를 사용합니다."
                : "통합 모드: 플레이어 프리팹을 생성합니다. 독립 플레이어를 연결하면 독립 모드가 됩니다.", MessageType.Info);
            if (mixed || !standalone) Draw("actorPrefab");
            if (mixed || standalone)
            {
                Draw("independentUi");
                Draw("independentMemos");
                Draw("independentCamera");
            }
            Draw("horizontalKnockbackSpeed");
            Draw("upwardKnockbackSpeed");
            serializedObject.ApplyModifiedProperties();

            // 다중 선택에서도 첫 객체의 유효성으로 나머지 누락을 숨기지 않는다.
            foreach (var host in targets)
            {
                using (var properties = new SerializedObject(host))
                {
                    bool isIndependent = properties.FindProperty("independentActor").objectReferenceValue != null;
                    if (isIndependent)
                    {
                        if (properties.FindProperty("independentUi").objectReferenceValue == null ||
                            properties.FindProperty("independentCamera").objectReferenceValue == null)
                            EditorGUILayout.HelpBox($"{host.name}: 독립 안내 담당과 독립 출력 카메라를 연결하세요.", MessageType.Error);
                    }
                    else
                    {
                        var prefab = properties.FindProperty("actorPrefab").objectReferenceValue as GameObject;
                        if (prefab == null)
                            EditorGUILayout.HelpBox($"{host.name}: 플레이어 프리팹을 연결하세요.", MessageType.Error);
                        else if (prefab.activeSelf)
                            EditorGUILayout.HelpBox($"{host.name}: 플레이어 프리팹은 비활성 상태로 저장되어야 합니다.", MessageType.Error);
                    }
                }
            }
        }

        private void Draw(string field) => EditorGUILayout.PropertyField(serializedObject.FindProperty(field), true);
    }
}
