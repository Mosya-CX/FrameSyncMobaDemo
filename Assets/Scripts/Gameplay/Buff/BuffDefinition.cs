using System;
using System.Collections.Generic;
using UnityEngine;
using FrameSyncMoba.RuntimeConfig;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Static buff configuration (design v14.2 3.1-3.2).
    /// Single ScriptableObject; no SO + Bake layer.
    /// </summary>
    [CreateAssetMenu(
        fileName = "BuffDefinition",
        menuName = "MOBA/Buff Definition")]
    public sealed class BuffDefinition :
        ScriptableObject
    {
        public BuffConfigId ConfigId;
        public BuffDisplayInfo Display;
        public BuffLifeRuleConfig Life;
        public BuffStackRuleConfig Stack;
        public BuffTagSet Tags;
        public BuffEffectConfig[] Effects;
        public BuffBlackboardLayout BlackboardLayout;
        public BuffLifecycleReactions LifecycleReactions;
        public BuffEventReactions EventReactions;

        /// <summary>
        /// Dispel priority for the formalized MaxBuffs rule
        /// (design v14.2 13A): 0 = highest, 255 = lowest.
        /// </summary>
        public byte Priority;

        [Tooltip("Periodic cadence in milliseconds. Bake converts it to Ticks.")]
        public DurationAuthoring PeriodicInterval;
        [HideInInspector]
        public int PeriodicIntervalTicks;

        /// <summary>
        /// VFX played once on the owner when the buff is first applied
        /// (presentation only, never affects Gameplay). 0 = none.
        /// </summary>
        public int ApplyVfxDefId;

        /// <summary>
        /// Seconds the apply VFX should stay visible. 0 = use the VFX
        /// prefab's own default duration.
        /// </summary>
        public int ApplyVfxDurationMilliseconds;
        [HideInInspector]
        public float ApplyVfxDurationSeconds;

        /// <summary>
        /// Transitional initial stack count; the reactions slice derives the
        /// initial StackChanged 0 -> InitialStacks event.
        /// </summary>
        public int InitialStacks = 1;

        [NonSerialized] private int bakedTickRate = 30;
        [NonSerialized] private int bakedPeriodicIntervalTicks;
        [NonSerialized] private bool hasBakedPeriodicInterval;

        public int BakedTickRate => bakedTickRate;

        public bool IsValid => ConfigId.IsValid;

        public bool IsInfinite =>
            Life != null && Life.Infinite;

        public int DurationTicks =>
            IsInfinite
                ? 0
                : BakeLifeDuration(
                    Life?.Duration ?? default,
                    Life?.DurationSeconds ?? 0f);

        public int ExtendTicks =>
            BakeLifeDuration(
                Life?.ExtendDuration ?? default,
                Life?.ExtendSeconds ?? 0f);

        public int BakedPeriodicIntervalTicks =>
            hasBakedPeriodicInterval
                ? bakedPeriodicIntervalTicks
                : PeriodicIntervalTicks;

        public void BakeOrThrow(int tickRate)
        {
            DeterministicTimeConversion.ValidateSupportedTickRate(
                tickRate);
            bakedTickRate = tickRate;
            bakedPeriodicIntervalTicks =
                PeriodicInterval.IsAuthored
                    ? PeriodicInterval.BakeTicks(tickRate)
                    : DeterministicTimeConversion
                        .Legacy30HzTicksToTicks(
                            PeriodicIntervalTicks,
                            tickRate);
            hasBakedPeriodicInterval = true;
            BuffEffect[] effects = GetEffects();
            for (int i = 0; i < effects.Length; i++)
                effects[i]?.BakeTime(tickRate);
            BakeReactionTimes(tickRate);
        }

        private int BakeLifeDuration(
            in DurationAuthoring duration,
            float legacySeconds)
        {
            return duration.IsAuthored
                ? duration.BakeTicks(bakedTickRate)
                : BuffTickConverter.SecondsToTicks(
                    legacySeconds,
                    bakedTickRate);
        }

        private void BakeReactionTimes(int tickRate)
        {
            BuffLifecycleReactions lifecycle = LifecycleReactions;
            if (lifecycle != null)
            {
                BakeGroups(lifecycle.Added, tickRate);
                BakeGroups(lifecycle.Reapplied, tickRate);
                BakeGroups(lifecycle.Removed, tickRate);
                BakeGroups(lifecycle.StackChanged, tickRate);
                BakeGroups(lifecycle.Periodic, tickRate);
            }

            BuffEventReactions events = EventReactions;
            if (events == null) return;
            BakeGroups(events.DamageTaken, tickRate);
            BakeGroups(events.DamageDealt, tickRate);
            BakeGroups(events.HealTaken, tickRate);
            BakeGroups(events.HealDealt, tickRate);
            BakeGroups(events.ShieldApplied, tickRate);
            BakeGroups(events.AbilityCast, tickRate);
            BakeGroups(events.LevelUp, tickRate);
            BakeGroups(events.UnitDying, tickRate);
            BakeGroups(events.UnitDeath, tickRate);
            BakeGroups(events.UnitKill, tickRate);
            BakeGroups(events.OnHitDealt, tickRate);
            BakeGroups(events.CollisionEnter, tickRate);
            BakeGroups(events.CollisionExit, tickRate);
        }

        private static void BakeGroups(
            BuffReactionGroup[] groups,
            int tickRate)
        {
            if (groups == null) return;
            for (int groupIndex = 0;
                 groupIndex < groups.Length;
                 groupIndex++)
            {
                BuffReactionActionConfig[] actions =
                    groups[groupIndex]?.Actions;
                if (actions == null) continue;
                for (int actionIndex = 0;
                     actionIndex < actions.Length;
                     actionIndex++)
                {
                    if (actions[actionIndex] is
                        IBuffTimeAuthoring timedAction)
                        timedAction.BakeTime(tickRate);
                }
            }
        }

        public int MaxStacks =>
            Mathf.Max(1, Stack?.MaxStacks ?? 1);

        public bool HasTag(byte tag) =>
            Tags != null && Tags.HasTag(tag);

        public BuffEffect[] GetEffects()
        {
            BuffEffectConfig[] configs = Effects;
            if (configs == null ||
                configs.Length == 0)
                return Array.Empty<BuffEffect>();
            var result =
                new BuffEffect[configs.Length];
            for (int i = 0; i < configs.Length; i++)
                result[i] = configs[i]?.Effect;
            return result;
        }

        /// <summary>
        /// Returns the authored layout, or derives one from the effects'
        /// required slot definitions when no explicit layout is configured.
        /// </summary>
        public BuffBlackboardLayout
            ResolveBlackboardLayout()
        {
            if (BlackboardLayout != null &&
                BlackboardLayout.Slots != null &&
                BlackboardLayout.Slots.Length > 0)
                return BlackboardLayout;

            var slots =
                new List<BuffStateSlotDefinition>();
            BuffEffect[] effects = GetEffects();
            for (int e = 0;
                 e < effects.Length;
                 e++)
            {
                BuffEffect effect = effects[e];
                if (effect == null)
                    continue;
                BuffStateSlotDefinition[] required =
                    effect.RequiredSlotDefinitions;
                if (required == null)
                    continue;
                for (int i = 0;
                     i < required.Length;
                     i++)
                {
                    BuffStateSlotDefinition definition =
                        required[i];
                    if (definition == null ||
                        !definition.SlotId.IsValid)
                        continue;
                    bool exists = false;
                    for (int j = 0;
                         j < slots.Count;
                         j++)
                    {
                        if (slots[j].SlotId ==
                            definition.SlotId)
                        {
                            exists = true;
                            break;
                        }
                    }
                    if (!exists)
                        slots.Add(definition);
                }
            }
            BuffPeriodicReactionGroup[] periodic =
                LifecycleReactions?.Periodic;
            if (periodic != null)
            {
                for (int i = 0;
                     i < periodic.Length;
                     i++)
                {
                    BuffPeriodicReactionGroup group =
                        periodic[i];
                    if (group == null ||
                        !group.NextTriggerTickSlot.IsValid)
                        continue;
                    bool exists = false;
                    for (int j = 0;
                         j < slots.Count;
                         j++)
                    {
                        if (slots[j].SlotId ==
                            group.NextTriggerTickSlot)
                        {
                            exists = true;
                            break;
                        }
                    }
                    if (!exists)
                    {
                        slots.Add(
                            new BuffStateSlotDefinition
                            {
                                SlotId =
                                    group
                                        .NextTriggerTickSlot,
                                Kind =
                                    BuffValueKind.Int,
                            });
                    }
                }
            }
            return new BuffBlackboardLayout
            {
                Slots = slots.ToArray(),
            };
        }
    }
}
