using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FrameSyncMoba.RuntimeConfig;
using FrameSyncMoba.Unit;
using UnityEngine;
using FrameSyncMoba.FrameSync;
using Debug = UnityEngine.Debug;

namespace FrameSyncMoba.ClientContent
{
    public sealed class ClientProjectileViewBinder : IDisposable
    {
        private sealed class Binding
        {
            public ProjectileRuntime Runtime;
            public CancellationTokenSource Cancellation;
            public GameObject Instance;
        }

        private readonly ProjectileWorld projectileWorld;
        private readonly GlobalPrefabTable prefabTable;
        private readonly IClientPresentationAssetLoader contentService;
        private readonly Dictionary<ProjectileUid, Binding> bindings =
            new Dictionary<ProjectileUid, Binding>();
        private readonly List<ProjectileUid> staleUids =
            new List<ProjectileUid>();
        // Projectiles are short-lived (attack missiles). Holding one lease
        // per view address keeps the prefab resident for the whole match so
        // every missile instantiates from the cached asset immediately;
        // otherwise the last lease disposal unloads the asset and the next
        // missile races an asynchronous reload that outlives it.
        private readonly Dictionary<string,
            IPresentationAssetLease<GameObject>>
            addressLeases =
                new Dictionary<string,
                    IPresentationAssetLease<GameObject>>();
        private readonly CancellationTokenSource
            lifetimeCancellation =
                new CancellationTokenSource();
        private bool isDisposed;

        public ClientProjectileViewBinder(
            ProjectileWorld projectileWorld,
            GlobalPrefabTable prefabTable,
            IClientPresentationAssetLoader contentService)
        {
            this.projectileWorld = projectileWorld ??
                throw new ArgumentNullException(nameof(projectileWorld));
            this.prefabTable = prefabTable ??
                throw new ArgumentNullException(nameof(prefabTable));
            this.contentService = contentService ??
                throw new ArgumentNullException(nameof(contentService));
        }

        public int BindingCount => bindings.Count;

        public void Reconcile()
        {
            if (isDisposed)
                return;
            IReadOnlyList<ProjectileRuntime> projectiles =
                projectileWorld.GetAllOrdered();
            staleUids.Clear();
            foreach (KeyValuePair<ProjectileUid, Binding> pair in bindings)
                staleUids.Add(pair.Key);

            for (int i = 0; i < projectiles.Count; i++)
            {
                ProjectileRuntime runtime = projectiles[i];
                ProjectileUid uid = runtime.Uid;
                staleUids.Remove(uid);
                if (bindings.TryGetValue(uid, out Binding existing))
                {
                    if (ReferenceEquals(existing.Runtime, runtime))
                        continue;
                    RemoveBinding(uid);
                }
                if (!prefabTable.TryGetEntry(
                        PrefabKind.Projectile,
                        uid.RuntimeEntityPrefabId,
                        out PrefabEntry entry) ||
                    string.IsNullOrEmpty(entry.ClientViewAddress))
                    continue;
                var binding = new Binding
                {
                    Runtime = runtime,
                    Cancellation = new CancellationTokenSource(),
                };
                bindings.Add(uid, binding);
                Debug.Log(
                    $"[ClientProjectileView] bind begin uid={uid} " +
                    $"spawnTick={uid.SpawnLogicTick} " +
                    $"prefabId={uid.RuntimeEntityPrefabId} " +
                    $"address='{entry.ClientViewAddress}'");
                _ = LoadAndBindAsync(
                    uid,
                    entry.ClientViewAddress,
                    binding);
            }

            for (int i = 0; i < staleUids.Count; i++)
                RemoveBinding(staleUids[i]);
        }

        public void Dispose()
        {
            if (isDisposed)
                return;
            isDisposed = true;
            lifetimeCancellation.Cancel();
            staleUids.Clear();
            foreach (KeyValuePair<ProjectileUid, Binding> pair in bindings)
                staleUids.Add(pair.Key);
            for (int i = 0; i < staleUids.Count; i++)
                RemoveBinding(staleUids[i]);
            staleUids.Clear();
            foreach (KeyValuePair<string,
                     IPresentationAssetLease<GameObject>> pair
                     in addressLeases)
            {
                pair.Value.Dispose();
            }
            addressLeases.Clear();
            lifetimeCancellation.Dispose();
        }

        private async Task LoadAndBindAsync(
            ProjectileUid uid,
            string address,
            Binding binding)
        {
            Stopwatch bindTimer = Stopwatch.StartNew();
            GameObject instance = null;
            try
            {
                IPresentationAssetLease<GameObject> lease =
                    await GetOrCreateAddressLeaseAsync(
                        address);
                if (binding.Cancellation.IsCancellationRequested ||
                    !bindings.TryGetValue(uid, out Binding current) ||
                    !ReferenceEquals(current, binding) ||
                    binding.Runtime == null ||
                    binding.Runtime.Uid != uid ||
                    binding.Runtime.PhysicsEntity == null)
                    return;
                instance =
                    UnityEngine.Object.Instantiate(
                        lease.Asset,
                        binding.Runtime.PhysicsEntity
                            .transform,
                        false);
                instance.name =
                    $"ClientView_{lease.Asset.name}_{uid}";
                binding.Instance = instance;
                instance = null;
                Debug.Log(
                    $"[ClientProjectileView] bind complete uid={uid} " +
                    $"spawnTick={uid.SpawnLogicTick} address='{address}' " +
                    $"elapsedMs={bindTimer.ElapsedMilliseconds}");
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[ClientProjectileView] Failed uid={uid} address='{address}': {exception}");
            }
            finally
            {
                if (instance != null)
                    UnityEngine.Object.Destroy(instance);
            }
        }

        private async Task<
            IPresentationAssetLease<GameObject>>
            GetOrCreateAddressLeaseAsync(
                string address)
        {
            if (addressLeases.TryGetValue(
                    address,
                    out IPresentationAssetLease<GameObject>
                        existing) &&
                existing != null)
            {
                return existing;
            }
            IPresentationAssetLease<GameObject> lease =
                await contentService.AcquirePrefabAsync(
                    address,
                    lifetimeCancellation.Token);
            if (isDisposed)
            {
                lease.Dispose();
                throw new OperationCanceledException();
            }
            if (addressLeases.TryGetValue(
                    address,
                    out existing) &&
                existing != null)
            {
                lease.Dispose();
                return existing;
            }
            addressLeases.Add(address, lease);
            return lease;
        }

        private void RemoveBinding(ProjectileUid uid)
        {
            if (!bindings.TryGetValue(uid, out Binding binding))
                return;
            bindings.Remove(uid);
            binding.Cancellation.Cancel();
            binding.Cancellation.Dispose();
            if (binding.Instance != null)
                UnityEngine.Object.Destroy(binding.Instance);
        }
    }
}
