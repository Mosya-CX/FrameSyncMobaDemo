using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// A deferred combat request stored for execution on a future Tick
    /// (Combat v13.2 section 1.2, D-010).
    ///
    /// Produced by UnitDeath / UnitKill reactions; imported at
    /// begin-Tick when ExecuteLogicTick matches current Tick.
    /// </summary>
    public struct DeferredCombatRequest
    {
        /// <summary>The future Tick when this request should execute.</summary>
        public int ExecuteLogicTick;

        /// <summary>The Tick that produced this request.</summary>
        public int SourceLogicTick;

        /// <summary>
        /// Stable ordering identity within the source Tick
        /// (Combat v13.2 section 1.2). Gaps are legal and never renumbered.
        /// </summary>
        public ushort DeferredSequenceInSourceTick;

        /// <summary>Which kind of request this holds.</summary>
        public CombatRequestKind RequestKind;

        public ShieldRequest Shield;
        public DamageRequest Damage;
        public HealRequest Heal;

        public static DeferredCombatRequest CreateShield(
            ShieldRequest req, int executeTick, int sourceTick, ushort seq)
        {
            return new DeferredCombatRequest
            {
                ExecuteLogicTick = executeTick,
                SourceLogicTick = sourceTick,
                DeferredSequenceInSourceTick = seq,
                RequestKind = CombatRequestKind.Shield,
                Shield = req,
            };
        }

        public static DeferredCombatRequest CreateDamage(
            DamageRequest req, int executeTick, int sourceTick, ushort seq)
        {
            return new DeferredCombatRequest
            {
                ExecuteLogicTick = executeTick,
                SourceLogicTick = sourceTick,
                DeferredSequenceInSourceTick = seq,
                RequestKind = CombatRequestKind.Damage,
                Damage = req,
            };
        }

        public static DeferredCombatRequest CreateHeal(
            HealRequest req, int executeTick, int sourceTick, ushort seq)
        {
            return new DeferredCombatRequest
            {
                ExecuteLogicTick = executeTick,
                SourceLogicTick = sourceTick,
                DeferredSequenceInSourceTick = seq,
                RequestKind = CombatRequestKind.Heal,
                Heal = req,
            };
        }
    }
}
