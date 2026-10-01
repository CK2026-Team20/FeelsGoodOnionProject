namespace Cooked.UI
{
    public interface IApplicationService { void Quit(); }
    public sealed class ApplicationService : IApplicationService
    {
        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit();
#endif
        }
    }
}
