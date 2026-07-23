using System;
using System.Collections.Generic;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Base and growth values from a UnitPrototype (Unit v27.3 section 5.2.3).
    /// </summary>
    [Serializable]
    public sealed class StatPreset
    {
        public LevelExperienceConfig LevelExperience = LevelExperienceConfig.Disabled;
        public List<StatPresetEntry> Stats = new List<StatPresetEntry>();
    }
}
