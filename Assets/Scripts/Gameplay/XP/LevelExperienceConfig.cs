using System;
using System.Collections.Generic;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Static level-experience configuration attached to a StatPreset.
    /// Defines whether a unit can level up, its level bounds, and
    /// the cumulative experience required for each level.
    /// (Unit v27.3 §7.4)
    /// </summary>
    [Serializable]
    public sealed class LevelExperienceConfig
    {
        /// <summary>False for structures, wards, and other non-leveling units.</summary>
        public bool CanLevelUp;

        /// <summary>Level at spawn (default 1).</summary>
        public ushort InitialLevel = 1;

        /// <summary>Hard cap; cannot exceed this level.</summary>
        public ushort MaxLevel = 1;

        /// <summary>Starting experience value at spawn.</summary>
        public int InitialExperience;

        /// <summary>
        /// Experience required to advance from each level.
        /// Index 0 = level 1 to 2, index 1 = level 2 to 3, etc.
        /// Length = MaxLevel - 1.
        /// </summary>
        public List<int> RequiredExperiencePerLevel = new List<int>();

        /// <summary>How CurrentHealth is adjusted when levelling up.</summary>
        public LevelUpCurrentValueRule HealthOnLevelUp = LevelUpCurrentValueRule.KeepCurrent;

        /// <summary>How CurrentCastResource is adjusted when levelling up.</summary>
        public LevelUpCurrentValueRule CastResourceOnLevelUp = LevelUpCurrentValueRule.KeepCurrent;

        /// <summary>
        /// Returns the total XP needed to have reached the given level.
        /// </summary>
        public int GetTotalXpForLevel(int level)
        {
            if (level <= 1) return 0;
            int idx = level - 2;
            if (idx < 0 || idx >= RequiredExperiencePerLevel.Count)
                return int.MaxValue;
            int total = 0;
            for (int i = 0; i <= idx; i++)
                total = checked(total + RequiredExperiencePerLevel[i]);
            return total;
        }

        /// <summary>
        /// Returns the cumulative XP threshold for the next level, or int.MaxValue at cap.
        /// </summary>
        public int GetXpForNextLevel(int currentLevel)
        {
            if (currentLevel < 1) currentLevel = 1;
            if (currentLevel >= MaxLevel) return int.MaxValue;
            int idx = currentLevel - 1;
            if (idx < 0 || idx >= RequiredExperiencePerLevel.Count)
                return int.MaxValue;
            return RequiredExperiencePerLevel[idx];
        }

        public static readonly LevelExperienceConfig Disabled = new LevelExperienceConfig
        {
            CanLevelUp = false,
            InitialLevel = 1,
            MaxLevel = 1,
        };

        /// <summary>Default MOBA config: levels 1-18 with standard XP curve.</summary>
        public static LevelExperienceConfig CreateDefault18()
        {
            var config = new LevelExperienceConfig
            {
                CanLevelUp = true,
                InitialLevel = 1,
                MaxLevel = 18,
                RequiredExperiencePerLevel = new List<int>
                {
                    280,   // Lv 2
                    380,   // Lv 3
                    480,   // Lv 4
                    580,   // Lv 5
                    680,   // Lv 6
                    780,   // Lv 7
                    880,   // Lv 8
                    980,   // Lv 9
                    1080,  // Lv10
                    1180,  // Lv11
                    1280,  // Lv12
                    1380,  // Lv13
                    1480,  // Lv14
                    1580,  // Lv15
                    1680,  // Lv16
                    1740,  // Lv17
                    1880,  // Lv18
                },
            };
            return config;
        }
    }

    /// <summary>
    /// How current resource values are adjusted when a unit levels up.
    /// (Unit v27.3 §7.4.1)
    /// </summary>
    public enum LevelUpCurrentValueRule : byte
    {
        /// <summary>Current value unchanged (standard for health / mana).</summary>
        KeepCurrent = 0,
        AddMaximumDelta = 1,
        PreserveRatio = 2,
        Refill = 3,
    }
}
