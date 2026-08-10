using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit.Tests
{
    internal static class StatTestHelpers
    {
        public static StatDefinitionTable CreateDefaultTable()
        {
            var table = new StatDefinitionTable();

            table.Add(new StatDefinition
            {
                Id = StatId.AttackDamage,
                DebugName = "AD",
                DefaultBaseValue = 0m,
                SupportsLevelGrowth = true,
            });

            table.Add(new StatDefinition
            {
                Id = StatId.MaxHealth,
                DebugName = "HP",
                DefaultBaseValue = 0m,
                SupportsLevelGrowth = true,
            });

            table.Add(new StatDefinition
            {
                Id = StatId.Armor,
                DebugName = "Armor",
                DefaultBaseValue = 0m,
                SupportsLevelGrowth = false,
                HasMinValue = true,
                MinValue = 0m,
                HasMaxValue = true,
                MaxValue = 200m,
            });

            table.Add(new StatDefinition
            {
                Id = StatId.HealingReceivedRatio,
                DebugName = "HealingReceivedRatio",
                DefaultBaseValue = 1m,
                SupportsLevelGrowth = false,
            });

            return table;
        }

        public static StatPreset CreateSimplePreset()
        {
            var preset = new StatPreset();
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.AttackDamage,
                BaseValue = 100m,
                GrowthValue = 10m,
            });
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.MaxHealth,
                BaseValue = 500m,
                GrowthValue = 50m,
            });
            preset.Stats.Add(new StatPresetEntry
            {
                StatId = StatId.Armor,
                BaseValue = 30m,
                GrowthValue = 0m,
            });
            return preset;
        }

        public static UnitUid DefaultOwnerUid =>
            new UnitUid(100, 1, 0);
    }
}
