using System;
using Cooked.Authoring;
using Cooked.Contracts;
using Cooked.Session;
using UnityEngine;

namespace Cooked.Chase
{
    /// <summary>One straight chase per Initialize. Owns movement and capture; the trigger saves before TryStart.</summary>
    [DefaultExecutionOrder(200)] // Capture precedes the final Stage's LateUpdate(500) ending reservation.
    [DisallowMultipleComponent]
    public sealed class ChaseRuntimeDriver : MonoBehaviour
    {
        [SerializeField, InspectorLabel("추격 설정"), Tooltip("월드 +X 방향의 추격 속도를 제공하는 자산입니다.")]
        private ChaseDefinition definition;

        private enum State { Uninitialized, Ready, Running, Stopped, Caught }
        private State state;
        private PlayerBridgeService bridge;
        private IGameplayControlService control;
        private IGameFlowService flow;
        private ChaseSwarmPresentation boundPresentation;
        private BoxCollider frontCollider;
        private CapsuleCollider actorCollider;
        private PlayerFacade actor;
        private long actorGeneration;
        private Vector3 swarmPosition, previousActorPosition;
        private float speed;
        public string LastRejection { get; private set; }
        public long AttemptVersion { get; private set; }
        public bool IsRunningAttempt(long version, PlayerFacade candidate) => version == AttemptVersion &&
            state == State.Running && actor == candidate && bridge != null && bridge.IsCurrentActor(candidate, actorGeneration);

        public void Initialize(IPlayerBridgeService playerBridge, IGameplayControlService control,
            IGameFlowService flow)
        {
            Shutdown();
            if (!(playerBridge is PlayerBridgeService owner))
                throw new ArgumentException("Chase needs the current session death owner.", nameof(playerBridge));
            if (control == null) throw new ArgumentNullException(nameof(control));
            if (flow == null) throw new ArgumentNullException(nameof(flow));
            var presentation = GetComponent<ChaseSwarmPresentation>();
            if (definition == null || presentation == null || !presentation.isActiveAndEnabled)
                throw new InvalidOperationException("추격 설정과 같은 루트의 활성 무리 표현을 연결하세요.");
            // Reuse the actual authored physics child. Never create a collider or guess by object name.
            var colliders = GetComponentsInChildren<Collider>(true);
            if (colliders.Length != 1 || !(colliders[0] is BoxCollider box) || box.transform == transform ||
                !box.enabled || !box.gameObject.activeInHierarchy || !box.isTrigger ||
                GetComponentsInChildren<Rigidbody>(true).Length != 0 || GetComponentInParent<Rigidbody>() != null)
                throw new InvalidOperationException("추격 루트에는 Rigidbody 없이 활성 자식 Trigger BoxCollider 하나가 필요합니다.");
            float configuredSpeed = definition.Speed;
            ValidateBox(box);
            try
            {
                boundPresentation = presentation;
                boundPresentation.Bind(control);
                frontCollider = box;
                bridge = owner;
                this.control = control;
                this.flow = flow;
                speed = configuredSpeed;
                state = State.Ready;
                LastRejection = null;
                flow.Changed += OnFlowChanged;
            }
            catch { Shutdown(); throw; }
        }

        public bool CanStart(PlayerFacade candidate)
        {
            LastRejection = null;
            if (!isActiveAndEnabled || state != State.Ready || bridge == null)
                return Reject("추격이 초기화되지 않았거나 이미 시작/종료됐습니다.");
            if (flow.Snapshot.State != FlowState.Playing || flow.Snapshot.IsBusy ||
                control.State.WorldPaused || control.State.PresentationPaused)
                return Reject("게임 진행 중에만 추격을 시작할 수 있습니다.");
            if (candidate == null || !bridge.IsCurrentActor(candidate, bridge.ActorGeneration) || candidate.IsDead)
                return Reject("현재 세션의 살아 있는 플레이어가 필요합니다.");
            var body = candidate.GetComponent<CapsuleCollider>();
            if (!UsableBody(body) || !Finite(candidate.transform.position))
                return Reject("플레이어의 실제 물리 CapsuleCollider와 유효한 위치가 필요합니다.");
            if (frontCollider == null || !frontCollider.enabled || !frontCollider.gameObject.activeInHierarchy ||
                boundPresentation == null || !boundPresentation.isActiveAndEnabled)
                return Reject("추격 물리 자식 또는 표현이 비활성화됐습니다.");
            return true;
        }

        public bool TryStart(PlayerFacade candidate)
        {
            if (!CanStart(candidate)) return false;
            long version = ++AttemptVersion;
            actor = candidate;
            actorGeneration = bridge.ActorGeneration;
            actorCollider = candidate.GetComponent<CapsuleCollider>();
            previousActorPosition = actorCollider.transform.position;
            swarmPosition = candidate.transform.position - Vector3.right * 8f;
            transform.position = swarmPosition; // Y/Z are captured once, never follow the actor afterward.
            state = State.Running;
            try { boundPresentation.Show(); }
            catch { if (IsRunningAttempt(version, candidate)) Stop(); throw; }
            return IsRunningAttempt(version, candidate);
        }

        private void LateUpdate()
        {
            if (state == State.Uninitialized) return;
            if (state == State.Running)
            {
                if (flow.Snapshot.State != FlowState.Playing || flow.Snapshot.IsBusy ||
                    !bridge.IsCurrentActor(actor, actorGeneration) || actor.IsDead || !UsableBody(actorCollider))
                    Stop();
                else if (control.State.WorldPaused || control.State.PresentationPaused)
                    previousActorPosition = actorCollider.transform.position;
                else
                {
                    try { Advance(Time.deltaTime); }
                    catch { Stop(); throw; }
                }
            }
            // ForceDeath observers may synchronously Shutdown this driver.
            if (state != State.Uninitialized) boundPresentation.Tick(Time.deltaTime, Time.unscaledDeltaTime);
        }

        private void Advance(float deltaTime)
        {
            if (!float.IsFinite(deltaTime) || deltaTime < 0) throw new ArgumentOutOfRangeException(nameof(deltaTime));
            if (frontCollider == null || !frontCollider.enabled || !frontCollider.gameObject.activeInHierarchy ||
                boundPresentation == null || !boundPresentation.isActiveAndEnabled)
                throw new InvalidOperationException("실행 중 추격 물리 자식 또는 표현 연결이 사라졌습니다.");
            ValidateBox(frontCollider);
            Vector3 actorPosition = actorCollider.transform.position;
            Vector3 actorDelta = actorPosition - previousActorPosition;
            Vector3 displacement = Vector3.right * (speed * deltaTime);
            if (!Finite(actorDelta) || !Finite(swarmPosition + displacement))
                throw new InvalidOperationException("추격 이동 값은 유한해야 합니다.");
            Vector3 startPosition = frontCollider.transform.position;
            Quaternion rotation = frontCollider.transform.rotation;
            bool caught = Touches(startPosition, rotation);
            // Relative sweep includes actor motion through a thin box between rendered frames.
            Vector3 relativeStart = startPosition + actorDelta;
            Vector3 relativeMove = displacement - actorDelta;
            caught |= Touches(relativeStart, rotation);
            if (!caught && relativeMove.sqrMagnitude > 0)
            {
                // Only the sweep needs the native broadphase. Include Transform changes earlier
                // this frame before Stage LateUpdate; explicit-pose overlap tests need no global sync.
                if (!Physics.autoSyncTransforms) Physics.SyncTransforms();
                Vector3 scale = frontCollider.transform.lossyScale;
                Vector3 half = Vector3.Scale(frontCollider.size, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z))) * .5f;
                Vector3 center = frontCollider.transform.TransformPoint(frontCollider.center) + actorDelta;
                foreach (var hit in Physics.BoxCastAll(center, half, relativeMove.normalized, rotation,
                    relativeMove.magnitude, 1 << actorCollider.gameObject.layer, QueryTriggerInteraction.Collide))
                    if (hit.collider == actorCollider) { caught = true; break; }
            }
            swarmPosition += displacement;
            transform.position = swarmPosition;
            previousActorPosition = actorPosition;
            caught |= Touches(frontCollider.transform.position, frontCollider.transform.rotation);
            if (!caught) return;
            // Mark terminal before any external observer runs; never send duplicate death requests.
            state = State.Caught;
            var deathOwner = bridge;
            boundPresentation.Hide();
            if (deathOwner.IsCurrentActor(actor, actorGeneration) && !actor.IsDead && deathOwner.ForceDeath(Vector3.zero) <= 0)
                throw new InvalidOperationException("추격 포획의 공통 사망 요청이 거절됐습니다.");
        }

        private bool Touches(Vector3 position, Quaternion rotation) => Physics.ComputePenetration(
            frontCollider, position, rotation, actorCollider, actorCollider.transform.position,
            actorCollider.transform.rotation, out _, out _);

        public void Stop()
        {
            if (state != State.Running && state != State.Ready) return;
            state = State.Stopped;
            actor = null;
            actorCollider = null;
            boundPresentation?.Hide();
        }

        public void Shutdown()
        {
            AttemptVersion++;
            state = State.Uninitialized;
            if (flow != null) flow.Changed -= OnFlowChanged;
            var released = boundPresentation;
            boundPresentation = null;
            bridge = null; control = null; flow = null;
            actor = null; actorCollider = null; frontCollider = null;
            released?.Unbind();
        }

        private void OnFlowChanged(FlowSnapshot snapshot)
        {
            if (state == State.Running && (snapshot.State != FlowState.Playing || snapshot.IsBusy)) Stop();
        }
        private bool Reject(string reason) { LastRejection = reason; return false; }
        private bool UsableBody(CapsuleCollider body) => body != null && body.enabled &&
            body.gameObject.activeInHierarchy && !body.isTrigger && body.attachedRigidbody != null && (!body.attachedRigidbody.isKinematic ||
                (bridge != null && bridge.HasCorrectionOwner && bridge.IsCurrentActor(body.attachedRigidbody.GetComponent<PlayerFacade>(), bridge.ActorGeneration)));
        private static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        private static void ValidateBox(BoxCollider box)
        {
            Vector3 size = box.size, scale = box.transform.lossyScale;
            if (!Finite(size) || !Finite(scale) || !Finite(box.center) || size.x <= 0 || size.y <= 0 || size.z <= 0 ||
                Mathf.Abs(scale.x) < .0001f || Mathf.Abs(scale.y) < .0001f || Mathf.Abs(scale.z) < .0001f)
                throw new InvalidOperationException("추격 BoxCollider 크기와 스케일은 유한하고 0보다 커야 합니다.");
        }
        private void OnDisable() => Shutdown();
        private void OnDestroy() => Shutdown();
    }
}
