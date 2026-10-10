using System;
using System.Collections.Generic;
using Cooked.Authoring;
using Cooked.Contracts;
using Cooked.Session;
using UnityEngine;
namespace Cooked.UI
{
    public sealed class TitleUiRoot : MonoBehaviour
    {
        [SerializeField, InspectorLabel("타이틀 화면"), Tooltip("새 게임·설정·종료 버튼이 있는 TitleView를 연결하세요.")]
        private TitleView view;
        private TitleViewModel model;
#if UNITY_EDITOR
        // Temporary Editor field setter for the retained Builder. T12 removes both after T11 reference migration.
        public void Configure(TitleView value) => view = value;
#endif
        public void Bind(IGameFlowService flow, OptionsService options, Action quit)
        {
            if (view == null) throw new InvalidOperationException("TitleUiRoot에 TitleView를 연결하세요.");
            if (flow == null) throw new ArgumentNullException(nameof(flow));
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (quit == null) throw new ArgumentNullException(nameof(quit));
            Unbind();
            try { model = new TitleViewModel(flow, options, quit); view.Bind(model); }
            catch (Exception error) { SessionCleanup.RethrowAfterCleanup(error, Unbind); throw; }
        }
        public void Unbind()
        {
            var previous = model; model = null;
            var errors = new List<Exception>();
            SessionCleanup.Attempt(errors, () => { if (view != null) view.Unbind(); });
            SessionCleanup.Attempt(errors, () => previous?.Dispose());
            SessionCleanup.ThrowIfAny(errors, "Title UI unbind failed.");
        }
        private void OnDestroy() => Unbind();
    }
}
