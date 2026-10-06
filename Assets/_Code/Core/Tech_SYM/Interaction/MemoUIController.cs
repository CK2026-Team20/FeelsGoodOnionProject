using System;
using System.Collections.Generic;
using Cooked.Contracts;
using Cooked.Session;
using UnityEngine;
namespace FeelsGoodOnion.TechSYM.Interaction
{
    /// <summary>씬 안의 화면 메모 생성·연결·해제 담당. 월드 프롬프트와는 무관하다.</summary>
    public sealed class MemoUIController : MonoBehaviour, IInteractionOverlay
    {
        [Tooltip("메모 열기 전에 입력 가능한 상태인지 확인할 플레이어 참조입니다. 비워 두면 이 추가 검사를 생략합니다. 메모 표시 중 입력·월드 정지는 별도로 주입된 제어 서비스가 관리합니다.")]
        [SerializeField] private PlayerFacade player;
        [Tooltip("메모 본문을 생성할 스크린 공간 Canvas 안의 RectTransform입니다. World Space Canvas는 사용할 수 없습니다.")]
        [SerializeField] private RectTransform screenRoot;
        [Tooltip("메모 본문 화면 프리팹입니다. 이미지·크기 표현을 연결하며 마우스 닫기 버튼 없이 E로만 닫습니다.")]
        [SerializeField] private MemoView viewPrefab;
        private readonly Dictionary<int, MemoObject> owners = new Dictionary<int, MemoObject>();
        private MemoView view;
        private MemoViewModel model;
        private IGameplayControlService control;
        private IDisposable inputLease, worldLease;
        private MemoObject currentOwner;
        private bool releasing;
        private int suppressInputThroughFrame = -1;
        public bool IsOpen => model != null;
        // 조립 지점은 세션의 공유 서비스를 주입하고 세션 종료 전에 Unbind한다.
        // Facade의 bool 차단으로 되돌아가면 다른 모달의 소유권을 침해한다.
        public void Initialize(IGameplayControlService gameplayControl)
        {
            if (gameplayControl == null) throw new ArgumentNullException(nameof(gameplayControl));
            Unbind();
            control = gameplayControl;
            control.Changed += OnControlChanged;
        }
        public void Unbind()
        {
            var previous = control;
            control = null;
            if (previous != null) previous.Changed -= OnControlChanged;
            ReleaseView();
        }
        private void OnControlChanged(ControlState state)
        {
            // 옵션이 닫히는 프레임의 E/클릭이 아래 메모까지 이어지지 않는다.
            suppressInputThroughFrame = Time.frameCount;
            model?.SetSuspended(state.PresentationPaused);
        }
        public bool Register(MemoObject owner)
        {
            if (owner == null) return false;
            MemoModel data = owner.Data;
            if (data == null || data.MemoID <= 0 || data.MemoImage == null)
            {
                Debug.LogError("MemoObject에는 고유 양수 PK와 Sprite가 설정된 MemoModel이 필요합니다.", owner);
                return false;
            }
            if (owners.TryGetValue(data.MemoID, out var existing) && existing != null && existing != owner)
            {
                Debug.LogError($"MemoID {data.MemoID}: 다른 MemoObject와 모델/PK를 공유할 수 없습니다.", owner);
                return false;
            }
            owners[data.MemoID] = owner;
            return true;
        }
        public void Unregister(MemoObject owner)
        {
            if (owner == null) return;
            if (owner.Data != null && owners.TryGetValue(owner.Data.MemoID, out var existing) && existing == owner)
                owners.Remove(owner.Data.MemoID);
            if (currentOwner == owner) ReleaseView();
        }
        public bool TryOpen(MemoObject owner)
        {
            if (!isActiveAndEnabled || releasing || IsOpen || Time.frameCount <= suppressInputThroughFrame ||
                owner == null || !owner.isActiveAndEnabled) return false;
            if (control == null || screenRoot == null || viewPrefab == null)
            {
                Debug.LogError("MemoUIController에 공유 GameplayControlService, 화면 Canvas와 MemoView 프리팹을 연결하세요.", this);
                return false;
            }
            var canvas = screenRoot.GetComponentInParent<Canvas>();
            if (canvas == null || canvas.renderMode == RenderMode.WorldSpace)
            {
                Debug.LogError("메모 본문은 월드 프롬프트와 별도 스크린 공간 Canvas가 필요합니다.", this);
                return false;
            }
            if (control.State.GameplayBlocked || control.State.WorldPaused || control.State.PresentationPaused ||
                (player != null && !player.CanReceiveInput)) return false;
            if (!Register(owner)) return false;
            try
            {
                model = new MemoViewModel(owner.Data);
                currentOwner = owner;
                model.StateChanged += OnStateChanged;
                inputLease = control.BlockGameplay("Memo");
                if (model == null || !isActiveAndEnabled) throw new OperationCanceledException("메모 열기 중 수명이 종료되었습니다.");
                worldLease = control.PauseWorld("Memo");
                if (model == null || !isActiveAndEnabled) throw new OperationCanceledException("메모 열기 중 수명이 종료되었습니다.");
                view = Instantiate(viewPrefab, screenRoot, false);
                view.Bind(model);
                if (!view.isActiveAndEnabled) throw new InvalidOperationException("메모 본문 View는 활성 상태여야 합니다.");
                return true;
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
                try { ReleaseView(); }
                catch (Exception cleanup) { Debug.LogException(cleanup, this); }
                return false;
            }
        }
        // E 라우터는 게임 입력 차단 검사보다 먼저 이 명령을 호출하고 해당 E를 소비한다.
        public void RequestClose()
        {
            if (Time.frameCount > suppressInputThroughFrame) model?.RequestClose();
        }
        private void OnStateChanged(MemoDisplayState state)
        {
            if (state == MemoDisplayState.Closed) ReleaseView();
        }
        private void ReleaseView()
        {
            if (releasing) return;
            releasing = true;
            var previousModel = model;
            var previousView = view;
            var previousInput = inputLease;
            var previousWorld = worldLease;
            model = null; view = null; currentOwner = null; inputLease = null; worldLease = null;
            suppressInputThroughFrame = Time.frameCount;
            var errors = new List<Exception>();
            try
            {
                if (previousModel != null) previousModel.StateChanged -= OnStateChanged;
                if (previousView != null)
                {
                    SessionCleanup.Attempt(errors, previousView.Unbind);
                    SessionCleanup.Attempt(errors, () => Destroy(previousView.gameObject));
                }
                if (previousModel != null) SessionCleanup.Attempt(errors, previousModel.Dispose);
                if (previousInput != null) SessionCleanup.Attempt(errors, previousInput.Dispose);
                if (previousWorld != null) SessionCleanup.Attempt(errors, previousWorld.Dispose);
            }
            finally { releasing = false; }
            SessionCleanup.ThrowIfAny(errors, "메모 종료 중 정리 실패.");
        }
        private void OnDisable() => Unbind();
        private void OnDestroy() => Unbind();
    }
}
