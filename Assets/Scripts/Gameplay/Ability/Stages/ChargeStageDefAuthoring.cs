using System;
using UnityEngine;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    [Serializable]
    public sealed class ChargeStageDefAuthoring : StageDefAuthoring
    {
        [Min(1)]
        [SerializeField] private int chargeRatioBlackboardKeyId = 1;
        [Min(1)]
        [SerializeField] private int maxChargeTicks = 45;
        [SerializeField] private byte consumeToggleSlot = byte.MaxValue;
        [Min(0)]
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

        public override StageDef Bake()
        {
            return new ChargeStageDef
            {
                StageDefId = StageKey,
                DebugName = DebugName,
                ChargeRatioBlackboardKeyId =
                    chargeRatioBlackboardKeyId,
                MaxChargeTicks = maxChargeTicks,
                ConsumeToggleSlot = consumeToggleSlot,
                ConsumeToggleCooldownTicks =
                    consumeToggleCooldownTicks,
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
