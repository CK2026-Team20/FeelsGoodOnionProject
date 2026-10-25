using System.Collections.Generic;
using UnityEngine;
namespace Cooked.Occlusion
{
    [DisallowMultipleComponent]
    public sealed class OcclusionGroup : MonoBehaviour
    {
        [SerializeField, InspectorName("가림 처리 허용"), Tooltip("이 그룹의 Collider가 PC를 가리면 하위 Renderer들을 함께 반투명화합니다.")]
        bool allowFade = true;
        public bool AllowFade => allowFade && isActiveAndEnabled;
        public float CurrentAlpha { get; private set; } = 1f;
        readonly List<Renderer> renderers = new List<Renderer>();
        readonly List<Material> scratch = new List<Material>();
        readonly List<Slot> slots = new List<Slot>();
        OcclusionFadeController owner;
        bool initialized;
        sealed class Slot
        {
            public Renderer Renderer;
            public Material[] Original, Runtime;
            public readonly List<Material> Copies = new List<Material>();
        }
        public bool Acquire(OcclusionFadeController controller, Shader shader)
        {
            if (owner && owner != controller) return false;
            owner = controller;
            if (initialized) return slots.Count > 0;
            initialized = true;
            GetComponentsInChildren(true, renderers);
            foreach (var renderer in renderers)
            {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
                if (renderer.GetComponentInParent<OcclusionGroup>() != this) continue;
                renderer.GetSharedMaterials(scratch);
                if (scratch.Count == 0) continue;
                bool supported = !renderer.HasPropertyBlock();
                foreach (var material in scratch) supported &= RealToonFadeMaterials.Supported(material);
                if (!supported)
                {
                    Debug.LogWarning($"[{name}] {renderer.name}: Opaque RealToon Default이며 MaterialPropertyBlock이 없는 Renderer만 Fade합니다. Alpha Clip/기존 Transparent/다른 셰이더는 유지합니다.", renderer);
                    continue;
                }
                var slot = new Slot { Renderer = renderer, Original = scratch.ToArray(), Runtime = new Material[scratch.Count] };
                for (int i = 0; i < scratch.Count; i++)
                {
                    var copy = RealToonFadeMaterials.Create(scratch[i], shader);
                    slot.Runtime[i] = copy; slot.Copies.Add(copy);
                }
                slots.Add(slot);
            }
            return slots.Count > 0;
        }
        public void SetAlpha(float alpha)
        {
            CurrentAlpha = Mathf.Clamp01(alpha);
            foreach (var slot in slots)
            {
                if (!slot.Renderer) continue;
                // 다른 시스템이 교체한 슬롯은 덮어쓰지 않습니다.
                slot.Renderer.GetSharedMaterials(scratch);
                bool changed = false;
                for (int i = 0; i < scratch.Count && i < slot.Original.Length; i++)
                {
                    if (scratch[i] != slot.Original[i] && scratch[i] != slot.Runtime[i]) continue;
                    var wanted = CurrentAlpha < 0.99999f ? slot.Runtime[i] : slot.Original[i];
                    if (scratch[i] != wanted) { scratch[i] = wanted; changed = true; }
                    slot.Runtime[i].SetFloat("_Opacity", CurrentAlpha);
                }
                if (changed) slot.Renderer.SetSharedMaterials(scratch);
            }
        }
        public void Release(OcclusionFadeController controller)
        {
            if (owner != controller) return;
            Cleanup();
        }
        void Cleanup()
        {
            SetAlpha(1f);
            foreach (var slot in slots) foreach (var material in slot.Copies) RealToonFadeMaterials.Destroy(material);
            slots.Clear(); renderers.Clear(); initialized = false; owner = null;
        }
        void OnDisable() => Cleanup();
        void OnDestroy() => Cleanup();
        void OnTransformChildrenChanged() => Cleanup();
    }
}
