#if UNITY_EDITOR
using System;
using Cooked.Authoring;
using Cooked.Contracts;
using UnityEngine;
namespace Cooked.DebugTesting
{
    [DisallowMultipleComponent]
    public sealed class DirectSceneTestSettings : MonoBehaviour
    {
        [SerializeField, InspectorLabel("플레이어 시작점"), Tooltip("플레이어 Capsule 중심을 맞출 현재 씬의 Transform입니다.")]
        private Transform startPoint;
        [SerializeField, InspectorLabel("플레이어 프리팹"), Tooltip("비활성 PlayerRoot 프리팹입니다. 비우면 프로젝트 기본 플레이어를 사용합니다.")]
        private GameObject playerPrefab;
        [SerializeField, InspectorLabel("초기 조각"), Tooltip("생성과 재생성 때 제공할 조각 수입니다."), Range(0, 5)]
        private int initialFragments;
        [SerializeField, InspectorLabel("초기 능력"), Tooltip("생성과 재생성 때 해금할 능력입니다.")]
        private AbilityMask initialAbilities;
        [SerializeField, InspectorLabel("추가 이동 지점"), Tooltip("이동 버튼에서 선택할 현재 씬의 실제 Transform 목록입니다.")]
        private Transform[] additionalPoints = Array.Empty<Transform>();
        [SerializeField, Min(1), InspectorLabel("보관할 로그 수"), Tooltip("최근 로그를 보관할 최대 개수입니다. 기본값은 200이며 오래된 항목부터 제거합니다.")]
        private int logCapacity = 200;
        public int LogCapacity => Mathf.Max(1, logCapacity);
        public Transform StartPoint => startPoint;
        public GameObject PlayerPrefab => playerPrefab;
        public int InitialFragments => initialFragments;
        public AbilityMask InitialAbilities => initialAbilities;
        public System.Collections.Generic.IReadOnlyList<Transform> AdditionalPoints => additionalPoints;
        public void SetStartPoint(Transform value) => startPoint = value;
    }
}
#endif
