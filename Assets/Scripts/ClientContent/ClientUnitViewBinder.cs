using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FrameSyncMoba.FrameSync;
using FrameSyncMoba.RuntimeConfig;
using FrameSyncMoba.Unit;
using UnityEngine;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.ClientContent
{
    public sealed class ClientUnitViewBinder : IDisposable
    {
        private sealed class Binding
        {
            public UnitType Unit;
            public CancellationTokenSource Cancellation;
            public IPresentationAssetLease<GameObject> Lease;
            public GameObject Instance;
        }

        private readonly UnitWorld unitWorld;
        private readonly GlobalPrefabTable prefabTable;
        private readonly IClientContentService contentService;
        private readonly Dictionary<UnitUid, Binding> bindings =
            new Dictionary<UnitUid, Binding>();
        private readonly List<UnitUid> staleUids = new List<UnitUid>();
        private bool isDisposed;

        public ClientUnitViewBinder(
            UnitWorld unitWorld,
            GlobalPrefabTable prefabTable,
            IClientContentService contentService)
        {
            this.unitWorld = unitWorld ??
                throw new ArgumentNullException(nameof(unitWorld));
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

            IReadOnlyList<UnitType> units = unitWorld.GetAllUnits();
            staleUids.Clear();
            foreach (KeyValuePair<UnitUid, Binding> pair in bindings)
                staleUids.Add(pair.Key);

            for (int i = 0; i < units.Count; i++)
            {
                UnitType unit = units[i];
                UnitUid uid = unit.UnitUid;
                staleUids.Remove(uid);
                if (bindings.TryGetValue(uid, out Binding existing))
                {
                    if (ReferenceEquals(existing.Unit, unit))
                        continue;
                    RemoveBinding(uid);
                }
                if (!prefabTable.TryGetEntry(
                        PrefabKind.Unit,
                        uid.RuntimeEntityPrefabId,
                        out PrefabEntry entry) ||
                    string.IsNullOrEmpty(entry.ClientViewAddress))
                    continue;

                var binding = new Binding
                {
                    Unit = unit,
                    Cancellation = new CancellationTokenSource(),
                };
                bindings.Add(uid, binding);
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
            staleUids.Clear();
            foreach (KeyValuePair<UnitUid, Binding> pair in bindings)
                staleUids.Add(pair.Key);
            for (int i = 0; i < staleUids.Count; i++)
                RemoveBinding(staleUids[i]);
            staleUids.Clear();
        }

        private async Task LoadAndBindAsync(
            UnitUid uid,
            string address,
            Binding binding)
        {
            IPresentationAssetLease<GameObject> lease = null;
            GameObject instance = null;
            try
            {
                lease = await contentService.AcquirePrefabAsync(
                    address,
                    binding.Cancellation.Token);
                if (binding.Cancellation.IsCancellationRequested ||
                    !bindings.TryGetValue(uid, out Binding current) ||
                    !ReferenceEquals(current, binding) ||
                    binding.Unit == null ||
                    binding.Unit.UnitUid != uid)
                    return;

                instance = UnityEngine.Object.Instantiate(
                    lease.Asset,
                    binding.Unit.transform,
                    false);
                instance.name = $"ClientView_{lease.Asset.name}_{uid}";
                UnitPresentationHost host =
                    instance.GetComponent<UnitPresentationHost>();
                if (host == null)
                    throw new InvalidOperationException(
                        $"Client view '{address}' requires UnitPresentationHost on its root.");
                host.Bind(binding.Unit);
                binding.Lease = lease;
                binding.Instance = instance;
                lease = null;
                instance = null;
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[ClientUnitView] Failed uid={uid} address='{address}': {exception}");
            }
            finally
            {
                if (instance != null)
                    UnityEngine.Object.Destroy(instance);
                lease?.Dispose();
            }
        }

        private void RemoveBinding(UnitUid uid)
        {
            if (!bindings.TryGetValue(uid, out Binding binding))
                return;
            bindings.Remove(uid);
            binding.Cancellation.Cancel();
            binding.Cancellation.Dispose();
            if (binding.Instance != null)
                UnityEngine.Object.Destroy(binding.Instance);
            binding.Lease?.Dispose();
        }
    }
}
