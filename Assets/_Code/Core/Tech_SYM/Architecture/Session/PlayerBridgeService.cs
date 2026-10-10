using System;
using System.Collections.Generic;
using Cooked.Contracts;
using FeelsGoodOnion.TechSYM.Enemies;
using UnityEngine;

namespace Cooked.Session
{
    /// <summary>Compatibility boundary: gameplay commands are issued by GameplayInputRouter.</summary>
    public sealed class PlayerBridgeService : IPlayerBridgeService
    {
        // Matches the existing HMS Facade's form/tear slots; never reinitializes its controller.
        private const int FormSlot = 0, TearSlot = 1;
        // Current EnemyDefinition/DamageFloor authoring uses 0ms; retain that finite fallback
        // until the actual common Knockback clip is connected. Pending HMS force still runs.
        public const float MissingKnockbackClipFallbackMilliseconds = 0f;
        public static bool IsDeathComplete(bool presentationReady, bool separationPending, bool paused, bool reported)
            => presentationReady && !separationPending && !paused && !reported;
        public static bool MatchesActor(object expected, long expectedGeneration, object current, long currentGeneration)
            => expected != null && ReferenceEquals(expected, current) && expectedGeneration == currentGeneration;
        private readonly IGameplayControlService control;
        private readonly IGameSessionService session;
        private readonly Func<PlayerFacade, long, bool> requestRetry;
        public float HorizontalKnockbackSpeed { get; }
        public float UpwardKnockbackSpeed { get; }
        private PlayerFacade actor;
        private IReadOnlyPlayerModel model;
        private PlayerSkillController skills;
        private PlayerFormController form;
        private PlayerSkill observedTear;
        private CharacterMovement movement;
        private Rigidbody actorBody;
        private Component separationOwner;
        private PlayerFacade separationActor;
        private long separationGeneration;
        private float separationTargetY, separationRemaining;
        private float requestedSeparationVelocityY, separationStepLoss;
        private bool separationPending, issuingMovementCommand, completingSeparation;
        private double lastSeparationPhysicsTime = double.NegativeInfinity;
        private Action movementHandler;
        private PlayerMovementModeController movementMode;
        private bool deathReported, disposed, detaching, pendingTear, pendingForm;
        private bool deathPending, deathReady, deathInterrupted, deathNotified, skipDeathPresentation;
        private int deathFrame;
        private long actorGeneration, deathBaselineRequestId, deathRequestId;
        private float nextFormChangeTime;
        private PlayerAnimationController animation;
        private Action<PlayerAnimationRequest, PlayerAnimationEndReason> endedHandler;
        private Action<int, int> healthHandler;
        private Action<int> fragmentsHandler;
        private Action<PlayerForm> formHandler;
        private PlayerForm lastCommittedForm;
        private Func<EnemyActor, Collider> hurtColliderResolver;
        public PlayerFacade Actor => actor;
        public long ActorGeneration => actorGeneration;
        public bool SeparationPending => separationPending;
        public bool PhysicsPaused => control.State.WorldPaused || control.State.PresentationPaused;
        public Transform VisualRoot { get; private set; }
        public event Action Dying;
        public event Action<AbilityId, PlayerSkillBlockReason> AbilityRejected;
        // T5/T9 may inject EnemyActor.HurtCollider when its read-only public contract is available.
        // Until then the existing authored contract requires one direct-child Trigger Box at BodyPosition.
        public void SetEnemyHurtColliderResolver(Func<EnemyActor, Collider> resolver)
        { ThrowIfDisposed(); hurtColliderResolver = resolver ?? throw new ArgumentNullException(nameof(resolver)); }
        public bool HasActor => actor != null;
        public Transform CameraTarget { get; private set; }
        public Transform BodyTransform => actor != null ? actor.transform : null;
        public IReadOnlyPlayerModel Model => model;
        public Vector3 FacingDirection => movement != null ? movement.FacingDirection : Vector3.right;
        public bool CanAct => !disposed && session.IsActive && HasActor && !control.State.GameplayBlocked && !control.State.WorldPaused && actor.CanReceiveInput;
        public PlayerSnapshot Snapshot => model == null ? new PlayerSnapshot(0, 1, 0, 0) : new PlayerSnapshot(model.CurrentHP, model.MaxHP, model.CurrentSkillFragment, model.MaxSkillFragment);
        public event Action<PlayerSnapshot> Changed;
        public event Action ActorChanged;
        /// <summary>Presentation-only notification of committed activation/recovery, never request acceptance.
        /// Integration maps these to audio once. Failed observers are logged, not rethrown into HMS execution.</summary>
        public event Action<AbilityId> AbilitySucceeded;

        /// <summary>Retry 요청은 현재 Actor/generation을 확인한 조립자가 수락한다. 독립 모드는 null을 전달한다.
        /// 두 넉백 속도는 Core/독립 Host가 같은 작성 설정에서 전달한다(m/s).</summary>
        public PlayerBridgeService(IGameplayControlService control, IGameSessionService session,
            Func<PlayerFacade, long, bool> requestRetry, float horizontalKnockbackSpeed = 3f, float upwardKnockbackSpeed = 3f)
        {
            ValidateKnockbackSpeed(horizontalKnockbackSpeed, nameof(horizontalKnockbackSpeed));
            ValidateKnockbackSpeed(upwardKnockbackSpeed, nameof(upwardKnockbackSpeed));
            this.control = control ?? throw new ArgumentNullException(nameof(control));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.requestRetry = requestRetry;
            HorizontalKnockbackSpeed = horizontalKnockbackSpeed;
            UpwardKnockbackSpeed = upwardKnockbackSpeed;
            control.Changed += OnControlChanged;
        }

        public void Attach(PlayerFacade value)
        {
            ThrowIfDisposed();
            if (detaching) throw new InvalidOperationException("Cannot attach during actor teardown.");
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (actor == value) return;
            var presentation = value.GetComponent<PlayerDamagePresentation>();
            if (presentation == null) throw new InvalidOperationException("현재 ActorBody의 PlayerDamagePresentation 참조가 필요합니다.");
            presentation.ValidateReferences(value);
            if (value.GetComponent<CharacterMovement>() == null || value.GetComponent<Rigidbody>() == null ||
                value.GetComponent<CapsuleCollider>() == null || value.GetComponent<PlayerSkillController>() == null ||
                value.GetComponent<PlayerFormController>() == null)
                throw new InvalidOperationException("현재 ActorBody의 HMS 이동·물리·스킬·폼 연결이 필요합니다.");
            Detach();
            actor = value;
            model = actor.Model;
            skills = actor.GetComponent<PlayerSkillController>();
            form = actor.GetComponent<PlayerFormController>();
            movement = actor.GetComponent<CharacterMovement>();
            actorBody = actor.GetComponent<Rigidbody>();
            movementMode = actor.GetComponent<PlayerMovementModeController>();
            observedTear = skills.GetEquippedSkill(TearSlot);
            CameraTarget = presentation.CameraAnchor;
            VisualRoot = presentation.VisualRoot;
            deathReported = false; pendingTear = false; pendingForm = false;
            deathPending = deathReady = deathInterrupted = deathNotified = skipDeathPresentation = false;
            deathRequestId = 0; nextFormChangeTime = 0;
            long generation = ++actorGeneration;
            animation = actor.GetComponent<PlayerAnimationController>();
            deathBaselineRequestId = animation != null ? animation.CurrentRequest.RequestId : 0;
            var attachedActor = actor;
            lastCommittedForm = form.CurrentForm;
            healthHandler = (hp, max) => { if (actorGeneration == generation && actor == attachedActor) OnHealthChanged(hp, max); };
            fragmentsHandler = count => { if (actorGeneration == generation && actor == attachedActor) OnFragmentsChanged(count); };
            formHandler = value => { if (actorGeneration == generation && actor == attachedActor) OnFormChanged(value); };
            endedHandler = (request, reason) =>
            {
                if (actorGeneration == generation && actor == attachedActor) OnPlaybackEnded(request, reason);
            };
            movementHandler = () =>
            {
                if (!issuingMovementCommand && separationPending && IsCurrentActor(attachedActor, generation) && Time.inFixedTimeStep &&
                    lastSeparationPhysicsTime != Time.fixedTimeAsDouble)
                {
                    lastSeparationPhysicsTime = Time.fixedTimeAsDouble;
                    AdvanceSeparation(Time.fixedDeltaTime);
                }
            };
            movement.MovementUpdated += movementHandler;
            if (animation != null) animation.PlaybackEnded += endedHandler;
            if (observedTear is TearSkill tear) tear.SetTargetValidation(actor.transform, ValidateTearTarget);
            model.HealthChanged += healthHandler;
            model.SkillFragmentChanged += fragmentsHandler;
            form.FormChanged += formHandler;
            if (observedTear != null) observedTear.Activated += OnTearActivated;
            OnControlChanged(control.State);
            var errors = new List<Exception>();
            SessionCleanup.Notify(errors, ActorChanged);
            SessionCleanup.Notify(errors, Changed, Snapshot);
            SessionCleanup.ThrowIfAny(errors, "Actor attach observer failure.");
        }
        public void Detach()
        {
            if (detaching) return;
            detaching = true;
            var errors = new List<Exception>();
            var oldModel = model;
            var oldActor = actor;
            var oldSkills = skills;
            var oldForm = form;
            var oldTear = observedTear;
            var oldAnimation = animation;
            var oldMovement = movement;
            var oldMovementHandler = movementHandler;
            bool hadSeparation = separationPending;
            var oldEnded = endedHandler;
            var oldHealth = healthHandler;
            var oldFragments = fragmentsHandler;
            var oldFormHandler = formHandler;
            ++actorGeneration;
            ClearSeparation();
            deathPending = deathReady = deathInterrupted = deathNotified = skipDeathPresentation = false;
            animation = null; endedHandler = null;
            healthHandler = null; fragmentsHandler = null; formHandler = null;
            bool hadActor = oldModel != null;
            actor = null; model = null; skills = null; movement = null; movementMode = null; form = null; observedTear = null;
            actorBody = null; movementHandler = null; CameraTarget = null; VisualRoot = null;
            pendingTear = false; pendingForm = false;
            try
            {
                if (oldMovement != null && oldMovementHandler != null)
                    SessionCleanup.Attempt(errors, () => oldMovement.MovementUpdated -= oldMovementHandler);
                if (hadSeparation && oldMovement != null) SessionCleanup.Attempt(errors, () => oldMovement.ResetForRevive(true));
                if (oldModel != null)
                {
                    SessionCleanup.Attempt(errors, () => oldModel.HealthChanged -= oldHealth);
                    SessionCleanup.Attempt(errors, () => oldModel.SkillFragmentChanged -= oldFragments);
                }
                if (oldForm != null) SessionCleanup.Attempt(errors, () => oldForm.FormChanged -= oldFormHandler);
                if (oldTear != null) SessionCleanup.Attempt(errors, () => oldTear.Activated -= OnTearActivated);
                if (oldActor != null)
                {
                    SessionCleanup.Attempt(errors, () => oldActor.SetInputBlocked(true));
                    SessionCleanup.Attempt(errors, oldActor.ClearInput);
                }
                if (oldAnimation != null && oldEnded != null)
                    SessionCleanup.Attempt(errors, () => oldAnimation.PlaybackEnded -= oldEnded);
                if (oldSkills != null) SessionCleanup.Attempt(errors, oldSkills.CancelAll);
                if (oldTear is TearSkill tear)
                {
                    SessionCleanup.Attempt(errors, tear.Cancel);
                    SessionCleanup.Attempt(errors, () => tear.SetTargetValidation(null, null));
                }
                if (hadActor)
                {
                    SessionCleanup.Notify(errors, ActorChanged);
                    SessionCleanup.Notify(errors, Changed, Snapshot);
                }
            }
            finally { detaching = false; }
            SessionCleanup.ThrowIfAny(errors, "Actor detach completed with teardown failures.");
        }
        public void RestoreFragments(int fragments)
        {
            RequireActor();
            if (fragments < 0 || fragments > model.MaxSkillFragment) throw new ArgumentOutOfRangeException(nameof(fragments));
            int delta = fragments - model.CurrentSkillFragment;
            if (delta > 0) actor.AddSkillFragments(delta);
            else if (delta < 0 && !actor.TryConsumeSkillFragments(-delta)) throw new InvalidOperationException("Fragment restore rejected.");
            if (model.CurrentSkillFragment != fragments) throw new InvalidOperationException("Fragment restore mismatch.");
        }
        public void SetDepthMovementAllowed(bool allowed)
        {
            RequireActor();
            if (movementMode == null || !movementMode.TrySetMode(allowed ? PlayerMovementMode.Quarter : PlayerMovementMode.Side))
                throw new InvalidOperationException("HMS movement mode is not ready.");
            ClearInput();
        }
        public void Move(Vector3 input)
        {
            if (!CanAct) { ClearInput(); return; }
            input.x = movement.AllowHorizontalMovement ? input.x : 0;
            input.z = movement.AllowDepthMovement ? input.z : 0;
            Vector3 world = new Vector3(input.x, 0, actor.AllowDepthMovement ? input.z : 0);
            actor.SetMoveInput(Vector3.ClampMagnitude(world, 1));
        }
        public void ClearInput() { if (actor != null) actor.ClearInput(); }
        public bool Jump() { if (!CanAct || movement == null || !movement.IsGrounded) return false; actor.RequestJump(); return true; }
        /// <returns>Request accepted. Preparation may still cancel; only AbilitySucceeded is an audio success signal.</returns>
        public bool Tear()
        {
            if (!CanUse(AbilityId.Tear) || observedTear == null || observedTear.IsInUse) return false;
            pendingTear = true;
            try
            {
                bool accepted = actor.TryUseTearSkill(out _);
                if (!accepted) pendingTear = false;
                return accepted;
            }
            catch { pendingTear = false; throw; }
        }
        public bool ChangeForm()
        {
            if (!CanUse(AbilityId.FormChange)) return false;
            if (Time.time < nextFormChangeTime) return RejectAbility(AbilityId.FormChange, PlayerSkillBlockReason.Cooldown);
            var current = skills.GetEquippedSkill(FormSlot);
            if (current != null && current.IsInUse) return false;
            pendingForm = true;
            try
            {
                bool accepted = actor.TryUseFormChangeSkill(out var reason);
                if (!accepted) { pendingForm = false; RejectAbility(AbilityId.FormChange, reason); }
                return accepted;
            }
            catch { pendingForm = false; throw; }
        }
        private void OnTearActivated(PlayerSkill skill)
        {
            if (!pendingTear || disposed || !ReferenceEquals(skill, observedTear)) return;
            pendingTear = false;
            // HMS invokes Activated after fragment payment, before applying the AoE stun.
            NotifyAbility(AbilityId.Tear);
        }
        private void OnFormChanged(PlayerForm value)
        {
            if (disposed || value == lastCommittedForm) return;
            lastCommittedForm = value;
            nextFormChangeTime = Time.time + 1f; // Only a committed size change starts this gate.
            if (!pendingForm) return;
            pendingForm = false;
            NotifyAbility(AbilityId.FormChange);
        }
        private void NotifyAbility(AbilityId ability)
        {
            var errors = new List<Exception>();
            SessionCleanup.Notify(errors, AbilitySucceeded, ability);
            // Never let a failing Audio/View observer interrupt TearSkill before ApplyStun.
            if (errors.Count > 0) Debug.LogException(new AggregateException("Ability presentation observer failed: " + ability, errors));
        }
        private bool CanUse(AbilityId ability) => CanAct && session.IsUnlocked(ability);
        private void OnControlChanged(ControlState state)
        {
            if (!HasActor) return;
            bool blocked = state.GameplayBlocked || state.WorldPaused;
            actor.SetInputBlocked(blocked);
            if (blocked) { pendingTear = false; pendingForm = false; actor.ClearInput(); skills.CancelAll(); }
            if (separationPending && !state.WorldPaused && !state.PresentationPaused) ScheduleSeparationVelocity();
        }
        private bool RejectAbility(AbilityId ability, PlayerSkillBlockReason reason)
        {
            var errors = new List<Exception>();
            var handlers = AbilityRejected;
            if (handlers != null) foreach (Action<AbilityId, PlayerSkillBlockReason> handler in handlers.GetInvocationList())
                SessionCleanup.Attempt(errors, () => handler(ability, reason));
            if (errors.Count > 0) Debug.LogException(new AggregateException("Ability rejection observer failed.", errors));
            return false;
        }
        public bool IsCurrentActor(PlayerFacade expected, long generation)
            => !disposed && !detaching && session.IsActive && actor != null && actor.isActiveAndEnabled &&
                MatchesActor(expected, generation, actor, actorGeneration);
        private bool HasLiveActor => IsCurrentActor(actor, actorGeneration) && !actor.IsDead &&
            movement != null && movement.isActiveAndEnabled && actorBody != null && !actorBody.isKinematic;

        /// <summary>인자는 피해원의 월드 위치다. 방향/세기는 모든 일반 피해원이 이 한 규칙을 사용한다.</summary>
        public int Damage(Vector3 sourcePosition)
        {
            ThrowIfDisposed();
            if (!HasLiveActor || PhysicsPaused) return 0;
            Vector3 velocity = CommonKnockback(sourcePosition, actorBody.position, FacingDirection,
                movement.AllowHorizontalMovement, movement.AllowDepthMovement, HorizontalKnockbackSpeed, UpwardKnockbackSpeed);
            PlayerFacade expected = actor;
            long generation = actorGeneration;
            bool mayDie = model.CurrentHP == 1;
            if (mayDie) BeginDeathObservation(false);
            try { return expected.Damage(1, velocity, GetKnockbackControlLockMilliseconds(expected)); }
            finally { if (mayDie && IsCurrentActor(expected, generation) && !expected.IsDead) ClearDeathObservation(); }
        }
        /// <summary>작성 평면은 HMS 이동 축(X/Z)이다. 동일 위치는 논리 정면 반대, Y는 항상 상향이다.</summary>
        public static Vector3 CommonKnockback(Vector3 sourcePosition, Vector3 bodyPosition, Vector3 facing,
            bool horizontalAllowed, bool depthAllowed, float horizontalSpeed = 3f, float upwardSpeed = 3f)
        {
            if (!Finite(sourcePosition) || !Finite(bodyPosition) || !Finite(facing)) throw new ArgumentOutOfRangeException(nameof(sourcePosition));
            ValidateKnockbackSpeed(horizontalSpeed, nameof(horizontalSpeed));
            ValidateKnockbackSpeed(upwardSpeed, nameof(upwardSpeed));
            double x = horizontalAllowed ? (double)bodyPosition.x - sourcePosition.x : 0;
            double z = depthAllowed ? (double)bodyPosition.z - sourcePosition.z : 0;
            double length = Math.Sqrt(x * x + z * z);
            if (length < .000001)
            {
                x = horizontalAllowed ? -facing.x : 0;
                z = depthAllowed ? -facing.z : 0;
                length = Math.Sqrt(x * x + z * z);
                if (length < .000001)
                {
                    x = horizontalAllowed ? -1 : 0;
                    z = !horizontalAllowed && depthAllowed ? -1 : 0;
                    length = Math.Sqrt(x * x + z * z);
                }
            }
            return new Vector3(length > 0 ? (float)(x / length * horizontalSpeed) : 0, upwardSpeed,
                length > 0 ? (float)(z / length * horizontalSpeed) : 0);
        }
        private static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);
        private static void ValidateKnockbackSpeed(float speed, string name)
        { if (!float.IsFinite(speed) || speed < 0) throw new ArgumentOutOfRangeException(name); }
        public static float GetKnockbackControlLockMilliseconds(PlayerFacade source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            float value = source.GetComponent<PlayerAnimatorOutput>()?.KnockbackControlLockMilliseconds ?? 0f;
            return value > 0f ? value : MissingKnockbackClipFallbackMilliseconds;
        }
        public int ForceDeath(Vector3 knockbackVelocity, bool skipPresentation = false)
        {
            ThrowIfDisposed();
            if (!HasLiveActor || PhysicsPaused) return 0;
            PlayerFacade expected = actor;
            long generation = actorGeneration;
            skipDeathPresentation = skipPresentation;
            BeginDeathObservation(skipPresentation);
            issuingMovementCommand = true;
            try { return expected.ForceDeath(knockbackVelocity, GetKnockbackControlLockMilliseconds(expected)); }
            finally
            {
                issuingMovementCommand = false;
                if (IsCurrentActor(expected, generation) && !expected.IsDead) ClearDeathObservation();
            }
        }

        /// <summary>단발 얕은 보정 또는 HMS 외력으로 깊은 분리를 요청한다. 지속 pose 작성자는 추가하지 않는다.</summary>
        public bool TryResolvePlatformOverlap(Component owner, PlayerFacade expected, long generation,
            Vector3 correction, float seconds, bool fatal)
        {
            ThrowIfDisposed();
            if (owner == null || !Finite(correction) || correction.y < 0 || (correction.x == 0 && correction.y == 0 && correction.z == 0) ||
                (fatal && (correction.x != 0 || correction.z != 0 || correction.y <= 0)) || !float.IsFinite(seconds) || seconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(correction), "실제 상향 몸체 보정과 양수 분리 시간/요청 소유자가 필요합니다. 깊은 분리는 수직만 사용합니다.");
            if (!IsCurrentActor(expected, generation) || !HasLiveActor || PhysicsPaused || separationPending) return false;
            if (!fatal)
            {
                // 실제 몸체 pose만 한 번 보정한다. HMS 속도·외력·점프·접촉·착지 예약은 그대로 소비한다.
                // 0 속도 넉백도 다음 FixedUpdate의 RefreshGround/UpdateLanding을 건너뛰므로 예약하지 않는다.
                // 다음 HMS 물리 단계가 접지와 착지를 확정한다. ResetForRevive나 가짜 접지는 사용하지 않는다.
                if ((!movement.AllowHorizontalMovement && correction.x != 0) || (!movement.AllowDepthMovement && correction.z != 0))
                    throw new ArgumentException("현재 HMS의 고정 이동축을 침범하는 착지 보정입니다.", nameof(correction));
                if (!IsCurrentActor(expected, generation) || !HasLiveActor || PhysicsPaused ||
                    (!movement.AllowHorizontalMovement && correction.x != 0) || (!movement.AllowDepthMovement && correction.z != 0)) return false;
                actorBody.position += correction;
                actorBody.WakeUp();
                return true;
            }
            // ForceDeath 안에서 Missing 종료가 동기로 와도 분리가 먼저 대기 상태여야 한다.
            separationPending = true;
            separationOwner = owner;
            separationActor = expected;
            separationGeneration = generation;
            separationTargetY = actorBody.position.y + correction.y;
            separationRemaining = seconds;
            requestedSeparationVelocityY = separationStepLoss = 0;
            lastSeparationPhysicsTime = double.NegativeInfinity;
            int applied;
            try { applied = ForceDeath(Vector3.zero); }
            catch { CancelSeparation(owner); throw; }
            if (!IsCurrentActor(expected, generation)) return false;
            if (applied <= 0 || !expected.IsDead) { CancelSeparation(owner); return false; }
            ScheduleSeparationVelocity();
            return true;
        }

        /// <summary>HMS MovementUpdated의 물리 스텝 끝에서 진행한다. 외부 helper는 중지 Edit Mode에서만 직접 확인한다.</summary>
        public void AdvanceSeparation(float deltaSeconds)
        {
            if (!float.IsFinite(deltaSeconds) || deltaSeconds < 0) throw new ArgumentOutOfRangeException(nameof(deltaSeconds));
            if (!separationPending || completingSeparation || PhysicsPaused || deltaSeconds == 0 ||
                !IsCurrentActor(separationActor, separationGeneration)) return;
            if (movement == null || !movement.isActiveAndEnabled || actorBody == null)
                throw new InvalidOperationException("현재 사망 Actor의 HMS 이동/몸체가 분리 중 해제되었습니다.");
            // MovementUpdated는 HMS가 이번 스텝 중력/축 제약을 적용한 뒤 온다.
            // 공개 실제 속도로 손실을 관측하므로 HMS의 private 중력 수치를 복제하지 않는다.
            separationStepLoss = Mathf.Max(0, requestedSeparationVelocityY - movement.Velocity.y);
            separationRemaining = Mathf.Max(0, separationRemaining - deltaSeconds);
            if (TryFinishSeparation(IsCurrentActor(separationActor, separationGeneration) && separationActor.IsDead,
                PhysicsPaused, false, separationRemaining, actorBody.position.y, separationTargetY, out _))
                FinishSeparation();
            else ScheduleSeparationVelocity();
        }
        private void ScheduleSeparationVelocity()
        {
            if (!separationPending || completingSeparation || PhysicsPaused || !IsCurrentActor(separationActor, separationGeneration)) return;
            float distance = Mathf.Max(0, separationTargetY - actorBody.position.y);
            // 목표까지의 잔여 거리는 매 물리 관측으로 조정한다. clip ms는 시계/거리 계산에 쓰지 않는다.
            requestedSeparationVelocityY = SeparationVelocity(distance, separationRemaining, Time.fixedDeltaTime, separationStepLoss);
            IssueMovementVelocity(new Vector3(0, requestedSeparationVelocityY, 0), Mathf.Max(separationRemaining, Time.fixedDeltaTime));
        }
        public static float SeparationVelocity(float distance, float remainingSeconds, float stepSeconds, float observedStepLoss)
        {
            if (!float.IsFinite(distance) || distance < 0 || !float.IsFinite(remainingSeconds) || remainingSeconds < 0 ||
                !float.IsFinite(stepSeconds) || stepSeconds <= 0 || !float.IsFinite(observedStepLoss) || observedStepLoss < 0)
                throw new ArgumentOutOfRangeException(nameof(distance));
            if (remainingSeconds == 0) return 0;
            double velocity = distance / (double)Math.Max(remainingSeconds, stepSeconds) + observedStepLoss;
            if (velocity > float.MaxValue) throw new ArgumentOutOfRangeException(nameof(distance), "분리 속도가 유한 범위를 벗어났습니다.");
            return (float)velocity;
        }
        /// <summary>현재 사망 Actor의 유한 종결 경계. 시간 종료/소유자 해제는 잔여 lift를 한 번 보정한다.</summary>
        public static bool TryFinishSeparation(bool currentDeadActor, bool paused, bool ownerReleased,
            float remainingSeconds, float bodyY, float targetY, out float residualLift)
        {
            if (!float.IsFinite(remainingSeconds) || remainingSeconds < 0 || !float.IsFinite(bodyY) || !float.IsFinite(targetY))
                throw new ArgumentOutOfRangeException(nameof(remainingSeconds));
            residualLift = 0;
            if (!currentDeadActor || paused || (!ownerReleased && remainingSeconds > 0)) return false;
            double lift = Math.Max(0, (double)targetY - bodyY);
            if (lift > float.MaxValue) throw new ArgumentOutOfRangeException(nameof(targetY));
            residualLift = (float)lift;
            return true;
        }
        private void FinishSeparation()
        {
            PlayerFacade expected = separationActor; long generation = separationGeneration;
            if (!separationPending || completingSeparation || PhysicsPaused || !IsCurrentActor(expected, generation) || !expected.IsDead) return;
            completingSeparation = true;
            try
            {
                RunSeparationCompletion(() =>
                {
                    issuingMovementCommand = true;
                    try { movement.ResetForRevive(true); } // 운동/외력만 정리한다. HP/Dead/폼 소유자는 유지한다.
                    finally { issuingMovementCommand = false; }
                }, () => separationPending && IsCurrentActor(expected, generation) && expected.IsDead,
                    () => PhysicsPaused, () =>
                {
                    // Reset의 동기 통지로 pose가 바뀔 수 있다. 재검증 뒤 실제 잔여 거리만 한 번 보정한다.
                    float remaining = Mathf.Max(0, separationTargetY - actorBody.position.y);
                    if (remaining > 0) actorBody.position += Vector3.up * remaining;
                    actorBody.WakeUp();
                }, ClearSeparation);
            }
            finally { completingSeparation = false; }
        }
        /// <summary>현재 Actor를 재검증하고, 동기 통지가 있는 HMS 정리와 실제 몸체 보정 뒤에만 완료를 공개한다.</summary>
        public static bool RunSeparationCompletion(Action resetMovement, Func<bool> stillCurrent, Func<bool> paused,
            Action correctBody, Action publishCompletion)
        {
            if (resetMovement == null || stillCurrent == null || paused == null || correctBody == null || publishCompletion == null)
                throw new ArgumentNullException(nameof(resetMovement));
            if (!stillCurrent() || paused()) return false;
            Exception resetError = null;
            try { resetMovement(); }
            catch (Exception error) { resetError = error; } // HMS는 통지 전에 외력을 정리한다. observer 예외는 보존한다.
            bool completed = false;
            try
            {
                if (stillCurrent() && !paused())
                {
                    correctBody();
                    if (stillCurrent() && !paused()) { publishCompletion(); completed = true; }
                }
            }
            catch (Exception error)
            {
                if (resetError != null) throw new AggregateException("HMS 운동 정리 통지와 몸체 분리 종결에 실패했습니다.", resetError, error);
                throw;
            }
            if (resetError != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(resetError).Throw();
            return completed;
        }
        private void IssueMovementVelocity(Vector3 velocity, float lockSeconds)
        {
            issuingMovementCommand = true;
            try { movement.ApplyKnockback(velocity, lockSeconds); } // Dead Facade는 외력을 거절하므로 실제 소유자에게 요청한다.
            finally { issuingMovementCommand = false; }
        }
        private void ClearSeparation()
        {
            separationPending = false; separationOwner = null; separationActor = null;
            separationGeneration = 0; separationTargetY = separationRemaining = 0;
            requestedSeparationVelocityY = separationStepLoss = 0;
            lastSeparationPhysicsTime = double.NegativeInfinity;
        }
        public void CancelSeparation(Component owner)
        {
            if (!separationPending || completingSeparation || !ReferenceEquals(separationOwner, owner)) return;
            bool current = IsCurrentActor(separationActor, separationGeneration);
            if (current && separationActor.IsDead)
            {
                // 현재 사망과 낡은 Actor 취소는 다르다. owner Disable/reset은 Dead 표현을 Interrupted로 만들지 않는다.
                separationRemaining = 0;
                if (TryFinishSeparation(true, PhysicsPaused, true, 0, actorBody.position.y, separationTargetY, out _))
                    FinishSeparation();
                else IssueMovementVelocity(Vector3.zero, 0); // 옵션 중에는 보정을 미루고 상승 외력만 중단한다.
                return;
            }
            ClearSeparation();
            if (current && movement != null) movement.ResetForRevive(true);
        }

        public bool OwnsDebris(PlayerDebris shell)
            => HasLiveActor && shell != null && shell.isActiveAndEnabled && form != null &&
                ReferenceEquals(shell.Owner, form) && ReferenceEquals(form.OwnedDebris, shell);
        public bool TrySeparateOwnedDebris(PlayerDebris shell, Vector3 displacement)
        {
            ThrowIfDisposed();
            if (!Finite(displacement)) throw new ArgumentOutOfRangeException(nameof(displacement));
            if (!OwnsDebris(shell) || PhysicsPaused) return false;
            var body = shell.GetComponent<Rigidbody>();
            var shape = shell.GetComponent<BoxCollider>();
            if (body == null || body.isKinematic || shape == null || !shape.enabled || shape.isTrigger || shape.attachedRigidbody != body) return false;
            body.position += displacement;
            // 안쪽 속도만 제거한다. 질량·무게·Owner·폼·IgnoreCollision은 건드리지 않는다.
            float length = displacement.magnitude;
            if (length > 0)
            {
                Vector3 normal = displacement / length;
                float inward = Vector3.Dot(body.linearVelocity, normal);
                if (inward < 0) body.linearVelocity -= normal * inward;
            }
            body.WakeUp();
            return true;
        }
        public bool ReturnOwnedDebris(PlayerDebris shell)
        {
            ThrowIfDisposed();
            if (!OwnsDebris(shell) || PhysicsPaused) return false;
            var body = shell.GetComponent<Rigidbody>();
            var shape = shell.GetComponent<BoxCollider>();
            if (body == null || body.isKinematic || shape == null || !shape.enabled || shape.isTrigger || shape.attachedRigidbody != body) return false;
            var shellMovement = shell.GetComponent<PlatformRigidbodyMovement>();
            var passenger = shell.GetComponent<PlatformPassenger>();
            bool resumeMovement = shellMovement != null && shellMovement.enabled;
            bool resumePassenger = passenger != null && passenger.enabled;
            // 공개 enabled 수명으로 오래된 접지/운반 속도를 해제하고 같은 컴포넌트를 복구한다.
            // 껍질 Root/Collider/HeavyState와 충돌 제외는 그대로 유지한다.
            try
            {
                if (resumeMovement) shellMovement.enabled = false;
                if (resumePassenger) passenger.enabled = false;
                body.position = actorBody.position;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.WakeUp();
            }
            finally
            {
                if (resumePassenger && passenger != null) passenger.enabled = true;
                if (resumeMovement && shellMovement != null) shellMovement.enabled = true;
            }
            return true;
        }
        /// <summary>실제 데드존 Trigger의 전달 Collider 경계. Player와 현재 OwnedDebris를 여기서 구별한다.</summary>
        public bool HandleDeadzoneContact(Collider other)
        {
            ThrowIfDisposed();
            if (other == null || !other.enabled || !other.gameObject.activeInHierarchy || PhysicsPaused) return false;
            var body = other.attachedRigidbody;
            if (body == null) return false;
            var shell = body.GetComponent<PlayerDebris>();
            if (shell != null) return ReturnOwnedDebris(shell);
            var source = body.GetComponent<PlayerFacade>();
            return IsCurrentActor(source, actorGeneration) && ForceDeath(Vector3.zero, true) > 0;
        }
        private void OnHealthChanged(int current, int maximum)
        {
            var observedActor = actor;
            long observedGeneration = actorGeneration;
            var errors = new List<Exception>();
            SessionCleanup.Notify(errors, Changed, Snapshot);
            if (current <= 0 && !deathNotified && !deathReported && session.IsActive && actor != null && actor.IsDead &&
                actor == observedActor && actorGeneration == observedGeneration)
            {
                if (!deathPending) BeginDeathObservation(skipDeathPresentation);
                deathNotified = true;
                pendingTear = pendingForm = false;
                SessionCleanup.Attempt(errors, () => actor.ClearInput());
                SessionCleanup.Attempt(errors, skills.CancelAll);
                SessionCleanup.Notify(errors, Dying);
            }
            // Do not dispatch inside HealthChanged: HMS has yet to select Dead/apply fatal knockback.
            if (errors.Count > 0) Debug.LogException(new AggregateException("Player health observer failure.", errors));
        }
        private void BeginDeathObservation(bool skipPresentation)
        {
            deathPending = true; deathFrame = Time.frameCount;
            deathRequestId = 0; deathInterrupted = false; deathReady = skipPresentation;
            deathBaselineRequestId = animation != null ? animation.CurrentRequest.RequestId : 0;
        }
        private void ClearDeathObservation()
        {
            deathPending = deathReady = deathInterrupted = deathNotified = skipDeathPresentation = false;
            deathRequestId = 0;
        }
        private void OnPlaybackEnded(PlayerAnimationRequest request, PlayerAnimationEndReason reason)
        {
            if (!deathPending || deathReported || deathInterrupted || !HasActor || !actor.IsDead || !session.IsActive ||
                animation == null || request.Animation != PlayerAnimationId.Dead ||
                request.RequestId <= deathBaselineRequestId) return;
            if (deathRequestId != 0 && deathRequestId != request.RequestId) return;
            if (deathRequestId == 0)
            {
                long currentId = animation.CurrentRequest.RequestId;
                // T1 selects the successor BEFORE reporting the previous request Interrupted.
                // Before the first Tick, accept that immediate predecessor; older/future IDs
                // still cannot establish this death's request. Actor/generation is gated by endedHandler.
                if (request.RequestId != currentId &&
                    (reason != PlayerAnimationEndReason.Interrupted || request.RequestId != currentId - 1)) return;
            }
            deathRequestId = request.RequestId;
            if (reason == PlayerAnimationEndReason.Interrupted) { deathInterrupted = true; deathReady = false; return; }
            if (reason == PlayerAnimationEndReason.Completed || reason == PlayerAnimationEndReason.Missing) deathReady = true;
        }
        public void TickDeathPresentation()
        {
            if (disposed || !HasActor || !session.IsActive) return;
            if (!deathPending)
            {
                if (animation != null && !actor.IsDead) deathBaselineRequestId = animation.CurrentRequest.RequestId;
                return;
            }
            if (!actor.IsDead || deathInterrupted || deathReported || Time.frameCount <= deathFrame) return;
            if (!deathReady)
            {
                if (animation == null || !animation.isActiveAndEnabled)
                {
                    Debug.LogWarning("Dead controller unavailable; missing presentation fallback. Actual clip playback remains unverified.");
                    deathReady = true;
                }
                else
                {
                    var request = animation.CurrentRequest;
                    if (request.Animation != PlayerAnimationId.Dead || request.RequestId <= deathBaselineRequestId) return;
                    deathRequestId = request.RequestId;
                    var output = actor.GetComponent<PlayerAnimatorOutput>();
                    // A live output owns clip availability/result. Only an absent/disabled output is
                    // synthesized as Missing, with the NEW Dead request ID, never an old Idle request.
                    if (output == null || !output.isActiveAndEnabled)
                        animation.TryReportResult(request.RequestId, PlayerAnimationEndReason.Missing);
                }
            }
            if (!IsDeathComplete(deathReady, separationPending, PhysicsPaused, deathReported)) return;
            deathReported = true; deathPending = false;
            PlayerFacade source = actor;
            long generation = actorGeneration;
            if (IsCurrentActor(source, generation) && (requestRetry == null || !requestRetry(source, generation)))
                Debug.LogWarning("현재 Actor의 사망 종결을 한 번 처리했지만 Retry 소비자가 없거나 요청을 거절했습니다. 독립 모드는 자동 Retry를 추가하지 않습니다.");
        }
        private Collider ResolveHurtCollider(EnemyActor enemy)
        {
            if (hurtColliderResolver != null) return hurtColliderResolver(enemy);
            return enemy == null ? null : enemy.HurtCollider;
        }
        private bool IsSelfCollider(Collider shape)
            => shape.transform.IsChildOf(actor.transform) ||
                (shape.attachedRigidbody != null && shape.attachedRigidbody.gameObject == actor.gameObject);
        private bool ValidateTearTarget(Vector3 origin, Collider candidate, IStunnable target)
        {
            if (disposed || actor == null || !(target is EnemyActor enemy) || !enemy.isActiveAndEnabled ||
                candidate == null || IsSelfCollider(candidate)) return false;
            Collider body = ResolveHurtCollider(enemy);
            // EnemyContactSensor is the body contact relay; its presence does NOT exclude a hurt body.
            if (body == null || candidate != body || !body.enabled || !body.gameObject.activeInHierarchy) return false;
            Vector3 delta = body.bounds.center - origin;
            if (delta.sqrMagnitude < .000001f) return false;
            var hits = Physics.RaycastAll(origin, delta.normalized, delta.magnitude + .01f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            foreach (var hit in hits)
            {
                var shape = hit.collider;
                if (shape == null || IsSelfCollider(shape)) continue;
                var hitEnemy = shape.GetComponentInParent<EnemyActor>();
                if (hitEnemy != null)
                {
                    if (ResolveHurtCollider(hitEnemy) != shape) continue; // actual sensors, not body relay
                }
                else if (shape.isTrigger) continue; // nonphysical interaction/detection volumes
                return shape == body; // first physical body/wall/object must be the candidate body
            }
            return false;
        }
        private void OnFragmentsChanged(int count)
        {
            var errors = new List<Exception>();
            SessionCleanup.Notify(errors, Changed, Snapshot);
            if (errors.Count > 0) Debug.LogException(new AggregateException("Player fragment observer failure.", errors));
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            var errors = new List<Exception>();
            try
            {
                SessionCleanup.Attempt(errors, () => control.Changed -= OnControlChanged);
                SessionCleanup.Attempt(errors, Detach);
            }
            finally { Changed = null; ActorChanged = null; AbilitySucceeded = null; AbilityRejected = null; Dying = null; hurtColliderResolver = null; }
            SessionCleanup.ThrowIfAny(errors, "Player bridge disposal completed with teardown failures.");
        }
        private void RequireActor() { ThrowIfDisposed(); if (!HasActor) throw new InvalidOperationException("No player actor attached."); }
        private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(PlayerBridgeService)); }
    }
}
