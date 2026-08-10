using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Hold stage for charge (hold-release) abilities. Writes the current
    /// charge ratio (0..1, full charge at MaxChargeTicks) into the ability
    /// Blackboard every Tick, and optionally consumes an active Toggle slot
    /// (e.g. a "next cast is empowered" toggle) when the charge begins.
    /// </summary>
    public sealed class ChargeStageDef : StageDef
    {
        public int ChargeRatioBlackboardKeyId;
        public int MaxChargeTicks;
        /// <summary>Ability slot whose active Toggle session is consumed on
        /// charge start; byte.MaxValue = none.</summary>
        public byte ConsumeToggleSlot = byte.MaxValue;
        public int ConsumeToggleCooldownTicks;
        /// <summary>Blackboard fp key written as 1 when the toggle was
        /// consumed; 0 = none.</summary>
        public int EmpoweredBlackboardKeyId;
        /// <summary>Self move-speed reduction while charging (0.2 = -20%).
        /// 0 = none.</summary>
        public fp SelfSlowModifierPercent;
        /// <summary>Blackboard key that stores the self-slow
        /// StatModifierHandle; 0 = no handle stored.</summary>
        public int SlowModifierBlackboardKeyId;

        public override StageResult OnEnter(
            AbilitySession session,
            AbilityRuntime runtime)
        {
            if (MaxChargeTicks <= 0 ||
                ChargeRatioBlackboardKeyId <= 0)
                return StageResult.Failed;

            WriteRatio(
                session,
                fp.zero);

            if (SelfSlowModifierPercent > fp.zero &&
                SlowModifierBlackboardKeyId > 0)
            {
                if (runtime.World == null ||
                    !runtime.World.TryGetUnit(
                        runtime.CasterUnitUid,
                        out Unit caster))
                    return StageResult.Failed;
                if (caster.StatHandler != null)
                {
                    StatModifierHandle handle =
                        caster.StatHandler.AddModifier(
                            StatId.MoveSpeed,
                            StatModifierOperation
                                .FinalRatioAdd,
                            -SelfSlowModifierPercent);
                    session.Blackboard.Set(
                        new AbilityBlackboardKey<
                            StatModifierHandle>(
                            SlowModifierBlackboardKeyId),
                        handle);
                }
            }

            if (ConsumeToggleSlot != byte.MaxValue &&
                EmpoweredBlackboardKeyId > 0)
            {
                if (runtime.World == null ||
                    !runtime.World.TryGetUnit(
                        runtime.CasterUnitUid,
                        out Unit caster))
                    return StageResult.Failed;

                AbilityRuntime toggle =
                    caster.AbilityHandler?.GetActiveRuntime(
                        ConsumeToggleSlot);
                if (toggle?.ActiveSession != null &&
                    toggle.Definition?.CastModel is
                        ToggleCastModelDef)
                {
                    int currentTick =
                        SimulationTickContext.Current.Tick;
                    toggle.EndSession(currentTick, 0);
                    if (ConsumeToggleCooldownTicks > 0)
                        toggle.StartCooldown(
                            currentTick,
                            ConsumeToggleCooldownTicks);
                    session.Blackboard.Set(
                        new AbilityBlackboardKey<fp>(
                            EmpoweredBlackboardKeyId),
                        fp.one);
                }
            }

            return StageResult.Running;
        }

        public override void OnExit(
            AbilitySession session,
            AbilityRuntime runtime)
        {
            if (SlowModifierBlackboardKeyId <= 0)
                return;
            if (session.Blackboard.TryGet(
                    new AbilityBlackboardKey<
                        StatModifierHandle>(
                        SlowModifierBlackboardKeyId),
                    out StatModifierHandle handle) &&
                handle.IsValid &&
                runtime.World != null &&
                runtime.World.TryGetUnit(
                    runtime.CasterUnitUid,
                    out Unit caster) &&
                caster.StatHandler != null)
            {
                caster.StatHandler.RemoveModifier(handle);
            }
        }

        public override StageResult OnTick(
            AbilitySession session,
            AbilityRuntime runtime)
        {
            fp elapsed =
                (fp)session.StageElapsedTicks;
            fp ratio = elapsed / (fp)MaxChargeTicks;
            if (ratio > fp.one)
                ratio = fp.one;
            WriteRatio(session, ratio);
            return StageResult.Running;
        }

        private void WriteRatio(
            AbilitySession session,
            fp ratio)
        {
            session.Blackboard.Set(
                new AbilityBlackboardKey<fp>(
                    ChargeRatioBlackboardKeyId),
                ratio);
        }
    }
}
