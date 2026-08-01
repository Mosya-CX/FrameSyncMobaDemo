using System;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Unit Framework v27.3 §1.6 — determines how the UnitWorld handles a unit
    /// after its death animation or logical death representation ends.
    /// </summary>
    public enum UnitDisposePolicyKind : byte
    {
        /// <summary>Keep the GameObject alive (e.g., hero awaiting respawn).</summary>
        KeepAlive = 0,

        /// <summary>Return to object pool for reuse.</summary>
        Pool = 1,

        /// <summary>Destroy the GameObject.</summary>
        Destroy = 2,

        /// <summary>Destroy and spawn a ruin prefab in its place.</summary>
        SpawnRuin = 3,
    }

    /// <summary>
    /// Unit Framework v27.3 §1.6 — configuration for post-death object lifecycle.
    /// Stored on UnitPrototype and read by UnitWorld at death resolution time.
    /// </summary>
    [Serializable]
    public struct UnitDisposePolicyConfig
    {
        /// <summary>How to handle the object after death.</summary>
        public UnitDisposePolicyKind Kind;

        /// <summary>Deterministic delay between formal death and disposal.</summary>
        public int DeathPresentationTicks;

        /// <summary>
        /// When Kind is SpawnRuin: the UnitPrototypeId of the ruin prefab to spawn.
        /// Ignored for other kinds.
        /// </summary>
        public int RuinPrototypeId;

        public static readonly UnitDisposePolicyConfig Default = new UnitDisposePolicyConfig
        {
            Kind = UnitDisposePolicyKind.Destroy,
            DeathPresentationTicks = 0,
            RuinPrototypeId = 0,
        };
    }
}
