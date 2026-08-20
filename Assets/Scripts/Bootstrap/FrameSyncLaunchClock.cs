using System;
using Unity.Netcode;
using UnityEngine;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Test seam for application scheduling clocks. Neither value may enter
    /// deterministic Gameplay state, snapshots or checksums.
    /// </summary>
    public interface IFrameSyncLaunchClock
    {
        long SynchronizedServerTimeMilliseconds { get; }
        long MonotonicTimeMilliseconds { get; }
    }

    public sealed class NgoFrameSyncLaunchClock : IFrameSyncLaunchClock
    {
        private readonly NetworkManager networkManager;

        public NgoFrameSyncLaunchClock(NetworkManager networkManager)
        {
            this.networkManager = networkManager ??
                throw new ArgumentNullException(nameof(networkManager));
        }

        public long SynchronizedServerTimeMilliseconds
        {
            get
            {
                if (!networkManager.IsListening)
                    throw new InvalidOperationException(
                        "Synchronized NGO server time requires a listening NetworkManager.");
                return FrameSyncLaunchSchedule.SecondsToMilliseconds(
                    networkManager.ServerTime.Time);
            }
        }

        public long MonotonicTimeMilliseconds =>
            FrameSyncLaunchSchedule.SecondsToMilliseconds(
                Time.realtimeSinceStartupAsDouble);
    }
}
