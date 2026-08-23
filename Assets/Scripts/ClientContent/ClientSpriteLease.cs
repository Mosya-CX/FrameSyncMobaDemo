using System;
using FrameSyncMoba.FrameSync;
using UnityEngine;

namespace FrameSyncMoba.ClientContent
{
    internal sealed class ClientSpriteLease :
        IPresentationAssetLease<Sprite>
    {
        private Action release;

        public ClientSpriteLease(Sprite asset, Action releaseAction)
        {
            Asset = asset != null
                ? asset
                : throw new ArgumentNullException(nameof(asset));
            release = releaseAction ??
                throw new ArgumentNullException(nameof(releaseAction));
        }

        public Sprite Asset { get; }

        public void Dispose()
        {
            Action action = release;
            release = null;
            action?.Invoke();
        }
    }
}
