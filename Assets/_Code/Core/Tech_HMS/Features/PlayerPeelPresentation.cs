using System.Collections.Generic;
using Cooked.Authoring;
using UnityEngine;

namespace Cooked.HMS.AnimationIntegration
{
    /// <summary>형태 전환 껍질의 외형만 재생합니다. 실제 껍질 생성·회수·물리는 기존 Form과 Debris가 소유합니다.</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(PlayerFormController)), DefaultExecutionOrder(300)]
    public sealed class PlayerPeelPresentation : MonoBehaviour
    {
        [SerializeField, InspectorLabel("기본 외형 기준"), Tooltip("기본 형태의 Animator Transform을 연결하세요. 축소 껍질은 생성 순간의 자세를 가져오고, 확대 껍질은 이 지점의 이동·회전·배율을 따릅니다.")]
        private Transform normalVisualAnchor;
        [SerializeField, InspectorLabel("벗겨지는 껍질 외형"), Tooltip("분리된 축소 껍질 FBX를 연결하세요. 생성 후 PC가 아닌 기존 껍질 물리 오브젝트를 따릅니다.")]
        private GameObject shrinkVisual;
        [SerializeField, InspectorLabel("복귀 껍질 외형"), Tooltip("분리된 복귀 껍질 FBX를 연결하세요. 확대 동작 중 PC의 기본 외형을 따라가며 재생합니다.")]
        private GameObject restoreVisual;
        [SerializeField, InspectorLabel("축소 껍질 재생"), Tooltip("분리된 축소 껍질 클립이 들어 있는 Controller를 연결하세요.")]
        private RuntimeAnimatorController shrinkController;
        [SerializeField, InspectorLabel("복귀 껍질 재생"), Tooltip("분리된 복귀 껍질 클립이 들어 있는 Controller를 연결하세요.")]
        private RuntimeAnimatorController restoreController;
        [SerializeField, Min(.01f), InspectorLabel("복귀 효과 수명"), Tooltip("복귀 껍질 외형을 제거할 시간입니다(초). 복귀 클립 길이에 맞춥니다.")]
        private float restoreSeconds = 1f;
        [SerializeField, InspectorLabel("껍질 재질"), Tooltip("기존 셸의 재질을 연결하세요. 분리 FBX의 임포트 재질 대신 이 재질을 외형에 사용합니다.")]
        private Material peelMaterial;
        [SerializeField, Min(0f), InspectorLabel("껍질 착지 보정 대기"), Tooltip("축소 껍질 생성 후 외형을 충돌체 바닥 높이에 맞추기 시작할 시간입니다(초). 초반의 벗겨지는 동작은 유지합니다.")]
        private float shellGroundingDelay = .35f;
        [SerializeField, Min(.01f), InspectorLabel("껍질 착지 보정 시간"), Tooltip("각 껍질 조각의 외형 바닥을 물리 껍질의 충돌체 바닥에 부드럽게 맞추는 시간입니다(초). 충돌체와 실제 물리 위치는 바꾸지 않습니다.")]
        private float shellGroundingDuration = .3f;
        [Header("벗겨지는 껍질 물리 연출")]
        [SerializeField, InspectorLabel("조각 물리 사용")]
        private bool usePhysicsPieces = true;
        [SerializeField, Min(0f), InspectorLabel("물리 전환 시점"), Tooltip("벗겨지는 애니메이션 시작 후 물리로 전환할 시간입니다(초). 물리 사용 시 기존 Y축 착지 보정은 적용하지 않습니다.")]
        private float physicsHandoffTime = .35f;
        [SerializeField, Min(0f), InspectorLabel("조각 벌어지는 속도")]
        private float pieceOutwardSpeed = .7f;
        [SerializeField, Min(0f), InspectorLabel("조각 위쪽 속도")]
        private float pieceUpwardSpeed = .5f;
        [SerializeField, Min(0f), InspectorLabel("조각 회전 속도"), Tooltip("무작위 축으로 회전할 각속력입니다(rad/s).")]
        private float pieceSpinSpeed = 5f;
        [SerializeField, Min(.01f), InspectorLabel("조각 수명")]
        private float pieceLifetime = 2f;
        [SerializeField, Min(0f), InspectorLabel("조각 소멸 시간"), Tooltip("수명 마지막에 작아지며 사라지는 시간입니다(초).")]
        private float pieceShrinkSeconds = .35f;
        [SerializeField, InspectorLabel("조각 지형 충돌 레이어"), Tooltip("지형과 충돌할 레이어입니다. 캐릭터·실제 껍질·Trigger·동적 Rigidbody는 별도로 제외합니다.")]
        private LayerMask pieceCollisionLayers = Physics.DefaultRaycastLayers;
        private PlayerFormController form;
        private GameObject shellVisual;
        private Collider shellCollider;
        private float shellCreatedAt;
        private readonly List<ShellPiece> shellPieces = new List<ShellPiece>();
        private readonly List<GameObject> restoreEffects = new List<GameObject>();
        private readonly List<PlayerPeelPhysicsPiece> physicsPieces = new List<PlayerPeelPhysicsPiece>();
        private sealed class ShellPiece
        {
            public MeshRenderer Renderer;
            public Vector3 OriginalLocalPosition;
        }
        private void Awake() => form = GetComponent<PlayerFormController>();
        private void OnEnable() => form.FormChanged += OnFormChanged;
        private void OnFormChanged(PlayerForm value)
        {
            if (value == PlayerForm.Small)
            {
                ReleaseShellVisual();
                var shell = form.OwnedDebris;
                if (!shell || !shrinkVisual || !normalVisualAnchor) return;
                shellVisual = CreateVisual(shrinkVisual, shrinkController, shell.transform);
                shellVisual.transform.localPosition = new Vector3(0f, -.09f, 0f);
                shellCollider = shell.GetComponent<Collider>();
                shellCreatedAt = Time.time;
                // 분리 FBX의 메시 노드에는 위치 키가 없어 부모 Dummy의 애니메이션을 유지하며 보정할 수 있습니다.
                foreach (var renderer in shellVisual.GetComponentsInChildren<MeshRenderer>(true))
                    shellPieces.Add(new ShellPiece { Renderer = renderer, OriginalLocalPosition = renderer.transform.localPosition });
            }
            else
            {
                ReleaseShellVisual();
                if (!restoreVisual || !normalVisualAnchor) return;
                restoreEffects.RemoveAll(effect => !effect);
                var effect = CreateVisual(restoreVisual, restoreController, normalVisualAnchor);
                restoreEffects.Add(effect);
                Destroy(effect, restoreSeconds);
            }
        }
        private void LateUpdate()
        {
            if (!shellVisual || !shellCollider) return;
            if (usePhysicsPieces)
            {
                if (Time.time - shellCreatedAt >= physicsHandoffTime) CreatePhysicsPieces();
                return;
            }
            float weight = Mathf.Clamp01((Time.time - shellCreatedAt - shellGroundingDelay) / Mathf.Max(.01f, shellGroundingDuration));
            weight = weight * weight * (3f - 2f * weight);
            float floorY = shellCollider.bounds.min.y;
            foreach (var piece in shellPieces)
            {
                if (!piece.Renderer) continue;
                var target = piece.Renderer.transform;
                // 이전 프레임의 보정을 제거하고 현재 부모 Dummy의 자세에서 매번 계산합니다.
                target.localPosition = piece.OriginalLocalPosition;
                if (weight <= 0f) continue;
                Vector3 scale = target.parent.lossyScale;
                // 소멸 프레임의 0 배율에서는 월드 위치를 로컬 위치로 역변환하지 않습니다.
                if (Mathf.Abs(scale.x) < .00001f || Mathf.Abs(scale.y) < .00001f || Mathf.Abs(scale.z) < .00001f) continue;
                float offsetY = floorY - piece.Renderer.bounds.min.y;
                target.position += Vector3.up * (offsetY * weight);
            }
        }
        private void CreatePhysicsPieces()
        {
            var body = shellCollider.attachedRigidbody;
            for (int i = 0; i < shellPieces.Count; i++)
            {
                var renderer = shellPieces[i].Renderer;
                if (!renderer || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                Vector3 outward = Vector3.ProjectOnPlane(renderer.bounds.center - shellCollider.bounds.center, Vector3.up);
                if (outward.sqrMagnitude < .0001f) outward = i % 2 == 0 ? Vector3.left : Vector3.right;
                Vector3 velocity = body ? body.GetPointVelocity(renderer.bounds.center) : Vector3.zero;
                velocity += outward.normalized * pieceOutwardSpeed + Vector3.up * pieceUpwardSpeed;
                var piece = PlayerPeelPhysicsPiece.Create(renderer, velocity, Random.onUnitSphere * pieceSpinSpeed,
                    pieceLifetime, pieceShrinkSeconds, pieceCollisionLayers);
                if (piece) physicsPieces.Add(piece);
            }
            shellVisual.SetActive(false);
            Destroy(shellVisual);
            shellVisual = null;
            shellCollider = null;
            shellPieces.Clear();
        }
        private GameObject CreateVisual(GameObject source, RuntimeAnimatorController controller, Transform parent)
        {
            var visual = Instantiate(source);
            visual.name = source.name + " (Peel Presentation)";
            visual.transform.SetPositionAndRotation(normalVisualAnchor.position, normalVisualAnchor.rotation);
            visual.transform.localScale = normalVisualAnchor.lossyScale;
            visual.transform.SetParent(parent, true);
            var animator = visual.GetComponent<Animator>();
            if (!animator) animator = visual.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (peelMaterial)
                foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
                {
                    var materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++) materials[i] = peelMaterial;
                    renderer.sharedMaterials = materials;
                }
            visual.SetActive(true);
            return visual;
        }
        private void ReleaseShellVisual()
        {
            foreach (var piece in physicsPieces) if (piece) { piece.gameObject.SetActive(false); Destroy(piece.gameObject); }
            physicsPieces.Clear();
            shellPieces.Clear();
            shellCollider = null;
            if (shellVisual) { shellVisual.SetActive(false); Destroy(shellVisual); }
            shellVisual = null;
        }
        private void OnDisable()
        {
            form.FormChanged -= OnFormChanged;
            ReleaseShellVisual();
            foreach (var effect in restoreEffects) if (effect) Destroy(effect);
            restoreEffects.Clear();
        }
    }
}
