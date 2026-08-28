using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FrameSyncMoba.RuntimeConfig;
using FrameSyncMoba.Unit;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace FrameSyncMoba.Bootstrap
{
    public sealed class AddressableMatchContentScope : IDisposable
    {
        private static int activeScopeCount;
        private readonly List<AsyncOperationHandle> ownedHandles;
        private bool isDisposed;

        internal AddressableMatchContentScope(
            List<AsyncOperationHandle> handles,
            GlobalPrefabTable prefabTable,
            GlobalPrefabSubTableAsset[] subTables,
            UnitRuntimeCatalogAsset[] unitCatalogs,
            AbilityRuntimeCatalogAsset[] abilityCatalogs,
            ProjectileRuntimeCatalogAsset[] projectileCatalogs,
            BuffCatalogAsset[] buffCatalogs,
            CrowdControlCatalogAsset crowdControlCatalog,
            EquipmentCatalogAsset equipmentCatalog,
            DeterministicMapConfig mapConfig,
            MatchContentSelection selection)
        {
            ownedHandles = handles ??
                throw new ArgumentNullException(nameof(handles));
            PrefabTable = prefabTable ??
                throw new ArgumentNullException(nameof(prefabTable));
            SubTables = subTables ??
                throw new ArgumentNullException(nameof(subTables));
            UnitCatalogs = unitCatalogs ??
                throw new ArgumentNullException(nameof(unitCatalogs));
            AbilityCatalogs = abilityCatalogs ??
                throw new ArgumentNullException(nameof(abilityCatalogs));
            ProjectileCatalogs = projectileCatalogs ??
                throw new ArgumentNullException(nameof(projectileCatalogs));
            BuffCatalogs = buffCatalogs ??
                throw new ArgumentNullException(nameof(buffCatalogs));
            CrowdControlCatalog = crowdControlCatalog ??
                throw new ArgumentNullException(nameof(crowdControlCatalog));
            EquipmentCatalog = equipmentCatalog ??
                throw new ArgumentNullException(nameof(equipmentCatalog));
            MapConfig = mapConfig ??
                throw new ArgumentNullException(nameof(mapConfig));
            Selection = selection ??
                throw new ArgumentNullException(nameof(selection));
            Interlocked.Increment(ref activeScopeCount);
        }

        public GlobalPrefabTable PrefabTable { get; }
        public IReadOnlyList<GlobalPrefabSubTableAsset> SubTables { get; }
        public IReadOnlyList<UnitRuntimeCatalogAsset> UnitCatalogs { get; }
        public IReadOnlyList<AbilityRuntimeCatalogAsset> AbilityCatalogs { get; }
        public IReadOnlyList<ProjectileRuntimeCatalogAsset> ProjectileCatalogs { get; }
        public IReadOnlyList<BuffCatalogAsset> BuffCatalogs { get; }
        public CrowdControlCatalogAsset CrowdControlCatalog { get; }
        public EquipmentCatalogAsset EquipmentCatalog { get; }
        public DeterministicMapConfig MapConfig { get; }
        public MatchContentSelection Selection { get; }
        public int OwnedHandleCount => ownedHandles.Count;
        public static int ActiveScopeCount =>
            Volatile.Read(ref activeScopeCount);

        public void Dispose()
        {
            if (isDisposed)
                return;
            isDisposed = true;
            if (PrefabTable != null)
                UnityEngine.Object.Destroy(PrefabTable);
            for (int i = ownedHandles.Count - 1;
                 i >= 0;
                 i--)
            {
                AsyncOperationHandle handle =
                    ownedHandles[i];
                if (handle.IsValid())
                    Addressables.Release(handle);
            }
            ownedHandles.Clear();
            Interlocked.Decrement(ref activeScopeCount);
        }
    }

    public static class AddressableMatchContentService
    {
        private readonly struct LoadedPartition
        {
            public readonly GlobalPrefabPartitionReference Reference;
            public readonly GlobalPrefabSubTableAsset Asset;

            public LoadedPartition(
                GlobalPrefabPartitionReference reference,
                GlobalPrefabSubTableAsset asset)
            {
                Reference = reference;
                Asset = asset;
            }
        }

        public static async Task<AddressableMatchContentScope>
            LoadAsync(
                GlobalPrefabTable rootTable,
                MatchContentSelection selection,
                CancellationToken cancellationToken)
        {
            if (rootTable == null)
                throw new ArgumentNullException(nameof(rootTable));
            if (selection == null)
                throw new ArgumentNullException(nameof(selection));
            rootTable.ValidateOrThrow();

            var handles = new List<AsyncOperationHandle>();
            try
            {
                AsyncOperationHandle<UnityEngine.AddressableAssets.ResourceLocators.IResourceLocator>
                    initialization =
                        Addressables.InitializeAsync(false);
                handles.Add(initialization);
                await initialization.Task;
                cancellationToken.ThrowIfCancellationRequested();
                RequireSucceeded(
                    initialization,
                    "local Addressables initialization");

                IReadOnlyList<GlobalPrefabPartitionReference>
                    selectedReferences = rootTable.SelectPartitions(
                        selection.MapConfigId,
                        selection.HeroConfigIds);
                var partitions =
                    new List<LoadedPartition>(
                        selectedReferences.Count);
                for (int i = 0;
                     i < selectedReferences.Count;
                     i++)
                {
                    GlobalPrefabPartitionReference reference =
                        selectedReferences[i];
                    GlobalPrefabSubTableAsset asset =
                        await LoadAssetAsync<GlobalPrefabSubTableAsset>(
                            reference.SubTableAddress,
                            handles,
                            cancellationToken);
                    asset.ValidateAgainst(reference);
                    partitions.Add(
                        new LoadedPartition(reference, asset));
                }
                partitions.Sort(
                    (left, right) =>
                        GlobalPrefabPartitionReference.Compare(
                            left.Reference,
                            right.Reference));

                var resolvedPrefabs =
                    new Dictionary<string, GameObject>(
                        StringComparer.Ordinal);
                var unitCatalogs =
                    new List<UnitRuntimeCatalogAsset>();
                var abilityCatalogs =
                    new List<AbilityRuntimeCatalogAsset>();
                var projectileCatalogs =
                    new List<ProjectileRuntimeCatalogAsset>();
                var buffCatalogs =
                    new List<BuffCatalogAsset>();
                CrowdControlCatalogAsset crowdControlCatalog = null;
                EquipmentCatalogAsset equipmentCatalog = null;
                DeterministicMapConfig mapConfig = null;
                var subTables =
                    new GlobalPrefabSubTableAsset[partitions.Count];

                for (int partitionIndex = 0;
                     partitionIndex < partitions.Count;
                     partitionIndex++)
                {
                    GlobalPrefabSubTableAsset subTable =
                        partitions[partitionIndex].Asset;
                    subTables[partitionIndex] = subTable;
                    await LoadPrefabEntriesAsync(
                        subTable,
                        resolvedPrefabs,
                        handles,
                        cancellationToken);
                    IReadOnlyList<MatchContentAssetAddress>
                        contentAssets = subTable.ContentAssets;
                    for (int assetIndex = 0;
                         assetIndex < contentAssets.Count;
                         assetIndex++)
                    {
                        MatchContentAssetAddress content =
                            contentAssets[assetIndex];
                        switch (content.AssetKind)
                        {
                            case MatchContentAssetKind.UnitRuntimeCatalog:
                                unitCatalogs.Add(
                                    await LoadAssetAsync<UnitRuntimeCatalogAsset>(
                                        content.Address,
                                        handles,
                                        cancellationToken));
                                break;
                            case MatchContentAssetKind.AbilityRuntimeCatalog:
                                abilityCatalogs.Add(
                                    await LoadAssetAsync<AbilityRuntimeCatalogAsset>(
                                        content.Address,
                                        handles,
                                        cancellationToken));
                                break;
                            case MatchContentAssetKind.ProjectileRuntimeCatalog:
                                projectileCatalogs.Add(
                                    await LoadAssetAsync<ProjectileRuntimeCatalogAsset>(
                                        content.Address,
                                        handles,
                                        cancellationToken));
                                break;
                            case MatchContentAssetKind.BuffCatalog:
                                buffCatalogs.Add(
                                    await LoadAssetAsync<BuffCatalogAsset>(
                                        content.Address,
                                        handles,
                                        cancellationToken));
                                break;
                            case MatchContentAssetKind.CrowdControlCatalog:
                                crowdControlCatalog = RequireSingle(
                                    crowdControlCatalog,
                                    await LoadAssetAsync<CrowdControlCatalogAsset>(
                                        content.Address,
                                        handles,
                                        cancellationToken),
                                    content.AssetKind);
                                break;
                            case MatchContentAssetKind.EquipmentCatalog:
                                equipmentCatalog = RequireSingle(
                                    equipmentCatalog,
                                    await LoadAssetAsync<EquipmentCatalogAsset>(
                                        content.Address,
                                        handles,
                                        cancellationToken),
                                    content.AssetKind);
                                break;
                            case MatchContentAssetKind.DeterministicMapConfig:
                                mapConfig = RequireSingle(
                                    mapConfig,
                                    await LoadAssetAsync<DeterministicMapConfig>(
                                        content.Address,
                                        handles,
                                        cancellationToken),
                                    content.AssetKind);
                                break;
                            default:
                                throw new InvalidOperationException(
                                    $"Unsupported match content asset kind {content.AssetKind}.");
                        }
                    }
                }

                if (unitCatalogs.Count == 0 ||
                    abilityCatalogs.Count == 0 ||
                    projectileCatalogs.Count == 0 ||
                    buffCatalogs.Count == 0 ||
                    crowdControlCatalog == null ||
                    equipmentCatalog == null ||
                    mapConfig == null)
                    throw new InvalidOperationException(
                        "Loaded match content is missing one or more required deterministic catalogs.");

                GlobalPrefabTable resolvedTable =
                    rootTable.CreateResolvedRuntimeTable(
                        subTables,
                        resolvedPrefabs);
                Debug.Log(
                    $"[MatchContent] Loaded {selection}; partitions={subTables.Length} " +
                    $"prefabs={resolvedPrefabs.Count} handles={handles.Count}.");
                return new AddressableMatchContentScope(
                    handles,
                    resolvedTable,
                    subTables,
                    unitCatalogs.ToArray(),
                    abilityCatalogs.ToArray(),
                    projectileCatalogs.ToArray(),
                    buffCatalogs.ToArray(),
                    crowdControlCatalog,
                    equipmentCatalog,
                    mapConfig,
                    selection);
            }
            catch
            {
                for (int i = handles.Count - 1;
                     i >= 0;
                     i--)
                    if (handles[i].IsValid())
                        Addressables.Release(handles[i]);
                throw;
            }
        }

        private static async Task LoadPrefabEntriesAsync(
            GlobalPrefabSubTableAsset subTable,
            Dictionary<string, GameObject> resolvedPrefabs,
            List<AsyncOperationHandle> handles,
            CancellationToken cancellationToken)
        {
            IReadOnlyList<PrefabGroup> groups =
                subTable.PrefabGroups;
            for (int groupIndex = 0;
                 groupIndex < groups.Count;
                 groupIndex++)
            {
                IReadOnlyList<PrefabEntry> entries =
                    groups[groupIndex].Entries;
                for (int entryIndex = 0;
                     entryIndex < entries.Count;
                     entryIndex++)
                {
                    string address =
                        entries[entryIndex]
                            .LogicAssetAddress;
                    if (string.IsNullOrEmpty(address) ||
                        resolvedPrefabs.ContainsKey(address))
                        continue;
                    resolvedPrefabs.Add(
                        address,
                        await LoadAssetAsync<GameObject>(
                            address,
                            handles,
                            cancellationToken));
                }
            }
        }

        private static async Task<T> LoadAssetAsync<T>(
            string address,
            List<AsyncOperationHandle> handles,
            CancellationToken cancellationToken)
            where T : UnityEngine.Object
        {
            AsyncOperationHandle<T> handle =
                Addressables.LoadAssetAsync<T>(address);
            handles.Add(handle);
            T asset = await handle.Task;
            cancellationToken.ThrowIfCancellationRequested();
            RequireSucceeded(handle, address);
            if (asset == null)
                throw new InvalidOperationException(
                    $"Addressable match content '{address}' loaded a null {typeof(T).Name}.");
            return asset;
        }

        private static void RequireSucceeded<T>(
            AsyncOperationHandle<T> handle,
            string operation)
        {
            if (handle.Status !=
                AsyncOperationStatus.Succeeded)
                throw new InvalidOperationException(
                    $"Addressable match content operation '{operation}' failed.",
                    handle.OperationException);
        }

        private static T RequireSingle<T>(
            T current,
            T loaded,
            MatchContentAssetKind kind)
            where T : UnityEngine.Object
        {
            if (current != null)
                throw new InvalidOperationException(
                    $"Loaded match content defines {kind} more than once.");
            return loaded;
        }
    }
}
