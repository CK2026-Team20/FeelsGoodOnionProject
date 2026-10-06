using System;
using System.Collections.Generic;
using Cooked.Contracts;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Cooked.Session
{
    [DisallowMultipleComponent]
    public sealed class GameplayInputRouter : MonoBehaviour
    {
        private PlayerInputReader reader;
        private PlayerBridgeService bridge;
        private PlayerInteractionService interaction;
        private IGameplayControlService control;
        private int suppressButtonsThroughFrame;

        public void Initialize(PlayerInputReader reader, PlayerBridgeService bridge, PlayerInteractionService interaction, IGameplayControlService control)
        {
            Unbind();
            this.reader = reader != null ? reader : throw new ArgumentNullException(nameof(reader));
            this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
            this.interaction = interaction ?? throw new ArgumentNullException(nameof(interaction));
            this.control = control ?? throw new ArgumentNullException(nameof(control));
            control.Changed += OnControlChanged;
            suppressButtonsThroughFrame = Time.frameCount;
        }
        private void OnControlChanged(ControlState state)
        {
            suppressButtonsThroughFrame = Time.frameCount;
            if (state.GameplayBlocked || state.WorldPaused) bridge?.ClearInput();
        }
        private void Update()
        {
            if (interaction != null && Time.frameCount > suppressButtonsThroughFrame &&
                control != null && !control.State.PresentationPaused && Keyboard.current != null &&
                Keyboard.current.eKey.wasPressedThisFrame && interaction.TryCloseOverlay())
            { bridge?.ClearInput(); return; }
            if (bridge == null || reader == null || !reader.CanReadInput || !bridge.CanAct)
            {
                bridge?.ClearInput(); interaction?.Refresh(); return;
            }
            if (Time.frameCount <= suppressButtonsThroughFrame)
            {
                bridge.ClearInput(); interaction.Refresh(); return;
            }
            // UI/메모가 이 프레임에 제어를 바꾸면 아래 게임 명령을 모두 소비합니다.
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                interaction.TryInteract();
                if (bridge == null || !bridge.CanAct || Time.frameCount <= suppressButtonsThroughFrame) return;
            }
            Vector2 move = reader.MoveInput;
            bridge.Move(new Vector3(move.x, 0, move.y));
            interaction.Refresh();
            if (Time.frameCount <= suppressButtonsThroughFrame) return;
            if (reader.JumpPressedThisFrame) bridge.Jump();
            if (reader.TearSkillPressedThisFrame) bridge.Tear();
            if (reader.FormChangePressedThisFrame) bridge.ChangeForm();
        }
        public void Unbind()
        {
            var oldControl = control;
            var oldBridge = bridge;
            reader = null; bridge = null; interaction = null; control = null;
            var errors = new List<Exception>();
            if (oldControl != null) SessionCleanup.Attempt(errors, () => oldControl.Changed -= OnControlChanged);
            if (oldBridge != null) SessionCleanup.Attempt(errors, oldBridge.ClearInput);
            SessionCleanup.ThrowIfAny(errors, "Input router unbind failures.");
        }
        private void OnDisable() => Unbind();
        private void OnDestroy() => Unbind();
    }
}
