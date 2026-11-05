#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using Cooked.Contracts;
using Cooked.Level;
using Cooked.Session;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Cooked.DebugTesting
{
    // Borrowed gameplay boundary; the panel never owns a player or session.
    public interface IDebugPanelContext
    {
        PlayerBridgeService Player { get; }
        GameSessionService Session { get; }
        IGameplayControlService Control { get; }
        IGameFlowService Flow { get; }
        Scene WorldScene { get; }
        Transform StartPoint { get; }
        IEnumerable<Transform> Points { get; }
        bool PointsUseFeet { get; }
        bool IsReady { get; }
        bool CanSelectCamera { get; }
        CameraMovementMode CurrentView { get; }
        bool SelectCamera(CameraMovementMode mode);
        bool Respawn();
        void RefreshBinding();
        event Action ActorReady;
        event Action ActorRemoving;
    }
}
#endif
