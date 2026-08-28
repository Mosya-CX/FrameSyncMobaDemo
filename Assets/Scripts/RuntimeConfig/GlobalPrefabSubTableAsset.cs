using System;
using System.Collections.Generic;
using UnityEngine;

namespace FrameSyncMoba.RuntimeConfig
{
    public enum GlobalPrefabPartitionKind : byte
    {
        Core = 0,
        Map = 1,
        Hero = 2,
        Shared = 3,
    }

    public enum MatchContentAssetKind : byte
    {
        UnitRuntimeCatalog = 0,
        AbilityRuntimeCatalog = 1,
        ProjectileRuntimeCatalog = 2,
        BuffCatalog = 3,
        CrowdControlCatalog = 4,
        EquipmentCatalog = 5,
        DeterministicMapConfig = 6,
    }

    [Serializable]
    public sealed class MatchContentAssetAddress
    {
        [SerializeField] private MatchContentAssetKind assetKind;
        [SerializeField] private string address;

        public MatchContentAssetKind AssetKind => assetKind;
        public string Address => address ?? string.Empty;

        public MatchContentAssetAddress(
            MatchContentAssetKind assetKind,
            string address)
        {
            this.assetKind = assetKind;
            this.address = address ?? string.Empty;
        }

        internal void ValidateOrThrow(string partitionName)
        {
            if (!Enum.IsDefined(
                    typeof(MatchContentAssetKind),
                    assetKind))
                throw new InvalidOperationException(
                    $"Partition '{partitionName}' has undefined content asset kind {assetKind}.");
            ValidateAddress(
                Address,
                $"Partition '{partitionName}' {assetKind}");
        }

        internal static void ValidateAddress(
            string value,
            string owner)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.Trim() != value)
                throw new InvalidOperationException(
                    $"{owner} requires a non-empty trimmed local Addressables address.");
        }
    }

    [Serializable]
    public sealed class GlobalPrefabPartitionReference
    {
        [SerializeField] private GlobalPrefabPartitionKind partitionKind;
        [SerializeField, Min(0)] private int ownerConfigId;
        [SerializeField] private string subTableAddress;
        [SerializeField, Min(1)] private uint contentVersion = 1;
        [SerializeField] private ulong contentHash;

        public GlobalPrefabPartitionKind PartitionKind => partitionKind;
        public int OwnerConfigId => ownerConfigId;
        public string SubTableAddress => subTableAddress ?? string.Empty;
        public uint ContentVersion => contentVersion;
        public ulong ContentHash => contentHash;

        public GlobalPrefabPartitionReference(
            GlobalPrefabPartitionKind partitionKind,
            int ownerConfigId,
            string subTableAddress,
            uint contentVersion,
            ulong contentHash)
        {
            this.partitionKind = partitionKind;
            this.ownerConfigId = ownerConfigId;
            this.subTableAddress = subTableAddress ?? string.Empty;
            this.contentVersion = contentVersion;
            this.contentHash = contentHash;
        }

        public void ValidateOrThrow()
        {
            if (!Enum.IsDefined(
                    typeof(GlobalPrefabPartitionKind),
                    partitionKind))
                throw new InvalidOperationException(
                    $"Undefined content partition kind {partitionKind}.");
            if ((partitionKind == GlobalPrefabPartitionKind.Core ||
                 partitionKind == GlobalPrefabPartitionKind.Shared) &&
                ownerConfigId != 0)
                throw new InvalidOperationException(
                    $"{partitionKind} partition owner must be 0.");
            if ((partitionKind == GlobalPrefabPartitionKind.Map ||
                 partitionKind == GlobalPrefabPartitionKind.Hero) &&
                ownerConfigId <= 0)
                throw new InvalidOperationException(
                    $"{partitionKind} partition owner must be positive.");
            if (contentVersion == 0 || contentHash == 0)
                throw new InvalidOperationException(
                    $"{partitionKind}/{ownerConfigId} requires non-zero content version and hash.");
            MatchContentAssetAddress.ValidateAddress(
                SubTableAddress,
                $"{partitionKind}/{ownerConfigId} child table");
        }

        public static int Compare(
            GlobalPrefabPartitionReference left,
            GlobalPrefabPartitionReference right)
        {
            int kind = ((byte)left.partitionKind)
                .CompareTo((byte)right.partitionKind);
            return kind != 0
                ? kind
                : left.ownerConfigId.CompareTo(right.ownerConfigId);
        }
    }

    [CreateAssetMenu(
        fileName = "GlobalPrefabSubTable",
        menuName = "FrameSyncMoba/Runtime/Global Prefab Sub-Table")]
    public sealed class GlobalPrefabSubTableAsset : ScriptableObject
    {
        [SerializeField] private GlobalPrefabPartitionKind partitionKind;
        [SerializeField, Min(0)] private int ownerConfigId;
        [SerializeField, Min(1)] private uint contentVersion = 1;
        [SerializeField] private ulong contentHash;
        [SerializeField] private List<PrefabGroup> prefabGroups =
            new List<PrefabGroup>();
        [SerializeField] private List<MatchContentAssetAddress>
            contentAssets = new List<MatchContentAssetAddress>();

        public GlobalPrefabPartitionKind PartitionKind => partitionKind;
        public int OwnerConfigId => ownerConfigId;
        public uint ContentVersion => contentVersion;
        public ulong ContentHash => contentHash;
        public IReadOnlyList<PrefabGroup> PrefabGroups => prefabGroups;
        public IReadOnlyList<MatchContentAssetAddress> ContentAssets =>
            contentAssets;

        public void ValidateAgainst(
            GlobalPrefabPartitionReference expected)
        {
            if (expected == null)
                throw new ArgumentNullException(nameof(expected));
            ValidateOrThrow();
            if (partitionKind != expected.PartitionKind ||
                ownerConfigId != expected.OwnerConfigId ||
                contentVersion != expected.ContentVersion ||
                contentHash != expected.ContentHash)
                throw new InvalidOperationException(
                    $"Loaded content partition {partitionKind}/{ownerConfigId} version {contentVersion} hash {contentHash} does not match root expectation {expected.PartitionKind}/{expected.OwnerConfigId} version {expected.ContentVersion} hash {expected.ContentHash}.");
        }

        public void ValidateOrThrow()
        {
            var reference = new GlobalPrefabPartitionReference(
                partitionKind,
                ownerConfigId,
                "validation/child-table",
                contentVersion,
                contentHash);
            reference.ValidateOrThrow();

            var prefabKeys = new HashSet<long>();
            for (int groupIndex = 0;
                 groupIndex < prefabGroups.Count;
                 groupIndex++)
            {
                PrefabGroup group = prefabGroups[groupIndex] ??
                    throw new InvalidOperationException(
                        $"Partition '{name}' prefab group {groupIndex} is null.");
                for (int entryIndex = 0;
                     entryIndex < group.Entries.Count;
                     entryIndex++)
                {
                    PrefabEntry entry = group.Entries[entryIndex] ??
                        throw new InvalidOperationException(
                            $"Partition '{name}' {group.Kind} entry {entryIndex} is null.");
                    if (entry.PrefabId <= 0 ||
                        entry.HasLegacyDirectReference)
                        throw new InvalidOperationException(
                            $"Partition '{name}' {group.Kind}/{entry.PrefabId} must be path-only with a positive ID.");
                    bool requiresLogicAsset =
                        group.Kind == PrefabKind.Unit ||
                        group.Kind == PrefabKind.Projectile;
                    if (requiresLogicAsset ||
                        !string.IsNullOrEmpty(
                            entry.LogicAssetAddress))
                    {
                        MatchContentAssetAddress.ValidateAddress(
                            entry.LogicAssetAddress,
                            $"Partition '{name}' {group.Kind}/{entry.PrefabId} logic asset");
                    }
                    else if (string.IsNullOrWhiteSpace(
                                 entry.ClientViewAddress))
                    {
                        throw new InvalidOperationException(
                            $"Partition '{name}' {group.Kind}/{entry.PrefabId} requires a logic or client-view address.");
                    }
                    long key =
                        ((long)(byte)group.Kind << 32) |
                        (uint)entry.PrefabId;
                    if (!prefabKeys.Add(key))
                        throw new InvalidOperationException(
                            $"Partition '{name}' contains duplicate {group.Kind}/{entry.PrefabId}.");
                }
            }

            var assetKinds = new HashSet<MatchContentAssetKind>();
            for (int i = 0; i < contentAssets.Count; i++)
            {
                MatchContentAssetAddress asset = contentAssets[i] ??
                    throw new InvalidOperationException(
                        $"Partition '{name}' content asset {i} is null.");
                asset.ValidateOrThrow(name);
                if (!assetKinds.Add(asset.AssetKind))
                    throw new InvalidOperationException(
                        $"Partition '{name}' defines {asset.AssetKind} twice.");
            }
        }

        internal void ReplaceForTests(
            GlobalPrefabPartitionKind kind,
            int ownerId,
            uint version,
            ulong hash,
            IEnumerable<PrefabGroup> groups,
            IEnumerable<MatchContentAssetAddress> assets)
        {
            partitionKind = kind;
            ownerConfigId = ownerId;
            contentVersion = version;
            contentHash = hash;
            prefabGroups.Clear();
            if (groups != null)
                prefabGroups.AddRange(groups);
            contentAssets.Clear();
            if (assets != null)
                contentAssets.AddRange(assets);
        }

#if UNITY_EDITOR
        public void ConfigureForEditor(
            GlobalPrefabPartitionKind kind,
            int ownerId,
            uint version,
            ulong hash,
            IEnumerable<PrefabGroup> groups,
            IEnumerable<MatchContentAssetAddress> assets)
        {
            ReplaceForTests(
                kind,
                ownerId,
                version,
                hash,
                groups,
                assets);
        }
#endif
    }
}
