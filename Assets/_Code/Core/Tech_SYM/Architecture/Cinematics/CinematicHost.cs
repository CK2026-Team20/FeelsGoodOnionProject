using System;
using System.Collections.Generic;
using Cooked.Contracts;
using Cooked.Authoring;
using UnityEngine;

namespace Cooked.Cinematics
{
    // Composition supplies authored references; never creates a runtime View or Timeline.
    public sealed class CinematicHost : MonoBehaviour
    {
        [Tooltip("오프닝·엔딩 컷을 표시 순서대로 지정한 CinematicCatalog 자산입니다. 기존 원화·하단 문구·표시 시간(초)을 각 컷에 연결하세요.")]
        [SerializeField, InspectorLabel("연출 목록")] private CinematicCatalog catalog;
        [Tooltip("원화 한 장·하단 문구·전체 Skip 버튼을 표시하는 CinematicView입니다. 이 연출의 화면을 연결하세요.")]
        [SerializeField, InspectorLabel("연출 화면")] private CinematicView view;
        private CinematicService service;
        private CinematicViewModel viewModel;
        private bool changingLifetime;
        public ICinematicService Service => service ?? throw new InvalidOperationException("Initialize CinematicHost first.");

        public ICinematicService Initialize(IGameplayControlService control)
        {
            if (changingLifetime || service != null) throw new InvalidOperationException("CinematicHost is already initialized or changing lifetime.");
            if (view == null || !view.IsConfigured) throw new InvalidOperationException("CinematicView serialized bindings are incomplete.");
            changingLifetime = true;
            try
            {
                service = new CinematicService(catalog, control);
                viewModel = new CinematicViewModel(service);
                view.Bind(viewModel);
                return service;
            }
            catch (Exception error)
            {
                var errors = new List<Exception> { error };
                Cleanup(errors);
                throw new AggregateException("CinematicHost initialization failed.", errors);
            }
            finally { changingLifetime = false; }
        }
        private void LateUpdate() { service?.Tick(Time.unscaledDeltaTime); }
        private void OnDisable() { service?.Hide(); }
        public void Shutdown()
        {
            if (changingLifetime) return;
            changingLifetime = true;
            var errors = new List<Exception>();
            try { Cleanup(errors); }
            finally { changingLifetime = false; }
            if (errors.Count > 0) throw new AggregateException("CinematicHost shutdown failed.", errors);
        }
        private void Cleanup(List<Exception> errors)
        {
            var oldViewModel = viewModel;
            var oldService = service;
            viewModel = null;
            service = null;
            try { if (view != null) view.Unbind(); } catch (Exception error) { errors.Add(error); }
            try { oldViewModel?.Dispose(); } catch (Exception error) { errors.Add(error); }
            try { oldService?.Dispose(); } catch (Exception error) { errors.Add(error); }
        }
        private void OnDestroy() { Shutdown(); }
    }
}
