using UnityEditor;
using UnityEngine;

namespace Cooked.Session.Editor
{
    // 설정 원본은 HMS의 기존 직렬화 필드다. 이 Editor는 표시와 명시적 공개 API 명령만 담당한다.
    internal static class PlayerTuningInspector
    {
        public static void Field(SerializedObject data, string name, string label, string help)
        {
            var property = data.FindProperty(name);
            if (property == null) EditorGUILayout.HelpBox("필드 확인 필요: " + name, MessageType.Error);
            else EditorGUILayout.PropertyField(property, new GUIContent(label, help), true);
        }

        public static bool IsLive(Component component) => Application.isPlaying &&
            !EditorUtility.IsPersistent(component) && component.gameObject.activeInHierarchy;

        public static bool CapsuleHeight(Component component, bool normal, out float height)
        {
            height = 0;
            var capsule = component.GetComponent<CapsuleCollider>();
            var form = component.GetComponent<PlayerFormController>();
            // 정상 캐시는 공개 API가 없다. 소형 실행값을 정상 원본으로 추정하지 않는다.
            if (capsule == null || (normal && Application.isPlaying && !EditorUtility.IsPersistent(component) && form != null && form.IsSmall)) return false;
            var scale = capsule.transform.lossyScale;
            scale = new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            float axis = capsule.direction == 0 ? scale.x : capsule.direction == 1 ? scale.y : scale.z;
            float radial = capsule.direction == 0 ? Mathf.Max(scale.y, scale.z) : capsule.direction == 1 ? Mathf.Max(scale.x, scale.z) : Mathf.Max(scale.x, scale.y);
            height = Mathf.Max(capsule.height * axis, capsule.radius * radial * 2);
            return float.IsFinite(height) && height > 0;
        }

        public static void Ratio(Component component, float value, bool normal, string label)
        {
            EditorGUILayout.LabelField(label, CapsuleHeight(component, normal, out float height)
                ? $"{value / height:F2} (Capsule 높이 {height:F3}m 기준)"
                : "정상 높이 확인 불가 — 배수 계산 불가");
        }

        public static void Runtime(Component component)
        {
            if (!IsLive(component)) return;
            var movement = component.GetComponent<CharacterMovement>();
            if (movement == null) return;
            using (var current = new SerializedObject(movement))
            {
                EditorGUILayout.LabelField("현재 적용 속도 / 점프 설정", $"{current.FindProperty("moveSpeed").floatValue:F3} m/s / {current.FindProperty("jumpHeight").floatValue:F3} m");
            }
            EditorGUILayout.LabelField("자체 이동 계산 속도", $"{movement.SelfHorizontalSpeed:F3} m/s");
            var body = movement.GetComponent<Rigidbody>();
            if (body != null && body.isKinematic)
                EditorGUILayout.LabelField("물리 몸체 수평 속도", "Kinematic 보정 중 — 속도 지표 제외");
            else
            {
                var velocity = movement.Velocity;
                EditorGUILayout.LabelField("물리 몸체 수평 속도", $"{new Vector2(velocity.x, velocity.z).magnitude:F3} m/s (발판·충돌 포함)");
            }
            EditorGUILayout.HelpBox("점프 설정은 정지 지면 기준 목표입니다. 실제 정점은 물리 스텝·천장·발판 속도의 영향을 받습니다.", MessageType.Info);
        }

        public static void PersistenceNotice()
        {
            if (Application.isPlaying)
                EditorGUILayout.HelpBox("Play 중 씬 인스턴스의 설정·적용값 변경은 종료 시 사라집니다. Project의 prefab 자산을 직접 편집한 변경은 자산에 저장되어 종료 후에도 유지됩니다. 현재 선택 대상을 확인하세요.", MessageType.Info);
        }
    }

    [CustomEditor(typeof(CharacterMovement)), CanEditMultipleObjects]
    public sealed class CharacterMovementEditor : UnityEditor.Editor
    {
        public override bool RequiresConstantRepaint() => Application.isPlaying;
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            PlayerTuningInspector.PersistenceNotice();
            void F(string n, string label, string help) => PlayerTuningInspector.Field(serializedObject, n, label, help);
            using (new EditorGUI.DisabledScope(true)) F("m_Script", "스크립트", "이동을 실행하는 컴포넌트입니다.");
            EditorGUILayout.HelpBox("이동속도는 이동 모드 설정이, 점프 높이는 폼 설정이 시작·전환 때 덮어씁니다. 리밸런스는 해당 컴포넌트에서 편집하고 명시적 적용 버튼을 사용하세요.", MessageType.Info);
            F("moveSpeed", "현재/기본 이동속도 (m/s)", "실행 중에는 모드 설정이 적용된 값입니다. 모드 변경 시 다시 덮어씁니다.");
            F("groundAcceleration", "지상 가속도 (m/s²)", "목표 수평 속도에 도달하는 빠르기를 조절합니다.");
            F("groundDeceleration", "지상 감속도 (m/s²)", "입력을 놓았을 때 멈추는 빠르기를 조절합니다.");
            F("airAcceleration", "공중 가속도 (m/s²)", "공중 수평 조작 반응을 조절합니다.");
            F("airDeceleration", "공중 감속도 (m/s²)", "공중에서 입력을 놓았을 때의 감속을 조절합니다.");
            F("allowHorizontalMovement", "X축 이동 허용", "이동 모드가 적용할 때 덮어쓰는 월드 X축 허용 상태입니다.");
            F("allowDepthMovement", "Z축 이동 허용", "통합 통로·쿼터에서는 허용하며 측면에서는 제한합니다.");
            F("jumpHeight", "현재/기본 점프 높이 (m)", "폼 설정이 덮어씁니다. 다음 점프의 초기 상승 속도 계산에 사용합니다.");
            F("gravity", "중력 가속도 (m/s²)", "상승·낙하 속도 변화를 조절합니다. 실행 중 편집하면 다음 물리 스텝부터 사용합니다.");
            F("fallGravityMultiplier", "낙하 중력 배수", "하강 중 중력에 곱합니다. 1은 상승과 같은 중력입니다.");
            F("maximumFallSpeed", "최대 낙하속도 (m/s)", "하강 속도의 상한입니다.");
            F("maximumGroundAngle", "최대 접지 경사 (도)", "바닥으로 인정할 경사의 상한입니다.");
            F("groundStickSpeed", "접지 유지 속도 (m/s)", "접지 중 바닥을 따라가는 아래 방향 속도입니다.");
            F("externalDeceleration", "외부 수평속도 감속 (m/s²)", "외부 충격의 수평 속도가 줄어드는 빠르기입니다.");
            serializedObject.ApplyModifiedProperties();
            if (targets.Length == 1) PlayerTuningInspector.Runtime((CharacterMovement)target);
        }
    }

    [CustomEditor(typeof(PlayerMovementModeController)), CanEditMultipleObjects]
    public sealed class PlayerMovementModeControllerEditor : UnityEditor.Editor
    {
        public override bool RequiresConstantRepaint() => Application.isPlaying;
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            PlayerTuningInspector.PersistenceNotice();
            void F(string n, string label, string help) => PlayerTuningInspector.Field(serializedObject, n, label, help);
            using (new EditorGUI.DisabledScope(true)) F("m_Script", "스크립트", "이동축과 속도 설정을 소유합니다.");
            F("initialMode", "초기 이동 모드", "시작할 때 사용합니다. 통합 게임에서는 구간 시점이 이동 모드를 선택합니다.");
            F("sideMoveSpeed", "측면 속도 (m/s)", "월드 X축 최대 자체 속도입니다. 초기화 또는 다른 모드에서 진입할 때 적용합니다.");
            F("quarterMoveSpeed", "통로·쿼터 속도 (m/s)", "통합 Corridor와 Quarter 모두 사용하는 XZ 속도입니다. 측면 속도 이하로 자동 제한합니다.");
            F("backMoveSpeed", "독립 백뷰 속도 (m/s)", "독립 Back/BackFixed의 Z축 속도입니다. 통합 통로에서는 사용하지 않습니다.");
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("같은 모드 재요청은 속도를 재적용하지 않습니다. Play 중 즉시 반영하려면 아래 버튼을 누르세요. 이동축이나 현재 모드를 바꾸지 않습니다.", MessageType.Info);
            if (targets.Length == 1)
            {
                var mode = (PlayerMovementModeController)target;
                EditorGUILayout.LabelField("현재 모드", mode.CurrentMode + (mode.IsInitialized ? " (초기화됨)" : " (시작 전)"));
                PlayerTuningInspector.Ratio(mode, serializedObject.FindProperty("sideMoveSpeed").floatValue, true, "측면 속도 / 정상 Capsule 높이 (초⁻¹)");
                PlayerTuningInspector.Runtime(mode);
            }
            bool live = targets.Length > 0;
            foreach (PlayerMovementModeController mode in targets) live &= PlayerTuningInspector.IsLive(mode) && mode.IsInitialized && mode.GetComponent<CharacterMovement>() != null;
            using (new EditorGUI.DisabledScope(!live))
                if (GUILayout.Button("현재 플레이어에 속도 적용"))
                    foreach (PlayerMovementModeController mode in targets)
                    {
                        using var data = new SerializedObject(mode);
                        string field = mode.CurrentMode == PlayerMovementMode.Side ? "sideMoveSpeed" : mode.CurrentMode == PlayerMovementMode.Quarter ? "quarterMoveSpeed" : "backMoveSpeed";
                        var movement = mode.GetComponent<CharacterMovement>();
                        Undo.RecordObject(movement, "현재 플레이어 속도 적용");
                        movement.SetMoveSpeed(data.FindProperty(field).floatValue);
                    }
        }
    }

    [CustomEditor(typeof(PlayerFormController)), CanEditMultipleObjects]
    public sealed class PlayerFormControllerEditor : UnityEditor.Editor
    {
        public override bool RequiresConstantRepaint() => Application.isPlaying;
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            PlayerTuningInspector.PersistenceNotice();
            void F(string n, string label, string help) => PlayerTuningInspector.Field(serializedObject, n, label, help);
            using (new EditorGUI.DisabledScope(true)) F("m_Script", "스크립트", "폼별 형상과 점프 설정을 소유합니다.");
            F("visualRoot", "외형 루트", "NormalVisual·SmallVisual 자식이 있는 Transform을 연결합니다.");
            F("debrisPrefab", "껍질 프리팹", "PlayerDebris와 발판 운반 처리가 있는 활성 프리팹을 연결합니다.");
            F("smallRadius", "소형 반경 (로컬 m)", "소형 전환 때 적용합니다. 월드 크기는 몸체 배율의 영향을 받습니다.");
            F("smallHeight", "소형 전체 높이 (로컬 m)", "소형 전환 때 적용합니다. 지름 이상으로 제한합니다.");
            F("restoreCheckTolerance", "정상 복귀 겹침 허용 (m)", "정상 복귀 공간 검사에서 바닥의 미세 겹침을 허용합니다.");
            F("normalJumpHeight", "정상 점프 목표 높이 (m)", "정상 폼 적용 때 이동 컴포넌트에 전달합니다. 실제 정점은 별도 측정합니다.");
            F("smallJumpHeight", "소형 점프 목표 높이 (m)", "소형 폼 적용 때 전달합니다. 현재 점프의 속도는 바꾸지 않습니다.");
            F("debrisRecoveryDistance", "근접 회수 API 거리 (m)", "TryRecoverNearbyDebris에 적용하는 플레이어와 껍질 루트 사이의 월드 거리입니다. 통합 Q 정상 복귀는 이 거리로 제한하지 않습니다.");
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("정상 형상 원본은 초기 Capsule입니다. 형상 수치는 다음 폼 적용 때 반영됩니다. 아래 버튼은 현재 폼의 점프 설정만 적용하며 폼·껍질·형상을 전환하지 않습니다.", MessageType.Info);
            if (targets.Length == 1)
            {
                var form = (PlayerFormController)target;
                PlayerTuningInspector.Ratio(form, serializedObject.FindProperty("normalJumpHeight").floatValue, true, "정상 점프 / 정상 Capsule 높이");
                EditorGUILayout.LabelField("현재 폼", form.CurrentForm.ToString());
                PlayerTuningInspector.Runtime(form);
            }
            bool live = targets.Length > 0;
            foreach (PlayerFormController form in targets) live &= PlayerTuningInspector.IsLive(form) && form.isActiveAndEnabled && form.GetComponent<CharacterMovement>() != null;
            using (new EditorGUI.DisabledScope(!live))
                if (GUILayout.Button("현재 플레이어에 점프 설정 적용"))
                    foreach (PlayerFormController form in targets)
                    {
                        using var data = new SerializedObject(form);
                        var movement = form.GetComponent<CharacterMovement>();
                        Undo.RecordObject(movement, "현재 플레이어 점프 설정 적용");
                        movement.SetJumpHeight(data.FindProperty(form.IsSmall ? "smallJumpHeight" : "normalJumpHeight").floatValue);
                    }
        }
    }
}
