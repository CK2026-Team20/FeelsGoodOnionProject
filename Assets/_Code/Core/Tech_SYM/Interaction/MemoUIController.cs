using System;
using System.Collections.Generic;
using Cooked.Contracts;
using Cooked.Session;
using Cooked.Authoring;
using UnityEngine;
namespace FeelsGoodOnion.TechSYM.Interaction
{
    /// <summary>씬 안의 화면 메모 생성·연결·해제 담당. 월드 프롬프트와는 무관하다.</summary>
    public sealed class MemoUIController : MonoBehaviour, IInteractionOverlay
    {
        [Tooltip("메모 본문을 생성할 스크린 공간 Canvas 안의 RectTransform입니다. World Space Canvas는 사용할 수 없습니다.")]
        [SerializeField, InspectorLabel("화면 부모")] private RectTransform screenRoot;
        [Tooltip("메모 본문 화면 프리팹입니다. 이미지·크기 표현을 연결하며 마우스 닫기 버튼 없이 F로만 닫습니다.")]
        [SerializeField, InspectorLabel("메모 화면")] private MemoView viewPrefab;
        private readonly Dictionary<MemoModel, MemoObject> owners = new Dictionary<MemoModel, MemoObject>();
        private MemoView view;
        private MemoViewModel model;
        private IGameplayControlService control;
        private IDisposable inputLease, worldLease;
        private MemoObject currentOwner;
        private bool releasing;
        private int suppressInputThroughFrame = -1;
        public bool IsOpen => model != null;
        public RectTransform ScreenRoot => screenRoot;
        public MemoView ViewPrefab => viewPrefab;
        public void ConfigureScreen(RectTransform root, MemoView prefab)
        {
            if (IsOpen) throw new InvalidOperationException("열린 메모 화면의 구성을 바꿀 수 없습니다.");
            screenRoot = root;
            viewPrefab = prefab;
        }
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
            // 옵션이 닫히는 프레임의 F/클릭이 아래 메모까지 이어지지 않는다.
            suppressInputThroughFrame = Time.frameCount;
            model?.SetSuspended(state.PresentationPaused);
        }
        public bool Register(MemoObject owner)
        {
            if (owner == null) return false;
            MemoModel data = owner.Data;
            if (data == null || data.MemoImage == null)
            {
                Debug.LogError("MemoObject에는 실제 Sprite가 연결된 MemoModel 원본이 필요합니다.", owner);
                return false;
            }
            if (owners.TryGetValue(data, out var existing) && existing != null && existing != owner)
            {
                Debug.LogError("같은 MemoModel 원본을 서로 다른 MemoObject가 등록할 수 없습니다: " + data.name, owner);
                return false;
            }
            // An authored Model can change while this component is enabled. Remove this
            // owner's previous reference instead of retaining an orphaned registration.
            var previousSources = new List<MemoModel>();
            foreach (var entry in owners)
                if (entry.Value == owner && entry.Key != data) previousSources.Add(entry.Key);
            foreach (var source in previousSources) owners.Remove(source);
            owners[data] = owner;
            return true;
        }
        public void Unregister(MemoObject owner)
        {
            if (owner == null) return;
            var ownedSources = new List<MemoModel>();
            foreach (var entry in owners)
                if (entry.Value == owner) ownedSources.Add(entry.Key);
            foreach (var source in ownedSources) owners.Remove(source);
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
            if (control.State.GameplayBlocked || control.State.WorldPaused || control.State.PresentationPaused) return false;
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
        // F 라우터는 게임 입력 차단 검사보다 먼저 이 명령을 호출하고 해당 F를 소비한다.
        public void RequestClose()
        {
            if (Time.frameCount > suppressInputThroughFrame) model?.RequestClose();
        }
        public void ForceClose() => ReleaseView();
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
