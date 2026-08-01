using System;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Static configuration source for a Unit (Unit v27.3 §1.6).
    /// Loaded at match start and read-only thereafter. SpawnUnit reads this
    /// to populate Unit identity fields and initialize subsystems.
    ///
    /// Fields whose types don't exist yet (HandlerLoadout, LocomotionProfile,
    /// PhysicsProfile2D, UnitRespawnConfig, UnitPoolConfig) are deferred to
    /// future slices. This slice covers the fields already defined by
    /// completed ExecPlans: PrototypeId, Name, RuntimeEntityPrefabId,
    /// UnitKind, UnitSubKindId, BaseStats, BaseGoldValue, BaseExperienceValue.
    /// </summary>
    [Serializable]
    public sealed class UnitPrototype
    {
        public int UnitPrototypeId;
        public string Name;

        /// <summary>
        /// Stable ID into the GlobalPrefabTable for Unity Prefab lookup.
        /// Distinct from UnitPrototypeId (§1.3).
        /// </summary>
        public int RuntimeEntityPrefabId;

        public UnitKind UnitKind;

        public ushort UnitSubKindId;

        /// <summary>
        /// Base stat configuration (§1.6/§5.2.3).
        /// </summary>
        public StatPreset BaseStats;

        public int BaseGoldValue;

        /// <summary>
        /// Post-death object lifecycle policy (Unit v27.3 §1.6).
        /// Defaults to Destroy for non-hero units.
        /// </summary>
        public ushort UnitDisposePolicyId;

        /// <summary>
        /// Respawn configuration (Unit v27.3 §1.6).
        /// Only meaningful when DisposePolicy is KeepAlive.
        /// </summary>
        public UnitRespawnConfig RespawnConfig;

        public UnitPoolConfig PoolConfig;

        public int BaseExperienceValue;

        public HandlerLoadout Loadout;
        public LocomotionProfile LocomotionProfile;
        public PhysicsProfile2D PhysicsProfile;
    }
}
