using System;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

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
        public Sprite Icon;
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

        public void Bake()
        {
            EquipmentFixedStatAuthoring[] source =
                FixedStats ?? Array.Empty<EquipmentFixedStatAuthoring>();
            var baked = new EquipmentFixedStat[source.Length];
            for (int i = 0; i < source.Length; i++)
                baked[i] = new EquipmentFixedStat(source[i].Stat, (fp)source[i].Value);
            BakedFixedStats = baked;
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
