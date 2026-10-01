using System;
using System.Collections.Generic;
using Cooked.Contracts;
using UnityEngine;

namespace Cooked.Integration
{
    /// <summary>Authoring data only. Pose remains authoritative in the loaded Stage registry.</summary>
    [CreateAssetMenu(menuName = "Cooked/Integration Entry Settings")]
    public sealed class IntegrationEntrySettings : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private string stageId;
            [SerializeField] private string checkpointId;
            [SerializeField, Range(0, 1)] private int fragments;
            [SerializeField] private AbilityId[] abilities = Array.Empty<AbilityId>();
            public string StageId => stageId;
            public CheckpointSnapshot Checkpoint => new CheckpointSnapshot(stageId, checkpointId, fragments);
            public IReadOnlyList<AbilityId> Abilities => Array.AsReadOnly(abilities);
            public Entry(string stageId, string checkpointId, int fragments, params AbilityId[] abilities)
            {
                this.stageId = stageId; this.checkpointId = checkpointId; this.fragments = fragments;
                this.abilities = abilities == null ? Array.Empty<AbilityId>() : (AbilityId[])abilities.Clone();
                Validate();
            }
            public void Validate()
            {
                _ = Checkpoint;
                if (fragments > 1) throw new InvalidOperationException("Current HMS tear capacity is one fragment.");
                if (abilities == null) throw new InvalidOperationException("Entry abilities must be specified.");
                foreach (var ability in abilities)
                    if (!Enum.IsDefined(typeof(AbilityId), ability)) throw new InvalidOperationException("Invalid entry ability.");
            }
        }
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();
        public Entry Require(string stageId)
        {
            Entry found = null;
            foreach (var entry in entries)
            {
                if (entry == null) throw new InvalidOperationException("Null entry settings.");
                entry.Validate();
                if (entry.StageId != stageId) continue;
                if (found != null) throw new InvalidOperationException("Duplicate entry Stage: " + stageId);
                found = entry;
            }
            return found ?? throw new InvalidOperationException("Missing entry settings for " + stageId);
        }
        public void Configure(Entry[] values)
        {
            entries = values == null ? throw new ArgumentNullException(nameof(values)) : (Entry[])values.Clone();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (entry == null) throw new ArgumentException("Null entry.", nameof(values));
                entry.Validate();
                if (!ids.Add(entry.StageId)) throw new ArgumentException("Duplicate Stage entry.", nameof(values));
            }
        }
    }
}
