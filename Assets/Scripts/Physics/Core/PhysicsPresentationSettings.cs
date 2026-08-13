using UnityEngine;

namespace FrameSyncMoba.Physics
{
    /// <summary>
    /// Client-only render-pose settings. The values never enter Gameplay,
    /// snapshots, checksums, rollback, or physics queries.
    /// </summary>
    public static class PhysicsPresentationSettings
    {
        public static bool Enabled { get; private set; }
        public static float DurationSeconds { get; private set; } = 0.033333f;
        public static float SnapDistance { get; private set; } = 6f;

        public static void Configure(
            bool enabled,
            float durationSeconds,
            float snapDistance)
        {
            Enabled = enabled;
            DurationSeconds = Mathf.Max(0.001f, durationSeconds);
            SnapDistance = Mathf.Max(0.01f, snapDistance);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            Enabled = false;
            DurationSeconds = 0.033333f;
            SnapDistance = 6f;
        }
    }
}
