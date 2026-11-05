using System;
using Cooked.Contracts;
using Cooked.Session;
using UnityEngine;
namespace Cooked.Audio
{
    /// <summary>한 Actor의 성공 이벤트와 이동 소리 간격. Dispose는 자기 구독만 해제한다.</summary>
    public sealed class PlayerAudioBinding : IDisposable
    {
        private readonly AudioService audio;
        private readonly PlayerBridgeService player;
        private readonly PlayerFacade actor;
        private readonly CharacterMovement movement;
        private readonly CharacterSlipperyEffect oil;
        private readonly PlayerInputReader input;
        private readonly GameplayAudioSettings settings;
        private readonly IGameplayControlService control;
        private readonly IGameFlowService flow;
        private int fragments;
        private readonly FootstepCadence cadence = new FootstepCadence();
        private bool disposed;
        public PlayerAudioBinding(AudioService audio, PlayerBridgeService player, IGameplayControlService control,
            IGameFlowService flow, GameplayAudioSettings settings)
        {
            this.audio = audio; this.player = player; this.control = control; this.flow = flow; this.settings = settings;
            actor = player.Actor ?? throw new InvalidOperationException("효과음을 연결할 Actor가 없습니다.");
            movement = actor.GetComponent<CharacterMovement>(); oil = actor.GetComponent<CharacterSlipperyEffect>(); input = actor.GetComponent<PlayerInputReader>();
            fragments = player.Snapshot.Fragments;
            actor.Jumped += OnJump; actor.Landed += OnLand;
            player.AbilitySucceeded += OnAbility; player.Changed += OnPlayer;
            player.DamageAccepted += OnDamage; player.Dying += OnDeath;
            audio.SetWorldListener(actor.transform);
        }
        private bool CanPlay => !disposed && actor != null && flow.Snapshot.State == FlowState.Playing && !control.State.WorldPaused && !control.State.PresentationPaused;
        private bool Slippery => oil != null && oil.IsActive;
        private void OnJump() { if (CanPlay) { cadence.Jump(); audio.PlaySfx(AudioCueIds.Jump); } }
        private void OnLand()
        {
            if (!CanPlay) return;
            audio.PlaySfx(Slippery ? AudioCueIds.Oil : AudioCueIds.Land);
            cadence.Land(settings.FootstepSeconds);
        }
        private void OnAbility(AbilityId ability)
        {
            if (!CanPlay) return;
            if (ability == AbilityId.Tear) audio.PlaySfx(AudioCueIds.Tear);
            else if (ability == AbilityId.FormChange)
            {
                var form = actor.GetComponent<PlayerFormController>();
                if (form != null) audio.PlaySfx(PlayerAudioCueSelection.FormChanged(form.IsSmall));
            }
        }
        private void OnPlayer(PlayerSnapshot value)
        {
            var cue = PlayerAudioCueSelection.FragmentsChanged(fragments, value.Fragments); fragments = value.Fragments;
            if (CanPlay && cue != null) audio.PlaySfx(cue);
        }
        private void OnDamage(PlayerBridgeService.AcceptedDamage damage)
        {
            if (disposed || damage.Actor != actor) return;
            audio.PlaySfx(AudioCueIds.Hit);
            if (damage.EnemySource != null) audio.PlaySfx(AudioCueIds.HitMonster);
            if (damage.KnockbackApplied) audio.PlaySfx(AudioCueIds.Knockback);
        }
        private void OnDeath() { if (!disposed) { audio.ClearWorldSfx(); audio.PlaySfx(AudioCueIds.Death); } }
        public void Tick(float delta)
        {
            if (!CanPlay || actor.IsDead || delta <= 0) return;
            bool moving = movement != null && movement.IsGrounded && movement.SelfHorizontalSpeed > .01f &&
                input != null && input.MoveInput.sqrMagnitude > .0001f && !movement.IsControlLocked;
            if (cadence.Tick(moving, delta, settings.FootstepSeconds)) audio.PlaySfx(Slippery ? AudioCueIds.Oil : AudioCueIds.Run);
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            if (actor != null) { actor.Jumped -= OnJump; actor.Landed -= OnLand; }
            player.AbilitySucceeded -= OnAbility; player.Changed -= OnPlayer; player.DamageAccepted -= OnDamage; player.Dying -= OnDeath;
            audio.SetWorldListener(null);
        }
    }
}
