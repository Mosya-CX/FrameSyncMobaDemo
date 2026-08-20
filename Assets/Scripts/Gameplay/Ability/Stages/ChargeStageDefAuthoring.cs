using System;
using UnityEngine;
using Unity.Mathematics.FixedPoint;
using FrameSyncMoba.RuntimeConfig;

namespace FrameSyncMoba.Unit
{
    [Serializable]
    public sealed class ChargeStageDefAuthoring : StageDefAuthoring
    {
        [Min(1)]
        [SerializeField] private int chargeRatioBlackboardKeyId = 1;
        [SerializeField] private DurationAuthoring maxChargeDuration;
        [HideInInspector, Min(1)]
        [SerializeField] private int maxChargeTicks = 45;
        [SerializeField] private byte consumeToggleSlot = byte.MaxValue;
        [SerializeField] private DurationAuthoring consumeToggleCooldown;
        [HideInInspector, Min(0)]
        [SerializeField] private int consumeToggleCooldownTicks;
        [SerializeField] private int empoweredBlackboardKeyId;
        [Min(0f)]
        [SerializeField] private float selfSlowModifierPercent;
        [SerializeField] private int slowModifierBlackboardKeyId;

        public int ChargeRatioBlackboardKeyId =>
            chargeRatioBlackboardKeyId;
        public int MaxChargeTicks => maxChargeTicks;
        public byte ConsumeToggleSlot => consumeToggleSlot;
        public int ConsumeToggleCooldownTicks =>
            consumeToggleCooldownTicks;
        public int EmpoweredBlackboardKeyId =>
            empoweredBlackboardKeyId;
        public float SelfSlowModifierPercent =>
            selfSlowModifierPercent;
        public int SlowModifierBlackboardKeyId =>
            slowModifierBlackboardKeyId;

        public override StageDef Bake(int tickRate = 30)
        {
            return new ChargeStageDef
            {
                StageDefId = StageKey,
                DebugName = DebugName,
                ChargeRatioBlackboardKeyId =
                    chargeRatioBlackboardKeyId,
                MaxChargeTicks = BakeHelpers.BakeDuration(
                    maxChargeDuration, maxChargeTicks, tickRate),
                ConsumeToggleSlot = consumeToggleSlot,
                ConsumeToggleCooldownTicks =
                    BakeHelpers.BakeDuration(
                        consumeToggleCooldown,
                        consumeToggleCooldownTicks,
                        tickRate),
                EmpoweredBlackboardKeyId =
                    empoweredBlackboardKeyId,
                SelfSlowModifierPercent =
                    (Unity.Mathematics.FixedPoint.fp)
                        selfSlowModifierPercent,
                SlowModifierBlackboardKeyId =
                    slowModifierBlackboardKeyId,
            };
        }
    }
}
