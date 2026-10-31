#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Cooked.Contracts;
using Cooked.Level;
using Cooked.Session;
using FeelsGoodOnion.TechSYM.Platforms;
using UnityEditor;
using UnityEngine;
namespace Cooked.DebugTesting
{
    /// <summary>Debug use-case adapter. Gameplay state remains owned by the real session and actor.</summary>
    public sealed class DebugPanelViewModel : IDisposable
    {
        private readonly DirectSceneTestRuntime runtime;
        private readonly DebugLogBuffer logs;
        private readonly List<Transform> points = new List<Transform>();
        private readonly List<DamageFloorPlatform> platforms = new List<DamageFloorPlatform>();
        private IDisposable pause;
        private PlayerFacade invincibleActor;
        private bool previousInvincible, disposed, executing;
        private long logVersion = -1;
        public event Action Changed;
        public event Action<bool> VisibilityChanged;
        public bool Visible { get; private set; }
        public bool DebugInvincible { get; private set; }
        public bool Paused => pause != null;
        public bool CanCommand => !disposed && runtime != null && runtime.IsReady && !executing;
        public bool CanDamage => CanCommand && SelectedPlatform != null && SelectedPlatform.isActiveAndEnabled && !runtime.Player.PhysicsPaused;
        public bool CanSelectCamera => CanCommand && runtime.TestCamera != null && runtime.TestCamera.CanSelect;
        public string CameraLabel => runtime.TestCamera == null ? "준비 중" : runtime.TestCamera.CurrentView switch
        {
            CameraMovementMode.Side => "측면",
            CameraMovementMode.Corridor => "통로",
            CameraMovementMode.Quarter => "쿼터뷰",
            _ => "준비 중"
        };
        public bool SelectCamera(CameraMovementMode mode) => CanSelectCamera && Run("카메라 시점 변경", () => runtime.TestCamera.Select(mode));
        public string Summary { get; private set; } = "플레이어 준비 중";
        public string LogText { get; private set; } = string.Empty;
        public long LogVersion => logVersion;
        public int PointIndex { get; private set; }
        public int PlatformIndex { get; private set; }
        public Transform SelectedPoint => points.Count == 0 ? null : points[PointIndex % points.Count];
        public DamageFloorPlatform SelectedPlatform => platforms.Count == 0 ? null : platforms[PlatformIndex % platforms.Count];
        public string PointLabel => SelectedPoint != null ? SelectedPoint.name : "이동 지점 없음";
        public string PlatformLabel => SelectedPlatform != null ? SelectedPlatform.name + " / 피해 " + SelectedPlatform.DamageAmount : "피해 플랫폼 없음";
        public DebugPanelViewModel(DirectSceneTestRuntime runtime, DebugLogBuffer logs)
        {
            this.runtime = runtime != null ? runtime : throw new ArgumentNullException(nameof(runtime));
            this.logs = logs ?? throw new ArgumentNullException(nameof(logs));
            if (runtime.Settings.StartPoint != null) points.Add(runtime.Settings.StartPoint);
            foreach (var point in runtime.Settings.AdditionalPoints) if (point != null && !points.Contains(point)) points.Add(point);
            foreach (var root in runtime.gameObject.scene.GetRootGameObjects())
                foreach (var platform in root.GetComponentsInChildren<DamageFloorPlatform>(true))
                    if (platform.enabled) platforms.Add(platform);
            runtime.ActorReady += OnActorReady;
            runtime.ActorRemoving += OnActorRemoving;
            Refresh();
        }
        public void ToggleVisible()
        {
            if (disposed) return;
            Visible = !Visible;
            VisibilityChanged?.Invoke(Visible);
            Changed?.Invoke();
        }
        public void Refresh()
        {
            if (disposed) return;
            var snapshot = logs.Snapshot(out long version);
            if (version != logVersion) { logVersion = version; LogText = string.Join("\n", snapshot); }
            if (runtime != null && runtime.Player != null && runtime.Player.HasActor)
            {
                var actor = runtime.Player.Actor;
                var model = actor.Model;
                var form = actor.GetComponent<PlayerFormController>();
                var movement = actor.GetComponent<CharacterMovement>();
                string values = "이동/점프 연결 없음";
                if (movement != null)
                {
                    using (var serialized = new SerializedObject(movement))
                    {
                        serialized.Update();
                        values = $"이동 {serialized.FindProperty("moveSpeed").floatValue:0.###} m/s · 점프 {serialized.FindProperty("jumpHeight").floatValue:0.###} m";
                    }
                }
                Summary = $"HP {model.CurrentHP}/{model.MaxHP} · {(form != null && form.IsSmall ? "작음" : "기본")}\n조각 {model.CurrentSkillFragment}/{model.MaxSkillFragment} · 해금 {runtime.Session.UnlockedAbilities}\n{values}";
            }
            else Summary = "플레이어 준비 중";
            Changed?.Invoke();
        }
        private bool Run(string label, Action action)
            => Run(label, () => { action(); return true; });
        private bool Run(string label, Func<bool> action)
        {
            if (!CanCommand) return false;
            executing = true;
            try { bool accepted = action(); Debug.Log("[테스트] " + label + (accepted ? " 완료" : " 거절")); return accepted; }
            finally { executing = false; Refresh(); }
        }
        public bool Heal() => Run("체력 회복", () => runtime.Player.Actor.Heal(runtime.Player.Model.MaxHP));
        public bool FillFragments() => Run("조각 채우기", () => runtime.Player.RestoreFragments(runtime.Player.Model.MaxSkillFragment));
        public bool UnlockAll() => Run("능력 해금", () => { runtime.Session.Unlock(AbilityId.FormChange); runtime.Session.Unlock(AbilityId.Tear); });
        public bool ToggleInvincible() => Run("디버그 무적 전환", () =>
        {
            DebugInvincible = !DebugInvincible;
            if (DebugInvincible) ApplyInvincible(); else ReleaseInvincible();
        });
        private void ApplyInvincible()
        {
            if (invincibleActor != null || !runtime.Player.HasActor) return;
            invincibleActor = runtime.Player.Actor;
            using (var serialized = new SerializedObject(invincibleActor))
                previousInvincible = serialized.FindProperty("invincible").boolValue;
            invincibleActor.SetInvincible(true);
        }
        private void ReleaseInvincible()
        {
            // SetInvincible changes only the persistent flag, never the independent hit timer.
            if (invincibleActor != null) invincibleActor.SetInvincible(previousInvincible);
            invincibleActor = null;
        }
        private void OnActorReady() { if (DebugInvincible) ApplyInvincible(); Refresh(); }
        private void OnActorRemoving() { ReleaseInvincible(); }
        public void NextPoint() { if (!disposed && points.Count > 0) { PointIndex = (PointIndex + 1) % points.Count; Changed?.Invoke(); } }
        public void NextPlatform() { if (!disposed && platforms.Count > 0) { PlatformIndex = (PlatformIndex + 1) % platforms.Count; Changed?.Invoke(); } }
        public bool MoveToStart() => Teleport(runtime != null ? runtime.Settings.StartPoint : null);
        public bool MoveToSelectedPoint() => Teleport(SelectedPoint);
        private bool Teleport(Transform point)
        {
            if (point == null) return false;
            return Run("이동: " + point.name, () =>
            {
                var actor = runtime.Player.Actor;
                long generation = runtime.Player.ActorGeneration;
                runtime.Player.CancelCorrection();
                if (!runtime.Player.IsCurrentActor(actor, generation) || actor.IsDead) return;
                var movement = actor.GetComponent<CharacterMovement>();
                movement?.ResetForRevive(true);
                if (!runtime.Player.IsCurrentActor(actor, generation) || actor.IsDead) return;
                SessionActorHost.PlaceColliderCenter(actor.GetComponent<CapsuleCollider>(), new Pose(point.position, point.rotation));
                Physics.SyncTransforms();
            });
        }
        public bool Respawn() => CanCommand && Run("플레이어 재생성", () => runtime.Respawn());
        public bool ApplySelectedDamage()
        {
            if (!CanDamage) return false;
            return Run("선택 플랫폼 피해 요청", () => runtime.Player.Damage(SelectedPlatform) > 0);
        }
        public bool TogglePause()
        {
            if (disposed || runtime == null || runtime.Flow.Snapshot.State != FlowState.Playing) return false;
            if (pause == null) pause = runtime.Control.PauseWorld("DebugPanel");
            else { pause.Dispose(); pause = null; }
            Debug.Log(Paused ? "[테스트] 일시정지" : "[테스트] 재개");
            Refresh(); return true;
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            runtime.ActorReady -= OnActorReady;
            runtime.ActorRemoving -= OnActorRemoving;
            ReleaseInvincible();
            pause?.Dispose(); pause = null;
            Changed = null; VisibilityChanged = null;
        }
    }
}
#endif
