using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Serializable snapshot of all StatHandler cross-Tick state (Unit v27.3 ��5.9.1).
    /// </summary>
    public struct StatHandlerSnapshot
    {
        public int Level;
        public fp CurrentHealth;
        public fp CurrentCastResource;
        public int CurrentExperience;
        public uint NextStatSeq;

        public int NextShieldInstanceId;
        public ShieldInstance[] ShieldInstances;

        public StatRuntimeEntrySnapshot[] Entries;
    }

    /// <summary>
    /// Snapshot of one StatRuntimeEntry (Unit v27.3 ��5.9.1).
    /// </summary>
    public struct StatRuntimeEntrySnapshot
    {
        public StatId StatId;
        public fp LevelBaseValue;
        public fp FinalValue;
        public fp PreviousLogicTickFinalValue;
        public bool Dirty;
        public StatModifier[] Modifiers;
    }
}
