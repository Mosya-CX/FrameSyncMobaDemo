namespace FrameSyncMoba.Unit
{
    public enum CombatSourceType : byte
    {
        Attack = 0,
        Ability = 1,
        Buff = 2,
        Equipment = 3,
        AttackEffect = 4,
        System = 5,
    }

    public struct SourceDescriptor
    {
        public CombatSourceType SourceType;
        public int SourceId;
        public UnitUid OwnerUnitUid;
        public UnitUid EmitterUnitUid;

        public bool IsValid =>
            SourceId > 0 &&
            (SourceType == CombatSourceType.System || OwnerUnitUid.IsValid());
    }

    public static class CombatBuiltinSourceId
    {
        public const int BasicAttack = 1;
    }

    public static class CombatBuiltinRecipeId
    {
        public const int BasicAttackDamage = 1;
        /// <summary>
        /// Recipe used by an empowered basic attack (e.g. an item passive
        /// that turns the next attack into a guaranteed critical strike).
        /// Settlement modifiers may Match this RecipeId to affect only the
        /// empowered strike (Combat v13.2 section 4.2/7.7).
        /// </summary>
        public const int EmpoweredAttackDamage = 2;
    }

    /// <summary>
    /// Global ordering header for Combat requests within a single LogicTick
    /// (Combat v13.2 section 2.2).
    ///
    /// All active requests (Shield/Damage/Heal) share a unified
    /// SequenceInTick assigned after the current causal wave is sealed into
    /// its canonical gameplay order. Submission call order is not settlement
    /// authority (Combat fairness D-049).
    /// </summary>
    public struct CombatRequestHeader
    {
        /// <summary>
        /// Stable ordering identity within the current LogicTick.
        /// Assigned by CombatSystem when the request's settlement wave seals.
        /// Smaller values execute first.
        /// </summary>
        public ushort SequenceInTick;

        /// <summary>The LogicTick when this request was created.</summary>
        public int SourceLogicTick;

        public UnitUid SourceUnitUid;

        public UnitUid TargetUnitUid;

        public SourceDescriptor SourceDescriptor;

        public int RecipeId;

        /// <summary>
        /// Stable gameplay action provenance for keyed random/tie mechanics.
        /// It is independent of SourceUnitUid technical labeling (D-050).
        /// </summary>
        public OriginActionId OriginActionId;

        /// <summary>Stable index of this effect inside the origin action.</summary>
        public int EffectOrdinal;

        public static CombatRequestHeader Create(
            UnitUid sourceUnitUid,
            UnitUid targetUnitUid,
            CombatSourceType sourceType,
            int sourceId,
            int recipeId,
            UnitUid ownerUnitUid = default,
            OriginActionId originActionId = default,
            int effectOrdinal = 0)
        {
            UnitUid owner = ownerUnitUid.IsValid()
                ? ownerUnitUid
                : sourceUnitUid;
            return new CombatRequestHeader
            {
                SourceUnitUid = sourceUnitUid,
                TargetUnitUid = targetUnitUid,
                SourceDescriptor = new SourceDescriptor
                {
                    SourceType = sourceType,
                    SourceId = sourceId,
                    OwnerUnitUid = owner,
                    EmitterUnitUid = sourceUnitUid,
                },
                RecipeId = recipeId,
                OriginActionId = originActionId,
                EffectOrdinal = effectOrdinal,
            };
        }

        public static readonly CombatRequestHeader None = default;
    }
}
