using System;
using UnityEngine;

namespace FrameSyncMoba.RuntimeConfig
{
    [Serializable]
    public struct MinionWaveMember
    {
        [Min(1)] public int UnitPrototypeId;
        [Min(1)] public int Count;
        [Min(0)] public int FirstSpawnOffsetTicks;
        [Min(0)] public int SpawnStepTicks;
        [Min(0)] public int FormationGroup;
    }

    [Serializable]
    public struct MinionWaveComposition
    {
        public MinionWaveMember[] Members;
    }

    [Serializable]
    public struct MinionWavePhase
    {
        [Min(0)] public int StartWaveIndex;
        public MinionWaveComposition[] CompositionCycle;
    }

    public readonly struct BakedMinionWaveConfig
    {
        public readonly int WaveIntervalTicks;
        public readonly int FirstWaveTick;
        public readonly MinionWavePhase[] Phases;

        public BakedMinionWaveConfig(
            int waveIntervalTicks,
            int firstWaveTick,
            MinionWavePhase[] phases)
        {
            if (waveIntervalTicks <= 0 || firstWaveTick < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(waveIntervalTicks),
                    "Wave interval must be positive and first wave Tick nonnegative.");
            WaveIntervalTicks = waveIntervalTicks;
            FirstWaveTick = firstWaveTick;
            Phases = ClonePhases(phases);
        }

        public static BakedMinionWaveConfig FromConfig(
            MinionWaveConfig config)
        {
            if (config == null)
                return new BakedMinionWaveConfig(
                    1800,
                    90,
                    Array.Empty<MinionWavePhase>());
            return new BakedMinionWaveConfig(
                config.WaveIntervalTicks,
                config.FirstWaveTick,
                config.Phases);
        }

        private static MinionWavePhase[] ClonePhases(
            MinionWavePhase[] source)
        {
            if (source == null || source.Length == 0)
                return Array.Empty<MinionWavePhase>();

            var result = new MinionWavePhase[source.Length];
            for (int phaseIndex = 0;
                 phaseIndex < source.Length;
                 phaseIndex++)
            {
                MinionWavePhase phase = source[phaseIndex];
                MinionWaveComposition[] cycle =
                    phase.CompositionCycle ??
                    Array.Empty<MinionWaveComposition>();
                var cycleCopy =
                    new MinionWaveComposition[cycle.Length];
                for (int compositionIndex = 0;
                     compositionIndex < cycle.Length;
                     compositionIndex++)
                {
                    MinionWaveMember[] members =
                        cycle[compositionIndex].Members ??
                        Array.Empty<MinionWaveMember>();
                    cycleCopy[compositionIndex].Members =
                        (MinionWaveMember[])members.Clone();
                }
                result[phaseIndex] = new MinionWavePhase
                {
                    StartWaveIndex = phase.StartWaveIndex,
                    CompositionCycle = cycleCopy,
                };
            }
            return result;
        }
    }

    [CreateAssetMenu(
        fileName = "MinionWaveConfig",
        menuName = "FrameSyncMoba/Minion Wave Config")]
    public sealed class MinionWaveConfig : ScriptableObject
    {
        [Header("Timing")]
        [Min(1)]
        [SerializeField] private int waveIntervalTicks = 1800;
        [Min(0)]
        [SerializeField] private int firstWaveTick = 90;

        [Header("Phase and composition cycle")]
        [SerializeField] private MinionWavePhase[] phases =
            Array.Empty<MinionWavePhase>();

        public int WaveIntervalTicks => waveIntervalTicks;
        public int FirstWaveTick => firstWaveTick;
        public MinionWavePhase[] Phases => phases;

        private void OnValidate()
        {
            if (waveIntervalTicks < 1) waveIntervalTicks = 1;
            if (firstWaveTick < 0) firstWaveTick = 0;
        }
    }
}
