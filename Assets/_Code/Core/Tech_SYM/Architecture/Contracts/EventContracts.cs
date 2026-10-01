using System;
namespace Cooked.Contracts
{
    public interface IGameEvent { }
    public interface IGameEventBus : IDisposable
    {
        IDisposable Subscribe<T>(Action<T> handler) where T : struct, IGameEvent;
        void Publish<T>(T message) where T : struct, IGameEvent;
    }
    public readonly struct FlowRequestedEvent : IGameEvent
    {
        public FlowRequestedEvent(FlowRequest request) { Request = request; }
        public FlowRequest Request { get; }
    }
    public readonly struct StageEnteredEvent : IGameEvent
    {
        public StageEnteredEvent(string stageId, string cameraZoneId) { StageId = stageId; CameraZoneId = cameraZoneId; }
        public string StageId { get; }
        public string CameraZoneId { get; }
    }
    public readonly struct CheckpointReachedEvent : IGameEvent
    {
        public CheckpointReachedEvent(string stageId, string checkpointId) { StageId = stageId; CheckpointId = checkpointId; }
        public string StageId { get; }
        public string CheckpointId { get; }
    }
    public readonly struct DialogueRequestedEvent : IGameEvent
    {
        public DialogueRequestedEvent(string dialogueCode, string stageId) { DialogueCode = dialogueCode; StageId = stageId; }
        public string DialogueCode { get; }
        public string StageId { get; }
    }
    public readonly struct AbilityUnlockedEvent : IGameEvent
    {
        public AbilityUnlockedEvent(AbilityId ability) { Ability = ability; }
        public AbilityId Ability { get; }
    }
}
