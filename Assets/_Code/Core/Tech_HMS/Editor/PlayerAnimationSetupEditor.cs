using UnityEditor;
using UnityEngine;

/// <summary>선택한 PC에 애니메이션 출력 컴포넌트와 기본 설정을 연결하는 편집 도구입니다.</summary>
public static class PlayerAnimationSetupEditor
{
    internal const string DefaultSetPath = "Assets/_Code/Core/Tech_HMS/Animation/PlayerAnimations.asset";

    /// <summary>플레이어 루트 선택 후 메뉴에서 실행합니다. 변경 내용은 Undo로 되돌릴 수 있습니다.</summary>
    [MenuItem("Tools/Player/Setup Animation Output")]
    private static void SetupSelectedPlayer()
    {
        GameObject root = Selection.activeGameObject;
        PlayerAnimationSet settings = AssetDatabase.LoadAssetAtPath<PlayerAnimationSet>(DefaultSetPath);
        if (root == null || root.GetComponent<PlayerFacade>() == null || settings == null)
        {
            Debug.LogWarning("PlayerFacade가 있는 PC 루트를 선택하고 PlayerAnimations 설정 에셋을 확인해 주세요.");
            return;
        }
        PlayerAnimatorOutput output = root.GetComponent<PlayerAnimatorOutput>();
        if (output == null) output = Undo.AddComponent<PlayerAnimatorOutput>(root);
        SerializedObject serialized = new SerializedObject(output);
        SerializedProperty settingProperty = serialized.FindProperty("animationSet");
        // 사용자가 연결한 다른 설정은 유지합니다.
        if (settingProperty.objectReferenceValue == null)
        {
            settingProperty.objectReferenceValue = settings;
            serialized.ApplyModifiedProperties();
        }
        Selection.activeGameObject = root;
        Debug.Log("PC 애니메이션 출력을 연결했습니다. PlayerAnimations 에셋에 클립을 할당하고 씬 또는 프리팹을 저장해 주세요.", root);
    }
}

/// <summary>설정 에셋을 출력 컴포넌트에서 바로 열 수 있도록 합니다.</summary>
[CustomEditor(typeof(PlayerAnimatorOutput))]
public sealed class PlayerAnimatorOutputEditor : Editor
{
    /// <summary>기본 Inspector와 설정 에셋 선택 버튼을 표시합니다.</summary>
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        if (GUILayout.Button("Open Default Animation Set"))
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<PlayerAnimationSet>(PlayerAnimationSetupEditor.DefaultSetPath);
        }
        EditorGUILayout.HelpBox("PC 루트에서 Tools > Player > Setup Animation Output으로 초기 연결합니다. 현재 형태의 외형 아래 Animator가 필요합니다. 클립 미할당 상태에서는 일회성 표현을 건너뜁니다.", MessageType.Info);
    }
}
