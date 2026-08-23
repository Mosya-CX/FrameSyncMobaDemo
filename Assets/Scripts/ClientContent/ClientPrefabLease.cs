using System;
using System.Threading;
using UnityEngine;

namespace FrameSyncMoba.ClientContent
{
    public sealed class ClientPrefabLease :
        FrameSync.IPresentationAssetLease<GameObject>
    {
        private Action release;

        internal ClientPrefabLease(GameObject asset, Action release)
        {
            Asset = asset != null
                ? asset
                : throw new ArgumentNullException(nameof(asset));
            this.release = release ??
                throw new ArgumentNullException(nameof(release));
        }

        public GameObject Asset { get; }
        public bool IsReleased => release == null;

        public void Dispose()
        {
            Action callback = Interlocked.Exchange(ref release, null);
            callback?.Invoke();
        }
    }
}
