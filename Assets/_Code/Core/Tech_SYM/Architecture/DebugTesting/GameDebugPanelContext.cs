#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Cooked.Contracts;
using Cooked.Integration;
using Cooked.Level;
using Cooked.Session;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Cooked.DebugTesting
{
    public sealed class GameDebugPanelContext : IDebugPanelContext, IDisposable
    {
        private readonly Func<GameSessionRuntime> current;
        private GameSessionRuntime bound;
        private bool disposed;
        public GameDebugPanelContext(Func<GameSessionRuntime> current, IGameplayControlService control, IGameFlowService flow)
        { this.current=current ?? throw new ArgumentNullException(nameof(current)); Control=control; Flow=flow; }
        public PlayerBridgeService Player => bound?.Player;
        public GameSessionService Session => bound?.Session;
        public IGameplayControlService Control { get; }
        public IGameFlowService Flow { get; }
        private StageService Stage
        {
            get
            {
                if (bound == null) return null;
                foreach (var stage in bound.Stages.Stages)
                    if (stage != null && stage.StageId == bound.ActiveStageId) return stage;
                return null;
            }
        }
        public Scene WorldScene => Stage != null ? Stage.gameObject.scene : default;
        public Transform StartPoint => Stage != null && Stage.InitialCheckpoint.SpawnPoint != null ? Stage.InitialCheckpoint.SpawnPoint.transform : null;
        public IEnumerable<Transform> Points
        {
            get
            {
                if(Stage==null) yield break;
                foreach(var section in Stage.Sections)
                {
                    if(section.Start.SpawnPoint!=null) yield return section.Start.SpawnPoint.transform;
                    if(section.End.SpawnPoint!=null) yield return section.End.SpawnPoint.transform;
                }
            }
        }
        public bool PointsUseFeet => true;
        public bool IsReady => !disposed && bound != null && Flow.Snapshot.State==FlowState.Playing &&
            !Flow.Snapshot.IsBusy && Player.HasActor && !Player.Actor.IsDead && bound.ActorViewReady &&
            !bound.Dialogue.IsActive && !Control.State.PresentationPaused;
        // Main-game camera transitions remain owned by authored sections.
        public bool CanSelectCamera => false;
        public CameraMovementMode CurrentView => Stage != null ? Stage.CurrentSection.View : default;
        public bool SelectCamera(CameraMovementMode mode) => false;
        public bool Respawn() => IsReady && Flow.Request(new FlowRequest(FlowCommand.RetryCheckpoint));
        public event Action ActorReady;
        public event Action ActorRemoving;
        public void RefreshBinding()
        {
            if(disposed) return;
            var next=current();
            if(ReferenceEquals(next,bound)) return;
            Detach(); bound=next;
            if(bound==null) return;
            bound.ActorReady+=Ready;bound.ActorRemoving+=Removing;
            if(bound.Player.HasActor) Ready();
        }
        private void Ready()=>ActorReady?.Invoke();
        private void Removing()=>ActorRemoving?.Invoke();
        private void Detach()
        {
            if(bound==null) return;
            Removing();bound.ActorReady-=Ready;bound.ActorRemoving-=Removing;bound=null;
        }
        public void Dispose(){if(disposed)return;disposed=true;Detach();ActorReady=null;ActorRemoving=null;}
    }
}
#endif
