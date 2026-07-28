using System;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    [Serializable]
    public struct UnitPoolConfig
    {
        [Min(0)] public int PrewarmCount;
        [Min(1)] public int MaxCapacity;
        public UnitPoolResizePolicy ResizePolicy;
        public static readonly UnitPoolConfig Default = new UnitPoolConfig { PrewarmCount = 0, MaxCapacity = 32, ResizePolicy = UnitPoolResizePolicy.Fixed };
    }
    public enum UnitPoolResizePolicy : byte { Fixed, Expand }
}
