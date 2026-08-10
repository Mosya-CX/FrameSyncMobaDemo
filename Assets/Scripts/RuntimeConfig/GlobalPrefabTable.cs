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
        [SerializeField] private GameObject unityPrefab;
        [SerializeField] private int gameplayConfigId;
        [SerializeField, HideInInspector] private string editorAssetGuid;

        public int PrefabId => prefabId;
        public GameObject UnityPrefab => unityPrefab;
        public int GameplayConfigId => gameplayConfigId;
        public string EditorAssetGuid => editorAssetGuid;

        public PrefabEntry(
            int prefabId,
            GameObject unityPrefab,
            int gameplayConfigId = 0,
            string editorAssetGuid = null)
        {
            this.prefabId = prefabId;
            this.unityPrefab = unityPrefab;
            this.gameplayConfigId = gameplayConfigId;
            this.editorAssetGuid = editorAssetGuid ?? string.Empty;
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
        [Header("Stable runtime prefab mappings")]
        [Tooltip("Entries are resolved by the fixed PrefabKind plus PrefabId pair.")]
        [SerializeField] private List<PrefabGroup> prefabGroups = new List<PrefabGroup>();
        [Header("Per-kind ID ranges (design v10.2 17.5)")]
        [Tooltip("Configured ranges override the built-in defaults. IDs must stay inside their kind range.")]
        [SerializeField] private List<PrefabKindRangeConfig> kindRanges =
            new List<PrefabKindRangeConfig>();

        private readonly Dictionary<long, GameObject> runtimeLookup =
            new Dictionary<long, GameObject>();
        private bool isLookupBuilt;

        public IReadOnlyList<PrefabGroup> PrefabGroups => prefabGroups;
        public IReadOnlyList<PrefabKindRangeConfig> KindRanges =>
            kindRanges;

        public bool TryGetPrefab(PrefabKind kind, int prefabId, out GameObject prefab)
        {
            EnsureLookup();
            return runtimeLookup.TryGetValue(BuildKey(kind, prefabId), out prefab);
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

        internal void ReplaceGroupsForTests(IEnumerable<PrefabGroup> groups)
        {
            prefabGroups.Clear();
            if (groups != null)
            {
                prefabGroups.AddRange(groups);
            }

            isLookupBuilt = false;
        }

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

                    if (entry.UnityPrefab == null)
                    {
                        throw new InvalidOperationException(
                            $"{group.Kind} prefab {entry.PrefabId} has no Unity prefab assigned.");
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

                    runtimeLookup.Add(key, entry.UnityPrefab);
                }
            }

            isLookupBuilt = true;
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
    }
}
