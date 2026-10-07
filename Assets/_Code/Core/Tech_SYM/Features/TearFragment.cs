using UnityEngine;

namespace FeelsGoodOnion.TechSYM.Features
{
    /// <summary>활성 PlayerFacade의 Trigger 접촉을 조각 획득으로 처리한다.</summary>
    [DisallowMultipleComponent]
    public sealed class TearFragment : MonoBehaviour
    {
        [Tooltip("획득 후 비활성화할 이 눈물조각의 Root. 씬 전체 루트가 아닙니다.")]
        [SerializeField] private GameObject pickupRoot;
        private bool collected;
        private Collider[] sensors;
        private bool[] sensorDefaults;
        private Transform[] visuals;
        private Vector3[] visualPositions;
        private Quaternion[] visualRotations;
        private Vector3[] visualScales;
        private bool[] visualActive;
        private Renderer[] renderers;
        private bool[] rendererDefaults;
        private bool rootWasActive;
        public bool IsCollected => collected;



        private void Awake()
        {
            var sensor = GetComponent<Collider>();
            if (pickupRoot != null)
            {
                rootWasActive = pickupRoot.activeSelf;
                sensors = pickupRoot.GetComponentsInChildren<Collider>(true);
                sensorDefaults = new bool[sensors.Length];
                for (int i = 0; i < sensors.Length; i++) sensorDefaults[i] = sensors[i].enabled;
                visuals = pickupRoot.GetComponentsInChildren<Transform>(true);
                visualPositions = new Vector3[visuals.Length];
                visualRotations = new Quaternion[visuals.Length];
                visualScales = new Vector3[visuals.Length];
                visualActive = new bool[visuals.Length];
                for (int i = 0; i < visuals.Length; i++)
                {
                    visualPositions[i] = visuals[i].localPosition; visualRotations[i] = visuals[i].localRotation;
                    visualScales[i] = visuals[i].localScale; visualActive[i] = visuals[i].gameObject.activeSelf;
                }
                renderers = pickupRoot.GetComponentsInChildren<Renderer>(true);
                rendererDefaults = new bool[renderers.Length];
                for (int i = 0; i < renderers.Length; i++) rendererDefaults[i] = renderers[i].enabled;
            }
            if (sensor == null || !sensor.isTrigger || pickupRoot == null
                || (pickupRoot != gameObject && !transform.IsChildOf(pickupRoot.transform)))
            {
                Debug.LogError("눈물조각에는 같은 객체의 Trigger Collider와 해당 아이템의 Root 참조가 필요합니다.", this);
                enabled = false;
            }
        }

        private void OnTriggerEnter(Collider other) => TryCollect(other);
        private void OnTriggerStay(Collider other) => TryCollect(other);

        /// <summary>소비 플래그·원래 표시 배치·Collider·활성 상태를 복원한다.</summary>
        public void ResetForRetry()
        {
            if (pickupRoot == null || sensors == null) return;
            collected = false;
            for (int i = 0; i < visuals.Length; i++) if (visuals[i] != null)
            {
                visuals[i].localPosition = visualPositions[i]; visuals[i].localRotation = visualRotations[i];
                visuals[i].localScale = visualScales[i];
                if (visuals[i].gameObject != pickupRoot) visuals[i].gameObject.SetActive(visualActive[i]);
            }
            for (int i = 0; i < sensors.Length; i++) if (sensors[i] != null) sensors[i].enabled = sensorDefaults[i];
            for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].enabled = rendererDefaults[i];
            foreach (var rotation in pickupRoot.GetComponentsInChildren<ObjectRotation>(true)) rotation.ResetForRetry();
            foreach (var bob in pickupRoot.GetComponentsInChildren<Platforms.FloatingMotion>(true)) bob.ResetForRetry();
            pickupRoot.SetActive(rootWasActive);
        }
        public void Restore() => ResetForRetry();

        private void TryCollect(Collider other)
        {
            if (!isActiveAndEnabled || collected || other == null || pickupRoot == null) return;
            GameObject owner = other.attachedRigidbody != null
                ? other.attachedRigidbody.gameObject : other.gameObject;
            if (!owner.TryGetComponent<PlayerFacade>(out var player) || !player.isActiveAndEnabled || player.IsDead) return;
            // Reserve before the model event to prevent reentrant duplicate pickup.
            collected = true;
            if (player.AddSkillFragments(1) != 1) { collected = false; return; }
            foreach (var sensor in sensors) if (sensor != null) sensor.enabled = false;
            pickupRoot.SetActive(false);
        }
    }
}
