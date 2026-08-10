using System;
using System.Collections.Generic;
using UnityEngine;

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

        /// <summary>
        /// Transitional until the PeriodicReaction slice (design v14.2 7).
        /// </summary>
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
        public float ApplyVfxDurationSeconds;

        /// <summary>
        /// Transitional initial stack count; the reactions slice derives the
        /// initial StackChanged 0 -> InitialStacks event.
        /// </summary>
        public int InitialStacks = 1;

        public bool IsValid => ConfigId.IsValid;

        public bool IsInfinite =>
            Life != null && Life.Infinite;

        public int DurationTicks =>
            IsInfinite
                ? 0
                : BuffTickConverter.SecondsToTicks(
                    Life?.DurationSeconds ?? 0f);

        public int ExtendTicks =>
            BuffTickConverter.SecondsToTicks(
                Life?.ExtendSeconds ?? 0f);

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
