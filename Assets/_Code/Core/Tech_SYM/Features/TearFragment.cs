using System;
using UnityEngine;
namespace FeelsGoodOnion.TechSYM.Features
{
    /// <summary>Single pickup and display owner on the actual Trigger sensor; Visual contains no scripts.</summary>
    [DisallowMultipleComponent]
    public sealed class TearFragment : MonoBehaviour
    {
        [Tooltip("획득 후 숨길 이 눈물조각의 Root입니다. 씬 전체 Root를 연결하지 마세요. 센서는 이 Root 아래에 둡니다.")]
        [SerializeField] private GameObject pickupRoot;
        [Tooltip("회전·부유할 이 아이템의 Render/Visual 자식입니다. Collider·Rigidbody·스크립트가 없는 표시 자식을 지정하며 루트와 센서는 움직이지 않습니다.")]
        [SerializeField] private Transform presentationVisual;
        [Tooltip("표시 자식의 월드 Y축 회전 속도입니다(도/초, 0은 회전 없음). 아이템 활성 수명 동안만 진행합니다.")]
        [SerializeField, Min(0)] private float rotationDegreesPerSecond = 90f;
        [Tooltip("표시 자식의 원래 위치에서 이동할 로컬 Y 높이입니다(m). 양수는 위, 음수는 아래, 0은 부유 없음이며 물리 Root는 이동하지 않습니다.")]
        [SerializeField] private float bobHeight = .24f;
        [Tooltip("표시 자식이 원래 높이에서 목표 높이까지 가는 편도 시간입니다(초, 최소 0.02). 같은 시간으로 되돌아오며 월드 정지에는 진행하지 않습니다.")]
        [SerializeField, Min(.02f)] private float bobSeconds = 1.5f;
        private bool collected, initialized, rootWasActive;
        private Collider[] sensors;
        private bool[] sensorDefaults;
        private Transform[] visuals;
        private Vector3[] visualPositions, visualScales;
        private Quaternion[] visualRotations;
        private bool[] visualActive;
        private Renderer[] renderers;
        private bool[] rendererDefaults;
        private Vector3 displayOrigin;
        private float displayElapsed;
        private long attemptRevision;
        public bool IsCollected => collected;
        public void Configure(GameObject itemRoot, Transform childVisual, float rotationDegreesPerSecond = 90f,
            float bobHeight = .24f, float bobSeconds = 1.5f)
        {
            if (Application.isPlaying && initialized) throw new InvalidOperationException("Configure pickup before source initialization.");
            pickupRoot = itemRoot; presentationVisual = childVisual; this.rotationDegreesPerSecond = rotationDegreesPerSecond;
            this.bobHeight = bobHeight; this.bobSeconds = bobSeconds; initialized = false;
        }
        // May be called while inactive. Never use an uninitialized cache to silently skip restoration.
        public void PrepareSource()
        {
            if (initialized) return;
            var sensor = GetComponent<Collider>();
            if (sensor == null || !sensor.isTrigger || pickupRoot == null ||
                (pickupRoot != gameObject && !transform.IsChildOf(pickupRoot.transform)))
                throw new InvalidOperationException("Pickup requires its same-object Trigger and item Root.");
            if (!float.IsFinite(rotationDegreesPerSecond) || rotationDegreesPerSecond < 0 || !float.IsFinite(bobHeight) ||
                !float.IsFinite(bobSeconds) || bobSeconds < .02f)
                throw new InvalidOperationException("Pickup display settings must be finite with nonnegative rotation and >=.02s bob.");
            if (presentationVisual == null || presentationVisual == pickupRoot.transform || !presentationVisual.IsChildOf(pickupRoot.transform) ||
                presentationVisual.GetComponentsInChildren<Collider>(true).Length != 0 ||
                presentationVisual.GetComponentsInChildren<Rigidbody>(true).Length != 0 ||
                presentationVisual.GetComponentsInChildren<MonoBehaviour>(true).Length != 0)
                throw new InvalidOperationException("Pickup display must be an explicit physics-free child Visual without scripts.");
            rootWasActive = pickupRoot.activeSelf;
            sensors = pickupRoot.GetComponentsInChildren<Collider>(true); sensorDefaults = new bool[sensors.Length];
            for (int i = 0; i < sensors.Length; i++) sensorDefaults[i] = sensors[i].enabled;
            visuals = pickupRoot.GetComponentsInChildren<Transform>(true);
            visualPositions = new Vector3[visuals.Length]; visualRotations = new Quaternion[visuals.Length];
            visualScales = new Vector3[visuals.Length]; visualActive = new bool[visuals.Length];
            for (int i = 0; i < visuals.Length; i++)
            {
                visualPositions[i] = visuals[i].localPosition; visualRotations[i] = visuals[i].localRotation;
                visualScales[i] = visuals[i].localScale; visualActive[i] = visuals[i].gameObject.activeSelf;
            }
            renderers = pickupRoot.GetComponentsInChildren<Renderer>(true); rendererDefaults = new bool[renderers.Length];
            for (int i = 0; i < renderers.Length; i++) rendererDefaults[i] = renderers[i].enabled;
            displayOrigin = presentationVisual.localPosition; displayElapsed = 0; initialized = true;
        }
        private void Awake() => PrepareSource();
        private void Update()
        {
            if (!initialized || collected || presentationVisual == null) return;
            displayElapsed = (displayElapsed + Time.deltaTime) % (2f * bobSeconds);
            presentationVisual.localPosition = displayOrigin + Vector3.up * (bobHeight * .5f * (1f - Mathf.Cos(Mathf.PI * displayElapsed / bobSeconds)));
            if (rotationDegreesPerSecond > 0) presentationVisual.Rotate(Vector3.up, rotationDegreesPerSecond * Time.deltaTime, Space.World);
        }
        private void OnTriggerEnter(Collider other) => TryCollect(other);
        private void OnTriggerStay(Collider other) => TryCollect(other);
        public void ResetForRetry()
        {
            PrepareSource(); attemptRevision++; collected = false; displayElapsed = 0;
            for (int i = 0; i < visuals.Length; i++) if (visuals[i] != null)
            {
                visuals[i].localPosition = visualPositions[i]; visuals[i].localRotation = visualRotations[i]; visuals[i].localScale = visualScales[i];
                if (visuals[i].gameObject != pickupRoot) visuals[i].gameObject.SetActive(visualActive[i]);
            }
            for (int i = 0; i < sensors.Length; i++) if (sensors[i] != null) sensors[i].enabled = sensorDefaults[i];
            for (int i = 0; i < renderers.Length; i++) if (renderers[i] != null) renderers[i].enabled = rendererDefaults[i];
            pickupRoot.SetActive(rootWasActive);
        }
        public void Restore() => ResetForRetry();
        private void TryCollect(Collider other)
        {
            if (!isActiveAndEnabled || collected || other == null || pickupRoot == null) return;
            PrepareSource();
            var owner = other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject;
            if (!owner.TryGetComponent<PlayerFacade>(out var player) || !player.isActiveAndEnabled || player.IsDead) return;
            collected = true;
            long revision = attemptRevision;
            int added = player.AddSkillFragments(1);
            if (revision != attemptRevision) return; // A synchronous retry reset owns the new attempt's presentation.
            if (added != 1) { collected = false; return; }
            foreach (var sensor in sensors) if (sensor != null) sensor.enabled = false;
            pickupRoot.SetActive(false);
        }
    }
}
