using System;
using System.Collections.Generic;
using UnityEngine;

namespace FrameSyncMoba.RuntimeConfig
{
    /// <summary>
    /// Wave entry definition for a single minion wave in a single lane.
    /// </summary>
    [Serializable]
    public struct MinionWaveEntry
    {
        [Tooltip("Lane identifier: 0=top, 1=mid, 2=bot")]
        [Range(0, 2)]
        public byte LaneId;

        [Tooltip("Number of melee minions")]
        [Min(0)]
        public int MeleeCount;

        [Tooltip("Prototype ID for melee minions")]
        public int MeleePrototypeId;

        [Tooltip("Number of ranged minions")]
        [Min(0)]
        public int RangedCount;

        [Tooltip("Prototype ID for ranged minions")]
        public int RangedPrototypeId;

        [Tooltip("Number of siege minions (usually 0, active every 3rd wave)")]
        [Min(0)]
        public int SiegeCount;

        [Tooltip("Prototype ID for siege minions")]
        public int SiegePrototypeId;

        public bool IsValid => MeleeCount + RangedCount + SiegeCount > 0;
    }

    /// <summary>
    /// ScriptableObject configuration for minion wave spawning.
    /// Baked at Editor time and consumed by MinionSystem at runtime.
    /// Non-Hero Design v5 section 3.2.
    /// </summary>
    [CreateAssetMenu(
        fileName = "MinionWaveConfig",
        menuName = "FrameSyncMoba/Minion Wave Config")]
    public sealed class MinionWaveConfig : ScriptableObject
    {
        [Header("Timing")]
        [Tooltip("Ticks between wave spawns (e.g., 1800 = 30s at 60 tick/sec)")]
        [Min(1)]
        [SerializeField] private int waveIntervalTicks = 1800;

        [Tooltip("First wave spawn tick")]
        [Min(0)]
        [SerializeField] private int firstWaveTick = 90;

        [Header("Waves")]
        [SerializeField] private MinionWaveEntry[] waves = Array.Empty<MinionWaveEntry>();

        public int WaveIntervalTicks => waveIntervalTicks;
        public int FirstWaveTick => firstWaveTick;
        public MinionWaveEntry[] Waves => waves;

        public int WaveCount => waves?.Length ?? 0;

        private void OnValidate()
        {
            if (waveIntervalTicks < 1) waveIntervalTicks = 1800;
            if (firstWaveTick < 0) firstWaveTick = 90;
        }
    }
}
