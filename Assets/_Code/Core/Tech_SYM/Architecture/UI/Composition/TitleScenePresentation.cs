using System;
using Cooked.Authoring;
using Unity.Cinemachine;
using UnityEngine;

namespace Cooked.UI
{
    /// <summary>Title-owned authored pose, borrowing the app's single output camera.</summary>
    [DisallowMultipleComponent]
    public sealed class TitleScenePresentation : MonoBehaviour
    {
        [SerializeField, InspectorLabel("타이틀 카메라 기준점")]
        [Tooltip("이 빈 루트 아래의 고정 기준점을 연결합니다. 기준점의 월드 위치와 회전을 앱 출력 카메라에 적용합니다.")]
        private Transform cameraAnchor;
        [SerializeField, Range(1f, 179f), InspectorLabel("타이틀 시야각")]
        [Tooltip("타이틀을 표시할 원근 카메라의 수직 시야각입니다. 단위는 도이며 1~179를 사용합니다.")]
        private float fieldOfView = 60f;

        private Camera output;
        private CinemachineBrain borrowedBrain;
        private bool previousBrainEnabled;
        private bool previousOrthographic;
        private float previousFieldOfView;

        public void Bind(Camera camera)
        {
            if (camera == null) throw new ArgumentNullException(nameof(camera));
            if (!isActiveAndEnabled)
                throw new InvalidOperationException("활성화된 타이틀 표시 루트에 출력 카메라를 연결하세요.");
            ValidateAnchor();
            if (output == camera) { Apply(); return; }
            Unbind();
            output = camera;
            previousOrthographic = camera.orthographic;
            previousFieldOfView = camera.fieldOfView;
            borrowedBrain = camera.GetComponent<CinemachineBrain>();
            previousBrainEnabled = borrowedBrain != null && borrowedBrain.enabled;
            try { Apply(); }
            catch { Unbind(); throw; }
        }

        /// <summary>Reapply after intentional authoring changes; never creates a camera or rig.</summary>
        public void Apply()
        {
            ValidateAnchor();
            if (output == null) throw new InvalidOperationException("타이틀 출력 카메라를 먼저 Bind하세요.");
            // A Brain can retain its last blend/state even with no live title rigs.
            // Suspend that writer for the title's lifetime; keep Camera/Listener enabled.
            if (borrowedBrain != null) borrowedBrain.enabled = false;
            output.transform.SetPositionAndRotation(cameraAnchor.position, cameraAnchor.rotation);
            output.orthographic = false;
            output.fieldOfView = fieldOfView;
        }

        public void Unbind()
        {
            var camera = output;
            var brain = borrowedBrain;
            output = null;
            borrowedBrain = null;
            // Called before the next scene's camera owner binds. Leave the last title pose
            // in place for the covered transition, but return borrowed projection/Brain state.
            if (camera != null)
            {
                camera.orthographic = previousOrthographic;
                camera.fieldOfView = previousFieldOfView;
            }
            if (brain != null && !brain.enabled) brain.enabled = previousBrainEnabled;
        }

        private void ValidateAnchor()
        {
            if (cameraAnchor == null || cameraAnchor == transform || !cameraAnchor.IsChildOf(transform))
                throw new InvalidOperationException("타이틀 표시의 빈 루트 아래에 카메라 기준점 자식을 연결하세요.");
            if (float.IsNaN(fieldOfView) || float.IsInfinity(fieldOfView) || fieldOfView < 1f || fieldOfView > 179f)
                throw new InvalidOperationException("타이틀 시야각은 유한한 1~179도여야 합니다.");
        }

        private void OnDisable() => Unbind();
        private void OnDestroy() => Unbind();
    }
}
