using System;
using System.Collections;
using System.Threading;
namespace Cooked.Contracts
{
    public interface IFadeService
    {
        IEnumerator Cover(CancellationToken token, OperationResult result);
        IEnumerator Reveal(CancellationToken token, OperationResult result);
        void SetCoveredImmediate();
    }
    public enum CinematicId { Opening, Ending }
    public enum CinematicOutcome { None, Completed, Skipped, Cancelled }
    public interface ICinematicService : IDisposable
    {
        CinematicOutcome Outcome { get; }
        IEnumerator Prepare(CinematicId id, CancellationToken token, OperationResult result);
        IEnumerator Play(CancellationToken token, OperationResult result);
        void Skip();
        void Hide();
    }
    public interface IAudioService : IDisposable
    {
        void PlayMusic(string cueId);
        void StopMusic();
        void PlaySfx(string cueId);
        void SetVolumes(float master, float music, float sfx);
    }
}
