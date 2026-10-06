using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cooked.Session
{
    /// <summary>양수 Damaged 이벤트만 표시하며 공유 머테리얼과 게임 판정을 변경하지 않습니다.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerDamagePresentation : MonoBehaviour
    {
        [Tooltip("실제 피격 이벤트를 받아 색상을 표시할 플레이어입니다. 체력·무적 판정은 이 설정에서 변경하지 않습니다.")]
        [SerializeField] private PlayerFacade player;
        [Tooltip("피격 시 색상을 바꿀 Renderer 목록입니다. _BaseColor 또는 _Color를 지원하는 머테리얼만 표시하며 공유 머테리얼 원본은 수정하지 않습니다.")]
        [SerializeField] private Renderer[] targets = Array.Empty<Renderer>();
        [Tooltip("피격 색상 유지 시간입니다(초, 0 이상). 높이면 더 오래 표시하며 0이면 색상 표현을 건너뜁니다. 연속 피격 시 시간을 다시 시작합니다.")]
        [SerializeField, Min(0f)] private float duration = .15f;
        [Tooltip("피격 시 지정할 색상입니다. 현재 빨간색을 사용하며 체력이나 피해량에는 영향을 주지 않습니다.")]
        [SerializeField] private Color damageColor = Color.red;
        private PlayerFacade boundPlayer;
        private float remaining;
        private readonly List<Slot> slots = new List<Slot>();
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");

        private sealed class Slot
        {
            public Renderer Renderer;
            public int Index;
            public MaterialPropertyBlock Original;
        }

        public void Bind(PlayerFacade source)
        {
            Unbind();
            player = source;
            if (!isActiveAndEnabled || source == null) return;
            boundPlayer = source;
            boundPlayer.Damaged += OnDamaged;
        }

        private void OnEnable() => Bind(player);
        private void OnDisable() => Unbind();
        private void OnDestroy() => Unbind();
        public void Unbind()
        {
            if (boundPlayer != null) boundPlayer.Damaged -= OnDamaged;
            boundPlayer = null;
            Restore();
        }

        private void OnDamaged(int applied)
        {
            if (applied <= 0 || !isActiveAndEnabled || boundPlayer == null) return;
            // 연속 피격에서도 빨간 값을 원래 값으로 저장하지 않습니다.
            Restore();
            if (!float.IsFinite(duration) || duration <= 0f) return;
            var seen = new HashSet<Renderer>();
            foreach (var target in targets)
            {
                if (target == null || !seen.Add(target)) continue;
                var materials = target.sharedMaterials;
                for (int i = 0; i < materials.Length; i++)
                {
                    var material = materials[i];
                    if (material == null) continue;
                    bool hasBase = material.HasProperty(BaseColor), hasColor = material.HasProperty(ColorProperty);
                    if (!hasBase && !hasColor) continue;
                    var original = new MaterialPropertyBlock();
                    target.GetPropertyBlock(original, i);
                    var tinted = new MaterialPropertyBlock();
                    target.GetPropertyBlock(tinted, i);
                    // 슬롯 override가 없으면 renderer 단위 속성을 보존하여 표시합니다.
                    if (tinted.isEmpty) target.GetPropertyBlock(tinted);
                    if (hasBase) tinted.SetColor(BaseColor, damageColor);
                    if (hasColor) tinted.SetColor(ColorProperty, damageColor);
                    slots.Add(new Slot { Renderer = target, Index = i, Original = original });
                    target.SetPropertyBlock(tinted, i);
                }
            }
            remaining = duration;
        }

        private void Update()
        {
            if (slots.Count == 0) return;
            remaining -= Time.deltaTime;
            if (remaining <= 0f) Restore();
        }

        private void Restore()
        {
            foreach (var slot in slots)
                if (slot.Renderer != null)
                    slot.Renderer.SetPropertyBlock(slot.Original.isEmpty ? null : slot.Original, slot.Index);
            slots.Clear();
            remaining = 0f;
        }
    }
}
