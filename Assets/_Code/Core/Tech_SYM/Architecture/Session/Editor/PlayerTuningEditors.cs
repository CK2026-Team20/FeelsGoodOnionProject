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

    /// <summary>Editor-only synchronization through serialized authoring data and public movement APIs.</summary>
    public static class PlayerTuningSynchronization
    {
        public static string SpeedField(PlayerMovementMode mode) => mode == PlayerMovementMode.Side
            ? "sideMoveSpeed" : mode == PlayerMovementMode.Quarter ? "quarterMoveSpeed" : "backMoveSpeed";

        /// <summary>Read-only: scene runtime values in Play; authoritative startup sources otherwise.</summary>
        public static void ReadDisplayedValues(CharacterMovement movement, out float speed, out float jumpHeight)
        {
            if (movement == null) throw new System.ArgumentNullException(nameof(movement));
            using var values = new SerializedObject(movement);
            speed = values.FindProperty("moveSpeed").floatValue;
            jumpHeight = values.FindProperty("jumpHeight").floatValue;
            if (Application.isPlaying && !EditorUtility.IsPersistent(movement)) return;
            var mode = movement.GetComponent<PlayerMovementModeController>();
            if (mode != null)
            {
                using var source = new SerializedObject(mode);
                var initialMode = (PlayerMovementMode)source.FindProperty("initialMode").intValue;
                speed = source.FindProperty(SpeedField(initialMode)).floatValue;
            }
            var form = movement.GetComponent<PlayerFormController>();
            if (form != null)
            {
                using var source = new SerializedObject(form);
                jumpHeight = source.FindProperty("normalJumpHeight").floatValue;
            }
        }

        /// <summary>Explicit edit helper for automation; null means leave that setting untouched.</summary>
        public static void ApplyEditedValues(CharacterMovement movement, float? speed, float? jumpHeight)
        {
            if (movement == null) throw new System.ArgumentNullException(nameof(movement));
            int group = Undo.GetCurrentGroup();
            using var values = new SerializedObject(movement);
            if (speed.HasValue) values.FindProperty("moveSpeed").floatValue = speed.Value;
            if (jumpHeight.HasValue) values.FindProperty("jumpHeight").floatValue = jumpHeight.Value;
            values.ApplyModifiedProperties();
            SyncMovement(movement, speed.HasValue, jumpHeight.HasValue);
            Undo.CollapseUndoOperations(group);
        }

        public static void SyncMovement(CharacterMovement movement, bool speedChanged, bool jumpChanged)
        {
            if (movement == null) return;
            int group = Undo.GetCurrentGroup();
            using var values = new SerializedObject(movement);
            var mode = movement.GetComponent<PlayerMovementModeController>();
            var form = movement.GetComponent<PlayerFormController>();
            if (speedChanged && mode != null)
            {
                using var data = new SerializedObject(mode);
                string field = SpeedField(Application.isPlaying && !EditorUtility.IsPersistent(mode)
                    ? mode.CurrentMode : (PlayerMovementMode)data.FindProperty("initialMode").intValue);
                float value = values.FindProperty("moveSpeed").floatValue;
                value = float.IsFinite(value) ? Mathf.Max(0f, value) : 0f;
                // HMS owns Quarter <= Side; reducing Side may also reduce Quarter in OnValidate.
                if (field == "quarterMoveSpeed") value = Mathf.Min(value, data.FindProperty("sideMoveSpeed").floatValue);
                data.FindProperty(field).floatValue = value;
                data.ApplyModifiedProperties();
                data.Update(); // Read back HMS validation rather than displaying the requested value.
                Undo.RecordObject(movement, "이동속도 원본 동기화");
                movement.SetMoveSpeed(data.FindProperty(field).floatValue);
                EditorUtility.SetDirty(movement);
                PrefabUtility.RecordPrefabInstancePropertyModifications(movement);
            }
            if (jumpChanged && form != null)
            {
                using var data = new SerializedObject(form);
                string field = Application.isPlaying && !EditorUtility.IsPersistent(form) && form.IsSmall ? "smallJumpHeight" : "normalJumpHeight";
                float height = values.FindProperty("jumpHeight").floatValue;
                data.FindProperty(field).floatValue = float.IsFinite(height) ? Mathf.Max(0f, height) : 0f;
                data.ApplyModifiedProperties();
                Undo.RecordObject(movement, "점프 높이 원본 동기화");
                movement.SetJumpHeight(data.FindProperty(field).floatValue);
                EditorUtility.SetDirty(movement);
                PrefabUtility.RecordPrefabInstancePropertyModifications(movement);
            }
            Undo.CollapseUndoOperations(group);
        }

        public static void ApplyCurrentSpeed(PlayerMovementModeController mode)
        {
            if (mode == null || !PlayerTuningInspector.IsLive(mode) || !mode.IsInitialized) return;
            var movement = mode.GetComponent<CharacterMovement>();
            if (movement == null) return;
            using var data = new SerializedObject(mode);
            Undo.RecordObject(movement, "현재 모드 속도 반영");
            movement.SetMoveSpeed(data.FindProperty(SpeedField(mode.CurrentMode)).floatValue);
            PrefabUtility.RecordPrefabInstancePropertyModifications(movement);
        }

        public static void ApplyCurrentJump(PlayerFormController form)
        {
            if (form == null || !PlayerTuningInspector.IsLive(form) || !form.isActiveAndEnabled) return;
            var movement = form.GetComponent<CharacterMovement>();
            if (movement == null) return;
            using var data = new SerializedObject(form);
            Undo.RecordObject(movement, "현재 폼 점프 높이 반영");
            movement.SetJumpHeight(data.FindProperty(form.IsSmall ? "smallJumpHeight" : "normalJumpHeight").floatValue);
            PrefabUtility.RecordPrefabInstancePropertyModifications(movement);
        }
    }

    [CustomEditor(typeof(CharacterMovement)), CanEditMultipleObjects]
    public sealed class CharacterMovementEditor : UnityEditor.Editor
    {
        private bool DrawTuningProxy(bool speed)
        {
            float displayed = 0f;
            bool mixed = false;
            for (int i = 0; i < targets.Length; i++)
            {
                PlayerTuningSynchronization.ReadDisplayedValues((CharacterMovement)targets[i], out float move, out float jump);
                float value = speed ? move : jump;
                if (i == 0) displayed = value;
                else mixed |= !displayed.Equals(value);
            }
            bool previousMixed = EditorGUI.showMixedValue;
            EditorGUI.showMixedValue = mixed;
            try
            {
                EditorGUI.BeginChangeCheck();
                float edited = EditorGUILayout.FloatField(new GUIContent(
                    speed ? "현재/초기 모드 이동속도 (m/s)" : "현재/정상 폼 점프 높이 (m)",
                    "Play 전에는 시작 원본을, Play 중 씬 객체는 실제 적용값을 표시합니다. 편집할 때만 원본과 이동 컴포넌트를 함께 변경합니다."), displayed);
                bool changed = EditorGUI.EndChangeCheck();
                if (changed) serializedObject.FindProperty(speed ? "moveSpeed" : "jumpHeight").floatValue = edited;
                return changed;
            }
            finally { EditorGUI.showMixedValue = previousMixed; }
        }

        public override bool RequiresConstantRepaint() => Application.isPlaying;
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            PlayerTuningInspector.PersistenceNotice();
            void F(string n, string label, string help) => PlayerTuningInspector.Field(serializedObject, n, label, help);
            using (new EditorGUI.DisabledScope(true)) F("m_Script", "스크립트", "이동을 실행하는 컴포넌트입니다.");
            EditorGUILayout.HelpBox("속도 편집은 현재/초기 모드 원본에, 점프 편집은 현재/정상 폼 원본에 함께 반영됩니다. 기존 HMS 규칙에 따라 측면 속도를 낮추면 그보다 큰 쿼터 속도도 함께 낮아집니다. 쿼터 속도는 측면 이하이며 다른 폼은 유지합니다.", MessageType.Info);
            bool speedChanged = DrawTuningProxy(true);
            F("groundAcceleration", "지상 가속도 (m/s²)", "목표 수평 속도에 도달하는 빠르기를 조절합니다.");
            F("groundDeceleration", "지상 감속도 (m/s²)", "입력을 놓았을 때 멈추는 빠르기를 조절합니다.");
            F("airAcceleration", "공중 가속도 (m/s²)", "공중 수평 조작 반응을 조절합니다.");
            F("airDeceleration", "공중 감속도 (m/s²)", "공중에서 입력을 놓았을 때의 감속을 조절합니다.");
            F("allowHorizontalMovement", "X축 이동 허용", "이동 모드가 적용할 때 덮어쓰는 월드 X축 허용 상태입니다.");
            F("allowDepthMovement", "Z축 이동 허용", "통합 통로·쿼터에서는 허용하며 측면에서는 제한합니다.");
            bool jumpChanged = DrawTuningProxy(false);
            F("gravity", "중력 가속도 (m/s²)", "상승·낙하 속도 변화를 조절합니다. 실행 중 편집하면 다음 물리 스텝부터 사용합니다.");
            F("fallGravityMultiplier", "낙하 중력 배수", "하강 중 중력에 곱합니다. 1은 상승과 같은 중력입니다.");
            F("maximumFallSpeed", "최대 낙하속도 (m/s)", "하강 속도의 상한입니다.");
            F("maximumGroundAngle", "최대 접지 경사 (도)", "바닥으로 인정할 경사의 상한입니다.");
            F("groundStickSpeed", "접지 유지 속도 (m/s)", "접지 중 바닥을 따라가는 아래 방향 속도입니다.");
            F("externalDeceleration", "외부 수평속도 감속 (m/s²)", "외부 충격의 수평 속도가 줄어드는 빠르기입니다.");
            serializedObject.ApplyModifiedProperties();
            if (speedChanged || jumpChanged)
                foreach (CharacterMovement movement in targets) PlayerTuningSynchronization.SyncMovement(movement, speedChanged, jumpChanged);
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
            EditorGUI.BeginChangeCheck();
            F("sideMoveSpeed", "측면 속도 (m/s)", "월드 X축 최대 자체 속도입니다. 초기화 또는 다른 모드에서 진입할 때 적용합니다.");
            bool sideMoveSpeedChanged = EditorGUI.EndChangeCheck();
            EditorGUI.BeginChangeCheck();
            F("quarterMoveSpeed", "통로·쿼터 속도 (m/s)", "통합 Corridor와 Quarter 모두 사용하는 XZ 속도입니다. 측면 속도 이하로 자동 제한합니다.");
            bool quarterMoveSpeedChanged = EditorGUI.EndChangeCheck();
            EditorGUI.BeginChangeCheck();
            F("backMoveSpeed", "독립 백뷰 속도 (m/s)", "독립 Back/BackFixed의 Z축 속도입니다. 통합 통로에서는 사용하지 않습니다.");
            bool backMoveSpeedChanged = EditorGUI.EndChangeCheck();
            serializedObject.ApplyModifiedProperties();
            foreach (PlayerMovementModeController mode in targets)
            {
                string current = PlayerTuningSynchronization.SpeedField(mode.CurrentMode);
                if ((current == "sideMoveSpeed" && sideMoveSpeedChanged) ||
                    (current == "quarterMoveSpeed" && (quarterMoveSpeedChanged || sideMoveSpeedChanged)) ||
                    (current == "backMoveSpeed" && backMoveSpeedChanged))
                    PlayerTuningSynchronization.ApplyCurrentSpeed(mode);
            }
            serializedObject.Update(); // Display validated per-target values, including a secondary Quarter clamp.
            EditorGUILayout.HelpBox("현재 모드의 속도를 편집하면 Play 중 즉시 적용합니다. 이동축·현재 모드는 바꾸지 않습니다. 기존 HMS 규칙에 따라 측면 속도를 낮추면 그보다 큰 쿼터 속도도 함께 낮아집니다. 쿼터 속도는 측면 이하입니다.", MessageType.Info);
            if (targets.Length == 1)
            {
                var mode = (PlayerMovementModeController)target;
                EditorGUILayout.LabelField("현재 모드", mode.CurrentMode + (mode.IsInitialized ? " (초기화됨)" : " (시작 전)"));
                PlayerTuningInspector.Ratio(mode, serializedObject.FindProperty("sideMoveSpeed").floatValue, true, "측면 속도 / 정상 Capsule 높이 (초⁻¹)");
                PlayerTuningInspector.Runtime(mode);
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
            EditorGUI.BeginChangeCheck();
            F("normalJumpHeight", "정상 점프 목표 높이 (m)", "정상 폼 적용 때 이동 컴포넌트에 전달합니다. 실제 정점은 별도 측정합니다.");
            bool normalJumpHeightChanged = EditorGUI.EndChangeCheck();
            EditorGUI.BeginChangeCheck();
            F("smallJumpHeight", "소형 점프 목표 높이 (m)", "소형 폼 적용 때 전달합니다. 현재 점프의 속도는 바꾸지 않습니다.");
            bool smallJumpHeightChanged = EditorGUI.EndChangeCheck();
            F("debrisRecoveryDistance", "근접 회수 API 거리 (m)", "TryRecoverNearbyDebris에 적용하는 플레이어와 껍질 루트 사이의 월드 거리입니다. 통합 Q 정상 복귀는 이 거리로 제한하지 않습니다.");
            serializedObject.ApplyModifiedProperties();
            foreach (PlayerFormController form in targets)
                if (form.IsSmall ? smallJumpHeightChanged : normalJumpHeightChanged)
                    PlayerTuningSynchronization.ApplyCurrentJump(form);
            EditorGUILayout.HelpBox("현재 폼의 점프 높이는 Play 중 편집 즉시 다음 점프에 반영됩니다. 형상 수치는 다음 폼 적용 때 반영되며 폼·껍질·입력축을 전환하지 않습니다.", MessageType.Info);
            if (targets.Length == 1)
            {
                var form = (PlayerFormController)target;
                PlayerTuningInspector.Ratio(form, serializedObject.FindProperty("normalJumpHeight").floatValue, true, "정상 점프 / 정상 Capsule 높이");
                EditorGUILayout.LabelField("현재 폼", form.CurrentForm.ToString());
                PlayerTuningInspector.Runtime(form);
            }

        }
    }
}
