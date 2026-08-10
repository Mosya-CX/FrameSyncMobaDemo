using FrameSyncMoba.Deterministic;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Fixed-passive effect that keeps a configured Buff applied on the owner
    /// (activate and respawn). The Buff itself reacts to unit events such as
    /// kills; this passive only guarantees the Buff exists to listen.
    /// </summary>
    public sealed class ApplyBuffPassiveEffectDef :
        PassiveAbilityEffectDef
    {
        private const ushort AllowedMask =
            (ushort)AbilityPassiveListenerMask
                .UnitKill |
            (ushort)AbilityPassiveListenerMask
                .UnitAssist;

        public BuffConfigId BuffConfigId;

        public override void ValidateOrThrow()
        {
            if (!BuffConfigId.IsValid)
            {
                throw new DeterministicSimulationException(
                    "ApplyBuff passive requires a valid BuffConfigId.");
            }
            ushort mask = (ushort)ListenerMask;
            if ((mask & ~AllowedMask) != 0)
            {
                throw new DeterministicSimulationException(
                    "ApplyBuff passive may listen only to " +
                    "UnitKill and/or UnitAssist.");
            }
        }

        public override void OnActivate(
            Unit owner,
            ref AbilityPassiveRuntimeState state)
        {
            // Kill-triggered passive: the Buff must NOT exist at spawn. It is
            // applied only when the owner scores a kill (OnUnitKill), so a
            // hero does not start the game with the passive Buff visible.
        }

        public override void OnRespawn(
            Unit owner,
            ref AbilityPassiveRuntimeState state)
        {
            // Same rule as OnActivate: no passive Buff at respawn.
        }

        public override bool OnUnitKill(
            Unit owner,
            Unit victim,
            ref AbilityPassiveRuntimeState state)
        {
            Apply(owner);
            UnityEngine.Debug.Log(
                $"[PassiveP] OnUnitKill owner={owner?.UnitUid} " +
                $"victim={victim?.UnitUid} " +
                $"victimKind={victim?.UnitKind} " +
                $"buff={BuffConfigId.Value}");
            return true;
        }

        public override bool OnUnitAssist(
            Unit owner,
            Unit victim,
            ref AbilityPassiveRuntimeState state)
        {
            Apply(owner);
            UnityEngine.Debug.Log(
                $"[PassiveP] OnUnitAssist owner={owner?.UnitUid} " +
                $"victim={victim?.UnitUid} " +
                $"victimKind={victim?.UnitKind} " +
                $"buff={BuffConfigId.Value}");
            return true;
        }

        private void Apply(Unit owner)
        {
            if (owner?.World?.BuffDefinitions == null ||
                owner.BuffHandler == null)
                return;
            if (!owner.World.BuffDefinitions.TryGet(
                    BuffConfigId,
                    out BuffDefinition definition))
                return;
            owner.BuffHandler.Apply(
                BuffConfigId,
                definition,
                BuffSource.Create(
                    owner.UnitUid,
                    BuffSourceType.Script,
                    0));
        }
    }
}
