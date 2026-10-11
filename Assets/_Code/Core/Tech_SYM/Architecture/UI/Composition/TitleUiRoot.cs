using System;
using System.Collections.Generic;
using Cooked.Contracts;
using Cooked.Session;
using UnityEngine;
namespace Cooked.UI
{
    [RequireComponent(typeof(TitleView))]
    public sealed class TitleUiRoot : MonoBehaviour
    {
        private TitleView view;
        private TitleViewModel model;
        public void Bind(IGameFlowService flow, OptionsService options, Action quit)
        {
            if (view == null) view = GetComponent<TitleView>();
            if (view == null) throw new InvalidOperationException("TitleUiRoot와 같은 오브젝트에 TitleView가 필요합니다.");
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
