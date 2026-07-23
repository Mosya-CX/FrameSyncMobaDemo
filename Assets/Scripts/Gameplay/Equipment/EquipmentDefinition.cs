using System;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Configuration for a single equipment item (Equipment/Gold v12 §2.1).
    /// Plain serializable class — ScriptableObject wrappers deferred to authoring layer.
    /// </summary>
    [Serializable]
    public sealed class EquipmentDefinition
    {
        public int Id;
        public string Name;
        public string Description;
        public EquipmentTier Tier;
        public int Value;
        public int MaxStack = 1;

        public EquipmentFixedStatAuthoring[] FixedStats;
        public EquipmentFixedStat[] BakedFixedStats { get; private set; } =
            Array.Empty<EquipmentFixedStat>();
        public bool IsBaked { get; private set; }
        public EquipmentEffectDef[] Effects;
        public string[] Tags;
        public EquipmentRecipe Recipe;

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
