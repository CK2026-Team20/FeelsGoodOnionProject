using System;
using Cooked.Level;
using Cooked.Occlusion;

namespace Cooked.Integration
{
    /// <summary>확정된 화면 시점에 따라 빌린 출력 카메라의 투명화만 제어합니다.</summary>
    public sealed class CameraOcclusionBinding : IDisposable
    {
        private readonly OcclusionFadeController controller;
        private readonly LevelCameraService source;
        private bool disposed;

        public CameraOcclusionBinding(OcclusionFadeController controller, LevelCameraService source = null)
        {
            this.controller = controller != null ? controller : throw new ArgumentNullException(nameof(controller));
            this.source = source;
            ApplyView(null);
            if (source != null)
            {
                source.PresentedViewChanged += ApplyView;
                ApplyView(source.PresentedView);
            }
        }

        public void ApplyView(CameraMovementMode? view)
        {
            if (disposed || controller == null) return;
            bool enabled = view == CameraMovementMode.Quarter;
            if (controller.enabled != enabled) controller.enabled = enabled;
        }

        public void Dispose()
        {
            if (disposed) return;
            if (source != null) source.PresentedViewChanged -= ApplyView;
            ApplyView(null); // HMS OnDisable이 자기 그룹과 원본 머테리얼을 복원한다.
            disposed = true;
        }
    }
}
