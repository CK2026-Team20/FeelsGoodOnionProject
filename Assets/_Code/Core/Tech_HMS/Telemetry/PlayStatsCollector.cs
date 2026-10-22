using System;
using System.Globalization;

namespace Cooked.Telemetry
{
    public sealed class PlayStatsCollector
    {
        private readonly string installationId = Guid.NewGuid().ToString("N");
        private string sessionId = Guid.NewGuid().ToString("N");
        private DateTime recordedAt = DateTime.UtcNow;
        private int jumpCount;

        public void StartNewSession()
        {
            sessionId = Guid.NewGuid().ToString("N");
            jumpCount = 0;
            recordedAt = DateTime.UtcNow;
        }

        public void RecordJump()
        {
            checked { jumpCount++; }
            var now = DateTime.UtcNow;
            recordedAt = now > recordedAt ? now : recordedAt.AddTicks(1);
        }

        public PlayStatsData CreateSnapshot()
        {
            return new PlayStatsData
            {
                installation_id = installationId,
                session_id = sessionId,
                jump_count = jumpCount,
                recorded_at = recordedAt.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture)
            };
        }
    }
}