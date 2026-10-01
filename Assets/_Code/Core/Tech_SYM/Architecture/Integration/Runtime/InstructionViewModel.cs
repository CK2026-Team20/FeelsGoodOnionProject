using Cooked.UI;
namespace Cooked.Integration
{
    /// <summary>Read-only tutorial/sign presentation. Level remains the text and position owner.</summary>
    public sealed class InstructionViewModel : ObservableViewModel
    {
        public string Text { get; private set; } = string.Empty;
        public bool Visible { get; private set; }
        public void Present(string text, bool visible)
        {
            if (IsDisposed) return;
            text = text ?? string.Empty;
            visible = visible && text.Length != 0;
            if (Text == text && Visible == visible) return;
            Text = text; Visible = visible; Notify();
        }
    }
}
