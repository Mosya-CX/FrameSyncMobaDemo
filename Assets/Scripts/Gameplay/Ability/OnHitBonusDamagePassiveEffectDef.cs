using System;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Active-ability passive: on basic-attack hit, deals bonus magic damage
    /// and applies a configured Buff (e.g. a stacking blight). Listens only to
    /// OnHitDealt (Ability v15.2 section 6.1).
    /// </summary>
    public sealed class OnHitBonusDamagePassiveEffectDef :
        ActiveAbilityPassiveEffectDef
    {
        public AbilityLevelValue FlatBonusDamageByLevel;
        public fp AttackDamageRatio;
        public fp AbilityPowerRatio;
        public int RecipeId;
        public BuffConfigId ApplyBuffConfigId;

        public override void ValidateOrThrow()
        {
            if (ListenerMask !=
                AbilityPassiveListenerMask.OnHitDealt)
            {
                throw new InvalidOperationException(
                    "OnHitBonusDamage passive must listen only to OnHitDealt.");
            }
            if (RecipeId <= 0)
            {
                throw new InvalidOperationException(
                    "OnHitBonusDamage passive requires a positive RecipeId.");
            }
            if (AttackDamageRatio < fp.zero ||
                AbilityPowerRatio < fp.zero)
            {
                throw new InvalidOperationException(
                    "OnHitBonusDamage passive ratios must be nonnegative.");
            }
        }

        public override bool OnHitDealt(
            Unit owner,
            in OnHitEventData data,
            ref AbilityPassiveRuntimeState state)
        {
            if (owner?.World == null ||
                owner.StatHandler == null ||
                !owner.World.TryGetUnit(
                    data.TargetUid,
                    out Unit target))
                return false;
            if (target.StatHandler == null)
                return false;

            fp amount = FlatBonusDamageByLevel.Resolve(
                Math.Max(1, state.AbilityLevel));
            if (AttackDamageRatio > fp.zero)
                amount +=
                    owner.StatHandler.GetStat(
                        StatId.AttackDamage) *
                    AttackDamageRatio;
            if (AbilityPowerRatio > fp.zero)
                amount +=
                    owner.StatHandler.GetStat(
                        StatId.AbilityPower) *
                    AbilityPowerRatio;

            if (amount > fp.zero &&
                owner.World.CombatSystem != null)
            {
                var request = new DamageRequest
                {
                    Header = new CombatRequestHeader
                    {
                        SourceUnitUid =
                            owner.UnitUid,
                        TargetUnitUid =
                            target.UnitUid,
                        SourceDescriptor =
                            new SourceDescriptor
                            {
                                SourceType =
                                    CombatSourceType
                                        .AttackEffect,
                                SourceId = RecipeId,
                                OwnerUnitUid =
                                    owner.UnitUid,
                                EmitterUnitUid =
                                    owner.UnitUid,
                        },
                        RecipeId = RecipeId,
                        OriginActionId =
                            data.OriginActionId,
                        EffectOrdinal =
                            CombatFairnessKey.ComposeChildEffectOrdinal(
                                data.EffectOrdinal,
                                RecipeId,
                                0),
                    },
                    DamageType = DamageType.Magic,
                    BaseDamage = amount,
                };
                owner.World.CombatSystem.SubmitDamage(
                    request);
            }

            if (ApplyBuffConfigId.IsValid &&
                owner.World.BuffDefinitions != null &&
                owner.World.BuffDefinitions.TryGet(
                    ApplyBuffConfigId,
                    out BuffDefinition definition))
            {
                StructureEffectPolicy.TryApplyBuff(
                    target,
                    ApplyBuffConfigId,
                    definition,
                    BuffSource.Create(
                        owner.UnitUid,
                        BuffSourceType.Attack,
                        0));
            }

            return true;
        }
    }
}
