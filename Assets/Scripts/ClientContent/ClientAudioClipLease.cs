using System;
using System.Threading;
using FrameSyncMoba.FrameSync;
using UnityEngine;

namespace FrameSyncMoba.ClientContent
{
    public sealed class ClientAudioClipLease :
        IPresentationAssetLease<AudioClip>
    {
        private Action release;

        internal ClientAudioClipLease(AudioClip asset, Action release)
        {
            Asset = asset != null
                ? asset
                : throw new ArgumentNullException(nameof(asset));
            this.release = release ??
                throw new ArgumentNullException(nameof(release));
        }

        public AudioClip Asset { get; }

        public void Dispose()
        {
            Action callback = Interlocked.Exchange(ref release, null);
            callback?.Invoke();
        }
    }
}
