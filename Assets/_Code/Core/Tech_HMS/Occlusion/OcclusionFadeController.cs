using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace Cooked.Occlusion
{
    [DisallowMultipleComponent, RequireComponent(typeof(Camera))]
    public sealed class OcclusionFadeController : MonoBehaviour
    {
        [SerializeField, InspectorName("플레이어 루트"), Tooltip("Normal·Small Visual을 모두 포함하는 플레이어 루트를 연결합니다. 비어 있으면 Player 태그로 간격을 두고 찾습니다.")]
        Transform playerRoot;
        [SerializeField, InspectorName("검사 레이어"), Tooltip("가림 후보 Collider가 있는 레이어를 선택합니다. OcclusionGroup이 없는 대상은 Fade하지 않습니다.")]
        LayerMask occluderLayers = ~0;
        [SerializeField, InspectorName("Trigger 포함"), Tooltip("가림 전용 Trigger Collider도 검사할지 선택합니다.")]
        bool includeTriggers;
        [SerializeField, Range(0.01f, 1f), InspectorName("가림 불투명도"), Tooltip("가리는 그룹의 목표 불투명도입니다. 1이면 불투명 상태를 유지합니다.")]
        float occludedAlpha = 0.3f;
        [SerializeField, Min(0f), InspectorName("투명화 시간"), Tooltip("불투명 상태에서 목표 불투명도까지 변하는 시간입니다. 초 단위이며 0이면 즉시 전환합니다.")]
        float fadeSeconds = 0.2f;
        [SerializeField, Min(0f), InspectorName("복귀 시간"), Tooltip("목표 불투명도에서 원래 모습으로 복귀하는 시간입니다. 초 단위이며 0이면 즉시 전환합니다.")]
        float restoreSeconds = 0.35f;
        [SerializeField, Min(0f), InspectorName("복귀 대기"), Tooltip("가림이 풀린 뒤 복귀를 시작하기까지 기다리는 시간입니다. 초 단위입니다.")]
        float restoreDelay = 0.12f;
        [SerializeField, Min(0f), InspectorName("검사 반경"), Tooltip("화면 검사선 주변을 검사하는 구체 반경입니다. 월드 미터 단위이며 0이면 Raycast입니다. 클수록 발판 오검출이 늘어납니다.")]
        float castRadius = 0f;
        [SerializeField, Min(0f), InspectorName("끝점 여유"), Tooltip("PC 검사점 직전에서 검사를 멈추는 거리입니다. 월드 미터 단위이며 발밑 접촉 오검출을 줄입니다.")]
        float endpointInset = 0.03f;
        [SerializeField, InspectorName("추가 검사 기준점"), Tooltip("선택적으로 실제 Transform을 연결합니다. 비어 있으면 활성 Renderer Bounds에서 머리·몸통 등 다섯 점을 계산합니다.")]
        Transform[] sampleAnchors = Array.Empty<Transform>();
        [SerializeField, InspectorName("윤곽선 보정 셰이더"), Tooltip("Tools/Cooked Occlusion 메뉴에서 생성한 셰이더를 연결합니다. 비어 있으면 기본 셰이더를 사용하지만 알파 0.5 아래에서 기본 윤곽선이 사라질 수 있습니다.")]
        Shader fadeShader;
        [SerializeField, InspectorName("가림 검사 기즈모"), Tooltip("Scene 뷰의 Gizmos가 켜져 있을 때 실제 검사선과 구체 반경을 초록색으로 표시합니다. 청록색은 PC Bounds와 검사점이며 빨강은 Play 중 충돌한 지점입니다. Play 전에도 검사 범위를 미리 볼 수 있습니다.")]
        bool showDebug = true;
        readonly HashSet<OcclusionGroup> detected = new HashSet<OcclusionGroup>();
        readonly Dictionary<OcclusionGroup, OcclusionFadeState> states = new Dictionary<OcclusionGroup, OcclusionFadeState>();
        readonly List<OcclusionGroup> remove = new List<OcclusionGroup>();
        readonly List<Renderer> playerRenderers = new List<Renderer>();
        readonly List<Renderer> previewRenderers = new List<Renderer>();
        readonly List<Vector3> samplePoints = new List<Vector3>();
        readonly List<Vector3> debugHits = new List<Vector3>();
        RaycastHit[] hits = new RaycastHit[32];
        Collider[] overlaps = new Collider[32];
        Camera viewCamera;
        Transform cachedPlayer;
        float nextRefresh;
        bool bufferWarned;
        void Reset() => fadeShader = Shader.Find(RealToonFadeMaterials.FadeShader);
        void OnEnable()
        {
            if (!fadeShader) fadeShader = Shader.Find(RealToonFadeMaterials.FadeShader);
            if (!fadeShader) Debug.LogWarning("윤곽선 보정 셰이더가 없습니다. 설치 메뉴에서 생성하고 연결하세요.", this);
            viewCamera = GetComponent<Camera>();
            RenderPipelineManager.beginCameraRendering += OnCamera;
            nextRefresh = 0;
        }
        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnCamera;
            foreach (var pair in states) if (pair.Key) pair.Key.Release(this);
            states.Clear(); detected.Clear(); playerRenderers.Clear(); debugHits.Clear();
        }
        void OnCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera != viewCamera || camera.cameraType != CameraType.Game || !Application.isPlaying) return;
            if (Time.unscaledTime >= nextRefresh || cachedPlayer != playerRoot)
            {
                nextRefresh = Time.unscaledTime + 0.5f;
                if (!playerRoot)
                {
                    var player = GameObject.FindGameObjectWithTag("Player");
                    if (player) playerRoot = player.transform;
                }
                cachedPlayer = playerRoot; playerRenderers.Clear();
                if (playerRoot) playerRoot.GetComponentsInChildren(true, playerRenderers);
            }
            detected.Clear();
            debugHits.Clear();
            if (playerRoot)
            {
                CollectSamplePoints(playerRenderers, viewCamera);
                foreach (var point in samplePoints) CastTo(point);
            }
            foreach (var group in detected)
                if (group.AllowFade && group.Acquire(this, fadeShader) && !states.ContainsKey(group)) states.Add(group, new OcclusionFadeState());
            remove.Clear();
            foreach (var pair in states)
            {
                var group = pair.Key;
                if (!group || !group.AllowFade) { if (group) group.Release(this); remove.Add(group); continue; }
                pair.Value.Tick(detected.Contains(group), Time.deltaTime, occludedAlpha, fadeSeconds, restoreSeconds, restoreDelay);
                group.SetAlpha(pair.Value.Alpha);
                if (!detected.Contains(group) && pair.Value.Alpha >= 1f) { group.Release(this); remove.Add(group); }
            }
            foreach (var group in remove) states.Remove(group);
        }
        bool TryBounds(List<Renderer> renderers, Camera camera, out Bounds bounds)
        {
            bounds = default; bool found = false;
            foreach (var renderer in renderers)
            {
                if (!renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
                if ((camera.cullingMask & (1 << renderer.gameObject.layer)) == 0) continue;
                if (!found) { bounds = renderer.bounds; found = true; } else bounds.Encapsulate(renderer.bounds);
            }
            return found;
        }
        void CollectSamplePoints(List<Renderer> renderers, Camera camera)
        {
            samplePoints.Clear();
            if (sampleAnchors != null)
                foreach (var anchor in sampleAnchors)
                    if (anchor && anchor.gameObject.activeInHierarchy) samplePoints.Add(anchor.position);
            if (samplePoints.Count > 0 || !TryBounds(renderers, camera, out var bounds)) return;
            var center = bounds.center; var extents = bounds.extents;
            samplePoints.Add(center);
            samplePoints.Add(center + Vector3.up * extents.y * 0.65f);
            samplePoints.Add(center - Vector3.up * extents.y * 0.45f);
            Vector3 right = camera.transform.right;
            float halfWidth = Vector3.Dot(new Vector3(Mathf.Abs(right.x), Mathf.Abs(right.y), Mathf.Abs(right.z)), extents);
            samplePoints.Add(center + right * halfWidth * 0.65f);
            samplePoints.Add(center - right * halfWidth * 0.65f);
        }
        bool TryBuildCast(Camera camera, Vector3 target, out Ray ray, out float distance)
        {
            ray = default; distance = 0f;
            Vector3 screen = camera.WorldToViewportPoint(target);
            if (screen.z <= camera.nearClipPlane) return false;
            ray = camera.ViewportPointToRay(screen); // Orthographic 카메라도 같은 화면 위치를 따라 검사합니다.
            distance = Vector3.Dot(target - ray.origin, ray.direction) - endpointInset;
            return distance > 0f;
        }
        void CastTo(Vector3 target)
        {
            if (!TryBuildCast(viewCamera, target, out var ray, out float distance)) return;
            var triggers = includeTriggers ? QueryTriggerInteraction.Collide : QueryTriggerInteraction.Ignore;
            int count;
            while (true)
            {
                count = castRadius > 0 ? Physics.SphereCastNonAlloc(ray, castRadius, hits, distance, occluderLayers, triggers)
                    : Physics.RaycastNonAlloc(ray, hits, distance, occluderLayers, triggers);
                if (count < hits.Length || hits.Length >= 4096) break;
                Array.Resize(ref hits, hits.Length * 2);
            }
            if (count == hits.Length) WarnBuffer();
            for (int i = 0; i < count; i++)
            {
                Register(hits[i].collider);
                if (showDebug) debugHits.Add(hits[i].point);
            }
            // 검사 시작점을 포함하는 Collider도 별도로 검사합니다.
            while (true)
            {
                count = Physics.OverlapSphereNonAlloc(ray.origin, Mathf.Max(0.001f, castRadius), overlaps, occluderLayers, triggers);
                if (count < overlaps.Length || overlaps.Length >= 4096) break;
                Array.Resize(ref overlaps, overlaps.Length * 2);
            }
            if (count == overlaps.Length) WarnBuffer();
            for (int i = 0; i < count; i++) Register(overlaps[i]);
        }
        void OnDrawGizmos()
        {
            if (!showDebug || !isActiveAndEnabled) return;
            var camera = viewCamera ? viewCamera : GetComponent<Camera>();
            if (!camera) return;
            // 미리보기는 씬의 직렬화 참조나 실제 Fade 상태를 변경하지 않습니다.
            var root = playerRoot;
            if (!root)
            {
                var player = GameObject.FindGameObjectWithTag("Player");
                if (player) root = player.transform;
            }
            previewRenderers.Clear();
            if (root) root.GetComponentsInChildren(true, previewRenderers);
            CollectSamplePoints(previewRenderers, camera);
            Color previousColor = Gizmos.color;
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.identity;
            if (TryBounds(previewRenderers, camera, out var bounds))
            {
                Gizmos.color = new Color(0f, 0.9f, 1f, 0.6f);
                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }
            foreach (var point in samplePoints)
            {
                if (!TryBuildCast(camera, point, out var ray, out float distance)) continue;
                Vector3 end = ray.GetPoint(distance);
                Gizmos.color = Color.green;
                Gizmos.DrawLine(ray.origin, end);
                if (castRadius > 0f) DrawCastVolume(ray, distance, castRadius);
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(point, 0.035f);
                Gizmos.color = Color.gray;
                Gizmos.DrawLine(end, point);
            }
            // SphereCast 시작점의 OverlapSphere 보완 검사도 같은 반경으로 표시됩니다.
            if (Application.isPlaying)
            {
                Gizmos.color = Color.red;
                foreach (var point in debugHits) Gizmos.DrawWireSphere(point, 0.045f);
            }
            Gizmos.color = previousColor;
            Gizmos.matrix = previousMatrix;
        }
        static void DrawCastVolume(Ray ray, float distance, float radius)
        {
            Vector3 end = ray.GetPoint(distance);
            Gizmos.DrawWireSphere(ray.origin, radius);
            Gizmos.DrawWireSphere(end, radius);
            Vector3 side = Vector3.Cross(ray.direction,
                Mathf.Abs(Vector3.Dot(ray.direction, Vector3.up)) > 0.99f ? Vector3.right : Vector3.up).normalized;
            Vector3 up = Vector3.Cross(side, ray.direction).normalized;
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI / 4f;
                Vector3 offset = (side * Mathf.Cos(angle) + up * Mathf.Sin(angle)) * radius;
                Gizmos.DrawLine(ray.origin + offset, end + offset);
            }
        }
        void Register(Collider collider)
        {
            if (!collider || collider.transform.IsChildOf(playerRoot)) return;
            var group = collider.GetComponentInParent<OcclusionGroup>();
            if (group && group.AllowFade) detected.Add(group);
        }
        void WarnBuffer()
        {
            if (bufferWarned) return; bufferWarned = true;
            Debug.LogWarning("가림 검사 결과가 4096개에 도달했습니다. 일부 대상을 놓칠 수 있으므로 검사 레이어를 좁히세요.", this);
        }
    }
}
