using System;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Kill-growth buff: on a kill, grants attack speed (percent) plus attack
    /// damage and ability power derived from that attack speed, refreshed for
    /// a configured duration. Hero victims apply an optional multiplier.
    /// Maximum one stack; values are recomputed on each kill.
    /// </summary>
    public sealed class KillStatGrowthBuffEffect : BuffEffect
    {
        /// <summary>Attack speed bonus (as a fraction) by hero level:
        /// entries at level 1/7/13 thresholds (10%/15%/20%).</summary>
        public float[] AttackSpeedPercentByUnitLevel =
            Array.Empty<float>();
        /// <summary>Attack damage bonus = attack-speed bonus * ratio
        /// (1100% => 11).</summary>
        public fp AttackDamagePerAttackSpeedRatio;
        /// <summary>Ability power bonus = attack-speed bonus * ratio
        /// (1100% => 11).</summary>
        public fp AbilityPowerPerAttackSpeedRatio;
        /// <summary>Multiplier applied to all bonuses when the victim is a
        /// hero (3x).</summary>
        public fp HeroVictimMultiplier = fp.one;
        /// <summary>Buff duration in seconds by hero level (5/7/9/11 at
        /// levels 1/6/11/16).</summary>
        public float[] DurationSecondsByUnitLevel =
            Array.Empty<float>();

        public BuffStateSlotId AttackSpeedHandleSlot;
        public BuffStateSlotId AttackDamageHandleSlot;
        public BuffStateSlotId AbilityPowerHandleSlot;
        /// <summary>True while the current buff is the empowered (hero-kill)
        /// version.</summary>
        public BuffStateSlotId IsEmpoweredSlot;
        /// <summary>True while an empowered buff has seen a non-hero kill and
        /// must re-apply a normal buff when the empowered buff expires.</summary>
        public BuffStateSlotId PendingNormalAfterEmpoweredSlot;

        public override BuffStateSlotDefinition[]
            RequiredSlotDefinitions =>
                new[]
                {
                    new BuffStateSlotDefinition
                    {
                        SlotId = AttackSpeedHandleSlot,
                        Kind =
                            BuffValueKind
                                .StatModifierHandle,
                    },
                    new BuffStateSlotDefinition
                    {
                        SlotId = AttackDamageHandleSlot,
                        Kind =
                            BuffValueKind
                                .StatModifierHandle,
                    },
                    new BuffStateSlotDefinition
                    {
                        SlotId = AbilityPowerHandleSlot,
                        Kind =
                            BuffValueKind
                                .StatModifierHandle,
                    },
                    new BuffStateSlotDefinition
                    {
                        SlotId = IsEmpoweredSlot,
                        Kind = BuffValueKind.Bool,
                    },
                    new BuffStateSlotDefinition
                    {
                        SlotId = PendingNormalAfterEmpoweredSlot,
                        Kind = BuffValueKind.Bool,
                    },
                };

        public override void OnAdded(
            BuffRuntime runtime,
            Unit owner)
        {
        }

        public override void OnRemoved(
            BuffRuntime runtime,
            Unit owner)
        {
            ReleaseHandles(runtime, owner);
        }

        public override void OnUnitKill(
            BuffRuntime runtime,
            Unit owner,
            Unit victim)
        {
            if (owner?.StatHandler == null)
                return;
            bool heroVictim = victim != null &&
                victim.UnitKind == UnitKind.Hero;
            UnityEngine.Debug.Log(
                $"[PassiveP] buff OnUnitKill owner={owner.UnitUid} " +
                $"victim={victim?.UnitUid} " +
                $"heroVictim={heroVictim}");

            if (heroVictim)
            {
                // Hero kill: empowered version (3x), refreshed duration.
                ApplyHeroEmpowered(runtime, owner);
                return;
            }

            bool wasEmpowered =
                runtime.Blackboard.ReadBoolOrDefault(
                    IsEmpoweredSlot);
            if (wasEmpowered)
            {
                // Non-hero kill during empowered: keep empowered, remember to
                // apply one normal buff when the empowered buff expires.
                runtime.Blackboard.WriteBool(
                    PendingNormalAfterEmpoweredSlot,
                    true);
                return;
            }

            // Normal kill: refresh the normal version.
            ApplyValues(
                runtime,
                owner,
                multiplier: fp.one,
                isEmpowered: false);
            RefreshDuration(runtime, owner);
        }

        public override void OnUnitAssist(
            BuffRuntime runtime,
            Unit owner,
            Unit victim)
        {
            // Hero assists are equivalent to hero kills for the empowered
            // Revenge buff (design: 英雄击杀或助攻).
            if (victim == null ||
                victim.UnitKind != UnitKind.Hero)
            {
                UnityEngine.Debug.Log(
                    $"[PassiveP] buff OnUnitAssist ignored " +
                    $"owner={owner?.UnitUid} " +
                    $"victim={victim?.UnitUid} " +
                    $"kind={victim?.UnitKind}");
                return;
            }
            UnityEngine.Debug.Log(
                $"[PassiveP] buff OnUnitAssist empowered " +
                $"owner={owner?.UnitUid} " +
                $"victim={victim.UnitUid}");
            ApplyHeroEmpowered(runtime, owner);
        }

        public override void OnRemovedComplete(
            BuffRuntime runtime,
            Unit owner)
        {
            bool empowered =
                runtime.Blackboard.ReadBoolOrDefault(
                    IsEmpoweredSlot);
            bool pendingNormal =
                runtime.Blackboard.ReadBoolOrDefault(
                    PendingNormalAfterEmpoweredSlot);
            if (!empowered || !pendingNormal)
                return;
            if (owner?.BuffHandler == null ||
                owner.World?.BuffDefinitions == null)
                return;
            if (!owner.World.BuffDefinitions.TryGet(
                    runtime.ConfigId,
                    out BuffDefinition definition))
                return;

            owner.BuffHandler.Apply(
                runtime.ConfigId,
                definition,
                runtime.Source);
            if (owner.BuffHandler.TryGetRuntime(
                    runtime.ConfigId,
                    out BuffRuntime successor) &&
                successor != runtime)
            {
                ApplyValues(
                    successor,
                    owner,
                    multiplier: fp.one,
                    isEmpowered: false);
                RefreshDuration(successor, owner);
            }
        }

        private void ApplyValues(
            BuffRuntime runtime,
            Unit owner,
            fp multiplier,
            bool isEmpowered)
        {
            if (owner?.StatHandler == null)
                return;
            fp attackSpeed = ResolvePercent(
                AttackSpeedPercentByUnitLevel,
                owner.Level);
            if (multiplier > fp.one)
                attackSpeed *= multiplier;
            if (attackSpeed <= fp.zero)
                return;

            fp attackDamage =
                attackSpeed *
                AttackDamagePerAttackSpeedRatio;
            fp abilityPower =
                attackSpeed *
                AbilityPowerPerAttackSpeedRatio;

            SetOrCreate(
                runtime,
                owner,
                AttackSpeedHandleSlot,
                StatId.AttackSpeed,
                StatModifierOperation.FinalRatioAdd,
                attackSpeed);
            SetOrCreate(
                runtime,
                owner,
                AttackDamageHandleSlot,
                StatId.AttackDamage,
                StatModifierOperation.FlatAdd,
                attackDamage);
            SetOrCreate(
                runtime,
                owner,
                AbilityPowerHandleSlot,
                StatId.AbilityPower,
                StatModifierOperation.FlatAdd,
                abilityPower);
            if (IsEmpoweredSlot.IsValid)
                runtime.Blackboard.WriteBool(
                    IsEmpoweredSlot,
                    isEmpowered);
        }

        private void RefreshDuration(
            BuffRuntime runtime,
            Unit owner)
        {
            float seconds = ResolveSeconds(
                DurationSecondsByUnitLevel,
                owner.Level);
            int durationTicks =
                BuffTickConverter.SecondsToTicks(
                    seconds);
            if (durationTicks > 0)
                runtime.SetRemainingTicks(
                    durationTicks);
        }

        private void ApplyHeroEmpowered(
            BuffRuntime runtime,
            Unit owner)
        {
            bool wasEmpowered =
                runtime.Blackboard.ReadBoolOrDefault(
                    IsEmpoweredSlot);
            ApplyValues(
                runtime,
                owner,
                multiplier: HeroVictimMultiplier,
                isEmpowered: true);
            if (!wasEmpowered)
            {
                // Transition from normal to empowered: no pending carry.
                runtime.Blackboard.WriteBool(
                    PendingNormalAfterEmpoweredSlot,
                    false);
            }
            RefreshDuration(runtime, owner);
        }

        public override void ClearForDeath(
            BuffRuntime runtime,
            Unit owner)
        {
            ReleaseHandles(runtime, owner);
        }

        public override void ClearForDespawn(
            BuffRuntime runtime,
            Unit owner)
        {
            ReleaseHandles(runtime, owner);
        }

        private static void SetOrCreate(
            BuffRuntime runtime,
            Unit owner,
            BuffStateSlotId slot,
            StatId statId,
            StatModifierOperation operation,
            fp value)
        {
            if (!slot.IsValid) return;
            if (runtime.Blackboard.TryGetStatHandle(
                    slot,
                    out StatModifierHandle handle) &&
                handle.IsValid)
            {
                owner.StatHandler.SetModifierValue(
                    handle,
                    value);
                return;
            }
            StatModifierHandle created =
                owner.StatHandler.AddModifier(
                    statId,
                    operation,
                    value);
            runtime.Blackboard.WriteStatHandle(
                slot,
                created);
        }

        private void ReleaseHandles(
            BuffRuntime runtime,
            Unit owner)
        {
            if (owner?.StatHandler == null) return;
            ReleaseHandle(
                runtime,
                owner,
                AttackSpeedHandleSlot);
            ReleaseHandle(
                runtime,
                owner,
                AttackDamageHandleSlot);
            ReleaseHandle(
                runtime,
                owner,
                AbilityPowerHandleSlot);
        }

        private void ReleaseHandle(
            BuffRuntime runtime,
            Unit owner,
            BuffStateSlotId slot)
        {
            if (!slot.IsValid) return;
            if (!runtime.Blackboard.TryGetStatHandle(
                    slot,
                    out StatModifierHandle handle) ||
                !handle.IsValid)
                return;
            owner.StatHandler.RemoveModifier(handle);
        }

        private static fp ResolvePercent(
            float[] byLevel,
            int heroLevel)
        {
            if (byLevel == null ||
                byLevel.Length == 0)
                return fp.zero;
            int index = LevelIndex(
                heroLevel,
                byLevel.Length,
                6);
            fp value = (fp)byLevel[index];
            return value < fp.zero
                ? fp.zero
                : value;
        }

        private static float ResolveSeconds(
            float[] byLevel,
            int heroLevel)
        {
            if (byLevel == null ||
                byLevel.Length == 0)
                return 0f;
            return byLevel[LevelIndex(
                heroLevel,
                byLevel.Length,
                5)];
        }

        private static int LevelIndex(
            int heroLevel,
            int count,
            int levelSpan)
        {
            int index = (heroLevel - 1) / levelSpan;
            if (index < 0) index = 0;
            if (index >= count)
                index = count - 1;
            return index;
        }
    }
}
