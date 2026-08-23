using System;
using System.Threading;
using System.Threading.Tasks;
using FrameSyncMoba.FrameSync;
using UnityEngine;

namespace FrameSyncMoba.ClientContent
{
    public interface IClientContentService :
        IDisposable,
        IClientPresentationAssetLoader
    {
        bool IsInitialized { get; }

        Task InitializeAsync(CancellationToken cancellationToken);

        new Task<IPresentationAssetLease<GameObject>> AcquirePrefabAsync(
            string address,
            CancellationToken cancellationToken);

        new Task<IPresentationAssetLease<AudioClip>> AcquireAudioClipAsync(
            string address,
            CancellationToken cancellationToken);

        new Task<IPresentationAssetLease<Sprite>> AcquireSpriteAsync(
            string address,
            CancellationToken cancellationToken);
    }
}
