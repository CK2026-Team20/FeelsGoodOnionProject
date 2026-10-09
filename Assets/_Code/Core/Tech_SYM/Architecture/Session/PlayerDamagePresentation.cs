using System;
using System.Collections.Generic;
using Cooked.Authoring;
using UnityEngine;

namespace Cooked.Session
{
    /// <summary>실제 외형·카메라 지점을 제공하고 양수 Damaged 이벤트만 표시합니다. 게임 판정과 공유 머테리얼은 변경하지 않습니다.</summary>
    [DisallowMultipleComponent]
    public sealed class PlayerDamagePresentation : MonoBehaviour
    {
        [SerializeField, InspectorLabel("플레이어"), Tooltip("피격 이벤트를 받을 같은 ActorBody의 PlayerFacade를 연결합니다.")]
        private PlayerFacade player;
        [SerializeField, InspectorLabel("외형 루트"), Tooltip("기본·작은 외형을 담은 ActorBody의 직계 자식을 연결합니다. HMS Form과 같은 VisualRoot를 사용합니다.")]
        private Transform visualRoot;
        [SerializeField, InspectorLabel("카메라 지점"), Tooltip("카메라가 따라갈 ActorBody의 별도 직계 자식을 연결합니다.")]
        private Transform cameraAnchor;
        [SerializeField, InspectorLabel("피격 렌더러"), Tooltip("외형 루트 안에서 피격 색상을 표시할 Renderer를 연결합니다. _BaseColor 또는 _Color를 지원해야 합니다.")]
        private Renderer[] targets = Array.Empty<Renderer>();
        [SerializeField, InspectorLabel("피격 표시 시간"), Tooltip("피격 색상을 유지할 시간을 조절합니다(초). 0이면 표시하지 않으며 연속 피격 시 다시 시작합니다."), Min(0f)]
        private float duration = .15f;
        [SerializeField, InspectorLabel("피격 색상"), Tooltip("피격 시 외형에 표시할 색상을 조절합니다.")]
        private Color damageColor = Color.red;
        private PlayerFacade boundPlayer;
        private float remaining;
        private readonly List<Slot> slots = new List<Slot>();
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");

        /// <summary>HMS Form과 시선 표현이 사용하는 실제 외형 루트입니다. 소비자는 Bind 전에 연결을 검증합니다.</summary>
        public Transform VisualRoot => visualRoot;
        /// <summary>현재 ActorBody와 함께 움직이는 카메라 추적 지점입니다. 소비자는 Actor 제거 전에 빌린 참조를 해제합니다.</summary>
        public Transform CameraAnchor => cameraAnchor;

        /// <summary>같은 ActorBody의 명시적 참조만 수락합니다. 이름 검색이나 계층·배율·물리 변경은 하지 않습니다.</summary>
        public void ValidateReferences(PlayerFacade source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (source.gameObject != gameObject)
                throw new InvalidOperationException("PlayerDamagePresentation은 같은 ActorBody의 PlayerFacade에 연결해야 합니다.");
            if (visualRoot == null || visualRoot.parent != source.transform)
                throw new InvalidOperationException("외형 루트는 현재 ActorBody의 직계 자식이어야 합니다.");
            if (cameraAnchor == null || cameraAnchor.parent != source.transform || cameraAnchor == visualRoot)
                throw new InvalidOperationException("카메라 지점은 외형 루트와 다른 현재 ActorBody의 직계 자식이어야 합니다.");
            if (visualRoot.GetComponentsInChildren<Collider>(true).Length != 0 ||
                visualRoot.GetComponentsInChildren<Rigidbody>(true).Length != 0)
                throw new InvalidOperationException("외형 루트에는 Collider와 Rigidbody를 배치하지 않습니다. HMS 물리는 ActorBody에 유지합니다.");
            if (targets == null) throw new InvalidOperationException("피격 렌더러 목록이 필요합니다. 표시 대상이 없으면 빈 목록을 사용합니다.");
            foreach (var target in targets)
                if (target != null && target.transform != visualRoot && !target.transform.IsChildOf(visualRoot))
                    throw new InvalidOperationException("피격 렌더러는 연결한 외형 루트 안에 있어야 합니다.");
        }

        private sealed class Slot
        {
            public Renderer Renderer;
            public int Index;
            public MaterialPropertyBlock Original;
        }

        public void Bind(PlayerFacade source)
        {
            // 잘못된 새 연결은 기존 구독과 표시를 해제하기 전에 거절합니다.
            if (source != null) ValidateReferences(source);
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
