using System;
using UnityEngine;

namespace FrameSyncMoba.RuntimeConfig
{
    [Serializable]
    public struct MinionTeamPrototypeOverride
    {
        [Range(1, byte.MaxValue)] public int TeamId;
        [Min(1)] public int UnitPrototypeId;
    }

    [Serializable]
    public struct MinionWaveMember
    {
        [Min(1)] public int UnitPrototypeId;
        [Tooltip("Optional team-specific runtime prototypes. Entries must be sorted by TeamId.")]
        public MinionTeamPrototypeOverride[] TeamPrototypeOverrides;
        [Min(1)] public int Count;
        public DurationAuthoring FirstSpawnOffset;
        [HideInInspector]
        [Min(0)] public int FirstSpawnOffsetTicks;
        public DurationAuthoring SpawnStep;
        [HideInInspector]
        [Min(0)] public int SpawnStepTicks;
        [Min(0)] public int FormationGroup;

        public int ResolveUnitPrototypeId(int teamId)
        {
            MinionTeamPrototypeOverride[] overrides =
                TeamPrototypeOverrides ??
                Array.Empty<MinionTeamPrototypeOverride>();
            for (int i = 0; i < overrides.Length; i++)
            {
                if (overrides[i].TeamId == teamId)
                    return overrides[i].UnitPrototypeId;
            }
            return UnitPrototypeId;
        }
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
            MinionWaveConfig config,
            int tickRate = 30)
        {
            if (config == null)
                return new BakedMinionWaveConfig(
                    DeterministicTimeConversion
                        .Legacy30HzTicksToTicks(1800, tickRate),
                    DeterministicTimeConversion
                        .Legacy30HzTicksToTicks(90, tickRate),
                    Array.Empty<MinionWavePhase>());
            return new BakedMinionWaveConfig(
                config.BakeWaveIntervalTicks(tickRate),
                config.BakeFirstWaveTick(tickRate),
                ClonePhases(config.Phases, tickRate));
        }

        private static MinionWavePhase[] ClonePhases(
            MinionWavePhase[] source)
        {
            return ClonePhases(source, 0);
        }

        private static MinionWavePhase[] ClonePhases(
            MinionWavePhase[] source,
            int tickRate)
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
                    var memberCopy =
                        new MinionWaveMember[members.Length];
                    for (int memberIndex = 0;
                         memberIndex < members.Length;
                         memberIndex++)
                    {
                        memberCopy[memberIndex] =
                            members[memberIndex];
                        if (tickRate > 0)
                        {
                            MinionWaveMember member =
                                memberCopy[memberIndex];
                            member.FirstSpawnOffsetTicks =
                                member.FirstSpawnOffset.IsAuthored
                                    ? member.FirstSpawnOffset
                                        .BakeTicks(tickRate)
                                    : DeterministicTimeConversion
                                        .Legacy30HzTicksToTicks(
                                            member.FirstSpawnOffsetTicks,
                                            tickRate);
                            member.SpawnStepTicks =
                                member.SpawnStep.IsAuthored
                                    ? member.SpawnStep.BakeTicks(tickRate)
                                    : DeterministicTimeConversion
                                        .Legacy30HzTicksToTicks(
                                            member.SpawnStepTicks,
                                            tickRate);
                            memberCopy[memberIndex] = member;
                        }
                        MinionTeamPrototypeOverride[] overrides =
                            members[memberIndex]
                                .TeamPrototypeOverrides;
                        memberCopy[memberIndex]
                            .TeamPrototypeOverrides =
                            overrides == null
                                ? Array.Empty<
                                    MinionTeamPrototypeOverride>()
                                : (MinionTeamPrototypeOverride[])
                                    overrides.Clone();
                    }
                    cycleCopy[compositionIndex].Members =
                        memberCopy;
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
        [SerializeField] private DurationAuthoring waveInterval;
        [HideInInspector]
        [Min(1)]
        [SerializeField] private int waveIntervalTicks = 1800;
        [SerializeField] private DurationAuthoring firstWaveDelay;
        [HideInInspector]
        [Min(0)]
        [SerializeField] private int firstWaveTick = 90;

        [Header("Phase and composition cycle")]
        [SerializeField] private MinionWavePhase[] phases =
            Array.Empty<MinionWavePhase>();

        public int WaveIntervalTicks => waveIntervalTicks;
        public int FirstWaveTick => firstWaveTick;
        public MinionWavePhase[] Phases => phases;

        internal int BakeWaveIntervalTicks(int tickRate)
        {
            return waveInterval.IsAuthored
                ? waveInterval.BakeTicks(tickRate)
                : DeterministicTimeConversion
                    .Legacy30HzTicksToTicks(
                        waveIntervalTicks,
                        tickRate);
        }

        internal int BakeFirstWaveTick(int tickRate)
        {
            return firstWaveDelay.IsAuthored
                ? firstWaveDelay.BakeTicks(tickRate)
                : DeterministicTimeConversion
                    .Legacy30HzTicksToTicks(
                        firstWaveTick,
                        tickRate);
        }

        private void OnValidate()
        {
            if (waveIntervalTicks < 1) waveIntervalTicks = 1;
            if (firstWaveTick < 0) firstWaveTick = 0;
        }
    }
}
