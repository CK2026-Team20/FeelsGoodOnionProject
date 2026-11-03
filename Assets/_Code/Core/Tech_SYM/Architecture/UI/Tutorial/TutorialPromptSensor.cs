using Cooked.Authoring;
using UnityEngine;
namespace Cooked.UI
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class TutorialPromptSensor : MonoBehaviour
    {
        [SerializeField, InspectorLabel("튜토리얼 담당"), Tooltip("같은 기능 객체의 Root에 있는 TutorialPrompt를 연결하세요.")]
        private TutorialPrompt owner;
        private void OnTriggerEnter(Collider other) { if (owner != null) owner.Contact(other); }
        // 준비/일시정지 중 진입한 접촉은 플레이 재개 후에도 정상 수락한다.
        private void OnTriggerStay(Collider other) { if (owner != null) owner.Contact(other); }
    }
}
