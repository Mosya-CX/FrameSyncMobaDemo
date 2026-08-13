using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Concrete equipment module that applies a BuffEffect when triggered.
    /// Used for equipment passives that grant permanent buffs (stat bonuses,
    /// on-hit effects, etc.) that are removed when the item is unequipped.
    /// </summary>
    [System.Serializable]
    public sealed class BuffEquipmentModule : EquipmentEffectModule
    {
        /// <summary>Buff definition to apply. Set at bake time from equipment definition.</summary>
        public BuffConfigId BuffConfigId;
        public bool IgnoreRepeatedOnHit;

        public override void Execute(
            ref EquipmentEffectExecutionContext context,
            ref EquipmentEffectModuleRuntimeState state)
        {
            Unit owner = context.Owner;
            EquipmentInstance instance = context.Instance;
            if (IgnoreRepeatedOnHit &&
                context.Timing == EquipmentEffectInvokeTiming.OnHitDealt &&
                context.OnHit.IsRepeated)
                return;
            if (owner?.BuffHandler == null || !BuffConfigId.IsValid) return;
            if (owner.World?.BuffDefinitions == null) return;
            if (!owner.World.BuffDefinitions.TryGet(BuffConfigId, out BuffDefinition def)) return;

            owner.BuffHandler.Apply(
                BuffConfigId,
                def,
                BuffSource.Create(
                    owner.UnitUid,
                    BuffSourceType.Item,
                    instance?.Definition?.Id ?? 0));
        }
    }
}
