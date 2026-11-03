namespace Cooked.Audio
{
    /// <summary>성공한 플레이어 상태 변화에서 명세의 소리 용도를 선택한다.</summary>
    public static class PlayerAudioCueSelection
    {
        public static string FormChanged(bool isSmall) => isSmall ? AudioCueIds.FormDown : AudioCueIds.FormUp;
        public static string FragmentsChanged(int previous, int current)
        {
            if (previous == 4 && current == 5) return AudioCueIds.Unlock;
            return current > previous && current >= 1 && current <= 4 ? AudioCueIds.Pickup : null;
        }
    }
}
