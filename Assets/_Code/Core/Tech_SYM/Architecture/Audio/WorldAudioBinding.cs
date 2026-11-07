using System;
using System.Collections.Generic;
using Cooked.Contracts;
using Cooked.Integration;
using Cooked.Level;
using Cooked.Session;
using FeelsGoodOnion.TechSYM.Enemies;
using FeelsGoodOnion.TechSYM.Features;
using FeelsGoodOnion.TechSYM.Platforms;
using FeelsGoodOnion.TechSYM.OvenTray;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Cooked.Audio
{
    /// <summary>Stage의 실제 성공 이벤트를 중앙 출력에 연결한다. 레벨 탐색은 Bind 시 한 번만 수행한다.</summary>
    public sealed class WorldAudioBinding : IDisposable
    {
        private sealed class Idle { public EnemyActor Enemy; public float Remaining; public bool Eligible; }
        private sealed class Ambient { public DamageFloorPlatform Owner; public string Cue; public int Handle; }
        private readonly List<Action> unbind = new List<Action>();
        private readonly List<Idle> enemies = new List<Idle>();
        private readonly List<Ambient> ambient = new List<Ambient>();
        private readonly AudioService audio;
        private readonly PlayerBridgeService player;
        private readonly IGameplayControlService control;
        private readonly IGameFlowService flow;
        private readonly GameplayAudioSettings settings;
        private bool disposed;
        private bool Playing => !disposed && player.HasActor && !player.Actor.IsDead && flow.Snapshot.State == FlowState.Playing && !control.State.WorldPaused && !control.State.PresentationPaused;
        public WorldAudioBinding(AudioService audio, PlayerBridgeService player, Scene scene,
            IGameplayControlService control, IGameFlowService flow, GameplayAudioSettings settings)
        {
            this.audio = audio; this.player = player; this.control = control; this.flow = flow; this.settings = settings;
            try
            {
                foreach (var enemy in SceneComponentLookup.FindAll<EnemyActor>(scene))
                {
                    Action hit = () => Play(AudioCueIds.MonsterHit, enemy.MotionBody);
                    enemy.StunApplied += hit; unbind.Add(() => { if (enemy != null) enemy.StunApplied -= hit; });
                    enemies.Add(new Idle { Enemy = enemy });
                }
                foreach (var plate in SceneComponentLookup.FindAll<PressurePlatePlatform>(scene))
                {
                    Action<bool> changed = pressed => Play(pressed ? AudioCueIds.WeightOn : AudioCueIds.WeightOff, Position(plate));
                    plate.PressedChanged += changed; unbind.Add(() => { if (plate != null) plate.PressedChanged -= changed; });
                }
                foreach (var platform in SceneComponentLookup.FindAll<ShatteredPlatform>(scene))
                {
                    Action collapse = () => Play(AudioCueIds.PlatformBreak, platform.SurfacePosition);
                    platform.CollapseStarted += collapse; unbind.Add(() => { if (platform != null) platform.CollapseStarted -= collapse; });
                }
                foreach (var platform in SceneComponentLookup.FindAll<PressureLinkedPlatform>(scene))
                {
                    Action advance = () => Play(AudioCueIds.PlatformSpawn, Position(platform));
                    platform.AdvanceStarted += advance; unbind.Add(() => { if (platform != null) platform.AdvanceStarted -= advance; });
                }
                foreach (var device in SceneComponentLookup.FindAll<OvenTrayObject>(scene))
                {
                    Action start = () => Play(AudioCueIds.Interaction, Position(device));
                    device.OperationStarted += start; unbind.Add(() => { if (device != null) device.OperationStarted -= start; });
                }
                foreach (var stage in SceneComponentLookup.FindAll<StageService>(scene))
                {
                    Action<StageTrigger> checkpoint = point => { if (Playing && stage.ShouldPlayCheckpointSound(point)) audio.PlaySfx(AudioCueIds.Checkpoint); };
                    stage.CheckpointCaptured += checkpoint; unbind.Add(() => { if (stage != null) stage.CheckpointCaptured -= checkpoint; });
                }
                foreach (var source in SceneComponentLookup.FindAll<BurnerDamagePlatform>(scene)) ambient.Add(new Ambient { Owner = source, Cue = AudioCueIds.Fire });
                foreach (var source in SceneComponentLookup.FindAll<PotDamagePlatform>(scene)) ambient.Add(new Ambient { Owner = source, Cue = AudioCueIds.Pot });
            }
            catch { Dispose(); throw; }
        }
        private static Vector3 Position(Component owner)
        {
            var motion = owner.GetComponent<KinematicPlatformMotion>();
            return motion != null ? motion.Position : owner.transform.position;
        }
        private void Play(string cue, Vector3 position) { if (Playing) audio.PlaySfxAt(cue, position); }
        private void Play(string cue, Transform position)
        { if (Playing && position != null) audio.PlaySfxAt(cue, position.position); }
        public void Tick(float delta)
        {
            if (disposed) return;
            foreach (var item in ambient)
            {
                // Owner 정리는 게임/옵션 정지 중에도 수행한다. ClearWorld 이후 낡은 핸들도 버린다.
                if (item.Handle != 0 && !audio.IsWorldVoiceRegistered(item.Handle)) item.Handle = 0;
                bool active = item.Owner != null && item.Owner.isActiveAndEnabled && item.Owner.gameObject.scene.isLoaded;
                if (!active && item.Handle != 0) { audio.StopWorldVoice(item.Handle); item.Handle = 0; }
                else if (active && item.Handle == 0 && Playing)
                {
                    var shape = item.Owner.PrimaryDamageTrigger;
                    var anchor = shape != null ? shape.transform : item.Owner.transform;
                    item.Handle = audio.PlaySfxAt(item.Cue, shape != null ? shape.bounds.center : anchor.position, anchor);
                }
            }
            if (!Playing || delta <= 0) return;
            foreach (var item in enemies)
            {
                var enemy = item.Enemy;
                bool eligible = enemy != null && enemy.isActiveAndEnabled && enemy.IsPrepared && !enemy.IsStunned && enemy.MotionBody != null &&
                    Vector3.Distance(player.Actor.transform.position, enemy.MotionBody.position) < settings.FarDistance;
                if (!eligible) { item.Eligible = false; continue; }
                if (!item.Eligible) { item.Eligible = true; item.Remaining = UnityEngine.Random.Range(settings.IdleMinimum, settings.IdleMaximum); }
                else item.Remaining -= delta;
                if (item.Remaining > 0) continue;
                Play(AudioCueIds.MonsterIdle, enemy.MotionBody);
                item.Remaining = UnityEngine.Random.Range(settings.IdleMinimum, settings.IdleMaximum);
            }
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            foreach (var action in unbind) action(); unbind.Clear();
            foreach (var item in ambient) audio.StopWorldVoice(item.Handle);
            ambient.Clear(); enemies.Clear();
        }
    }
}
