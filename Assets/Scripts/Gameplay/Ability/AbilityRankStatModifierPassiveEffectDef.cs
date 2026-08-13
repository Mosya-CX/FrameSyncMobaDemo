using FrameSyncMoba.Deterministic;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Active-ability passive that owns one stat modifier whose value follows
    /// the learned ability rank.
    /// </summary>
    public sealed class AbilityRankStatModifierPassiveEffectDef :
        ActiveAbilityPassiveEffectDef
    {
        public StatId StatId;
        public StatModifierOperation Operation;
        public AbilityLevelValue ValueByAbilityLevel;

        public override void ValidateOrThrow()
        {
            base.ValidateOrThrow();
            if (ListenerMask != AbilityPassiveListenerMask.None ||
                !ValueByAbilityLevel.HasValue)
            {
                throw new DeterministicSimulationException(
                    "Ability-rank stat passive requires no event listener and at least one rank value.");
            }
        }

        public override void OnActivate(
            Unit owner,
            ref AbilityPassiveRuntimeState state)
        {
            Apply(owner, ref state);
        }

        public override void OnAbilityRankChanged(
            Unit owner,
            int level,
            ref AbilityPassiveRuntimeState state)
        {
            state.AbilityLevel = level;
            if (state.StatModifierHandle.IsValid)
            {
                owner.StatHandler.SetModifierValue(
                    state.StatModifierHandle,
                    ValueByAbilityLevel.Resolve(level));
            }
            else
            {
                Apply(owner, ref state);
            }
        }

        public override void OnDeactivate(
            Unit owner,
            ref AbilityPassiveRuntimeState state) =>
            Release(owner, ref state);

        public override void OnUnitDeath(
            Unit owner,
            ref AbilityPassiveRuntimeState state) =>
            Release(owner, ref state);

        public override void OnRespawn(
            Unit owner,
            ref AbilityPassiveRuntimeState state) =>
            Apply(owner, ref state);

        public override void Rebuild(
            Unit owner,
            ref AbilityPassiveRuntimeState state)
        {
            // Unit v27.3 §7.15: the rollback Rebuild phase only rebuilds
            // derived state and must NOT re-attach StatModifiers. Restore
            // already brought back both this handle and the matching
            // StatHandler modifier, so rebuilding here would duplicate the
            // modifier and desync the client after any rollback. Life-stage
            // handle reconstruction happens in OnRespawn, not here.
        }

        private void Apply(
            Unit owner,
            ref AbilityPassiveRuntimeState state)
        {
            if (owner?.StatHandler == null || state.AbilityLevel <= 0 ||
                state.StatModifierHandle.IsValid)
                return;
            state.StatModifierHandle = owner.StatHandler.AddModifier(
                StatId,
                Operation,
                ValueByAbilityLevel.Resolve(state.AbilityLevel));
        }

        private static void Release(
            Unit owner,
            ref AbilityPassiveRuntimeState state)
        {
            if (state.StatModifierHandle.IsValid)
                owner?.StatHandler?.RemoveModifier(state.StatModifierHandle);
            state.StatModifierHandle = default;
        }
    }
}
