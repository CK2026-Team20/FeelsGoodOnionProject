using System;
using System.Collections.Generic;
using Cooked.Contracts;
using UnityEngine;

namespace Cooked.Session
{
    /// <summary>Compatibility boundary: gameplay commands are issued by GameplayInputRouter.</summary>
    public sealed class PlayerBridgeService : IPlayerBridgeService
    {
        // Matches the existing HMS Facade's form/tear slots; never reinitializes its controller.
        private const int FormSlot = 0, TearSlot = 1;
        private readonly IGameplayControlService control;
        private readonly IGameSessionService session;
        private readonly IGameEventBus eventBus;
        private PlayerFacade actor;
        private IReadOnlyPlayerModel model;
        private PlayerSkillController skills;
        private PlayerFormController form;
        private PlayerSkill observedTear;
        private CharacterMovement movement;
        private Vector3 inputRight = Vector3.right, inputForward = Vector3.forward;
        private bool deathReported, disposed, detaching, pendingTear, pendingForm;
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
            observedTear = skills.GetEquippedSkill(TearSlot);
            CameraTarget = actor.transform.Find("CameraAnchor") ?? actor.transform;
            deathReported = false; pendingTear = false; pendingForm = false;
            model.HealthChanged += OnHealthChanged;
            model.SkillFragmentChanged += OnFragmentsChanged;
            form.FormChanged += OnFormChanged;
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
            bool hadActor = oldModel != null;
            actor = null; model = null; skills = null; movement = null; form = null; observedTear = null; CameraTarget = null;
            pendingTear = false; pendingForm = false;
            try
            {
                if (oldModel != null)
                {
                    SessionCleanup.Attempt(errors, () => oldModel.HealthChanged -= OnHealthChanged);
                    SessionCleanup.Attempt(errors, () => oldModel.SkillFragmentChanged -= OnFragmentsChanged);
                }
                if (oldForm != null) SessionCleanup.Attempt(errors, () => oldForm.FormChanged -= OnFormChanged);
                if (oldTear != null) SessionCleanup.Attempt(errors, () => oldTear.Activated -= OnTearActivated);
                if (oldActor != null)
                {
                    SessionCleanup.Attempt(errors, () => oldActor.SetInputBlocked(true));
                    SessionCleanup.Attempt(errors, oldActor.ClearInput);
                }
                if (oldSkills != null) SessionCleanup.Attempt(errors, oldSkills.CancelAll);
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
        public void SetDepthMovementAllowed(bool allowed) { RequireActor(); movement.SetDepthMovementAllowed(allowed); }
        public void SetInputBasis(Vector3 right, Vector3 forward)
        {
            right.y = forward.y = 0;
            if (right.sqrMagnitude < .001f || forward.sqrMagnitude < .001f) return;
            inputRight = right.normalized; inputForward = forward.normalized;
        }
        public void Move(Vector3 input)
        {
            if (!CanAct) { ClearInput(); return; }
            Vector3 world = inputRight * input.x + inputForward * input.z;
            if (!actor.AllowDepthMovement) world = Vector3.right * Mathf.Sign(inputRight.x) * input.x;
            actor.SetMoveInput(Vector3.ClampMagnitude(world, 1));
        }
        public void ClearInput() { if (actor != null) actor.ClearInput(); }
        public bool Jump() { if (!CanAct) return false; actor.RequestJump(); return true; }
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
            var current = skills.GetEquippedSkill(FormSlot);
            if (current != null && current.IsInUse) return false;
            pendingForm = true;
            try
            {
                bool accepted = actor.TryUseFormChangeSkill(out _);
                if (!accepted) pendingForm = false;
                return accepted;
            }
            catch { pendingForm = false; throw; }
        }
        public bool RecoverShell()
        {
            if (!CanUse(AbilityId.RecoverShell) || !actor.TryRecoverDebris()) return false;
            NotifyAbility(AbilityId.RecoverShell);
            return true;
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
            if (!pendingForm || disposed) return;
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
        private void OnHealthChanged(int current, int maximum)
        {
            var errors = new List<Exception>();
            SessionCleanup.Notify(errors, Changed, Snapshot);
            if (current <= 0 && !deathReported && session.IsActive)
            {
                deathReported = true;
                pendingTear = false; pendingForm = false;
                SessionCleanup.Attempt(errors, () => eventBus.Publish(new FlowRequestedEvent(new FlowRequest(FlowCommand.RetryCheckpoint))));
            }
            // Presentation exceptions must not interrupt PlayerFacade.Damage before its own CancelAll.
            if (errors.Count > 0) Debug.LogException(new AggregateException("Player health observer failure.", errors));
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
            finally { Changed = null; ActorChanged = null; AbilitySucceeded = null; }
            SessionCleanup.ThrowIfAny(errors, "Player bridge disposal completed with teardown failures.");
        }
        private void RequireActor() { ThrowIfDisposed(); if (!HasActor) throw new InvalidOperationException("No player actor attached."); }
        private void ThrowIfDisposed() { if (disposed) throw new ObjectDisposedException(nameof(PlayerBridgeService)); }
    }
}
