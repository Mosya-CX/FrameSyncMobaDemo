using System;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Unit Framework v27.3 §1.6 — how the unit recovers from Dead to Alive.
    /// </summary>
    public enum RespawnHealthRule : byte
    {
        /// <summary>Restore to full MaxHealth.</summary>
        FullHealth = 0,

        /// <summary>Restore to a percentage of MaxHealth.</summary>
        PercentOfMax = 1,

        /// <summary>Restore to a fixed health value.</summary>
        FixedValue = 2,
    }

    /// <summary>
    /// Unit Framework v27.3 §1.6 — how cast resource (e.g., mana) is restored on respawn.
    /// </summary>
    public enum RespawnResourceRule : byte
    {
        /// <summary>Restore to full MaxResource.</summary>
        FullResource = 0,

        /// <summary>Restore to a percentage of MaxResource.</summary>
        PercentOfMax = 1,

        /// <summary>Restore to a fixed value.</summary>
        FixedValue = 2,
    }

    /// <summary>
    /// Unit Framework v27.3 §1.6 — configuration for normal respawn.
    /// Stored on UnitPrototype. Read by UnitWorld and RespawnTimer.
    /// 
    /// Separate from UnitDisposePolicy: DisposePolicy decides what happens to the
    /// object after death representation; RespawnConfig decides whether and how
    /// a kept-alive object returns to Alive.
    /// </summary>
    [Serializable]
    public struct UnitRespawnConfig
    {
        /// <summary>
        /// Whether this unit type may respawn at all.
        /// Must be compatible with the UnitDisposePolicy (KeepAlive + CanRespawn).
        /// </summary>
        public bool CanRespawn;

        /// <summary>
        /// Number of logic ticks between Dead state confirmation and the start
        /// of the Respawning→Alive transition.
        /// </summary>
        [Min(0)]
        public int RespawnDelayTicks;

        /// <summary>How health is restored on respawn.</summary>
        public RespawnHealthRule HealthRule;

        /// <summary>How cast resource is restored on respawn.</summary>
        public RespawnResourceRule ResourceRule;

        /// <summary>
        /// Used when HealthRule is PercentOfMax or FixedValue.
        /// For PercentOfMax: fraction of MaxHealth (e.g., 50 = 50%).
        /// For FixedValue: absolute health points.
        /// </summary>
        [Min(0)]
        public int HealthRespawnValue;

        /// <summary>
        /// Used when ResourceRule is PercentOfMax or FixedValue.
        /// </summary>
        [Min(0)]
        public int ResourceRespawnValue;

        /// <summary>
        /// Default: CannotRespawn. KeepAlive units should set CanRespawn=true.
        /// </summary>
        public static readonly UnitRespawnConfig CannotRespawn = new UnitRespawnConfig
        {
            CanRespawn = false,
            RespawnDelayTicks = 0,
            HealthRule = RespawnHealthRule.FullHealth,
            ResourceRule = RespawnResourceRule.FullResource,
            HealthRespawnValue = 100,
            ResourceRespawnValue = 100,
        };

        /// <summary>
        /// Convenience default for heroes: can respawn with full health/resource.
        /// </summary>
        public static readonly UnitRespawnConfig HeroDefault = new UnitRespawnConfig
        {
            CanRespawn = true,
            RespawnDelayTicks = 0, // overridden by MatchRuleRuntime
            HealthRule = RespawnHealthRule.FullHealth,
            ResourceRule = RespawnResourceRule.FullResource,
            HealthRespawnValue = 100,
            ResourceRespawnValue = 100,
        };

        /// <summary>
        /// Validates that the config is consistent with the dispose policy.
        /// CanRespawn only makes sense with KeepAlive dispose policy.
        /// </summary>
        public bool IsCompatibleWith(UnitDisposePolicyKind disposeKind)
        {
            if (!CanRespawn) return true;
            return disposeKind == UnitDisposePolicyKind.KeepAlive;
        }
    }
}
