using System;
namespace Cooked.Dialogue
{
    public sealed class DialogueSettings
    {
        public DialogueSettings(float clickCooldown = 0.3f, float autoDelay = 3f)
        {
            if (float.IsNaN(clickCooldown) || clickCooldown < 0.3f || clickCooldown > 1f)
                throw new ArgumentOutOfRangeException(nameof(clickCooldown));
            if (float.IsNaN(autoDelay) || autoDelay < 2f || autoDelay > 5f)
                throw new ArgumentOutOfRangeException(nameof(autoDelay));
            ClickCooldown = clickCooldown;
            AutoDelay = autoDelay;
        }
        public float ClickCooldown { get; }
        public float AutoDelay { get; }
    }
    public enum DialogueState { Closed, Revealing, AwaitingAdvance }
    public enum DialogueOutcome { None, Completed, Skipped, Cancelled }
}
