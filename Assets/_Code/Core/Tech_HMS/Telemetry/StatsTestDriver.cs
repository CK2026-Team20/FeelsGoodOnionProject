using System;
using UnityEngine;
using UnityEngine.Serialization;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Cooked.Telemetry
{
    [DisallowMultipleComponent]
    public sealed class StatsTestDriver : MonoBehaviour
    {
        [SerializeField, InspectorName("Worker 전송 주소")]
        [Tooltip("통계 POST 요청을 받을 HTTPS 주소를 입력하세요. 경로까지 포함하며 비워 두면 전송하지 않습니다.")]
        private string endpoint = "";

        [SerializeField, Min(1), InspectorName("응답 대기 시간")]
        [Tooltip("전송 응답을 기다릴 최대 시간입니다(초). 최소 1초입니다.")]
        private int timeoutSeconds = 15;

        [SerializeField, InspectorName("설치 GUID")]
        [Tooltip("이번 Play에서 생성한 테스트용 설치 식별자입니다. Play마다 새로 생성되며 실행 중 원본 값으로 갱신됩니다.")]
        private string installationGuid;

        [FormerlySerializedAs("userGuid")]
        [SerializeField, InspectorName("세션 GUID")]
        [Tooltip("현재 플레이 회차의 식별자입니다. 새 세션 시작 함수를 호출하면 재발급됩니다.")]
        private string sessionGuid;

        [SerializeField, InspectorName("현재 점프 횟수")]
        [Tooltip("현재 세션에서 Space 또는 테스트 메뉴로 발생시킨 점프 횟수입니다. 실행 중 집계 값으로 갱신됩니다.")]
        private int currentJumpCount;

        private static string playGeneration;
        private string initializedGeneration;
        [SerializeField, InspectorName("씬 전환 시 유지")]
        [Tooltip("통계 오브젝트를 씬 전환 후에도 유지합니다. 부모가 없는 전용 루트 오브젝트에서 사용하고 한 개만 배치하세요.")]
        private bool keepAcrossScenes;

        private PlayStatsCollector collector;
        private WorkerStatsSender sender;
        private event Action TestJumped;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void BeginPlay()
        {
            playGeneration = Guid.NewGuid().ToString("N");
        }

        private void Update()
        {
            EnsureCurrentPlay();
#if ENABLE_INPUT_SYSTEM
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
                SimulateJump();
#elif ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.Space))
                SimulateJump();
#endif
            RefreshInspector();
        }

        private void EnsureCurrentPlay()
        {
            if (collector != null && initializedGeneration == playGeneration) return;
            if (collector != null) TestJumped -= collector.RecordJump;
            collector = new PlayStatsCollector();
            initializedGeneration = playGeneration;
            TestJumped += collector.RecordJump;
            RefreshInspector();
        }

        private void RefreshInspector()
        {
            var snapshot = collector.CreateSnapshot();
            installationGuid = snapshot.installation_id;
            sessionGuid = snapshot.session_id;
            currentJumpCount = snapshot.jump_count;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying) return;
            EnsureCurrentPlay();
            ConfigureLifetime();
            sender = new WorkerStatsSender();
            TestJumped -= collector.RecordJump;
            TestJumped += collector.RecordJump;
        }

        private void ConfigureLifetime()
        {
            if (!keepAcrossScenes) return;
            if (transform.parent != null)
            {
                Debug.LogWarning("씬 전환 시 유지는 부모가 없는 전용 루트 오브젝트에서 사용하세요.", this);
                return;
            }
            DontDestroyOnLoad(gameObject);
        }

        [ContextMenu("통계 테스트/새 세션 시작")]
        public void StartNewSession()
        {
            if (!IsReady()) return;
            EnsureCurrentPlay();
            collector.StartNewSession();
            RefreshInspector();
        }

        private void OnDisable()
        {
            if (collector != null) TestJumped -= collector.RecordJump;
            StopAllCoroutines();
            sender?.Dispose();
            sender = null;
        }

        [ContextMenu("통계 테스트/점프 1회 발생")]
        public void SimulateJump()
        {
            if (!IsReady()) return;
            EnsureCurrentPlay();
            TestJumped?.Invoke();
            RefreshInspector();
            PrintSnapshot();
        }

        [ContextMenu("통계 테스트/현재 통계 출력")]
        public void PrintSnapshot()
        {
            if (!IsReady()) return;
            Debug.Log(JsonUtility.ToJson(collector.CreateSnapshot()), this);
        }

        [ContextMenu("통계 테스트/현재 통계 전송")]
        public void SendSnapshot()
        {
            if (!IsReady()) return;
            if (sender.IsSending)
            {
                Debug.LogWarning("이미 전송 중입니다.", this);
                return;
            }
            StartCoroutine(sender.Send(endpoint, collector.CreateSnapshot(), timeoutSeconds,
                (success, message) =>
                {
                    if (success) Debug.Log(message, this);
                    else Debug.LogWarning(message, this);
                }));
        }

        private bool IsReady()
        {
            if (Application.isPlaying && isActiveAndEnabled && collector != null && sender != null)
                return true;
            Debug.LogWarning("Play 모드에서 활성화된 통계 테스트 컴포넌트를 사용하세요.", this);
            return false;
        }
    }
}