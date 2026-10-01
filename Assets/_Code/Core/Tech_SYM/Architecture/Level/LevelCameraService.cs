using System;
using Cooked.Contracts;
using DG.Tweening;
using UnityEngine;
namespace Cooked.Level
{
    [Serializable]
    public struct CameraZoneData
    {
        public string Id;
        public Vector3 Origin;
        public Vector3 Forward;
        public float HalfWidth;
        public float MinimumProgress;
        public float MaximumProgress;
        public bool AllowDepth;
        public Vector3 Offset;
        public Vector3 LookOffset;
    }
    // This component and Camera prefab belong in persistent InGameCore. It never switches active scenes.
    public sealed class LevelCameraService : MonoBehaviour, IDisposable
    {
        [SerializeField] private Transform cameraRig;
        [SerializeField] private CameraZoneData[] zones;
        [SerializeField, Min(.01f)] private float blendSeconds = .6f;
        [SerializeField, Min(0)] private float hysteresis = .5f;
        private IPlayerBridgeService player;
        private Transform target;
        private int current = -1;
        private Vector3 offset, lookOffset;
        private Sequence zoneBlend;
        public string CurrentZoneId => current >= 0 ? zones[current].Id : null;
        public void Bind(IPlayerBridgeService bridge)
        {
            if (bridge == null) throw new ArgumentNullException(nameof(bridge));
            CancelBlend();
            player = bridge;
            target = bridge.CameraTarget;
            current = -1;
            if (player.HasActor && target != null) UpdateCamera(true);
        }
        private void LateUpdate()
        {
            if (player == null || !player.HasActor)
            {
                CancelBlend(); target = null; current = -1;
                return;
            }
            if (target != player.CameraTarget)
            {
                CancelBlend(); target = player.CameraTarget; current = -1;
            }
            if (target != null) UpdateCamera(current < 0);
        }
        private void UpdateCamera(bool snap)
        {
            int selected = -1;
            float best = float.PositiveInfinity;
            for (int i = 0; i < zones.Length; i++)
            {
                Vector3 direction = zones[i].Forward.sqrMagnitude > .1f ? zones[i].Forward.normalized : Vector3.right;
                Vector3 delta = target.position - zones[i].Origin; delta.y = 0;
                float progress = Vector3.Dot(delta, direction);
                float lateral = (delta - direction * progress).magnitude;
                if (progress < zones[i].MinimumProgress - hysteresis || progress > zones[i].MaximumProgress + hysteresis ||
                    lateral > Mathf.Max(1, zones[i].HalfWidth) + hysteresis) continue;
                float score = lateral;
                if (progress > zones[i].MaximumProgress - .75f) score += 2;
                if (i == current) score -= .1f;
                if (score <= best) { best = score; selected = i; }
            }
            if (selected < 0) return;
            if (selected != current)
            {
                // Physical funnel centres Quarter->Side before this boundary; never teleport the actor.
                current = selected;
                player.SetDepthMovementAllowed(zones[current].AllowDepth);
                TransitionToZone(zones[current], snap);
            }
            cameraRig.position = target.position + offset;
            cameraRig.rotation = Quaternion.LookRotation(target.position + lookOffset - cameraRig.position, Vector3.up);
        }
        private void TransitionToZone(CameraZoneData zone, bool snap)
        {
            CancelBlend();
            if (snap)
            {
                offset = zone.Offset; lookOffset = zone.LookOffset;
                return;
            }
            // One finite blend per zone change. Only offsets tween; the moving Actor is followed in LateUpdate.
            // Scaled Late update freezes progression during options timeScale=0 and resumes the remaining time.
            zoneBlend = DOTween.Sequence().SetUpdate(UpdateType.Late, false).SetRecyclable(false);
            zoneBlend.Join(DOTween.To(() => offset, value => offset = value, zone.Offset, blendSeconds).SetEase(Ease.Linear));
            zoneBlend.Join(DOTween.To(() => lookOffset, value => lookOffset = value, zone.LookOffset, blendSeconds).SetEase(Ease.Linear));
            zoneBlend.SetEase(Ease.InOutSine);
            if (!isActiveAndEnabled) zoneBlend.Pause();
        }
        private void CancelBlend()
        {
            if (zoneBlend != null && zoneBlend.IsActive()) zoneBlend.Kill(false);
            zoneBlend = null;
        }
        private void OnDisable() { if (zoneBlend != null && zoneBlend.IsActive()) zoneBlend.Pause(); }
        private void OnEnable() { if (zoneBlend != null && zoneBlend.IsActive()) zoneBlend.Play(); }
        public void Dispose() { CancelBlend(); player = null; target = null; current = -1; }
        private void OnDestroy() => Dispose();
    }
}
