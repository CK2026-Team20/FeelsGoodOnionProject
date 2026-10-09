using System;
using UnityEngine;
using Cooked.Authoring;
namespace FeelsGoodOnion.TechSYM.Features
{
    /// <summary>Single pickup and display owner on the actual Trigger sensor; Visual contains no scripts.</summary>
    [DisallowMultipleComponent]
    public sealed class TearFragment : MonoBehaviour
    {
        [Tooltip("획득 후 숨길 이 눈물조각의 Root입니다. 씬 전체 Root를 연결하지 마세요. 센서는 이 Root 아래에 둡니다.")]
        [SerializeField, InspectorLabel("수집 루트")] private GameObject pickupRoot;
        [Tooltip("이 눈물의 물리·스크립트 없는 외형 자식을 연결합니다. 회전과 상하 이동은 센서와 함께 수집 루트 전체에 적용합니다.")]
        [SerializeField, InspectorLabel("외형")] private Transform presentationVisual;
        [Tooltip("수집 루트의 월드 Y축 회전 속도를 조절합니다(도/초). 0이면 회전하지 않습니다.")]
        [SerializeField, InspectorLabel("회전 속도"), Min(0)] private float rotationDegreesPerSecond = 90f;
        [Tooltip("전체 루트의 상하 이동 높이를 조절합니다(초기 외형 부모의 로컬 m). 원래 방향·배율을 반영하며 0이면 이동하지 않습니다.")]
        [SerializeField, InspectorLabel("상하 높이")] private float bobHeight = .24f;
        [Tooltip("상하 이동의 편도 시간을 조절합니다(초, 최소 0.02). 같은 시간으로 돌아오며 월드 정지 때 멈춥니다.")]
        [SerializeField, InspectorLabel("상하 시간"), Min(.02f)] private float bobSeconds = 1.5f;
        private bool collected, initialized, rootWasActive;
        private Collider[] sensors;
        private bool[] sensorDefaults;
        private Transform[] visuals;
        private Vector3[] visualPositions, visualScales;
        private Quaternion[] visualRotations;
        private bool[] visualActive;
        private Renderer[] renderers;
        private bool[] rendererDefaults;
        private Vector3 rootOrigin, bobAxisInParent;
        private Quaternion rootLocalRotation;
        private double displayElapsed;
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
            var root = pickupRoot.transform;
            rootOrigin = root.localPosition;
            rootLocalRotation = root.localRotation;
            // 이전 외형의 authored 높이/방향은 보존하되 동일 변위를 Root 전체에 적용한다.
            Vector3 axisWorld = presentationVisual.parent.TransformVector(Vector3.up);
            bobAxisInParent = root.parent != null ? root.parent.InverseTransformVector(axisWorld) : axisWorld;
            displayElapsed = 0; initialized = true;
        }
        private void Awake() => PrepareSource();
        private void Update() => AdvanceMotion(Time.deltaTime);

        /// <summary>수집 루트 전체를 갱신한다. 비활성/소비/월드 정지 뒤에는 진행하지 않는다.</summary>
        public void AdvanceMotion(float scaledDeltaSeconds)
        {
            if (!float.IsFinite(scaledDeltaSeconds) || scaledDeltaSeconds < 0) throw new ArgumentOutOfRangeException(nameof(scaledDeltaSeconds));
            if (!initialized || !isActiveAndEnabled || collected || pickupRoot == null || !pickupRoot.activeInHierarchy || scaledDeltaSeconds == 0) return;
            displayElapsed += scaledDeltaSeconds;
            var root = pickupRoot.transform;
            root.localPosition = rootOrigin + bobAxisInParent * BobOffset(displayElapsed, bobHeight, bobSeconds);
            Quaternion authoredWorldRotation = root.parent != null ? root.parent.rotation * rootLocalRotation : rootLocalRotation;
            double angle = displayElapsed * rotationDegreesPerSecond % 360;
            root.rotation = Quaternion.AngleAxis((float)angle, Vector3.up) * authoredWorldRotation;
        }

        /// <summary>실행에서 쓰는 왕복 높이 규칙이다. 실제 Transform 갱신·접촉과 구분한다.</summary>
        public static float BobOffset(double elapsedSeconds, float height, float seconds)
        {
            if (!double.IsFinite(elapsedSeconds) || elapsedSeconds < 0 || !float.IsFinite(height)
                || !float.IsFinite(seconds) || seconds < .02f) throw new ArgumentOutOfRangeException(nameof(elapsedSeconds));
            double phase = elapsedSeconds % (2.0 * seconds);
            return (float)(height * .5 * (1 - Math.Cos(Math.PI * phase / seconds)));
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
            int added;
            try { added = player.AddSkillFragments(1); }
            catch { if (revision == attemptRevision) collected = false; throw; }
            if (revision != attemptRevision) return; // A synchronous retry reset owns the new attempt's presentation.
            if (added != 1) { collected = false; return; }
            foreach (var sensor in sensors) if (sensor != null) sensor.enabled = false;
            pickupRoot.SetActive(false);
        }
    }
}
