using Cooked.Contracts;
using UnityEngine;
namespace Cooked.UI
{
    public sealed class TitleUiRoot : MonoBehaviour
    {
        [Tooltip("새 게임·설정·종료 버튼을 포함한 타이틀 화면입니다.")]
        [SerializeField] private TitleView view; private TitleViewModel model;
        public void Configure(TitleView value) => view = value;
        public void Bind(IGameFlowService flow, OptionsService options)
        { Unbind(); model = new TitleViewModel(flow, options); view.Bind(model); }
        public void Unbind() { if (view != null) view.Unbind(); model?.Dispose(); model = null; }
        private void OnDestroy() => Unbind();
    }
}
