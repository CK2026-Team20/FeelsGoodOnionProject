using System;

namespace Cooked.Telemetry
{
    [Serializable]
    public sealed class PlayStatsData
    {
        public string installation_id;
        public string session_id;
        public int jump_count;
        public string recorded_at;
    }
}