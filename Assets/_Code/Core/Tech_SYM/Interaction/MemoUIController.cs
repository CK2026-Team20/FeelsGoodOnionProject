using System.Collections.Generic;
using UnityEngine;
namespace FeelsGoodOnion.TechSYM.Interaction
{
    /// <summary>씬 안의 화면 메모 생성·연결·해제 담당. 월드 프롬프트와는 무관하다.</summary>
    public sealed class MemoUIController : MonoBehaviour, IInteractionOverlay
    {
        [Tooltip("메모 표시 중 이동·점프 입력을 제한할 플레이어입니다.")]
        [SerializeField] private PlayerFacade player;
        [SerializeField] private RectTransform screenRoot;
        [SerializeField] private MemoView viewPrefab;
        private readonly Dictionary<int, MemoObject> owners = new Dictionary<int, MemoObject>();
        private MemoView view;
        private MemoViewModel model;
        // 단일 흐름용 Facade 계약이다. 다른 요청자의 동시 차단은 지원하지 않는다.
        private bool ownsInputBlock;
        public bool IsOpen => model != null;
        public bool Register(MemoObject owner)
        {
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
            if (owner.Data != null && owners.TryGetValue(owner.Data.MemoID, out var existing) && existing == owner)
                owners.Remove(owner.Data.MemoID);
        }
        public bool TryOpen(MemoObject owner)
        {
            if (!isActiveAndEnabled || IsOpen || owner == null || !owner.isActiveAndEnabled) return false;
            if (player == null || screenRoot == null || viewPrefab == null)
            {
                Debug.LogError("MemoUIController에 PlayerFacade, 화면 Canvas와 MemoView 프리팹을 연결하세요.", this);
                return false;
            }
            if (!player.CanReceiveInput) return false;
            if (!Register(owner)) return false;
            try
            {
                model = new MemoViewModel(owner.Data);
                model.StateChanged += OnStateChanged;
                player.SetInputBlocked(true);
                ownsInputBlock = true;
                view = Instantiate(viewPrefab, screenRoot, false);
                view.Bind(model);
                return true;
            }
            catch (System.Exception exception)
            {
                ReleaseView();
                Debug.LogException(exception, this);
                return false;
            }
        }
        public void RequestClose() => model?.RequestClose();
        private void OnStateChanged(MemoDisplayState state)
        {
            if (state == MemoDisplayState.Closed) ReleaseView();
        }
        private void ReleaseView()
        {
            if (model != null) model.StateChanged -= OnStateChanged;
            if (view != null)
            {
                view.Unbind();
                Destroy(view.gameObject);
            }
            view = null;
            model = null;
            if (ownsInputBlock)
            {
                ownsInputBlock = false;
                if (player != null) player.SetInputBlocked(false);
            }
        }
        private void OnDisable() => ReleaseView();
        private void OnDestroy() => ReleaseView();
    }
}
