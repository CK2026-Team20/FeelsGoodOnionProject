using System;
using Cooked.Authoring;
using Unity.Cinemachine;
using UnityEngine;

namespace Cooked.UI
{
    /// <summary>Owns the title output and temporarily suspends the separate gameplay output.</summary>
    [DisallowMultipleComponent]
    public sealed class TitleScenePresentation : MonoBehaviour
    {
        [SerializeField, InspectorLabel("타이틀 카메라 기준점")]
        [Tooltip("이 빈 루트 아래의 고정 기준점입니다. 타이틀 전용 카메라에만 위치와 회전을 적용합니다.")]
        private Transform cameraAnchor;
        [SerializeField, InspectorLabel("타이틀 전용 카메라")]
        [Tooltip("같은 타이틀 씬의 전용 Camera를 연결합니다. 같은 오브젝트에 AudioListener를 두고 기본 활성 상태는 끕니다.")]
        private Camera titleCamera;
        [SerializeField, Range(1f, 179f), InspectorLabel("타이틀 시야각")]
        [Tooltip("타이틀 전용 원근 카메라의 수직 시야각입니다(도, 1~179). 게임 카메라에는 적용하지 않습니다.")]
        private float fieldOfView = 60f;

        private Camera gameplayCamera;
        private CinemachineBrain gameplayBrain;
        private AudioListener gameplayListener, titleListener;
        private bool previousCameraEnabled, previousBrainEnabled, previousListenerEnabled;
        public Camera Output => titleCamera;

        public void Bind(Camera camera)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            if (!isActiveAndEnabled) throw new InvalidOperationException("활성화된 타이틀 표시 루트에 연결하세요.");
            ValidateReferences();
            if (camera == titleCamera) throw new ArgumentException("게임과 타이틀 출력 카메라는 서로 달라야 합니다.");
            if (gameplayCamera == camera) { Apply(); return; }
            Unbind();
            gameplayCamera = camera;
            gameplayBrain = camera.GetComponent<CinemachineBrain>();
            gameplayListener = camera.GetComponent<AudioListener>();
            previousCameraEnabled = camera.enabled;
            previousBrainEnabled = gameplayBrain != null && gameplayBrain.enabled;
            previousListenerEnabled = gameplayListener != null && gameplayListener.enabled;
            try
            {
                // Disable the old output before enabling the new one: never two listeners.
                if (gameplayListener != null) gameplayListener.enabled = false;
                if (gameplayBrain != null) gameplayBrain.enabled = false;
                gameplayCamera.enabled = false;
                Apply();
                titleCamera.enabled = true;
                titleListener.enabled = true;
            }
            catch { Unbind(); throw; }
        }

        public void Apply()
        {
            ValidateReferences();
            if (gameplayCamera == null) throw new InvalidOperationException("타이틀 출력을 먼저 Bind하세요.");
            titleCamera.transform.SetPositionAndRotation(cameraAnchor.position, cameraAnchor.rotation);
            titleCamera.orthographic = false;
            titleCamera.fieldOfView = fieldOfView;
        }

        public void Unbind()
        {
            if (titleListener != null) titleListener.enabled = false;
            if (titleCamera != null) titleCamera.enabled = false;
            var camera = gameplayCamera;
            var brain = gameplayBrain;
            var listener = gameplayListener;
            gameplayCamera = null; gameplayBrain = null; gameplayListener = null;
            if (camera != null) camera.enabled = previousCameraEnabled;
            if (brain != null) brain.enabled = previousBrainEnabled;
            if (listener != null) listener.enabled = previousListenerEnabled;
        }

        private void ValidateReferences()
        {
            if (cameraAnchor == null || cameraAnchor == transform || !cameraAnchor.IsChildOf(transform))
                throw new InvalidOperationException("타이틀 표시의 빈 루트 아래에 카메라 기준점 자식을 연결하세요.");
            if (titleCamera == null || titleCamera.gameObject.scene != gameObject.scene ||
                titleCamera.GetComponent<CinemachineBrain>() != null)
                throw new InvalidOperationException("같은 타이틀 씬의 Brain 없는 전용 Camera를 연결하세요.");
            titleListener = titleCamera.GetComponent<AudioListener>();
            if (titleListener == null) throw new InvalidOperationException("타이틀 전용 Camera에 AudioListener를 연결하세요.");
            if (!float.IsFinite(fieldOfView) || fieldOfView < 1f || fieldOfView > 179f)
                throw new InvalidOperationException("타이틀 시야각은 유한한 1~179도여야 합니다.");
        }
        private void OnDisable() => Unbind();
        private void OnDestroy() => Unbind();
    }
}
