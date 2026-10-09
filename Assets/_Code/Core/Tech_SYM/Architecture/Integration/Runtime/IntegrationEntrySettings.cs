using System;
using System.Collections.Generic;
using Cooked.Authoring;
using Cooked.Contracts;
using UnityEngine;

namespace Cooked.Integration
{
    /// <summary>Authoring data only. Pose remains authoritative in the loaded Stage registry.</summary>
    [CreateAssetMenu(menuName = "Cooked/Integration Entry Settings")]
    public sealed class IntegrationEntrySettings : ScriptableObject
    {
        public const string AssetPath = "Assets/_Scenes/Tech_SYM/Architecture/Configuration/IntegrationEntrySettings.asset";
        [Serializable]
        public sealed class Entry
        {
            // 숨은 생성 값/기존 이관 값. Scene의 Marker를 ScriptableObject에 저장하지 않는다.
            [SerializeField, HideInInspector] private string stageId;
            [SerializeField, HideInInspector] private string checkpointId;
            [SerializeField, HideInInspector] private string scenePath;
            [SerializeField, InspectorLabel("진입 스테이지"), Tooltip("진입할 Stage 씬을 연결하세요. 등장 위치는 해당 Stage의 초기 Marker를 사용합니다.")]
            private SceneReference stageScene;
            public SceneReference StageScene => stageScene;
            [InspectorLabel("초기 눈물 조각"), Tooltip("진입할 때 보유할 조각 수를 조절합니다(0~5개). 0은 조각 없음입니다.")]
            [SerializeField, Range(0, 5)] private int fragments;
            [InspectorLabel("초기 해금 능력"), Tooltip("진입할 때 사용할 능력을 선택하세요. 빈 목록은 모두 잠긴 상태입니다.")]
            [SerializeField] private AbilityId[] abilities = Array.Empty<AbilityId>();
            public string StageId => stageId;
            public string ScenePath => scenePath;
            // T10은 이 계약으로 Stage의 초기 Marker를 사용한다. 레거시 CheckpointId는 이관 동안만 보존한다.
            public bool UsesStageInitialCheckpoint => true;
            public string CheckpointId => checkpointId;
            public int Fragments => fragments;
            public IReadOnlyList<AbilityId> Abilities => Array.AsReadOnly(abilities);

            public Entry(string scenePath, int fragments, params AbilityId[] abilities)
            {
                this.scenePath = scenePath; stageId = SceneCatalog.StageIdFromPath(scenePath);
                this.fragments = fragments; this.abilities = CopyAbilities(abilities);
                stageScene = new SceneReference(scenePath);
                Validate();
            }
#if UNITY_EDITOR
            public Entry(UnityEditor.SceneAsset stageScene, int fragments, params AbilityId[] abilities)
                : this(SceneCatalog.RequireScenePath(stageScene, "진입 스테이지"), fragments, abilities) { }
#endif
            public Entry(SceneReference stageScene, int fragments, params AbilityId[] abilities)
                : this(stageScene?.ResolvePath("진입 스테이지") ?? throw new ArgumentNullException(nameof(stageScene)), fragments, abilities) { }
            // 기존 Builder 호출만 보존. T11은 SceneAsset 생성자를 사용하고 T12에서 소비 제거를 확인한다.
            public Entry(string stageId, string checkpointId, int fragments, params AbilityId[] abilities)
            {
                this.stageId = stageId; this.checkpointId = checkpointId; this.fragments = fragments;
                this.abilities = CopyAbilities(abilities);
                Validate();
            }
            private static AbilityId[] CopyAbilities(AbilityId[] values) => values == null ? Array.Empty<AbilityId>() : (AbilityId[])values.Clone();

            public void Validate() => Validate("Entry (" + stageId + ")");
            internal void Validate(string owner)
            {
                if (string.IsNullOrWhiteSpace(stageId)) throw new InvalidOperationException(owner + ": 진입 스테이지가 없습니다.");
                if (!string.IsNullOrEmpty(scenePath))
                {
                    if (stageId != SceneCatalog.StageIdFromPath(scenePath)) throw new InvalidOperationException(owner + ": Scene/Stage 식별이 일치하지 않습니다.");
                }
                else if (string.IsNullOrWhiteSpace(checkpointId))
                    throw new InvalidOperationException(owner + ": 레거시 Stage/Checkpoint 이관 값이 없습니다.");
                if (fragments < 0 || fragments > 5) throw new InvalidOperationException(owner + ": 초기 눈물 조각은 0~5개입니다.");
                if (abilities == null) throw new InvalidOperationException(owner + ": 초기 해금 능력 목록이 없습니다.");
                var selected = new HashSet<AbilityId>();
                foreach (var ability in abilities)
                {
                    if (ability != AbilityId.FormChange && ability != AbilityId.Tear) throw new InvalidOperationException(owner + ": 잘못된 초기 해금 능력입니다: " + ability);
                    if (!selected.Add(ability)) throw new InvalidOperationException(owner + ": 초기 해금 능력 중복: " + ability);
                }
            }
        }
        [InspectorLabel("스테이지 진입 설정"), Tooltip("등록 Stage마다 초기 조각과 해금 능력을 연결하세요. 각 씬은 한 번만 등록합니다.")]
        [SerializeField] private Entry[] entries = Array.Empty<Entry>();
        public Entry Require(string stageId)
        {
            ValidateEntries(entries, "스테이지 진입 설정");
            Entry found = null;
            foreach (var entry in entries)
            {
                if (entry.StageId != stageId) continue;
                found = entry;
            }
            return found ?? throw new InvalidOperationException("스테이지 진입 설정 누락: " + stageId);
        }

        public void ValidateCatalog(SceneCatalog catalog)
        {
            if (catalog == null) throw new ArgumentNullException(nameof(catalog));
#if UNITY_EDITOR
            RefreshAuthoring();
#endif
            ValidateEntries(entries, "스테이지 진입 설정");
            foreach (var entry in entries)
                if (entry.ScenePath != catalog.GetStagePath(entry.StageId))
                    throw new InvalidOperationException("Entry (" + entry.StageId + "): 등록 Stage 씬을 연결하세요.");
            foreach (string path in catalog.StagePaths) Require(catalog.GetStageId(path));
        }

        public void Configure(Entry[] values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            var copy = (Entry[])values.Clone();
            ValidateEntries(copy, "스테이지 진입 설정");
            entries = copy;
        }

        private static void ValidateEntries(Entry[] values, string owner)
        {
            if (values == null || values.Length == 0) throw new InvalidOperationException(owner + ": 목록이 비어 있습니다.");
            var ids = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < values.Length; i++)
            {
                var entry = values[i];
                if (entry == null) throw new InvalidOperationException(owner + "[" + i + "]: 항목이 없습니다.");
                entry.Validate(owner + "[" + i + "] (" + entry.StageId + ")");
                if (!ids.Add(entry.StageId)) throw new InvalidOperationException(owner + "[" + i + "]: Stage 중복: " + entry.StageId);
            }
        }
#if UNITY_EDITOR
        public void RefreshAuthoring()
        {
            if (entries == null) throw new InvalidOperationException(name + ": 진입 설정 목록이 없습니다.");
            var refreshed = new Entry[entries.Length];
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = entries[i];
                if (entry == null) throw new InvalidOperationException(name + " / 진입 설정[" + i + "]: 항목이 없습니다.");
                string path = entry.StageScene?.ResolvePath(name + " / 진입 설정[" + i + "]") ?? throw new InvalidOperationException(name + " / 진입 설정[" + i + "]: Stage 씬을 연결하세요.");
                refreshed[i] = new Entry(path, entry.Fragments, Copy(entry.Abilities));
            }
            ValidateEntries(refreshed, name + " / 진입 설정");
            entries = refreshed;
        }

        private static AbilityId[] Copy(IReadOnlyList<AbilityId> values)
        {
            var copy = new AbilityId[values.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = values[i];
            return copy;
        }

        private void OnValidate()
        {
            try { RefreshAuthoring(); }
            catch (Exception error) { Debug.LogError(name + " 작성 오류: " + error.Message, this); }
        }
#endif
    }
}
