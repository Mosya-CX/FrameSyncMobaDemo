using System;
using Unity.Mathematics.FixedPoint;
using UnityEngine;
using FrameSyncMoba.RuntimeConfig;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Authoring ScriptableObject for one equipment item
    /// (Equipment/Gold v12 section 2.1).
    /// </summary>
    [CreateAssetMenu(
        fileName = "Equipment",
        menuName = "MOBA/Equipment")]
    public sealed class EquipmentDefinition :
        ScriptableObject
    {
        public int Id;
        public string Name;
        [TextArea]
        public string Description;
        [HideInInspector]
        public Sprite Icon;
        public string IconAddress;
        public EquipmentTier Tier;
        public int Value;
        public int MaxStack = 1;

        public EquipmentFixedStatAuthoring[] FixedStats;
        public EquipmentEffectDef[] Effects;
        public EquipmentTagDefinition[] Tags;
        public EquipmentRecipe Recipe;

        /// <summary>
        /// Stackability is derived from Tier (design v12 2.4):
        /// only Consumable items stack.
        /// </summary>
        public bool CanStack =>
            Tier == EquipmentTier.Consumable;

        public EquipmentFixedStat[] BakedFixedStats { get; private set; } =
            Array.Empty<EquipmentFixedStat>();
        public bool IsBaked { get; private set; }

        public bool IsValid => Id != 0;

        public void Bake(int tickRate = 30)
        {
            DeterministicTimeConversion.ValidateSupportedTickRate(
                tickRate);
            EquipmentFixedStatAuthoring[] source =
                FixedStats ?? Array.Empty<EquipmentFixedStatAuthoring>();
            var baked = new EquipmentFixedStat[source.Length];
            for (int i = 0; i < source.Length; i++)
                baked[i] = new EquipmentFixedStat(source[i].Stat, (fp)source[i].Value);
            BakedFixedStats = baked;
            EquipmentEffectDef[] effects =
                Effects ?? Array.Empty<EquipmentEffectDef>();
            for (int effectIndex = 0;
                 effectIndex < effects.Length;
                 effectIndex++)
            {
                EquipmentEffectDef effect = effects[effectIndex];
                if (effect == null)
                    continue;
                EquipmentActiveSettings settings =
                    effect.ActiveSettings;
                settings.CooldownTicks =
                    settings.Cooldown.IsAuthored
                        ? settings.Cooldown.BakeTicks(tickRate)
                        : DeterministicTimeConversion
                            .Legacy30HzTicksToTicks(
                                settings.CooldownTicks,
                                tickRate);
                effect.ActiveSettings = settings;
                EquipmentEffectModule[] modules =
                    effect.Modules ??
                    Array.Empty<EquipmentEffectModule>();
                for (int moduleIndex = 0;
                     moduleIndex < modules.Length;
                     moduleIndex++)
                    modules[moduleIndex]?.BakeTime(tickRate);
            }
            IsBaked = true;
        }
    }

    /// <summary>
    /// Authoring-time fixed stat with float value, baked to fp at runtime.
    /// </summary>
    [Serializable]
    public struct EquipmentFixedStatAuthoring
    {
        public StatId Stat;
        public float Value;
    }

    /// <summary>
    /// Crafting recipe: components required plus their counts.
    /// </summary>
    [Serializable]
    public sealed class EquipmentRecipe
    {
        public EquipmentRecipePart[] Components;
    }

    [Serializable]
    public struct EquipmentRecipePart
    {
        public EquipmentDefinition Item;
        public int Count;
    }
}
