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
            [Tooltip("진입할 스테이지의 고유 ID입니다. 해당 StageService의 Stage Id와 정확히 일치해야 합니다.")]
            [SerializeField] private string stageId;
            [Tooltip("직접 진입 시 사용할 체크포인트 ID입니다. 해당 스테이지에 등록된 Checkpoint Id와 일치해야 합니다.")]
            [SerializeField] private string checkpointId;
            [Tooltip("직접 진입 시 보유할 눈물 조각 수입니다(0~5개). 0은 조각 없음이며 5개를 모아야 눈물을 사용할 수 있습니다.")]
            [SerializeField, Range(0, 5)] private int fragments;
            [Tooltip("직접 진입 시 이미 해금할 능력 목록입니다. 비어 있으면 능력이 잠겨 있으며 이후 체크포인트에서 해금됩니다.")]
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
                if (fragments < 0 || fragments > 5) throw new InvalidOperationException("Tear fragments must be between zero and five.");
                if (abilities == null) throw new InvalidOperationException("Entry abilities must be specified.");
                foreach (var ability in abilities)
                    if (!Enum.IsDefined(typeof(AbilityId), ability)) throw new InvalidOperationException("Invalid entry ability.");
            }
        }
        [Tooltip("스테이지 직접 진입용 초기 설정 목록입니다. 각 Stage Id는 중복 없이 한 번만 등록해야 합니다.")]
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
