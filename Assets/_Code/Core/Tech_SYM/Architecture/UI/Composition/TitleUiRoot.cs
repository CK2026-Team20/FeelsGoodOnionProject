using Cooked.Contracts;
using UnityEngine;
namespace Cooked.UI
{
    public sealed class TitleUiRoot : MonoBehaviour
    {
        [SerializeField] private TitleView view; private TitleViewModel model;
        public void Configure(TitleView value) => view = value;
        public void Bind(IGameFlowService flow, OptionsService options)
        { Unbind(); model = new TitleViewModel(flow, options); view.Bind(model); }
        public void Unbind() { if (view != null) view.Unbind(); model?.Dispose(); model = null; }
        private void OnDestroy() => Unbind();
    }
}
