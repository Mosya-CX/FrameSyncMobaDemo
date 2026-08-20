using Unity.Mathematics.FixedPoint;
using FrameSyncMoba.RuntimeConfig;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Equipment module that heals the owner when they kill an enemy unit.
    /// Invoke timing: UnitKill.
    /// Uses EquipmentEffectDispatch context to determine the kill event.
    /// </summary>
    [System.Serializable]
    public sealed class OnKillHealModule : EquipmentEffectModule
    {
        /// <summary>Heal amount on kill (absolute HP).</summary>
        public fp HealAmount;

        public DurationAuthoring InternalCooldown;
        [HideInInspector]
        public int InternalCooldownTicks;

        public override void BakeTime(int tickRate)
        {
            InternalCooldownTicks = InternalCooldown.IsAuthored
                ? InternalCooldown.BakeTicks(tickRate)
                : DeterministicTimeConversion
                    .Legacy30HzTicksToTicks(
                        InternalCooldownTicks,
                        tickRate);
        }

        public override void Execute(
            ref EquipmentEffectExecutionContext context,
            ref EquipmentEffectModuleRuntimeState state)
        {
            Unit owner = context.Owner;
            if (owner?.World?.CombatSystem == null)
                return;

            int currentTick = Deterministic.SimulationTickContext.Current.Tick;
            if (InternalCooldownTicks > 0 &&
                currentTick < state.InternalCooldownReadyTick)
                return;

            // Heal on kill
            if (HealAmount > fp.zero)
            {
                var request = new HealRequest
                {
                    TargetUnitUid = owner.UnitUid,
                    SourceUnitUid = owner.UnitUid,
                    BaseValue = HealAmount,
                };
                owner.World.CombatSystem.SubmitHeal(request);
            }

            state.InternalCooldownReadyTick = checked(
                currentTick + InternalCooldownTicks);
        }
    }
}
