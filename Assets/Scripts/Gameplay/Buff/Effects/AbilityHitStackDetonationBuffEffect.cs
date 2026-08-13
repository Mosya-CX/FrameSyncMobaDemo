using System;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Stacking debuff that detonates when the applying caster deals Ability
    /// damage to the owner: all stacks are consumed and each stack deals magic
    /// damage scaled by the target's MaxHealth plus the caster's ability
    /// power. Optionally grants the caster cooldown reduction on hero targets.
    /// Detonation damage uses CombatSourceType.Buff so it never re-triggers.
    /// </summary>
    public sealed class AbilityHitStackDetonationBuffEffect :
        BuffEffect
    {
        /// <summary>Percent of MaxHealth per stack, indexed by the caster's
        /// ability level (e.g. 0.03..0.05).</summary>
        public float[] PercentOfMaxHpPerStackByLevel =
            Array.Empty<float>();
        public fp AbilityPowerRatioPerStack;
        public fp MaxDamagePerStackVsNonHero;
        public fp HeroCooldownReductionPercentPerStack;
        public int RecipeId;
        /// <summary>Ability slot on the caster whose level drives the
        /// per-stack percent; byte.MaxValue = level 1.</summary>
        public byte SourceAbilitySlot = byte.MaxValue;
        /// <summary>Ability source ids that detonate. Empty = any Ability
        /// damage from the caster detonates.</summary>
        public int[] DetonateSourceAbilityIds =
            Array.Empty<int>();
        /// <summary>Presentation VFX played on the owner when stacks are
        /// consumed (0 = none). Never affects Gameplay.</summary>
        public int DetonateVfxDefId;

        public override void OnAdded(
            BuffRuntime runtime,
            Unit owner)
        {
        }

        public override void OnRemoved(
            BuffRuntime runtime,
            Unit owner)
        {
        }

        public override void OnDamageTaken(
            BuffRuntime runtime,
            Unit owner,
            in DamageEventData data)
        {
            if (runtime.IsRemoving ||
                owner?.World == null ||
                RecipeId <= 0)
                return;
            if (data.Source.SourceType !=
                CombatSourceType.Ability)
                return;
            if (data.SourceUid != runtime.SourceUnitUid)
                return;
            if (!MatchesSourceId(data.Source.SourceId))
                return;

            int stacks = runtime.CurrentStacks;
            if (stacks <= 0) return;
            if (owner.StatHandler == null ||
                owner.World.CombatSystem == null)
                return;
            if (!owner.World.TryGetUnit(
                    data.SourceUid,
                    out Unit caster))
                return;

            int level = 1;
            if (SourceAbilitySlot != byte.MaxValue)
            {
                level = Math.Max(
                    1,
                    caster.AbilityHandler?
                        .GetActiveRuntime(SourceAbilitySlot)?
                        .Level ?? 1);
            }

            fp maxHealth = owner.StatHandler.GetStat(
                StatId.MaxHealth);
            fp abilityPower = caster.StatHandler != null
                ? caster.StatHandler.GetStat(
                    StatId.AbilityPower)
                : fp.zero;
            fp percent = ResolvePercent(level);
            fp perStack = maxHealth * percent +
                abilityPower * AbilityPowerRatioPerStack;

            bool hero = owner.UnitKind == UnitKind.Hero;
            bool nonHero = owner.UnitKind != UnitKind.Hero;
            if (nonHero &&
                MaxDamagePerStackVsNonHero > fp.zero &&
                perStack > MaxDamagePerStackVsNonHero)
            {
                perStack = MaxDamagePerStackVsNonHero;
            }

            fp total = perStack * stacks;

            // Consume all stacks first; deterministic even if damage is zero.
            owner.BuffHandler.ReduceStack(
                runtime.ConfigId,
                int.MaxValue);

            if (DetonateVfxDefId > 0 &&
                owner.PhysicsEntity != null)
            {
                int tick =
                    SimulationTickContext.Current.Tick;
                VisualEventOutput.SubmitVfx(
                    new VfxEvent
                    {
                        Id = new PresentationEventId
                        {
                            SourceLogicTick = tick,
                            SourceKind =
                                PresentationSourceKind.Unit,
                            SourceRuntimeUid =
                                owner.UnitUid,
                            EventSequence =
                                (ushort)(
                                    runtime.ConfigId.Value &
                                    0xFFFF),
                            EventKey =
                                PresentationEventKeys
                                    .BuffDetonated,
                        },
                        VfxDefId =
                            DetonateVfxDefId,
                        WorldPosition =
                            owner.PhysicsEntity
                                .Transform2D.Position,
                        AttachToUnit =
                            owner.UnitUid,
                        DurationScale = fp.one,
                    });
            }

            if (total > fp.zero)
            {
                var request = new DamageRequest
                {
                    Header = new CombatRequestHeader
                    {
                        SourceUnitUid =
                            caster.UnitUid,
                        TargetUnitUid =
                            owner.UnitUid,
                        SourceDescriptor =
                            new SourceDescriptor
                            {
                                SourceType =
                                    CombatSourceType.Buff,
                                SourceId =
                                    runtime.ConfigId.Value,
                                OwnerUnitUid =
                                    caster.UnitUid,
                                EmitterUnitUid =
                                    caster.UnitUid,
                            },
                        RecipeId = RecipeId,
                    },
                    DamageType = DamageType.Magic,
                    BaseDamage = total,
                };
                owner.World.CombatSystem.SubmitDamage(
                    request);
            }

            if (hero &&
                HeroCooldownReductionPercentPerStack >
                    fp.zero)
            {
                caster.AbilityHandler?
                    .ApplyCooldownReductionPercent(
                        HeroCooldownReductionPercentPerStack *
                        stacks,
                        SimulationTickContext.Current.Tick);
            }
        }

        private bool MatchesSourceId(int sourceId)
        {
            if (DetonateSourceAbilityIds == null ||
                DetonateSourceAbilityIds.Length == 0)
                return true;
            for (int i = 0;
                 i < DetonateSourceAbilityIds.Length;
                 i++)
            {
                if (DetonateSourceAbilityIds[i] ==
                    sourceId)
                    return true;
            }
            return false;
        }

        private fp ResolvePercent(int level)
        {
            if (PercentOfMaxHpPerStackByLevel == null ||
                PercentOfMaxHpPerStackByLevel.Length == 0)
                return fp.zero;
            int index = level - 1;
            if (index < 0) index = 0;
            if (index >=
                PercentOfMaxHpPerStackByLevel.Length)
            {
                index =
                    PercentOfMaxHpPerStackByLevel.Length -
                    1;
            }
            fp value =
                (fp)PercentOfMaxHpPerStackByLevel[index];
            return value < fp.zero
                ? fp.zero
                : value;
        }
    }
}
