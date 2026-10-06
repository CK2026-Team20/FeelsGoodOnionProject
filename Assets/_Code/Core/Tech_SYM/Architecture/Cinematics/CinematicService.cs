using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cooked.Contracts;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace Cooked.Cinematics
{
    public enum CinematicPhase { Idle, Ready, Playing, Holding, Faulted, Disposed }

    public sealed class CinematicService : ICinematicService
    {
        private readonly PlayableDirector director;
        private readonly CinematicFrameBridge bridge;
        private readonly CinematicCatalog catalog;
        private readonly IGameplayControlService control;
        private CancellationToken operationToken;
        private OperationResult playResult;
        private long revision;
        private bool presentationPaused;
        private IDisposable inputLease, worldLease;
        public CinematicPresentationState Presentation { get; } = new CinematicPresentationState();
        public CinematicOutcome Outcome { get; private set; }
        public CinematicPhase Phase { get; private set; }
        public long Revision => revision;
        public double Time => director == null ? 0 : director.time;
        public string Error { get; private set; }

        public CinematicService(PlayableDirector director, CinematicFrameBridge bridge,
            CinematicCatalog catalog, IGameplayControlService control)
        {
            this.director = director != null ? director : throw new ArgumentNullException(nameof(director));
            this.bridge = bridge != null ? bridge : throw new ArgumentNullException(nameof(bridge));
            this.catalog = catalog != null ? catalog : throw new ArgumentNullException(nameof(catalog));
            this.control = control ?? throw new ArgumentNullException(nameof(control));
            bridge.Bind(Presentation);
            director.playOnAwake = false;
            director.timeUpdateMode = DirectorUpdateMode.UnscaledGameTime;
            director.extrapolationMode = DirectorWrapMode.Hold;
            presentationPaused = control.State.PresentationPaused;
            control.Changed += OnControlChanged;
        }

        public IEnumerator Prepare(CinematicId id, CancellationToken token, OperationResult result)
        {
            RequirePending(result);
            if (Phase == CinematicPhase.Disposed) { result.Fail("CinematicService is disposed."); yield break; }
            if (Phase != CinematicPhase.Idle) { result.Fail("Hide the previous cinematic before Prepare."); yield break; }
            if (token.IsCancellationRequested) { Outcome = CinematicOutcome.Cancelled; result.Cancel(); yield break; }
            revision++;
            operationToken = token;
            Outcome = CinematicOutcome.None;
            Error = null;
            try
            {
                TimelineAsset timeline = catalog.Get(id);
                CinematicArtworkTrack track = Validate(timeline, id);
                inputLease = control.BlockGameplay("Cinematic");
                worldLease = control.PauseWorld("Cinematic");
                Presentation.SetFrame(default);
                director.playableAsset = timeline;
                director.SetGenericBinding(track, bridge);
                director.RebuildGraph();
                director.time = 0;
                director.Evaluate();
                if (!Presentation.Frame[0].IsRevealed) throw new InvalidOperationException("Timeline did not prepare its first frame: " + id);
                Phase = CinematicPhase.Ready;
                UpdateFlags();
                result.Succeed();
            }
            catch (Exception exception)
            {
                var errors = new List<Exception> { exception };
                Phase = CinematicPhase.Faulted;
                Attempt(errors, () => director.Stop());
                Attempt(errors, ReleaseLeases);
                Attempt(errors, () => Presentation.SetFlags(false, false));
                Attempt(errors, () => Presentation.SetFrame(default));
                Error = "Cinematic Prepare " + id + ": " + new AggregateException(errors);
                result.Fail(Error);
            }
            yield break;
        }

        public IEnumerator Play(CancellationToken token, OperationResult result)
        {
            RequirePending(result);
            if (Phase != CinematicPhase.Ready) { result.Fail("Cinematic must be Ready before Play."); yield break; }
            playResult = result;
            operationToken = token;
            long run = revision;
            if (token.IsCancellationRequested) { Finish(CinematicOutcome.Cancelled); yield break; }
            Phase = CinematicPhase.Playing;
            try
            {
                director.Play();
                if (presentationPaused) director.Pause();
                UpdateFlags();
            }
            catch (Exception exception) { FailPlayback(exception); }
            try
            {
                while (run == revision && Phase == CinematicPhase.Playing)
                {
                    ObservePlayback();
                    if (Phase == CinematicPhase.Playing) yield return null;
                }
            }
            finally
            {
                // A coroutine owner can dispose the iterator without advancing it again.
                if (run == revision && Phase == CinematicPhase.Playing) Finish(CinematicOutcome.Cancelled);
            }
            // Hide/Dispose can invalidate this iterator before the next MoveNext.
            if (result.Status == OperationStatus.Pending) result.Cancel();
        }

        public void ObservePlayback()
        {
            if (Phase != CinematicPhase.Ready && Phase != CinematicPhase.Playing) return;
            if (operationToken.IsCancellationRequested) { Finish(CinematicOutcome.Cancelled); return; }
            if (Phase != CinematicPhase.Playing) return;
            try
            {
                double end = director.duration;
                if (director.time < end - 0.00001) return;
                director.time = Math.Max(0, end - 0.000001);
                director.Evaluate();
            }
            catch (Exception exception) { FailPlayback(exception); return; }
            Finish(CinematicOutcome.Completed);
        }

        public void Skip()
        {
            if (Phase != CinematicPhase.Playing || presentationPaused) return;
            Finish(CinematicOutcome.Skipped);
        }

        public void Hide()
        {
            if (Phase == CinematicPhase.Disposed) return;
            var errors = new List<Exception>();
            if (Phase == CinematicPhase.Ready || Phase == CinematicPhase.Playing)
                Attempt(errors, () => Finish(CinematicOutcome.Cancelled));
            revision++;
            Attempt(errors, () => director.Stop());
            if (director.playableAsset is TimelineAsset timeline)
                foreach (var track in timeline.GetOutputTracks()) Attempt(errors, () => director.ClearGenericBinding(track));
            Attempt(errors, () => director.playableAsset = null);
            playResult = null;
            Phase = CinematicPhase.Idle;
            Attempt(errors, ReleaseLeases);
            Attempt(errors, () => Presentation.SetFlags(false, false));
            Attempt(errors, () => Presentation.SetFrame(default));
            ThrowErrors(errors, "Cinematic Hide cleanup failed.");
        }

        public void Dispose()
        {
            if (Phase == CinematicPhase.Disposed) return;
            try { Hide(); }
            finally
            {
                control.Changed -= OnControlChanged;
                bridge.Bind(null);
                Phase = CinematicPhase.Disposed;
                ReleaseLeases();
            }
        }

        private static void Attempt(List<Exception> errors, Action action)
        { try { action(); } catch (Exception e) { errors.Add(e); } }
        private static void ThrowErrors(List<Exception> errors, string message)
        { if (errors.Count > 0) throw new AggregateException(message, errors); }
        private void ReleaseLeases()
        {
            var input = inputLease; var world = worldLease;
            inputLease = worldLease = null;
            var errors = new List<Exception>();
            Attempt(errors, () => input?.Dispose());
            Attempt(errors, () => world?.Dispose());
            ThrowErrors(errors, "Cinematic lease cleanup failed.");
        }

        private void OnControlChanged(ControlState state)
        {
            if (Phase == CinematicPhase.Disposed) return;
            if (presentationPaused == state.PresentationPaused) return;
            presentationPaused = state.PresentationPaused;
            if (Phase == CinematicPhase.Playing)
            {
                if (presentationPaused) director.Pause();
                else director.Resume();
            }
            UpdateFlags();
        }

        private void Finish(CinematicOutcome outcome)
        {
            if (Phase != CinematicPhase.Playing && Phase != CinematicPhase.Ready) return;
            Outcome = outcome;
            Phase = CinematicPhase.Holding;
            // Terminal result must survive a throwing presentation observer.
            if (playResult != null && playResult.Status == OperationStatus.Pending)
            {
                if (outcome == CinematicOutcome.Cancelled) playResult.Cancel(); else playResult.Succeed();
            }
            var errors = new List<Exception>();
            Attempt(errors, () => director.Pause());
            Attempt(errors, UpdateFlags);
            if (errors.Count > 0) Attempt(errors, ReleaseLeases);
            ThrowErrors(errors, "Cinematic completion observer failed.");
        }

        private void FailPlayback(Exception exception)
        {
            var errors = new List<Exception> { exception };
            Error = "Cinematic playback: " + exception;
            Phase = CinematicPhase.Faulted;
            Outcome = CinematicOutcome.None;
            if (playResult != null && playResult.Status == OperationStatus.Pending) playResult.Fail(Error);
            Attempt(errors, () => director.Pause());
            Attempt(errors, ReleaseLeases);
            Attempt(errors, () => Presentation.SetFlags(false, false));
            Error = new AggregateException("Cinematic playback failed.", errors).ToString();
        }

        private void UpdateFlags()
        {
            bool visible = Phase == CinematicPhase.Ready || Phase == CinematicPhase.Playing || Phase == CinematicPhase.Holding;
            Presentation.SetFlags(visible, Phase == CinematicPhase.Playing && !presentationPaused);
        }

        private static CinematicArtworkTrack Validate(TimelineAsset timeline, CinematicId id)
        {
            if (timeline == null) throw new InvalidOperationException("Missing Timeline for " + id);
            if (double.IsNaN(timeline.duration) || double.IsInfinity(timeline.duration) || timeline.duration <= 0)
                throw new InvalidOperationException("Expected a finite positive Timeline duration: " + id);
            var tracks = timeline.GetOutputTracks().ToArray();
            if (tracks.Length != 1 || !(tracks[0] is CinematicArtworkTrack track))
                throw new InvalidOperationException("Expected one CinematicArtworkTrack: " + id);
            var clips = track.GetClips().OrderBy(clip => clip.start).ToArray();
            if (clips.Length != 4) throw new InvalidOperationException("Expected four artwork clips: " + id);
            for (int i = 0; i < clips.Length; i++)
            {
                var art = clips[i].asset as CinematicArtworkClip;
                if (art == null || art.Artwork == null || double.IsNaN(clips[i].duration) || clips[i].duration <= 0)
                    throw new InvalidOperationException("Missing artwork or invalid duration at " + id + "/" + (i + 1));
                if (float.IsNaN(art.EndScale) || float.IsInfinity(art.EndScale) || Math.Abs(art.EndScale - 1f) > 0.0001f)
                    throw new InvalidOperationException("Cinematic scale must remain one at " + id + "/" + (i + 1));
                if (Math.Abs(clips[i].start - (i == 0 ? 0 : clips[i-1].end)) > 0.001 || double.IsInfinity(clips[i].duration))
                    throw new InvalidOperationException("Artwork clips must be contiguous and non-overlapping at " + id + "/" + (i + 1));
            }
            if (Math.Abs(clips[clips.Length - 1].end - timeline.duration) > 0.001)
                throw new InvalidOperationException("Timeline duration must match the last artwork.");
            return track;
        }

        private static void RequirePending(OperationResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (result.Status != OperationStatus.Pending) throw new ArgumentException("Provide a fresh OperationResult.", nameof(result));
        }
    }
}
