using Cooked.Authoring;
using DG.Tweening;
using UnityEngine;
namespace Cooked.UI
{
    /// <summary>View는 VM 상태와 순수 표시만 소유한다. 재바인딩은 최초 등장으로 취급하지 않는다.</summary>
    public sealed class TutorialPromptView : MonoBehaviour
    {
        [SerializeField, InspectorLabel("월드 캔버스"), Tooltip("튜토리얼 이미지를 표시할 자식 World Space Canvas를 연결하세요.")]
        private Canvas canvas;
        [SerializeField, InspectorLabel("등장 몸체"), Tooltip("등장 확대를 적용할 자식 RectTransform을 연결하세요.")]
        private RectTransform body;
        [SerializeField, InspectorLabel("스프라이트 재생"), Tooltip("40프레임 간격의 Image 스프라이트 클립을 재생할 Animator를 연결하세요.")]
        private Animator animator;
        [SerializeField, Min(0), InspectorLabel("등장 시간"), Tooltip("최초 확대 등장 시간입니다(초). 0은 즉시 표시합니다.")]
        private float revealSeconds = .3f;
        private TutorialPromptViewModel model;
        private Camera output;
        private Tween reveal;
        private bool subscribed, visible;
        public void Bind(TutorialPromptViewModel value, Camera camera)
        {
            Unbind();
            if (canvas == null || body == null || animator == null) throw new System.InvalidOperationException("튜토리얼 View 연결이 없습니다.");
            model = value ?? throw new System.ArgumentNullException(nameof(value));
            output = camera;
            visible = model.Visible;
            Apply(false);
            Subscribe();
        }
        public void Unbind()
        {
            Unsubscribe(); reveal?.Kill(false); reveal = null;
            model = null; output = null;
        }
        private void Subscribe()
        { if (model != null && isActiveAndEnabled && !subscribed) { model.Changed += OnChanged; subscribed = true; } }
        private void Unsubscribe()
        { if (subscribed && model != null) model.Changed -= OnChanged; subscribed = false; }
        private void OnChanged() => Apply(true);
        private void Apply(bool allowReveal)
        {
            bool next = model.Visible;
            bool first = next && !visible && allowReveal;
            visible = next;
            canvas.enabled = next;
            animator.enabled = next;
            animator.speed = model.Paused ? 0 : 1;
            if (first && revealSeconds > 0)
            {
                reveal?.Kill(false);
                body.localScale = Vector3.zero;
                reveal = body.DOScale(Vector3.one, revealSeconds).SetEase(Ease.OutBack).SetUpdate(true);
            }
            else if (reveal == null || !reveal.IsActive()) body.localScale = Vector3.one;
            if (reveal != null) { if (model.Paused) reveal.Pause(); else reveal.Play(); }
        }
        private void OnEnable() { if (model != null) { visible = model.Visible; Apply(false); Subscribe(); } }
        private void OnDisable()
        { Unsubscribe(); reveal?.Kill(false); reveal = null; if (body != null) body.localScale = Vector3.one; }
        private void OnDestroy() => Unbind();
        private void LateUpdate()
        { if (visible && output != null && canvas != null) canvas.transform.rotation = output.transform.rotation; }
    }
}
