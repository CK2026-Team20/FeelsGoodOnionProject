using System;
using Cooked.Contracts;
using UnityEngine;
using UnityEngine.Playables;

namespace Cooked.Cinematics
{
    // Composition entry point. InGameCore's composition root calls Initialize with its control service.
    public sealed class CinematicHost : MonoBehaviour
    {
        [SerializeField] private PlayableDirector director;
        [SerializeField] private CinematicFrameBridge frameBridge;
        [SerializeField] private CinematicCatalog catalog;
        [SerializeField] private CinematicView view;
        private CinematicService service;
        private CinematicViewModel viewModel;
        public ICinematicService Service => service ?? throw new InvalidOperationException("Initialize CinematicHost first.");
        public ICinematicService Initialize(IGameplayControlService control)
        {
            if (service != null) throw new InvalidOperationException("CinematicHost is already initialized.");
            if (view == null || !view.IsConfigured) throw new InvalidOperationException("CinematicView serialized bindings are incomplete.");
            service = new CinematicService(director, frameBridge, catalog, control);
            viewModel = new CinematicViewModel(service, service.Presentation);
            view.Bind(viewModel);
            return service;
        }
        private void LateUpdate() { service?.ObservePlayback(); }
        private void OnDisable() { service?.Hide(); }
        public void Shutdown()
        {
            if (view != null) view.Unbind();
            viewModel?.Dispose();
            viewModel = null;
            service?.Dispose();
            service = null;
        }
        private void OnDestroy() { Shutdown(); }
    }
}
