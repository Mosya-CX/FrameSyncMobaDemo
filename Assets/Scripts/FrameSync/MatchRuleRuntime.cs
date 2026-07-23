using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.Unit;
using Unity.Mathematics.FixedPoint;
using UnitType = FrameSyncMoba.Unit.Unit;

namespace FrameSyncMoba.FrameSync
{
    public enum MatchPhase : byte
    {
        Preparing = 0,
        Countdown = 1,
        Running = 2,
        Ending = 3,
        Finished = 4,
    }

    public enum MatchEndReason : byte
    {
        None = 0,
        BaseDestroyed = 1,
        SimultaneousBaseDestruction = 2,
    }

    public struct MatchStatisticsEntry
    {
        public UnitUid HeroUnitUid;
        public int Kills;
        public int Deaths;
        public int Assists;
    }

    public struct MatchStatisticsRuntimeSnapshot
    {
        public MatchStatisticsEntry[] Entries;
        public static readonly MatchStatisticsRuntimeSnapshot Empty = default;
    }

    public sealed class MatchStatisticsRuntime
    {
        private readonly List<MatchStatisticsEntry> entries =
            new List<MatchStatisticsEntry>();
        private readonly List<GoldAllocation> goldAllocations =
            new List<GoldAllocation>();

        public IReadOnlyList<MatchStatisticsEntry> Entries => entries;
        public IReadOnlyList<GoldAllocation> GoldAllocations => goldAllocations;

        public void Consume(
            IReadOnlyList<DeathResult> deathResults,
            UnitWorld unitWorld)
        {
            goldAllocations.Clear();
            if (deathResults == null) return;
            if (unitWorld == null) throw new ArgumentNullException(nameof(unitWorld));
            int previousSequence = -1;
            for (int i = 0; i < deathResults.Count; i++)
            {
                DeathResult result = deathResults[i];
                if (result.DeathSequenceInTick <= previousSequence)
                    throw new DeterministicSimulationException(
                        "Formal death results are not in stable DeathSequence order.");
                previousSequence = result.DeathSequenceInTick;
                if (!unitWorld.TryGetUnit(result.VictimUid, out UnitType victim))
                    throw new DeterministicSimulationException(
                        $"Formal death victim {result.VictimUid} is missing.");
                if (victim.UnitKind == UnitKind.Hero)
                    Increment(result.VictimUid, StatisticKind.Death);
                if (result.KillerHeroUid.IsValid())
                {
                    ValidateHero(unitWorld, result.KillerHeroUid, "killer");
                    Increment(result.KillerHeroUid, StatisticKind.Kill);
                    AddGoldAllocation(
                        result.KillerHeroUid,
                        victim.BaseGoldValue,
                        result.DeathSequenceInTick);
                }
                UnitUid[] assistants = result.AssistantHeroUids ?? Array.Empty<UnitUid>();
                UnitUid previousAssistant = default;
                for (int assistantIndex = 0; assistantIndex < assistants.Length; assistantIndex++)
                {
                    UnitUid assistant = assistants[assistantIndex];
                    if (!assistant.IsValid() ||
                        (assistantIndex > 0 && previousAssistant.CompareTo(assistant) >= 0))
                        throw new DeterministicSimulationException(
                            "Formal death assistants are not unique and stable-sorted.");
                    previousAssistant = assistant;
                    ValidateHero(unitWorld, assistant, "assistant");
                    Increment(assistant, StatisticKind.Assist);
                    AddGoldAllocation(
                        assistant,
                        victim.BaseGoldValue > 0
                            ? (int)(((long)victim.BaseGoldValue * 30L) / 100L)
                            : 0,
                        result.DeathSequenceInTick);
                }
            }
        }

        public void Capture(ref MatchStatisticsRuntimeSnapshot state) =>
            state.Entries = entries.ToArray();

        public void Restore(in MatchStatisticsRuntimeSnapshot state)
        {
            entries.Clear();
            MatchStatisticsEntry[] restored = state.Entries ?? Array.Empty<MatchStatisticsEntry>();
            UnitUid previous = default;
            for (int i = 0; i < restored.Length; i++)
            {
                MatchStatisticsEntry entry = restored[i];
                if (!entry.HeroUnitUid.IsValid() ||
                    (i > 0 && previous.CompareTo(entry.HeroUnitUid) >= 0) ||
                    entry.Kills < 0 || entry.Deaths < 0 || entry.Assists < 0)
                    throw new DeterministicSimulationException(
                        "Match statistics snapshot is invalid or non-canonical.");
                previous = entry.HeroUnitUid;
                entries.Add(entry);
            }
        }

        private void Increment(UnitUid uid, StatisticKind kind)
        {
            int index = Find(uid, out bool found);
            MatchStatisticsEntry entry = found
                ? entries[index]
                : new MatchStatisticsEntry { HeroUnitUid = uid };
            switch (kind)
            {
                case StatisticKind.Kill: entry.Kills++; break;
                case StatisticKind.Death: entry.Deaths++; break;
                case StatisticKind.Assist: entry.Assists++; break;
            }
            if (found) entries[index] = entry;
            else entries.Insert(index, entry);
        }

        private int Find(UnitUid uid, out bool found)
        {
            int low = 0;
            int high = entries.Count;
            while (low < high)
            {
                int middle = low + ((high - low) / 2);
                if (entries[middle].HeroUnitUid.CompareTo(uid) < 0) low = middle + 1;
                else high = middle;
            }
            found = low < entries.Count && entries[low].HeroUnitUid == uid;
            return low;
        }

        private void AddGoldAllocation(
            UnitUid receiver,
            int amount,
            ushort deathSequence)
        {
            if (amount <= 0) return;
            goldAllocations.Add(new GoldAllocation
            {
                ReceiverHeroUid = receiver,
                GoldAmount = (fp)amount,
                DeathSequenceInTick = deathSequence,
            });
        }

        private static void ValidateHero(
            UnitWorld world,
            UnitUid uid,
            string role)
        {
            if (!world.TryGetUnit(uid, out UnitType unit) ||
                unit.UnitKind != UnitKind.Hero)
                throw new DeterministicSimulationException(
                    $"Formal death {role} {uid} is missing or is not a Hero.");
        }

        private enum StatisticKind : byte { Kill, Death, Assist }
    }

    public struct MatchRuleRuntimeSnapshot
    {
        public MatchPhase CurrentPhase;
        public int PhaseEnterTick;
        public int RunningStartTick;
        public UnitUid BlueBaseUnitUid;
        public UnitUid RedBaseUnitUid;
        public int GameOverTick;
        public int FinishTick;
        public TeamId WinningTeamId;
        public MatchEndReason EndReason;
        public MatchStatisticsRuntimeSnapshot Statistics;
        public static readonly MatchRuleRuntimeSnapshot Empty = default;
    }

    public sealed class MatchRuleRuntime
    {
        private readonly int endingDurationTicks;
        public MatchStatisticsRuntime Statistics { get; } = new MatchStatisticsRuntime();
        public MatchPhase CurrentPhase { get; private set; } = MatchPhase.Preparing;
        public int PhaseEnterTick { get; private set; }
        public int RunningStartTick { get; private set; } = -1;
        public UnitUid BlueBaseUnitUid { get; private set; }
        public UnitUid RedBaseUnitUid { get; private set; }
        public int GameOverTick { get; private set; } = -1;
        public int FinishTick { get; private set; } = -1;
        public TeamId WinningTeamId { get; private set; } = TeamId.Neutral;
        public MatchEndReason EndReason { get; private set; }

        public MatchRuleRuntime(int endingDurationTicks)
        {
            if (endingDurationTicks < 0)
                throw new ArgumentOutOfRangeException(nameof(endingDurationTicks));
            this.endingDurationTicks = endingDurationTicks;
        }

        public void RegisterBases(UnitUid blueBaseUnitUid, UnitUid redBaseUnitUid)
        {
            if (!blueBaseUnitUid.IsValid() || !redBaseUnitUid.IsValid() ||
                blueBaseUnitUid == redBaseUnitUid)
                throw new ArgumentException("Match bases require two distinct valid UnitUids.");
            BlueBaseUnitUid = blueBaseUnitUid;
            RedBaseUnitUid = redBaseUnitUid;
        }

        public void BeginCountdown(int currentTick, int countdownTicks)
        {
            if (CurrentPhase != MatchPhase.Preparing || countdownTicks < 0)
                throw new InvalidOperationException("Match countdown transition is invalid.");
            CurrentPhase = MatchPhase.Countdown;
            PhaseEnterTick = currentTick;
            RunningStartTick = checked(currentTick + countdownTicks);
        }

        public void AdvanceTick(int currentTick)
        {
            if (CurrentPhase == MatchPhase.Countdown && currentTick >= RunningStartTick)
            {
                CurrentPhase = MatchPhase.Running;
                PhaseEnterTick = currentTick;
            }
            else if (CurrentPhase == MatchPhase.Ending && currentTick >= FinishTick)
            {
                CurrentPhase = MatchPhase.Finished;
                PhaseEnterTick = currentTick;
            }
        }

        public bool EvaluateAuthorityConfirmedTick(int tick, UnitWorld unitWorld)
        {
            if (CurrentPhase != MatchPhase.Running || unitWorld == null ||
                !BlueBaseUnitUid.IsValid() || !RedBaseUnitUid.IsValid())
                return false;
            bool blueDestroyed = IsDead(unitWorld, BlueBaseUnitUid);
            bool redDestroyed = IsDead(unitWorld, RedBaseUnitUid);
            if (!blueDestroyed && !redDestroyed) return false;

            CurrentPhase = MatchPhase.Ending;
            PhaseEnterTick = tick;
            GameOverTick = tick;
            FinishTick = checked(tick + endingDurationTicks);
            if (blueDestroyed && redDestroyed)
            {
                WinningTeamId = TeamId.Neutral;
                EndReason = MatchEndReason.SimultaneousBaseDestruction;
            }
            else
            {
                UnitUid winningBase = blueDestroyed ? RedBaseUnitUid : BlueBaseUnitUid;
                if (!unitWorld.TryGetUnit(winningBase, out UnitType winningUnit))
                    throw new DeterministicSimulationException(
                        $"Winning base Unit {winningBase} is missing.");
                WinningTeamId = winningUnit.TeamId;
                EndReason = MatchEndReason.BaseDestroyed;
            }
            return true;
        }

        public void Capture(ref MatchRuleRuntimeSnapshot state)
        {
            state.CurrentPhase = CurrentPhase;
            state.PhaseEnterTick = PhaseEnterTick;
            state.RunningStartTick = RunningStartTick;
            state.BlueBaseUnitUid = BlueBaseUnitUid;
            state.RedBaseUnitUid = RedBaseUnitUid;
            state.GameOverTick = GameOverTick;
            state.FinishTick = FinishTick;
            state.WinningTeamId = WinningTeamId;
            state.EndReason = EndReason;
            Statistics.Capture(ref state.Statistics);
        }

        public void Restore(in MatchRuleRuntimeSnapshot state)
        {
            CurrentPhase = state.CurrentPhase;
            PhaseEnterTick = state.PhaseEnterTick;
            RunningStartTick = state.RunningStartTick;
            BlueBaseUnitUid = state.BlueBaseUnitUid;
            RedBaseUnitUid = state.RedBaseUnitUid;
            GameOverTick = state.GameOverTick;
            FinishTick = state.FinishTick;
            WinningTeamId = state.WinningTeamId;
            EndReason = state.EndReason;
            Statistics.Restore(state.Statistics);
        }

        public void Resolve(UnitWorld unitWorld)
        {
            if (BlueBaseUnitUid.IsValid() && !unitWorld.TryGetUnit(BlueBaseUnitUid, out _))
                throw new DeterministicSimulationException($"Blue base {BlueBaseUnitUid} is missing.");
            if (RedBaseUnitUid.IsValid() && !unitWorld.TryGetUnit(RedBaseUnitUid, out _))
                throw new DeterministicSimulationException($"Red base {RedBaseUnitUid} is missing.");
        }

        private static bool IsDead(UnitWorld world, UnitUid uid)
        {
            if (!world.TryGetUnit(uid, out UnitType unit))
                throw new DeterministicSimulationException($"Registered base {uid} is missing.");
            return unit.LifeState == LifeState.Dead;
        }
    }
}
