using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace FrameSyncMoba.FrameSync
{
    public interface IPresentationAssetLease<out T> : IDisposable
        where T : UnityEngine.Object
    {
        T Asset { get; }
    }

    /// <summary>
    /// Client-only asynchronous presentation loading boundary. FrameSync owns
    /// the dependency-inversion contract; the Addressables implementation is
    /// compiled only when UNITY_SERVER is absent.
    /// </summary>
    public interface IClientPresentationAssetLoader
    {
        Task<IPresentationAssetLease<GameObject>> AcquirePrefabAsync(
            string address,
            CancellationToken cancellationToken);

        Task<IPresentationAssetLease<AudioClip>> AcquireAudioClipAsync(
            string address,
            CancellationToken cancellationToken);

        Task<IPresentationAssetLease<Sprite>> AcquireSpriteAsync(
            string address,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Process-local presentation service locator. Gameplay only carries
    /// stable addresses; the client-only Addressables assembly registers the
    /// loader. Dedicated servers never register or initialize this service.
    /// </summary>
    public static class ClientPresentationServices
    {
        private static readonly object Gate = new object();
        private static TaskCompletionSource<IClientPresentationAssetLoader>
            loaderReady = CreateCompletionSource();

        public static IClientPresentationAssetLoader Loader { get; private set; }

        public static Task<IClientPresentationAssetLoader> GetLoaderAsync()
        {
            lock (Gate)
                return Loader != null
                    ? Task.FromResult(Loader)
                    : loaderReady.Task;
        }

        public static void Register(IClientPresentationAssetLoader loader)
        {
            if (loader == null)
                throw new ArgumentNullException(nameof(loader));
            lock (Gate)
            {
                if (Loader != null && !ReferenceEquals(Loader, loader))
                    throw new InvalidOperationException(
                        "A client presentation loader is already registered.");
                Loader = loader;
                loaderReady.TrySetResult(loader);
            }
        }

        public static void Unregister(IClientPresentationAssetLoader loader)
        {
            lock (Gate)
            {
                if (!ReferenceEquals(Loader, loader))
                    return;
                Loader = null;
                loaderReady = CreateCompletionSource();
            }
        }

        private static TaskCompletionSource<IClientPresentationAssetLoader>
            CreateCompletionSource() =>
                new TaskCompletionSource<IClientPresentationAssetLoader>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>
    /// Non-authoritative Sprite cache. UI getters remain synchronous while a
    /// first request starts asynchronous Addressables loading; consumers can
    /// refresh when SpriteLoaded fires. Leases are retained until the client
    /// content host shuts down.
    /// </summary>
    public static class ClientSpriteRegistry
    {
        private static readonly Dictionary<string,
            IPresentationAssetLease<Sprite>> Leases =
                new Dictionary<string,
                    IPresentationAssetLease<Sprite>>(StringComparer.Ordinal);
        private static readonly HashSet<string> Pending =
            new HashSet<string>(StringComparer.Ordinal);
        private static CancellationTokenSource lifetime =
            new CancellationTokenSource();
        private static int generation;

        public static event Action SpriteLoaded;

        public static Sprite Resolve(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
                return null;
            if (Leases.TryGetValue(
                    address,
                    out IPresentationAssetLease<Sprite> lease))
                return lease.Asset;
            if (Pending.Add(address))
                _ = LoadAsync(
                    address,
                    generation,
                    lifetime.Token);
            return null;
        }

        public static void Clear()
        {
            generation++;
            lifetime.Cancel();
            lifetime.Dispose();
            lifetime = new CancellationTokenSource();
            foreach (IPresentationAssetLease<Sprite> lease in Leases.Values)
                lease.Dispose();
            Leases.Clear();
            Pending.Clear();
        }

        private static async Task LoadAsync(
            string address,
            int loadGeneration,
            CancellationToken cancellationToken)
        {
            try
            {
                IClientPresentationAssetLoader loader =
                    await ClientPresentationServices.GetLoaderAsync();
                IPresentationAssetLease<Sprite> lease =
                    await loader.AcquireSpriteAsync(
                        address,
                        cancellationToken);
                if (cancellationToken.IsCancellationRequested ||
                    loadGeneration != generation)
                {
                    lease.Dispose();
                    return;
                }
                if (Leases.ContainsKey(address))
                    lease.Dispose();
                else
                    Leases.Add(address, lease);
                SpriteLoaded?.Invoke();
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                if (!cancellationToken.IsCancellationRequested &&
                    loadGeneration == generation)
                {
                    Debug.LogError(
                        $"[ClientContent] Sprite '{address}' load failed: {exception}");
                }
            }
            finally
            {
                if (loadGeneration == generation)
                    Pending.Remove(address);
            }
        }
    }
}
