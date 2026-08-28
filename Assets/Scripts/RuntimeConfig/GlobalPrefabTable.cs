using System;
using System.Collections.Generic;
using UnityEngine;

namespace FrameSyncMoba.RuntimeConfig
{
    public enum PrefabKind : byte
    {
        Unit = 0,
        Projectile = 1,
        ParticleVfx = 2,
        AudioEmitter = 3,
        Misc = 4,
    }

    [Serializable]
    public sealed class PrefabEntry
    {
        [SerializeField, Min(1)] private int prefabId;
        [SerializeField, HideInInspector] private GameObject unityPrefab;
        [SerializeField] private string logicAssetAddress;
        [SerializeField] private int gameplayConfigId;
        [SerializeField, HideInInspector] private string editorAssetGuid;
        [SerializeField] private string clientViewAddress;

        [NonSerialized] private GameObject resolvedUnityPrefab;

        public int PrefabId => prefabId;
        public GameObject UnityPrefab =>
            resolvedUnityPrefab != null
                ? resolvedUnityPrefab
                : unityPrefab;
        public string LogicAssetAddress => logicAssetAddress ?? string.Empty;
        public int GameplayConfigId => gameplayConfigId;
        public string EditorAssetGuid => editorAssetGuid;
        public string ClientViewAddress => clientViewAddress ?? string.Empty;
        public bool HasLegacyDirectReference => unityPrefab != null;

        public PrefabEntry(
            int prefabId,
            GameObject unityPrefab,
            int gameplayConfigId = 0,
            string editorAssetGuid = null,
            string clientViewAddress = null)
        {
            this.prefabId = prefabId;
            this.unityPrefab = unityPrefab;
            logicAssetAddress = string.Empty;
            this.gameplayConfigId = gameplayConfigId;
            this.editorAssetGuid = editorAssetGuid ?? string.Empty;
            this.clientViewAddress = clientViewAddress ?? string.Empty;
        }

        public PrefabEntry(
            int prefabId,
            string logicAssetAddress,
            int gameplayConfigId = 0,
            string editorAssetGuid = null,
            string clientViewAddress = null)
        {
            this.prefabId = prefabId;
            unityPrefab = null;
            this.logicAssetAddress = logicAssetAddress ?? string.Empty;
            this.gameplayConfigId = gameplayConfigId;
            this.editorAssetGuid = editorAssetGuid ?? string.Empty;
            this.clientViewAddress = clientViewAddress ?? string.Empty;
        }

        internal PrefabEntry Resolve(GameObject prefab)
        {
            if (prefab == null)
                throw new ArgumentNullException(nameof(prefab));
            var resolved = new PrefabEntry(
                prefabId,
                LogicAssetAddress,
                gameplayConfigId,
                editorAssetGuid,
                ClientViewAddress)
            {
                resolvedUnityPrefab = prefab,
            };
            return resolved;
        }
    }

    [Serializable]
    public sealed class PrefabGroup
    {
        [SerializeField] private PrefabKind kind;
        [SerializeField] private List<PrefabEntry> entries = new List<PrefabEntry>();

        public PrefabKind Kind => kind;
        public IReadOnlyList<PrefabEntry> Entries => entries;

        public PrefabGroup(PrefabKind kind, IEnumerable<PrefabEntry> entries)
        {
            this.kind = kind;
            if (entries != null)
            {
                this.entries.AddRange(entries);
            }
        }
    }

    /// <summary>
    /// Authorable ID range for one PrefabKind (design v10.2 17.5). PrefabId
    /// allocation is per kind so hero prefabs never collide with projectile,
    /// VFX, audio or misc ids.
    /// </summary>
    [Serializable]
    public sealed class PrefabKindRangeConfig
    {
        public PrefabKind Kind;
        [Min(1)] public int IdRangeStart = 1000;
        [Min(1)] public int IdRangeEnd = 1999;

        public bool Contains(int prefabId)
        {
            return prefabId >= IdRangeStart &&
                   prefabId <= IdRangeEnd;
        }
    }

    [CreateAssetMenu(
        fileName = "GlobalPrefabTable",
        menuName = "FrameSyncMoba/Runtime/Global Prefab Table")]
    public sealed class GlobalPrefabTable : ScriptableObject
    {
        [Header("Resolved runtime mappings / legacy migration bridge")]
        [Tooltip("Production root assets keep this empty. Loaded match scopes populate a nonserialized runtime clone.")]
        [SerializeField] private List<PrefabGroup> prefabGroups = new List<PrefabGroup>();
        [Header("Addressable content partitions")]
        [SerializeField] private List<GlobalPrefabPartitionReference>
            partitions = new List<GlobalPrefabPartitionReference>();
        [Header("Per-kind ID ranges (design v10.2 17.5)")]
        [Tooltip("Configured ranges override the built-in defaults. IDs must stay inside their kind range.")]
        [SerializeField] private List<PrefabKindRangeConfig> kindRanges =
            new List<PrefabKindRangeConfig>();

        private readonly Dictionary<long, PrefabEntry> runtimeLookup =
            new Dictionary<long, PrefabEntry>();
        private bool isLookupBuilt;

        public IReadOnlyList<PrefabGroup> PrefabGroups => prefabGroups;
        public IReadOnlyList<GlobalPrefabPartitionReference> Partitions =>
            partitions;
        public IReadOnlyList<PrefabKindRangeConfig> KindRanges =>
            kindRanges;

        public bool TryGetPrefab(PrefabKind kind, int prefabId, out GameObject prefab)
        {
            EnsureLookup();
            if (runtimeLookup.TryGetValue(
                    BuildKey(kind, prefabId),
                    out PrefabEntry entry))
            {
                prefab = entry.UnityPrefab;
                return prefab != null;
            }

            prefab = null;
            return false;
        }

        public bool TryGetEntry(
            PrefabKind kind,
            int prefabId,
            out PrefabEntry entry)
        {
            EnsureLookup();
            return runtimeLookup.TryGetValue(
                BuildKey(kind, prefabId),
                out entry);
        }

        public GameObject GetRequiredPrefab(PrefabKind kind, int prefabId)
        {
            if (!TryGetPrefab(kind, prefabId, out GameObject prefab))
            {
                throw new InvalidOperationException(
                    $"GlobalPrefabTable has no {kind} prefab with id {prefabId}.");
            }

            return prefab;
        }

        public void ValidateOrThrow()
        {
            RebuildLookup();
        }

        public IReadOnlyList<GlobalPrefabPartitionReference>
            SelectPartitions(
                int mapConfigId,
                IReadOnlyList<int> sortedHeroConfigIds)
        {
            ValidateRootPartitions();
            if (sortedHeroConfigIds == null)
                throw new ArgumentNullException(
                    nameof(sortedHeroConfigIds));
            var selected =
                new List<GlobalPrefabPartitionReference>();
            for (int i = 0; i < partitions.Count; i++)
            {
                GlobalPrefabPartitionReference partition =
                    partitions[i];
                bool include = false;
                switch (partition.PartitionKind)
                {
                    case GlobalPrefabPartitionKind.Core:
                    case GlobalPrefabPartitionKind.Shared:
                        include = true;
                        break;
                    case GlobalPrefabPartitionKind.Map:
                        include =
                            partition.OwnerConfigId == mapConfigId;
                        break;
                    case GlobalPrefabPartitionKind.Hero:
                        include = ContainsSorted(
                            sortedHeroConfigIds,
                            partition.OwnerConfigId);
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Unknown content partition kind {partition.PartitionKind}.");
                }
                if (include)
                    selected.Add(partition);
            }
            selected.Sort(GlobalPrefabPartitionReference.Compare);
            for (int heroIndex = 0;
                 heroIndex < sortedHeroConfigIds.Count;
                 heroIndex++)
            {
                int heroId = sortedHeroConfigIds[heroIndex];
                bool found = false;
                for (int partitionIndex = 0;
                     partitionIndex < selected.Count;
                     partitionIndex++)
                {
                    GlobalPrefabPartitionReference partition =
                        selected[partitionIndex];
                    if (partition.PartitionKind ==
                            GlobalPrefabPartitionKind.Hero &&
                        partition.OwnerConfigId == heroId)
                    {
                        found = true;
                        break;
                    }
                }
                if (!found)
                    throw new InvalidOperationException(
                        $"GlobalPrefabTable has no Hero partition for HeroConfigId {heroId}.");
            }
            return selected;
        }

        public GlobalPrefabTable CreateResolvedRuntimeTable(
            IReadOnlyList<GlobalPrefabSubTableAsset> subTables,
            IReadOnlyDictionary<string, GameObject> resolvedPrefabs)
        {
            if (subTables == null)
                throw new ArgumentNullException(nameof(subTables));
            if (resolvedPrefabs == null)
                throw new ArgumentNullException(nameof(resolvedPrefabs));

            var entriesByKind =
                new Dictionary<PrefabKind, List<PrefabEntry>>();
            for (int tableIndex = 0;
                 tableIndex < subTables.Count;
                 tableIndex++)
            {
                GlobalPrefabSubTableAsset subTable =
                    subTables[tableIndex] ??
                    throw new InvalidOperationException(
                        $"Resolved sub-table {tableIndex} is null.");
                subTable.ValidateOrThrow();
                IReadOnlyList<PrefabGroup> groups =
                    subTable.PrefabGroups;
                for (int groupIndex = 0;
                     groupIndex < groups.Count;
                     groupIndex++)
                {
                    PrefabGroup group = groups[groupIndex];
                    if (!entriesByKind.TryGetValue(
                            group.Kind,
                            out List<PrefabEntry> destination))
                    {
                        destination = new List<PrefabEntry>();
                        entriesByKind.Add(group.Kind, destination);
                    }
                    for (int entryIndex = 0;
                         entryIndex < group.Entries.Count;
                         entryIndex++)
                    {
                        PrefabEntry entry = group.Entries[entryIndex];
                        if (string.IsNullOrEmpty(
                                entry.LogicAssetAddress))
                        {
                            destination.Add(entry);
                            continue;
                        }
                        if (!resolvedPrefabs.TryGetValue(
                                entry.LogicAssetAddress,
                                out GameObject prefab) ||
                            prefab == null)
                        {
                            throw new InvalidOperationException(
                                $"Resolved match content is missing logic asset '{entry.LogicAssetAddress}' for {group.Kind}/{entry.PrefabId}.");
                        }
                        destination.Add(entry.Resolve(prefab));
                    }
                }
            }

            var resolvedGroups = new List<PrefabGroup>();
            foreach (KeyValuePair<PrefabKind, List<PrefabEntry>> pair
                     in entriesByKind)
            {
                pair.Value.Sort(
                    (left, right) =>
                        left.PrefabId.CompareTo(right.PrefabId));
                resolvedGroups.Add(
                    new PrefabGroup(pair.Key, pair.Value));
            }
            resolvedGroups.Sort(
                (left, right) =>
                    ((byte)left.Kind).CompareTo((byte)right.Kind));

            GlobalPrefabTable runtime =
                CreateInstance<GlobalPrefabTable>();
            runtime.name = $"{name}_ResolvedMatch";
            runtime.kindRanges = CloneRanges(kindRanges);
            runtime.prefabGroups = resolvedGroups;
            runtime.partitions =
                new List<GlobalPrefabPartitionReference>();
            runtime.RebuildLookup();
            return runtime;
        }

        internal void ReplaceGroupsForTests(IEnumerable<PrefabGroup> groups)
        {
            prefabGroups.Clear();
            if (groups != null)
            {
                prefabGroups.AddRange(groups);
            }

            isLookupBuilt = false;
        }

        internal void ReplacePartitionsForTests(
            IEnumerable<GlobalPrefabPartitionReference> values)
        {
            partitions.Clear();
            if (values != null)
                partitions.AddRange(values);
            isLookupBuilt = false;
        }

#if UNITY_EDITOR
        public void ConfigureAddressableRootForEditor(
            IEnumerable<GlobalPrefabPartitionReference> values)
        {
            prefabGroups.Clear();
            partitions.Clear();
            if (values != null)
                partitions.AddRange(values);
            runtimeLookup.Clear();
            isLookupBuilt = false;
        }
#endif

        internal void ReplaceRangesForTests(
            IEnumerable<PrefabKindRangeConfig> ranges)
        {
            kindRanges.Clear();
            if (ranges != null)
            {
                kindRanges.AddRange(ranges);
            }

            isLookupBuilt = false;
        }

        private void OnEnable()
        {
            isLookupBuilt = false;
        }

        private void OnValidate()
        {
            try
            {
                RebuildLookup();
            }
            catch (Exception exception)
            {
                Debug.LogError($"Invalid GlobalPrefabTable '{name}': {exception.Message}", this);
            }
        }

        private void EnsureLookup()
        {
            if (!isLookupBuilt)
            {
                RebuildLookup();
            }
        }

        private void RebuildLookup()
        {
            runtimeLookup.Clear();
            ValidateRanges();

            if (partitions.Count > 0)
            {
                ValidateRootPartitions();
                if (prefabGroups.Count > 0)
                    throw new InvalidOperationException(
                        "GlobalPrefabTable root cannot serialize resolved prefab groups together with Addressable partitions.");
                isLookupBuilt = true;
                return;
            }

            for (int groupIndex = 0; groupIndex < prefabGroups.Count; groupIndex++)
            {
                PrefabGroup group = prefabGroups[groupIndex];
                if (group == null)
                {
                    throw new InvalidOperationException($"Prefab group {groupIndex} is null.");
                }

                IReadOnlyList<PrefabEntry> entries = group.Entries;
                for (int entryIndex = 0; entryIndex < entries.Count; entryIndex++)
                {
                    PrefabEntry entry = entries[entryIndex];
                    if (entry == null)
                    {
                        throw new InvalidOperationException(
                            $"{group.Kind} prefab entry {entryIndex} is null.");
                    }

                    if (entry.PrefabId <= 0)
                    {
                        throw new InvalidOperationException(
                            $"{group.Kind} PrefabId must be positive, got {entry.PrefabId}.");
                    }

                    bool requiresLogicPrefab =
                        group.Kind == PrefabKind.Unit ||
                        group.Kind == PrefabKind.Projectile;
                    if (entry.UnityPrefab == null &&
                        (requiresLogicPrefab ||
                         string.IsNullOrEmpty(
                             entry.ClientViewAddress)))
                    {
                        throw new InvalidOperationException(
                            $"Resolved {group.Kind} prefab {entry.PrefabId} requires " +
                            (requiresLogicPrefab
                                ? "a loaded logic asset."
                                : "a loaded logic asset or client view address."));
                    }

                    if (!string.IsNullOrEmpty(entry.ClientViewAddress) &&
                        entry.ClientViewAddress.Trim() != entry.ClientViewAddress)
                    {
                        throw new InvalidOperationException(
                            $"{group.Kind} prefab {entry.PrefabId} client view address must not contain leading or trailing whitespace.");
                    }

                    (int start, int end) range =
                        ResolveRange(group.Kind);
                    if (range.start > 0 &&
                        (entry.PrefabId < range.start ||
                         entry.PrefabId > range.end))
                    {
                        throw new InvalidOperationException(
                            $"{group.Kind} PrefabId {entry.PrefabId} is outside the " +
                            $"configured range [{range.start}, {range.end}].");
                    }

                    long key = BuildKey(group.Kind, entry.PrefabId);
                    if (runtimeLookup.ContainsKey(key))
                    {
                        throw new InvalidOperationException(
                            $"Duplicate {group.Kind} PrefabId {entry.PrefabId}.");
                    }

                    runtimeLookup.Add(key, entry);
                }
            }

            isLookupBuilt = true;
        }

        private void ValidateRootPartitions()
        {
            var keys = new HashSet<long>();
            for (int i = 0; i < partitions.Count; i++)
            {
                GlobalPrefabPartitionReference partition =
                    partitions[i] ??
                    throw new InvalidOperationException(
                        $"Global prefab partition {i} is null.");
                partition.ValidateOrThrow();
                long key =
                    ((long)(byte)partition.PartitionKind << 32) |
                    (uint)partition.OwnerConfigId;
                if (!keys.Add(key))
                    throw new InvalidOperationException(
                        $"Duplicate {partition.PartitionKind} content partition owner {partition.OwnerConfigId}.");
            }
        }

        private void ValidateRanges()
        {
            for (int i = 0; i < kindRanges.Count; i++)
            {
                PrefabKindRangeConfig range =
                    kindRanges[i];
                if (range == null)
                    throw new InvalidOperationException(
                        $"Prefab kind range {i} is null.");
                if (range.IdRangeStart <= 0 ||
                    range.IdRangeEnd < range.IdRangeStart)
                    throw new InvalidOperationException(
                        $"Prefab kind range {i} is invalid: " +
                        $"[{range.IdRangeStart}, {range.IdRangeEnd}].");
                for (int j = i + 1;
                     j < kindRanges.Count;
                     j++)
                {
                    PrefabKindRangeConfig other =
                        kindRanges[j];
                    if (other == null)
                        continue;
                    if (other.Kind == range.Kind)
                        throw new InvalidOperationException(
                            $"Prefab kind range for {range.Kind} is defined twice.");
                    if (other.IdRangeStart <= range.IdRangeEnd &&
                        other.IdRangeEnd >= range.IdRangeStart)
                        throw new InvalidOperationException(
                            $"Prefab kind ranges overlap: {range.Kind} " +
                            $"[{range.IdRangeStart},{range.IdRangeEnd}] and " +
                            $"{other.Kind} [{other.IdRangeStart},{other.IdRangeEnd}].");
                }
            }
        }

        private (int start, int end) ResolveRange(
            PrefabKind kind)
        {
            for (int i = 0;
                 i < kindRanges.Count;
                 i++)
            {
                PrefabKindRangeConfig range =
                    kindRanges[i];
                if (range != null &&
                    range.Kind == kind)
                    return (range.IdRangeStart,
                        range.IdRangeEnd);
            }
            switch (kind)
            {
                case PrefabKind.Unit:
                    return (1000, 1999);
                case PrefabKind.Projectile:
                    return (2000, 2999);
                case PrefabKind.ParticleVfx:
                    return (3000, 3999);
                case PrefabKind.AudioEmitter:
                    return (4000, 4999);
                case PrefabKind.Misc:
                    return (5000, 5999);
                default:
                    return (0, 0);
            }
        }

        private static long BuildKey(PrefabKind kind, int prefabId)
        {
            return ((long)(byte)kind << 32) | (uint)prefabId;
        }

        private static bool ContainsSorted(
            IReadOnlyList<int> values,
            int value)
        {
            int low = 0;
            int high = values.Count - 1;
            while (low <= high)
            {
                int middle = low + ((high - low) / 2);
                int candidate = values[middle];
                if (candidate == value)
                    return true;
                if (candidate < value)
                    low = middle + 1;
                else
                    high = middle - 1;
            }
            return false;
        }

        private static List<PrefabKindRangeConfig> CloneRanges(
            IReadOnlyList<PrefabKindRangeConfig> source)
        {
            var clone =
                new List<PrefabKindRangeConfig>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                PrefabKindRangeConfig value = source[i];
                clone.Add(new PrefabKindRangeConfig
                {
                    Kind = value.Kind,
                    IdRangeStart = value.IdRangeStart,
                    IdRangeEnd = value.IdRangeEnd,
                });
            }
            return clone;
        }
    }
}
