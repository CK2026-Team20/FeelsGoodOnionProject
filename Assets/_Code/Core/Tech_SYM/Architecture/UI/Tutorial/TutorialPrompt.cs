using Cooked.Authoring;
using Cooked.Contracts;
using UnityEngine;
namespace Cooked.UI
{
    /// <summary>빈 Root의 접촉 조정자. 현재 Actor와 게임 상태는 Core가 주입한다.</summary>
    public sealed class TutorialPrompt : MonoBehaviour
    {
        [SerializeField, InspectorLabel("표시 담당"), Tooltip("같은 기능 객체의 TutorialPromptView를 연결하세요.")]
        private TutorialPromptView view;
        private readonly object identity = new object();
        private PlayerFacade actor;
        private IGameFlowService flow;
        private TutorialPromptViewModel model;
        public void Bind(TutorialProgress progress, PlayerFacade actor, IGameFlowService flow,
            IGameplayControlService control, Camera camera)
        {
            Unbind();
            if (view == null) throw new System.InvalidOperationException("튜토리얼 표시 담당을 연결하세요.");
            this.actor = actor; this.flow = flow;
            model = new TutorialPromptViewModel(progress, identity, control);
            try { view.Bind(model, camera); }
            catch { Unbind(); throw; }
        }
        public void Contact(Collider other)
        {
            if (model == null || actor == null || actor.IsDead || flow.Snapshot.State != FlowState.Playing ||
                flow.Snapshot.IsBusy || other.GetComponentInParent<PlayerFacade>() != actor) return;
            model.TryReveal();
        }
        public void Unbind()
        {
            if (view != null) view.Unbind();
            model?.Dispose(); model = null; actor = null; flow = null;
        }
        private void OnDestroy() => Unbind();
    }
}
