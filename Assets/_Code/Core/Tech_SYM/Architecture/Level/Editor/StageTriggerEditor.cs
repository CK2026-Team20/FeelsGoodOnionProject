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
            if (mixed || request == StageTriggerKind.Checkpoint)
            {
                EditorGUILayout.HelpBox("시작·끝 역할과 시점·음악은 StageService 구간에서 작성합니다. 이 위치에 Capsule 중심을 맞추며 최초 실제 접촉만 저장합니다.", MessageType.Info);
            }
            if (mixed || request == StageTriggerKind.UnlockForm || request == StageTriggerKind.UnlockTear ||
                request == StageTriggerKind.Dialogue || request == StageTriggerKind.Escape) Draw("dialogue");
            if (mixed || request == StageTriggerKind.Chase)
            {
                Draw("chaseDriver");
                Draw("chaseRetreatGate");
            }
            if (mixed || request == StageTriggerKind.Fall)
            {
                Draw("independentHost");
                EditorGUILayout.HelpBox("데드존은 현재 플레이어와 그 플레이어의 소유 껍질을 각각 한 번 수락합니다. 독립 씬에서는 Host만 연결하세요.", MessageType.Info);
            }
            if (mixed || (request != StageTriggerKind.Checkpoint && request != StageTriggerKind.Escape && request != StageTriggerKind.Fall)) Draw("once");
            serializedObject.ApplyModifiedProperties();
        }
        private void Draw(string field) => EditorGUILayout.PropertyField(serializedObject.FindProperty(field), true);
    }
}

namespace Cooked.Level.Editor
{
    [CustomPropertyDrawer(typeof(LevelCameraService.ViewSettings))]
    public sealed class CameraViewSettingsDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => (property.isExpanded ? 12 : 1) * (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing);
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            float step = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            position.height = EditorGUIUtility.singleLineHeight;
            property.isExpanded = EditorGUI.Foldout(position, property.isExpanded, label, true);
            if (property.isExpanded)
            {
                EditorGUI.indentLevel++;
                void Field(string name, string title, string tip)
                { position.y += step; EditorGUI.PropertyField(position, property.FindPropertyRelative(name), new GUIContent(title, tip)); }
                Field("distance", "피사체 중심 반경 (m)", "시선 중심과 카메라의 정착 거리입니다. 감쇠·허용 폭·블렌드 중에는 편차가 있습니다.");
                var angles = property.FindPropertyRelative("angles");
                foreach (var axis in new[] { ("x", "수직각 (도)"), ("y", "수평각 (도)"), ("z", "롤 (도)") })
                { position.y += step; EditorGUI.PropertyField(position, angles.FindPropertyRelative(axis.Item1), new GUIContent(axis.Item2, "월드 기준 피사체 중심 회전입니다. 위치도 함께 변합니다.")); }
                Field("focusOffset", "시선 중심 오프셋 (m)", "CameraAnchor에 더하는 월드 XYZ 값입니다. Actor 회전에 따라 회전하지 않습니다.");
                Field("screenPercent", "화면 위치 (%)", "X는 왼쪽 0~오른쪽 100, Y는 아래 0~위 100입니다.");
                Field("deadZone", "허용 폭과 높이", "추적 없이 허용할 화면 비율입니다. 이 범위 안에서는 정확한 반경을 유지하지 않습니다.");
                Field("damping", "추적 감쇠", "카메라 축별 추적 지연입니다. 0이면 즉시 추적합니다.");
                Field("fieldOfView", "수직 시야각 (도)", "원근 카메라의 수직 시야각입니다.");
                position.y += step; EditorGUI.LabelField(position, "Play 변경은 종료 시 되돌아갑니다.", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }
    }

    [CustomEditor(typeof(StageService))]
    public sealed class StageServiceEditor : UnityEditor.Editor
    {
        // Editor 인스턴스만 소유하는 임시 선택이다. Stage나 prefab에 저장하지 않는다.
        private CapsuleCollider previewCapsule;
        private Transform correctionOrigin, previewTrackingPoint;
        private float supportDistance;
        private float previewAspect = 16f / 9f;
        private StageAuthoringDiagnostics.Issue[] issues;
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox("구간 시작만 구도를 선택합니다. 끝은 저장만 하며 동일 시점은 블렌드를 재시작하지 않습니다. Play 중 구도 변경은 즉시 반영되지만 종료 시 되돌아갑니다.", MessageType.Info);
            EditorGUI.BeginChangeCheck();
            previewCapsule = (CapsuleCollider)EditorGUILayout.ObjectField(new GUIContent("검사할 플레이어 Capsule", "실제 PlayerFacade·CharacterMovement의 Capsule을 선택하세요. prefab 드래그도 가능합니다. 현재 선택된 폼 형상만 검사합니다."), previewCapsule, typeof(CapsuleCollider), true);
            correctionOrigin = (Transform)EditorGUILayout.ObjectField(new GUIContent("보정 시작 중심 (임시)", "이 Transform 위치를 접촉 순간 Capsule 중심으로 가정하여 각 구간 시작까지 직선 전체를 검사합니다. 모든 진입 방향의 보증이 아닙니다."), correctionOrigin, typeof(Transform), true);
            previewTrackingPoint = (Transform)EditorGUILayout.ObjectField(new GUIContent("카메라 추적점 (임시)", "Player CameraAnchor를 선택하세요. 없으면 구간 시작을 추적점으로 가정한 미리보기입니다."), previewTrackingPoint, typeof(Transform), true);
            supportDistance = EditorGUILayout.FloatField(new GUIContent("지지 탐색 거리 (m)", "목표 Capsule 아래에서 검사할 최대 간격을 직접 정하세요. 0은 지지면 검사불가입니다."), supportDistance);
            previewAspect = EditorGUILayout.FloatField(new GUIContent("미리보기 화면비", "너비/높이입니다. 실제 출력 화면비와 맞추세요."), previewAspect);
            if (EditorGUI.EndChangeCheck()) { issues = null; SceneView.RepaintAll(); }
            EditorGUILayout.HelpBox("청록: 시작·끝 / 노랑: 선택한 보정 직선 / 초록: 시선 중심 / 자홍: 카메라·광축. 정착 구도 미리보기이며 감쇠·블렌드·동적 접촉·백페이스는 실행 검증이 필요합니다. 검사 결과는 버튼을 누른 시점의 정적 배치입니다.", MessageType.Info);
            if (GUILayout.Button("현재 작성 상태 검사"))
                issues = StageAuthoringDiagnostics.Inspect((StageService)target, previewCapsule, correctionOrigin, supportDistance);
            if (issues == null) EditorGUILayout.HelpBox("미검증: 검사 버튼을 눌러 현재 배치를 확인하세요.", MessageType.Warning);
            else foreach (var issue in issues) EditorGUILayout.HelpBox(issue.Target + " — " + issue.Reason, issue.Severity);
        }
        private void OnSceneGUI()
        {
            var stage = (StageService)target;
            if (stage.Sections == null) return;
            var color = Handles.color;
            try
            {
                foreach (var section in stage.Sections)
                {
                    if (section?.Start == null || section.End == null) continue;
                    Vector3 start = section.Start.transform.position, end = section.End.transform.position;
                    Handles.color = Color.cyan; Handles.DrawLine(start, end); Handles.Label((start + end) * .5f, section.DisplayName);
                    if (correctionOrigin != null)
                    { Handles.color = Color.yellow; Handles.DrawDottedLine(correctionOrigin.position, start, 4); }
                    if (previewCapsule != null)
                    {
                        StageAuthoringDiagnostics.Geometry(previewCapsule, start, out var a, out var b, out float radius);
                        Handles.color = Color.yellow;
                        Handles.DrawWireDisc(a, Vector3.up, radius); Handles.DrawWireDisc(b, Vector3.up, radius);
                        Handles.DrawLine(a + Vector3.right * radius, b + Vector3.right * radius);
                        Handles.DrawLine(a - Vector3.right * radius, b - Vector3.right * radius);
                    }
                    var profile = stage.ViewFor(section.View);
                    if (!profile.IsValidForAspect(previewAspect)) continue;
                    Vector3 tracking = previewTrackingPoint != null ? previewTrackingPoint.position : start;
                    Vector3 focus = tracking + profile.focusOffset;
                    Vector3 camera = profile.PreviewPosition(tracking, previewAspect);
                    Quaternion rotation = Quaternion.Euler(profile.angles);
                    float size = HandleUtility.GetHandleSize(focus) * .06f;
                    Handles.color = Color.green; Handles.SphereHandleCap(0, focus, Quaternion.identity, size, EventType.Repaint);
                    Handles.color = Color.magenta; Handles.DrawLine(camera, focus);
                    Handles.ArrowHandleCap(0, camera, rotation, HandleUtility.GetHandleSize(camera) * .5f, EventType.Repaint);
                    Handles.Label(camera, section.DisplayName + " / " + section.View + " / 정착 구도");
                    // 화면 테두리와 롤도 같은 수학으로 보여 준다.
                    float depth = profile.Depth(previewAspect), h = depth * Mathf.Tan(profile.fieldOfView * .5f * Mathf.Deg2Rad);
                    Vector3[] corners = new Vector3[5];
                    for (int i = 0; i < 4; i++)
                    { corners[i] = camera + rotation * new Vector3((i == 0 || i == 3 ? -1 : 1) * h * previewAspect, (i < 2 ? -1 : 1) * h, depth); Handles.DrawLine(camera, corners[i]); }
                    corners[4] = corners[0]; Handles.DrawPolyLine(corners);
                }
            }
            finally { Handles.color = color; }
        }
    }

    /// <summary>정적 작성 진단. 선택 Capsule의 현재 형상과 명시한 한 진입 직선만 검사한다.</summary>
    public static class StageAuthoringDiagnostics
    {
        public sealed class Issue
        {
            public string Code, Target, Reason;
            public MessageType Severity;
        }
        public static void Geometry(CapsuleCollider capsule, Vector3 center, out Vector3 a, out Vector3 b, out float radius)
        {
            var s = capsule.transform.lossyScale;
            s = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            int axis = capsule.direction;
            radius = capsule.radius * Mathf.Max(s[(axis + 1) % 3], s[(axis + 2) % 3]);
            float half = Mathf.Max(0, capsule.height * s[axis] * .5f - radius);
            Vector3 direction = capsule.transform.rotation * (axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward);
            a = center + direction * half; b = center - direction * half;
        }
        public static Issue[] Inspect(StageService stage, CapsuleCollider source, Transform origin, float supportDistance)
        {
            var results = new System.Collections.Generic.List<Issue>();
            void Add(string code, string target, string reason, MessageType severity = MessageType.Error)
                => results.Add(new Issue { Code = code, Target = target, Reason = reason, Severity = severity });
            try { stage.ValidateSections(); }
            catch (System.Exception e) { Add("SECTION_INVALID", stage.name, e.Message); }
            var points = new System.Collections.Generic.List<StageTrigger>();
            foreach (var section in stage.Sections)
                if (section != null) foreach (var point in new[] { section.Start, section.End })
                    if (point != null && !points.Contains(point)) points.Add(point);
            Physics.SyncTransforms();
            for (int i = 0; i < points.Count; i++)
                for (int j = i + 1; j < points.Count; j++)
                {
                    var a = points[i].GetComponent<BoxCollider>(); var b = points[j].GetComponent<BoxCollider>();
                    if (a != null && b != null && Physics.ComputePenetration(a, a.transform.position, a.transform.rotation, b, b.transform.position, b.transform.rotation, out _, out float depth))
                        Add("SENSOR_OVERLAP", points[i].name + " / " + points[j].name, "체크포인트 센서가 겹칩니다. 침투 " + depth.ToString("F4") + "m (비활성 작성 센서 포함).");
                }
            var movement = source != null ? source.GetComponent<CharacterMovement>() : null;
            if (source == null || movement == null || source.GetComponent<PlayerFacade>() == null || source.isTrigger)
            { Add("CAPSULE_REQUIRED", stage.name, "검사불가/대상필요: 실제 플레이어 Capsule과 같은 몸체의 PlayerFacade·CharacterMovement를 선택하세요.", MessageType.Warning); return results.ToArray(); }
            if (origin == null) Add("PATH_REQUIRED", stage.name, "검사불가/대상필요: 접촉 순간의 Capsule 중심을 나타내는 보정 시작 Transform이 필요합니다.", MessageType.Warning);
            if (!float.IsFinite(supportDistance) || supportDistance <= 0)
                Add("SUPPORT_RANGE_REQUIRED", stage.name, "검사불가/대상필요: 유한한 양수 지지 탐색 거리를 입력하세요.", MessageType.Warning);
            // Collider 인스턴스를 만들지 않는다. prefab의 실제 치수/scale/회전으로 물리 Capsule 질의를 구성한다.
            // PlayerBridgeService.IsCorrectionPathSafe의 경로 여유와 동일하다. contactOffset은 물리 접촉 생성 거리이며 침투 허용치가 아니다.
            const float correctionSkin = .002f;
            bool Obstacle(Collider c) => c != null && c.enabled && c.gameObject.activeInHierarchy && !c.isTrigger && c != source &&
                c.gameObject.scene == stage.gameObject.scene &&
                (source.attachedRigidbody == null || c.attachedRigidbody != source.attachedRigidbody) &&
                !Physics.GetIgnoreLayerCollision(source.gameObject.layer, c.gameObject.layer) && !Physics.GetIgnoreCollision(source, c);
            foreach (var section in stage.Sections)
            {
                if (section?.Start == null) continue;
                string label = section.DisplayName + " / " + section.Start.name;
                Vector3 target = section.Start.transform.position;
                Geometry(source, target, out var a, out var b, out float radius);
                if (!float.IsFinite(radius) || radius <= correctionSkin)
                { Add("CAPSULE_INVALID", label, "검사불가: 실제 반경이 보정 경로 여유 0.002m보다 커야 합니다."); continue; }
                // prefab asset에는 PhysX shape가 없을 수 있으므로 전체 형상 후보를 보수적으로 표시한다.
                // 런타임은 이 전체 후보의 ComputePenetration 깊이 > 0.002m를 거절한다. 여기서는 얕은 접촉도 검토 대상으로 남긴다.
                foreach (var other in Physics.OverlapCapsule(a, b, radius, ~0, QueryTriggerInteraction.Ignore))
                    if (Obstacle(other)) Add("TARGET_BLOCKED", label, "보정 목표의 전체 Capsule이 " + other.name + "과 겹치는 후보입니다(보수적 진단). 런타임 침투 허용치 0.002m 이하 접촉도 작성 검토 대상으로 표시합니다.");
                if (origin != null)
                {
                    Vector3 delta = target - origin.position;
                    Geometry(source, origin.position, out var fromA, out var fromB, out _);
                    foreach (var other in Physics.OverlapCapsule(fromA, fromB, radius, ~0, QueryTriggerInteraction.Ignore))
                        if (Obstacle(other)) Add("ORIGIN_BLOCKED", label, "선택한 보정 시작 중심의 전체 Capsule이 " + other.name + "과 겹치는 후보입니다(얕은 접촉도 포함한 보수적 진단).");
                    if (delta.sqrMagnitude > correctionSkin * correctionSkin)
                        foreach (var hit in Physics.CapsuleCastAll(fromA, fromB, Mathf.Max(radius - correctionSkin, .0001f), delta.normalized, delta.magnitude, ~0, QueryTriggerInteraction.Ignore))
                            if (Obstacle(hit.collider)) Add("PATH_BLOCKED", label, "선택 시작점→목표 직선이 " + hit.collider.name + "에 막힙니다. 거리 " + hit.distance.ToString("F4") + "m.");
                }
                if (float.IsFinite(supportDistance) && supportDistance > 0)
                {
                    float tolerance = source.contactOffset; // 지지면 탐색의 접촉 여유이며 목표/경로 침투 판정에는 쓰지 않는다.
                    float supportRadius = radius - tolerance;
                    if (!float.IsFinite(supportRadius) || supportRadius <= 0)
                    {
                        Add("SUPPORT_CAPSULE_INVALID", label, "검사불가: 지지 탐색 반경(실제 반경 - contactOffset)이 유효한 양수여야 합니다. 지지면 질의를 생략합니다.", MessageType.Warning);
                        continue;
                    }
                    bool supported = false;
                    foreach (var hit in Physics.CapsuleCastAll(a + Vector3.up * tolerance, b + Vector3.up * tolerance, supportRadius, Vector3.down, supportDistance + tolerance * 2, ~0, QueryTriggerInteraction.Ignore))
                        if (Obstacle(hit.collider) && Vector3.Angle(hit.normal, Vector3.up) <= movement.MaximumGroundAngle) { supported = true; break; }
                    if (!supported) Add("SUPPORT_MISSING", label, "선택 Capsule 아래 " + supportDistance + "m 안에 접지각 " + movement.MaximumGroundAngle + "도 이하 지지면이 없습니다.");
                }
            }
            if (results.Count == 0) Add("STATIC_CLEAR", stage.name, "선택한 현재 Capsule 형상·목표·명시한 직선·지지 탐색 범위에서 문제를 찾지 못했습니다. 다른 폼·동적 물체·전체 진입 경로는 미검증입니다.", MessageType.Info);
            return results.ToArray();
        }
    }
}
