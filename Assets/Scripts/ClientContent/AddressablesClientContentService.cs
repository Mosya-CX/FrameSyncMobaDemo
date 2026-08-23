using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using FrameSyncMoba.FrameSync;

namespace FrameSyncMoba.ClientContent
{
    public sealed class AddressablesClientContentService : IClientContentService
    {
        private sealed class CacheEntry
        {
            public AsyncOperationHandle<GameObject> Handle;
            public int LeaseCount;
        }

        private sealed class AudioCacheEntry
        {
            public AsyncOperationHandle<AudioClip> Handle;
            public int LeaseCount;
        }

        private sealed class SpriteCacheEntry
        {
            public AsyncOperationHandle<Sprite> Handle;
            public int LeaseCount;
        }

        private readonly Dictionary<string, CacheEntry> cache =
            new Dictionary<string, CacheEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, AudioCacheEntry> audioCache =
            new Dictionary<string, AudioCacheEntry>(StringComparer.Ordinal);
        private readonly Dictionary<string, SpriteCacheEntry> spriteCache =
            new Dictionary<string, SpriteCacheEntry>(StringComparer.Ordinal);
        private AsyncOperationHandle<IResourceLocator> initializationHandle;
        private bool ownsInitializationHandle;
        private bool isDisposed;

        public bool IsInitialized { get; private set; }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (IsInitialized)
                return;

            if (!ownsInitializationHandle)
            {
                initializationHandle = Addressables.InitializeAsync(false);
                ownsInitializationHandle = true;
            }

            await initializationHandle.Task;
            cancellationToken.ThrowIfCancellationRequested();
            if (initializationHandle.Status != AsyncOperationStatus.Succeeded)
            {
                Exception failure = initializationHandle.OperationException;
                throw new InvalidOperationException(
                    "Local Addressables initialization failed.",
                    failure);
            }
            IsInitialized = true;
        }

        public async Task<IPresentationAssetLease<GameObject>> AcquirePrefabAsync(
            string address,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (!IsInitialized)
                throw new InvalidOperationException(
                    "Client content service must be initialized before loading a prefab.");
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException(
                    "A non-empty Addressables address is required.",
                    nameof(address));

            if (!cache.TryGetValue(address, out CacheEntry entry))
            {
                entry = new CacheEntry
                {
                    Handle = Addressables.LoadAssetAsync<GameObject>(address),
                };
                cache.Add(address, entry);
            }
            entry.LeaseCount++;

            try
            {
                GameObject asset = await entry.Handle.Task;
                cancellationToken.ThrowIfCancellationRequested();
                if (entry.Handle.Status != AsyncOperationStatus.Succeeded ||
                    asset == null)
                {
                    throw new InvalidOperationException(
                        $"Addressables prefab load failed for '{address}'.",
                        entry.Handle.OperationException);
                }

                return new ClientPrefabLease(
                    asset,
                    () => Release(address, entry));
            }
            catch
            {
                Release(address, entry);
                throw;
            }
        }

        public async Task<IPresentationAssetLease<AudioClip>> AcquireAudioClipAsync(
            string address,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (!IsInitialized)
                throw new InvalidOperationException(
                    "Client content service must be initialized before loading audio.");
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException(
                    "A non-empty Addressables address is required.",
                    nameof(address));

            if (!audioCache.TryGetValue(address, out AudioCacheEntry entry))
            {
                entry = new AudioCacheEntry
                {
                    Handle = Addressables.LoadAssetAsync<AudioClip>(address),
                };
                audioCache.Add(address, entry);
            }
            entry.LeaseCount++;
            try
            {
                AudioClip asset = await entry.Handle.Task;
                cancellationToken.ThrowIfCancellationRequested();
                if (entry.Handle.Status != AsyncOperationStatus.Succeeded ||
                    asset == null)
                    throw new InvalidOperationException(
                        $"Addressables audio load failed for '{address}'.",
                        entry.Handle.OperationException);
                return new ClientAudioClipLease(
                    asset,
                    () => ReleaseAudio(address, entry));
            }
            catch
            {
                ReleaseAudio(address, entry);
                throw;
            }
        }

        public async Task<IPresentationAssetLease<Sprite>> AcquireSpriteAsync(
            string address,
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            if (!IsInitialized)
                throw new InvalidOperationException(
                    "Client content service must be initialized before loading sprites.");
            if (string.IsNullOrWhiteSpace(address))
                throw new ArgumentException(
                    "A non-empty Addressables address is required.",
                    nameof(address));

            if (!spriteCache.TryGetValue(address, out SpriteCacheEntry entry))
            {
                entry = new SpriteCacheEntry
                {
                    Handle = Addressables.LoadAssetAsync<Sprite>(address),
                };
                spriteCache.Add(address, entry);
            }
            entry.LeaseCount++;
            try
            {
                Sprite asset = await entry.Handle.Task;
                cancellationToken.ThrowIfCancellationRequested();
                if (entry.Handle.Status != AsyncOperationStatus.Succeeded ||
                    asset == null)
                    throw new InvalidOperationException(
                        $"Addressables sprite load failed for '{address}'.",
                        entry.Handle.OperationException);
                return new ClientSpriteLease(
                    asset,
                    () => ReleaseSprite(address, entry));
            }
            catch
            {
                ReleaseSprite(address, entry);
                throw;
            }
        }

        public void Dispose()
        {
            if (isDisposed)
                return;
            isDisposed = true;

            foreach (KeyValuePair<string, CacheEntry> pair in cache)
            {
                CacheEntry entry = pair.Value;
                if (entry.Handle.IsValid())
                    Addressables.Release(entry.Handle);
            }
            cache.Clear();
            foreach (KeyValuePair<string, AudioCacheEntry> pair in audioCache)
            {
                AudioCacheEntry entry = pair.Value;
                if (entry.Handle.IsValid())
                    Addressables.Release(entry.Handle);
            }
            audioCache.Clear();
            foreach (KeyValuePair<string, SpriteCacheEntry> pair in spriteCache)
            {
                SpriteCacheEntry entry = pair.Value;
                if (entry.Handle.IsValid())
                    Addressables.Release(entry.Handle);
            }
            spriteCache.Clear();

            if (ownsInitializationHandle && initializationHandle.IsValid())
                Addressables.Release(initializationHandle);
            ownsInitializationHandle = false;
            IsInitialized = false;
        }

        private void ReleaseAudio(string address, AudioCacheEntry expected)
        {
            if (expected.LeaseCount <= 0)
                throw new InvalidOperationException(
                    $"Addressables audio lease underflow for '{address}'.");
            expected.LeaseCount--;
            if (expected.LeaseCount != 0)
                return;
            if (audioCache.TryGetValue(address, out AudioCacheEntry current) &&
                ReferenceEquals(current, expected))
                audioCache.Remove(address);
            if (expected.Handle.IsValid())
                Addressables.Release(expected.Handle);
        }

        private void ReleaseSprite(string address, SpriteCacheEntry expected)
        {
            if (expected.LeaseCount <= 0)
                throw new InvalidOperationException(
                    $"Addressables sprite lease underflow for '{address}'.");
            expected.LeaseCount--;
            if (expected.LeaseCount != 0)
                return;
            if (spriteCache.TryGetValue(address, out SpriteCacheEntry current) &&
                ReferenceEquals(current, expected))
                spriteCache.Remove(address);
            if (expected.Handle.IsValid())
                Addressables.Release(expected.Handle);
        }

        private void Release(string address, CacheEntry expected)
        {
            if (expected.LeaseCount <= 0)
                throw new InvalidOperationException(
                    $"Addressables lease underflow for '{address}'.");
            expected.LeaseCount--;
            if (expected.LeaseCount != 0)
                return;
            if (cache.TryGetValue(address, out CacheEntry current) &&
                ReferenceEquals(current, expected))
                cache.Remove(address);
            if (expected.Handle.IsValid())
                Addressables.Release(expected.Handle);
        }

        private void ThrowIfDisposed()
        {
            if (isDisposed)
                throw new ObjectDisposedException(
                    nameof(AddressablesClientContentService));
        }
    }
}
