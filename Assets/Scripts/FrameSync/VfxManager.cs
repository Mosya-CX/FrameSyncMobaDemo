using System.Collections.Generic;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace FrameSyncMoba.FrameSync
{
    public sealed class VfxManager : MonoBehaviour
    {
        [SerializeField] private VfxLibrary _library;
        [SerializeField] private int _defaultPoolSize = 16;

        private readonly Dictionary<int, Queue<GameObject>> _poolByDefId =
            new Dictionary<int, Queue<GameObject>>();
        private readonly Dictionary<int, IPresentationAssetLease<GameObject>>
            _prefabLeases =
                new Dictionary<int, IPresentationAssetLease<GameObject>>();
        private readonly Dictionary<int, Task<IPresentationAssetLease<GameObject>>>
            _pendingLoads =
                new Dictionary<int, Task<IPresentationAssetLease<GameObject>>>();
        private IClientPresentationAssetLoader _assetLoader;
        private CancellationTokenSource _lifetimeCancellation;

        private void Awake()
        {
            _lifetimeCancellation = new CancellationTokenSource();
        }

        public void SetAssetLoader(IClientPresentationAssetLoader assetLoader)
        {
            _assetLoader = assetLoader;
        }

        public void SetLibrary(VfxLibrary library)
        {
            _library = library;
            foreach (Queue<GameObject> queue in _poolByDefId.Values)
                while (queue.Count > 0)
                    DestroyOwnedInstance(queue.Dequeue());
            ReleasePrefabLeases();
            _poolByDefId.Clear();
        }

        /// <summary>
        /// Loads shared entries and entries owned by selected heroes and
        /// creates one inactive pool instance before Gameplay can emit its
        /// first event. The manager retains both leases and instances for its
        /// normal lifetime, so warmup does not introduce a second resource
        /// owner.
        /// </summary>
        public async Task PreloadAsync(
            CancellationToken cancellationToken)
        {
            await PreloadAsync(
                null,
                cancellationToken);
        }

        /// <summary>
        /// Loads only shared VFX and entries owned by one of the selected
        /// heroes. A null hero list preserves legacy/full-library warmup for
        /// standalone scenes that do not have a match content scope.
        /// </summary>
        public async Task PreloadAsync(
            IReadOnlyList<int> selectedHeroConfigIds,
            CancellationToken cancellationToken)
        {
            if (_library == null)
                throw new InvalidOperationException(
                    "VfxManager requires a VfxLibrary before preload.");
            if (_assetLoader == null)
                throw new InvalidOperationException(
                    "VfxManager requires an asset loader before preload.");

            Stopwatch total = Stopwatch.StartNew();
            int loadedEntries = 0;
            Debug.Log(
                $"[VfxPreload] begin entries={_library.Count} " +
                $"manager={name}");
            for (int i = 0; i < _library.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                VfxLibrary.VfxPrefabEntry entry =
                    _library.GetEntry(i);
                if (entry.VfxDefId <= 0 ||
                    string.IsNullOrWhiteSpace(entry.Address))
                {
                    throw new InvalidOperationException(
                        $"VfxLibrary entry {i} must have a positive " +
                        "VfxDefId and a non-empty Address.");
                }
                if (selectedHeroConfigIds != null &&
                    entry.OwnerHeroConfigId > 0 &&
                    !ContainsHeroConfigId(
                        selectedHeroConfigIds,
                        entry.OwnerHeroConfigId))
                {
                    Debug.Log(
                        $"[VfxPreload] skip id={entry.VfxDefId} " +
                        $"address={entry.Address} " +
                        $"ownerHero={entry.OwnerHeroConfigId}");
                    continue;
                }

                Stopwatch entryTimer = Stopwatch.StartNew();
                GameObject prefab = await GetPrefabAsync(
                    entry.VfxDefId,
                    cancellationToken);
                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        $"VFX {entry.VfxDefId} at '{entry.Address}' " +
                        "resolved to a null prefab.");
                }
                bool createdPoolInstance =
                    EnsureWarmPoolInstance(
                        entry.VfxDefId,
                        prefab);
                Debug.Log(
                    $"[VfxPreload] entry={entry.VfxDefId} " +
                    $"address={entry.Address} elapsedMs={entryTimer.ElapsedMilliseconds} " +
                    $"ownerHero={entry.OwnerHeroConfigId} " +
                    $"createdPoolInstance={createdPoolInstance}");
                loadedEntries++;
            }
            Debug.Log(
                $"[VfxPreload] complete entries={loadedEntries}/{_library.Count} " +
                $"elapsedMs={total.ElapsedMilliseconds} manager={name}");
        }

        private static bool ContainsHeroConfigId(
            IReadOnlyList<int> selectedHeroConfigIds,
            int heroConfigId)
        {
            for (int i = 0; i < selectedHeroConfigIds.Count; i++)
                if (selectedHeroConfigIds[i] == heroConfigId)
                    return true;
            return false;
        }

        public async void PlayOrReconcile(Unit.VfxEvent evt)
        {
            if (_library == null)
            {
                Debug.Log(string.Format("[VfxManager] VFX {0} (no VfxLibrary configured)", evt.VfxDefId));
                return;
            }

            Stopwatch playTimer = Stopwatch.StartNew();
            bool prefabCacheHit =
                _prefabLeases.ContainsKey(evt.VfxDefId);
            GameObject prefab;
            try
            {
                prefab = await GetPrefabAsync(
                    evt.VfxDefId,
                    _lifetimeCancellation.Token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception exception)
            {
                Debug.LogError(
                    $"[VfxManager] VFX {evt.VfxDefId} load failed: {exception}");
                return;
            }
            if (prefab == null)
            {
                Debug.LogWarning(string.Format("[VfxManager] VFX {0}: prefab not found", evt.VfxDefId));
                return;
            }

            bool poolHit = HasPooledInstance(evt.VfxDefId);
            GameObject instance = GetFromPool(evt.VfxDefId, prefab);
            if (instance == null)
                return;

            Vector3 targetPosition = new Vector3(
                (float)evt.WorldPosition.x, 0f, (float)evt.WorldPosition.y);
            Vector3 sourcePosition = targetPosition;
            bool attachToUnit = false;
            UnitPresentationHost attachHost = null;
            if (evt.AttachToUnit.HasValue &&
                evt.AttachToUnit.Value.IsValid() &&
                UnitPresentationRegistry.TryGetHost(
                    evt.AttachToUnit.Value,
                    out attachHost) &&
                attachHost != null)
            {
                attachToUnit = true;
            }
            if (evt.Id.SourceRuntimeUid.IsValid() &&
                UnitPresentationRegistry.TryGetHost(
                    evt.Id.SourceRuntimeUid,
                    out UnitPresentationHost sourceHost) &&
                sourceHost != null)
            {
                sourcePosition = sourceHost.transform.position;
            }

            if (attachToUnit)
            {
                instance.transform.SetParent(
                    attachHost.transform,
                    false);
                instance.transform.localPosition =
                    Vector3.zero;
            }
            else
            {
                instance.transform.SetParent(
                    transform,
                    false);
                instance.transform.position =
                    targetPosition;
            }
            instance.SetActive(true);

            Vector3 eventDirection = new Vector3(
                (float)evt.WorldDirection.x,
                0f,
                (float)evt.WorldDirection.y);
            if (eventDirection.sqrMagnitude > 0.0001f)
                instance.transform.rotation =
                    Quaternion.LookRotation(eventDirection.normalized);
            VfxWorldDirectionLock directionLock =
                instance.GetComponent<VfxWorldDirectionLock>();
            if (attachToUnit &&
                eventDirection.sqrMagnitude > 0.0001f)
            {
                if (directionLock == null)
                {
                    directionLock = instance.AddComponent<
                        VfxWorldDirectionLock>();
                }
                directionLock.Begin(eventDirection);
            }
            else
            {
                directionLock?.ResetForPool();
            }

            VfxUnitTargetLineBinder targetLine =
                instance.GetComponent<VfxUnitTargetLineBinder>();
            if (targetLine != null)
                targetLine.Begin(targetPosition, evt.TargetUnit);

            VfxPlaybackHost playbackHost =
                instance.GetComponent<VfxPlaybackHost>();
            float duration;
            if (playbackHost != null)
            {
                duration = playbackHost.BeginPlayback(
                    sourcePosition,
                    targetPosition,
                    Mathf.Max(0.01f, (float)evt.DurationScale));
            }
            else
            {
                duration = PlayParticleFallback(
                    instance,
                    Mathf.Max(0.01f, (float)evt.DurationScale));
            }
            if (attachToUnit)
            {
                instance.transform.localPosition =
                    Vector3.zero;
            }

            StartCoroutine(ReturnAfterPlay(
                instance,
                evt.VfxDefId,
                duration));
            Debug.Log(
                $"[VfxPlayback] id={evt.VfxDefId} " +
                $"sourceTick={evt.Id.SourceLogicTick} " +
                $"prefabCacheHit={prefabCacheHit} poolHit={poolHit} " +
                $"prepareMs={playTimer.ElapsedMilliseconds} " +
                $"durationMs={Mathf.RoundToInt(duration * 1000f)}");
        }

        private async Task<GameObject> GetPrefabAsync(
            int vfxDefId,
            CancellationToken cancellationToken)
        {
            if (_prefabLeases.TryGetValue(
                    vfxDefId,
                    out IPresentationAssetLease<GameObject> cached))
                return cached.Asset;
            if (_assetLoader == null)
                return null;
            string address = _library.GetAddress(vfxDefId);
            if (string.IsNullOrEmpty(address))
                return null;
            if (!_pendingLoads.TryGetValue(
                    vfxDefId,
                    out Task<IPresentationAssetLease<GameObject>> pending))
            {
                pending = _assetLoader.AcquirePrefabAsync(
                    address,
                    cancellationToken);
                _pendingLoads.Add(vfxDefId, pending);
            }
            IPresentationAssetLease<GameObject> lease;
            try
            {
                lease = await pending;
            }
            finally
            {
                _pendingLoads.Remove(vfxDefId);
            }
            if (!_prefabLeases.ContainsKey(vfxDefId))
                _prefabLeases.Add(vfxDefId, lease);
            else if (!ReferenceEquals(_prefabLeases[vfxDefId], lease))
                lease.Dispose();
            return _prefabLeases[vfxDefId].Asset;
        }

        private bool EnsureWarmPoolInstance(
            int vfxDefId,
            GameObject prefab)
        {
            if (HasPooledInstance(vfxDefId))
                return false;
            GameObject instance = GetFromPool(
                vfxDefId,
                prefab);
            ReturnToPool(
                vfxDefId,
                instance);
            return true;
        }

        private bool HasPooledInstance(int vfxDefId)
        {
            return _poolByDefId.TryGetValue(
                    vfxDefId,
                    out Queue<GameObject> queue) &&
                queue.Count > 0;
        }

        private void OnDestroy()
        {
            _lifetimeCancellation?.Cancel();
            foreach (Queue<GameObject> queue in _poolByDefId.Values)
                while (queue.Count > 0)
                    DestroyOwnedInstance(queue.Dequeue());
            ReleasePrefabLeases();
            _lifetimeCancellation?.Dispose();
        }

        private void ReleasePrefabLeases()
        {
            foreach (IPresentationAssetLease<GameObject> lease in
                     _prefabLeases.Values)
                lease.Dispose();
            _prefabLeases.Clear();
            _pendingLoads.Clear();
        }

        private static void DestroyOwnedInstance(
            GameObject instance)
        {
            if (instance == null)
                return;
            if (Application.isPlaying)
                Destroy(instance);
            else
                DestroyImmediate(instance);
        }

        private GameObject GetFromPool(int vfxDefId, GameObject prefab)
        {
            if (!_poolByDefId.TryGetValue(vfxDefId, out Queue<GameObject> queue))
            {
                queue = new Queue<GameObject>();
                _poolByDefId[vfxDefId] = queue;
            }

            if (queue.Count > 0)
                return queue.Dequeue();

            GameObject instance = UnityEngine.Object.Instantiate(prefab, transform);
            instance.name = string.Format("VFX_{0}_{1}", vfxDefId, _poolByDefId.Count);
            return instance;
        }

        private void ReturnToPool(int vfxDefId, GameObject instance)
        {
            VfxUnitTargetLineBinder targetLine =
                instance.GetComponent<VfxUnitTargetLineBinder>();
            targetLine?.ResetForPool();
            VfxPlaybackHost playbackHost =
                instance.GetComponent<VfxPlaybackHost>();
            if (playbackHost != null)
                playbackHost.ResetForPool();
            instance.GetComponent<VfxWorldDirectionLock>()
                ?.ResetForPool();
            instance.transform.SetParent(
                transform,
                false);
            instance.transform.localPosition =
                Vector3.zero;
            instance.transform.localRotation =
                Quaternion.identity;
            instance.transform.localScale =
                Vector3.one;
            instance.SetActive(false);
            if (!_poolByDefId.TryGetValue(vfxDefId, out Queue<GameObject> queue))
            {
                queue = new Queue<GameObject>();
                _poolByDefId[vfxDefId] = queue;
            }
            queue.Enqueue(instance);
        }

        private static float PlayParticleFallback(
            GameObject instance,
            float durationScale)
        {
            ParticleSystem[] systems = instance.GetComponentsInChildren<ParticleSystem>();
            float maxDuration = 1f;
            for (int i = 0; i < systems.Length; i++)
            {
                systems[i].Play();
                float dur = systems[i].main.duration + systems[i].main.startLifetime.constantMax;
                if (dur > maxDuration) maxDuration = dur;
            }
            return maxDuration * durationScale;
        }

        private System.Collections.IEnumerator ReturnAfterPlay(
            GameObject instance,
            int vfxDefId,
            float duration)
        {
            yield return new WaitForSeconds(
                Mathf.Max(0.01f, duration) + 0.1f);
            ReturnToPool(vfxDefId, instance);
        }
    }

}
