#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Cooked.Integration;
using Cooked.Level;
using Cooked.Session;
using Unity.Cinemachine;
using UnityEngine;

namespace Cooked.DebugTesting
{
    /// <summary>Owns temporary gameplay camera components and restores borrowed scene outputs.</summary>
    public sealed class DirectSceneTestCamera : IDisposable
    {
        private readonly List<Behaviour> suspended = new List<Behaviour>();
        private readonly Dictionary<CameraMovementMode, CinemachineCamera> rigs = new Dictionary<CameraMovementMode, CinemachineCamera>();
        private readonly Dictionary<CameraMovementMode, LevelCameraService.ViewSettings> profiles = new Dictionary<CameraMovementMode, LevelCameraService.ViewSettings>();
        private readonly Dictionary<CameraMovementMode, LevelCameraService.ViewSettings> sourceProfiles = new Dictionary<CameraMovementMode, LevelCameraService.ViewSettings>();
        private GameObject root;
        private CinemachineBrain brain;
        private PlayerBridgeService player;
        private PlayerMovementModeController movement;
        private bool disposed, bound;
        public Camera Output { get; private set; }
        public CameraMovementMode CurrentView { get; private set; }
        public bool CanSelect => !disposed && bound && player != null && player.HasActor &&
            !player.Actor.IsDead && movement != null && movement.IsInitialized;

        public DirectSceneTestCamera(Transform owner, DirectSceneTestSettings settings)
        {
            try
            {
                var source = settings.CameraProfileSource;
                if (source != null && source.gameObject.scene != owner.gameObject.scene)
                    throw new ArgumentException("카메라 구도 원본은 현재 테스트 씬에 있어야 합니다.");
                if (source == null)
                {
                    var stages = SceneComponentLookup.FindAll<StageService>(owner.gameObject.scene);
                    if (stages.Length == 1) source = stages[0];
                }
                // Isolate our Brain from existing scene virtual cameras without editing them.
                uint used = 0;
                foreach (var camera in UnityEngine.Object.FindObjectsByType<CinemachineVirtualCameraBase>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                    used |= (uint)camera.OutputChannel;
                uint channel = 1;
                while (channel != 0 && (used & channel) != 0) channel <<= 1;
                if (channel == 0) throw new InvalidOperationException("테스트 카메라에 사용할 빈 Cinemachine 출력 채널이 없습니다.");
                foreach (var camera in SceneComponentLookup.FindAll<Camera>(owner.gameObject.scene))
                    if (camera.isActiveAndEnabled && camera.targetTexture == null)
                    {
                        Suspend(camera.GetComponent<CinemachineBrain>());
                        Suspend(camera);
                    }
                foreach (var listener in SceneComponentLookup.FindAll<AudioListener>(owner.gameObject.scene))
                    if (listener.isActiveAndEnabled) Suspend(listener);
                root = new GameObject("Test Camera Rig") { hideFlags = HideFlags.DontSave };
                root.transform.SetParent(owner, false);
                var output = new GameObject("Test Output") { hideFlags = HideFlags.DontSave, tag = "MainCamera" };
                output.transform.SetParent(root.transform, false);
                Output = output.AddComponent<Camera>();
                output.AddComponent<AudioListener>();
                brain = output.AddComponent<CinemachineBrain>();
                brain.ChannelMask = (OutputChannels)channel;
                brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;
                brain.BlendUpdateMethod = CinemachineBrain.BrainUpdateMethods.LateUpdate;
                brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0);
                foreach (CameraMovementMode mode in Enum.GetValues(typeof(CameraMovementMode)))
                {
                    var node = new GameObject("Test " + mode) { hideFlags = HideFlags.DontSave };
                    node.transform.SetParent(root.transform, false);
                    var camera = node.AddComponent<CinemachineCamera>();
                    node.AddComponent<CinemachinePositionComposer>();
                    camera.OutputChannel = (OutputChannels)channel;
                    camera.Priority = 0;
                    rigs.Add(mode, camera);
                    sourceProfiles.Add(mode, source != null ? source.ViewFor(mode) : settings.ViewFor(mode));
                    profiles.Add(mode, new LevelCameraService.ViewSettings(Vector2.zero, Vector2.zero, Vector3.zero));
                    CopySourceProfile(mode);
                    profiles[mode].Apply(camera, Output.aspect);
                }
                // Validate through the same mapping as gameplay, before an actor is bound.
                LevelCameraService.ResolveMode(settings.InitialView);
                CurrentView = settings.InitialView;
                var initial = profiles[CurrentView];
                var point = settings.StartPoint != null ? settings.StartPoint.position : owner.position;
                Output.transform.SetPositionAndRotation(initial.PreviewPosition(point, Output.aspect), Quaternion.Euler(initial.angles));
                Output.fieldOfView = initial.fieldOfView;
            }
            catch { Dispose(); throw; }
        }
        private void Suspend(Behaviour component)
        {
            if (component == null || !component.enabled) return;
            suspended.Add(component);
            component.enabled = false;
        }
        public void Bind(PlayerBridgeService bridge)
        {
            Unbind();
            player = bridge;
            movement = bridge.Actor.GetComponent<PlayerMovementModeController>();
            if (movement == null || bridge.CameraTarget == null)
                throw new InvalidOperationException("테스트 카메라에는 플레이어 이동 모드와 CameraAnchor가 필요합니다.");
            foreach (var camera in rigs.Values)
            {
                camera.Follow = bridge.CameraTarget;
                camera.PreviousStateIsValid = false;
            }
            bound = true;
        }
        public bool Select(CameraMovementMode mode)
        {
            var mapped = LevelCameraService.ResolveMode(mode);
            CopySourceProfile(mode);
            if (!CanSelect || !profiles[mode].IsValidForAspect(Output.aspect) || !movement.TrySetMode(mapped)) return false;
            CurrentView = mode;
            foreach (var pair in rigs)
                pair.Value.Priority = pair.Key == mode ? 100 : 0;
            profiles[mode].Apply(rigs[mode], Output.aspect);
            rigs[mode].PreviousStateIsValid = false;
            brain.ManualUpdate();
            return true;
        }
        public void Tick()
        {
            if (!CanSelect) return;
            if (movement.CurrentMode != LevelCameraService.ResolveMode(CurrentView) || rigs[CurrentView].Priority.Value != 100)
            {
                Select(CurrentView);
                return;
            }
            CopySourceProfile(CurrentView);
            profiles[CurrentView].Refresh(rigs[CurrentView], Output.aspect);
            brain.ManualUpdate();
        }
        private void CopySourceProfile(CameraMovementMode mode)
        {
            var source = sourceProfiles[mode];
            var target = profiles[mode];
            target.screenPercent = source.screenPercent;
            target.deadZone = source.deadZone;
            target.damping = source.damping;
            target.distance = source.distance;
            target.focusOffset = source.focusOffset;
            target.fieldOfView = source.fieldOfView;
            // Preserve the authored rotation exactly, as gameplay ViewSettings.Apply does.
            target.angles = source.angles;
        }
        public void Unbind()
        {
            bound = false;
            foreach (var camera in rigs.Values)
                if (camera != null) { camera.Follow = null; camera.Priority = 0; camera.PreviousStateIsValid = false; }
            player = null; movement = null;
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            Unbind();
            if (root != null)
            {
                root.SetActive(false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(root);
                else UnityEngine.Object.DestroyImmediate(root);
            }
            foreach (var component in suspended) if (component != null) component.enabled = true;
            suspended.Clear();
        }
    }
}
#endif
