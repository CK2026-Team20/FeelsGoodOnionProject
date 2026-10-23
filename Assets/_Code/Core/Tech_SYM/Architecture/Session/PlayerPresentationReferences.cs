using System;
using Cooked.Authoring;
using UnityEngine;

namespace Cooked.Session
{
    /// <summary>현재 플레이어 몸체가 소유한 외형 루트와 카메라 추적 지점을 제공합니다.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerFacade))]
    public sealed class PlayerPresentationReferences : MonoBehaviour
    {
        [SerializeField, InspectorLabel("외형 루트"), Tooltip("기본·작은 외형을 담은 ActorBody의 직계 자식을 연결합니다. HMS Form과 같은 VisualRoot를 사용합니다.")]
        private Transform visualRoot;
        [SerializeField, InspectorLabel("카메라 지점"), Tooltip("카메라가 따라갈 ActorBody의 별도 직계 자식을 연결합니다.")]
        private Transform cameraAnchor;

        /// <summary>HMS Form과 시선 표현이 사용하는 실제 외형 루트입니다. 소비자는 Bind 전에 연결을 검증합니다.</summary>
        public Transform VisualRoot => visualRoot;
        /// <summary>현재 ActorBody와 함께 움직이는 카메라 추적 지점입니다. 소비자는 Actor 제거 전에 빌린 참조를 해제합니다.</summary>
        public Transform CameraAnchor => cameraAnchor;

        /// <summary>같은 ActorBody의 명시적 참조만 수락합니다. 이름 검색이나 계층·배율·물리 변경은 하지 않습니다.</summary>
        public void ValidateReferences(PlayerFacade source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (source.gameObject != gameObject)
                throw new InvalidOperationException("PlayerPresentationReferences는 같은 ActorBody의 PlayerFacade에 연결해야 합니다.");
            if (visualRoot == null || visualRoot.parent != source.transform)
                throw new InvalidOperationException("외형 루트는 현재 ActorBody의 직계 자식이어야 합니다.");
            if (cameraAnchor == null || cameraAnchor.parent != source.transform || cameraAnchor == visualRoot)
                throw new InvalidOperationException("카메라 지점은 외형 루트와 다른 현재 ActorBody의 직계 자식이어야 합니다.");
            if (visualRoot.GetComponentsInChildren<Collider>(true).Length != 0 ||
                visualRoot.GetComponentsInChildren<Rigidbody>(true).Length != 0)
                throw new InvalidOperationException("외형 루트에는 Collider와 Rigidbody를 배치하지 않습니다. HMS 물리는 ActorBody에 유지합니다.");
        }
    }
}
