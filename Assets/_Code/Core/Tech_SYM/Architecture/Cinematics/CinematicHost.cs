using System;
using Cooked.Contracts;
using UnityEngine;
using UnityEngine.Playables;

namespace Cooked.Cinematics
{
    // Composition entry point. InGameCore's composition root calls Initialize with its control service.
    public sealed class CinematicHost : MonoBehaviour
    {
        [Tooltip("오프닝·엔딩 Timeline을 재생하는 PlayableDirector입니다. 현재 컷신 오브젝트의 재생 담당을 연결하세요.")]
        [SerializeField] private PlayableDirector director;
        [Tooltip("타임라인의 원화를 화면에 전달하는 연결 컴포넌트입니다. 같은 컷신의 CinematicFrameBridge를 연결하세요.")]
        [SerializeField] private CinematicFrameBridge frameBridge;
        [Tooltip("오프닝·엔딩 Timeline을 지정한 목록 자산입니다. 게임 흐름에 사용할 두 연출을 이 자산에서 선택합니다.")]
        [SerializeField] private CinematicCatalog catalog;
        [Tooltip("전체 화면 원화와 스킵 버튼을 표시하는 화면입니다. 이 컷신과 함께 사용할 CinematicView를 연결하세요.")]
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
