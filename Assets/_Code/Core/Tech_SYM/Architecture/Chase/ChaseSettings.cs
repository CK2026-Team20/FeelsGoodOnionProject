using System;

namespace Cooked.Chase
{
    /// <summary>Immutable rules for the Stage 3 chase. Distances are metres.</summary>
    public sealed class ChaseSettings
    {
        public const string StageId = "03_3_Stage";
        public const float InitialGap = 8f;
        public float Speed { get; }
        public float CaptureGap { get; }

        public ChaseSettings(float speed = 4f, float captureGap = .8f)
        {
            if (!IsFinite(speed) || speed <= 0) throw new ArgumentOutOfRangeException(nameof(speed));
            if (!IsFinite(captureGap) || captureGap < 0 || captureGap >= InitialGap)
                throw new ArgumentOutOfRangeException(nameof(captureGap));
            Speed = speed;
            CaptureGap = captureGap;
        }

        public static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
