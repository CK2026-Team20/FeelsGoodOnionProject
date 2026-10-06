using System;
using DG.Tweening;
using UnityEngine;
namespace FeelsGoodOnion.TechSYM.Interaction
{
    /// <summary>자신의 트윈만 관리한다. 활성/파괴 정책은 호출자가 결정한다.</summary>
    [DisallowMultipleComponent]
    public sealed class UIScaleTween : MonoBehaviour
    {
        [Tooltip("UI 열기·닫기 크기 변화 시간입니다(초, 인스펙터 최소 0.01). 높이면 더 천천히 바뀝니다. 월드 정지 중에도 진행하며 옵션이 표시를 정지하면 멈춥니다.")]
        [SerializeField, Min(0.01f)] private float duration = 0.18f;
        private Vector3 restScale;
        private bool initialized;
        private Tween tween;
        private void Initialize()
        {
            if (initialized) return;
            restScale = transform.localScale;
            initialized = true;
        }
        public void Show(Action completed = null)
        {
            Initialize();
            Stop();
            transform.localScale = Vector3.zero;
            tween = transform.DOScale(restScale, duration).SetEase(Ease.OutBack).SetUpdate(true)
                .OnComplete(() => { tween = null; completed?.Invoke(); });
        }
        public void Hide(Action completed)
        {
            Initialize();
            Stop();
            tween = transform.DOScale(Vector3.zero, duration).SetEase(Ease.InQuad).SetUpdate(true)
                .OnComplete(() => { tween = null; completed?.Invoke(); });
        }
        public void Stop()
        {
            tween?.Kill(false);
            tween = null;
        }
        public void SetPaused(bool paused)
        {
            if (paused) tween?.Pause();
            else tween?.Play();
        }
        private void OnDisable()
        {
            Stop();
            if (initialized) transform.localScale = restScale;
        }
        private void OnDestroy() => Stop();
    }
}
