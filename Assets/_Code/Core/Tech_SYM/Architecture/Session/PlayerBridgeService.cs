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
        private readonly IGameplayControlService control;
        private readonly IGameSessionService session;
        private readonly IGameEventBus eventBus;
        private PlayerFacade actor;
        private IReadOnlyPlayerModel model;
        private PlayerSkillController skills;
        private PlayerFormController form;
        private PlayerSkill observedTear;
        private CharacterMovement movement;
        private PlayerMovementModeController movementMode;
        private Vector3 inputRight = Vector3.right, inputForward = Vector3.forward;
        private bool deathReported, disposed, detaching, pendingTear, pendingForm;
        private bool deathPending, deathReady, deathInterrupted, skipDeathPresentation;
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

        public PlayerBridgeService(IGameplayControlService control, IGameSessionService session, IGameEventBus eventBus)
        {
            this.control = control ?? throw new ArgumentNullException(nameof(control));
            this.session = session ?? throw new ArgumentNullException(nameof(session));
            this.eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            control.Changed += OnControlChanged;
        }
        public void Attach(PlayerFacade value)
        {
            ThrowIfDisposed();
            if (detaching) throw new InvalidOperationException("Cannot attach during actor teardown.");
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (actor == value) return;
            Detach();
            actor = value;
            model = actor.Model;
            skills = actor.GetComponent<PlayerSkillController>();
            form = actor.GetComponent<PlayerFormController>();
            movement = actor.GetComponent<CharacterMovement>();
            movementMode = actor.GetComponent<PlayerMovementModeController>();
            observedTear = skills.GetEquippedSkill(TearSlot);
            CameraTarget = actor.transform.Find("CameraAnchor") ?? actor.transform;
            deathReported = false; pendingTear = false; pendingForm = false;
            deathPending = deathReady = deathInterrupted = skipDeathPresentation = false;
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
            var oldEnded = endedHandler;
            var oldHealth = healthHandler;
            var oldFragments = fragmentsHandler;
            var oldFormHandler = formHandler;
            ++actorGeneration;
            deathPending = deathReady = deathInterrupted = skipDeathPresentation = false;
            animation = null; endedHandler = null;
            healthHandler = null; fragmentsHandler = null; formHandler = null;
            bool hadActor = oldModel != null;
            actor = null; model = null; skills = null; movement = null; movementMode = null; form = null; observedTear = null; CameraTarget = null;
            pendingTear = false; pendingForm = false;
            try
            {
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
        public void SetDepthMovementAllowed(bool allowed) => SetWorldMovementAxes(true, allowed);
        public void SetInputBasis(Vector3 right, Vector3 forward)
        {
            right.y = forward.y = 0;
            if (right.sqrMagnitude < .001f || forward.sqrMagnitude < .001f) return;
            inputRight = right.normalized; inputForward = forward.normalized;
        }
        public void SetWorldMovementAxes(bool x, bool z)
        {
            RequireActor();
            if (!x && !z) throw new ArgumentException("An active movement mode requires at least one axis.");
            SetInputBasis(Vector3.right, Vector3.forward);
            if (movementMode == null || !movementMode.TrySetMode(!x ? PlayerMovementMode.BackFixed :
                z ? PlayerMovementMode.Quarter : PlayerMovementMode.Side))
                throw new InvalidOperationException("HMS movement mode is not ready.");
            ClearInput();
        }
        public void Move(Vector3 input)
        {
            if (!CanAct) { ClearInput(); return; }
            input.x = movement.AllowHorizontalMovement ? input.x : 0;
            input.z = movement.AllowDepthMovement ? input.z : 0;
            Vector3 world = inputRight * input.x + inputForward * input.z;
            if (!actor.AllowDepthMovement) world = Vector3.right * Mathf.Sign(inputRight.x) * input.x;
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
        // Damage sources keep their existing force calculation and call this boundary (T4/T5/T9).
        public int Damage(Vector3 knockbackVelocity)
        { RequireActor(); return actor.Damage(1, knockbackVelocity, GetKnockbackControlLockMilliseconds(actor)); }
        public static float GetKnockbackControlLockMilliseconds(PlayerFacade source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            float value = source.GetComponent<PlayerAnimatorOutput>()?.KnockbackControlLockMilliseconds ?? 0f;
            return value > 0f ? value : MissingKnockbackClipFallbackMilliseconds;
        }
        public int ForceDeath(Vector3 knockbackVelocity, bool skipPresentation = false)
        {
            RequireActor();
            if (actor.IsDead) return 0;
            skipDeathPresentation = skipPresentation;
            try { return actor.ForceDeath(knockbackVelocity, GetKnockbackControlLockMilliseconds(actor)); }
            finally { if (actor != null && !actor.IsDead) skipDeathPresentation = false; }
        }
        private void OnHealthChanged(int current, int maximum)
        {
            var observedActor = actor;
            long observedGeneration = actorGeneration;
            var errors = new List<Exception>();
            SessionCleanup.Notify(errors, Changed, Snapshot);
            if (current <= 0 && !deathPending && !deathReported && session.IsActive && actor != null && actor.IsDead &&
                actor == observedActor && actorGeneration == observedGeneration)
            {
                deathPending = true; deathFrame = Time.frameCount;
                deathRequestId = 0;
                if (animation != null && animation.CurrentRequest.Animation != PlayerAnimationId.Dead)
                    deathBaselineRequestId = animation.CurrentRequest.RequestId;
                deathReady = skipDeathPresentation;
                pendingTear = pendingForm = false;
                SessionCleanup.Attempt(errors, () => actor.ClearInput());
                SessionCleanup.Attempt(errors, skills.CancelAll);
                SessionCleanup.Notify(errors, Dying);
            }
            // Do not dispatch inside HealthChanged: HMS has yet to select Dead/apply fatal knockback.
            if (errors.Count > 0) Debug.LogException(new AggregateException("Player health observer failure.", errors));
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
            if (!deathReady || control.State.PresentationPaused) return;
            deathReported = true; deathPending = false;
            eventBus.Publish(new FlowRequestedEvent(new FlowRequest(FlowCommand.RetryCheckpoint)));
        }
        private Collider ResolveHurtCollider(EnemyActor enemy)
        {
            if (hurtColliderResolver != null) return hurtColliderResolver(enemy);
            Collider found = null;
            foreach (var shape in enemy.GetComponentsInChildren<BoxCollider>(true))
            {
                if (shape.transform.parent != enemy.transform || !shape.isTrigger ||
                    (shape.transform.position - enemy.BodyPosition).sqrMagnitude > .000001f) continue;
                if (found != null) throw new InvalidOperationException("Ambiguous Enemy body. T5/T9 must supply the exact HurtCollider resolver.");
                found = shape;
            }
            return found;
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
