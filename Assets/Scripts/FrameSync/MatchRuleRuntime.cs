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

    public enum MatchTopologyRole : byte
    {
        None = 0,
        BlueBase = 1,
        RedBase = 2,
    }

    public struct MatchStatisticsEntry
    {
        public UnitUid HeroUnitUid;
        public int Kills;
        public int Deaths;
        public int Assists;
        /// <summary>Last-hit minion/monster kills (creep score).</summary>
        public int CreepKills;
    }

    public struct MatchStatisticsRuntimeSnapshot
    {
        public System.Collections.Generic.List<MatchStatisticsEntry> Entries;
        public static readonly MatchStatisticsRuntimeSnapshot Empty = default;
    }

    public sealed class MatchStatisticsRuntime
    {
        /// <summary>
        /// Authored/stat-distance radius around a dying minion in which
        /// enemy heroes share the minion's base experience. It is converted
        /// to logic distance through UnitWorld.StatDistanceToLogicDistanceScale.
        /// Minion gold is not shared; only the killer receives gold.
        /// </summary>
        public const int MinionRewardShareRadius = 800;

        /// <summary>
        /// Killer share of a hero-victim reward (Combat v13.2 11.5); the
        /// remainder is split evenly among valid assisters.
        /// </summary>
        public const int HeroKillerShareNumerator = 3;
        public const int HeroKillerShareDenominator = 5;

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
                bool heroVictim =
                    victim.UnitKind == UnitKind.Hero;
                if (heroVictim)
                {
                    Increment(result.VictimUid, StatisticKind.Death);
                    string killerTeam = "-";
                    if (result.KillerHeroUid.IsValid() &&
                        unitWorld.TryGetUnit(
                            result.KillerHeroUid,
                            out UnitType killerUnit))
                    {
                        killerTeam =
                            killerUnit.TeamId.Value.ToString();
                    }
                    UnityEngine.Debug.Log(
                        $"[MatchStats] hero death tick=" +
                        $"{SimulationTickContext.Current.Tick} " +
                        $"victim={result.VictimUid} " +
                        $"victimTeam={victim.TeamId.Value} " +
                        $"killer={result.KillerHeroUid} " +
                        $"killerTeam={killerTeam}");
                }
                if (result.KillerHeroUid.IsValid())
                {
                    ValidateHero(unitWorld, result.KillerHeroUid, "killer");
                    string killStat =
                        heroVictim
                            ? "Kill"
                            : (victim.UnitKind ==
                                   UnitKind.Minion ||
                               victim.UnitKind ==
                                   UnitKind.Monster)
                                ? "CreepKill"
                                : "None";
                    UnityEngine.Debug.Log(
                        $"[MatchStats] death victim={result.VictimUid} " +
                        $"victimKind={victim.UnitKind} " +
                        $"killer={result.KillerHeroUid} " +
                        $"killStat={killStat}");
                    if (heroVictim)
                    {
                        Increment(result.KillerHeroUid, StatisticKind.Kill);
                    }
                    else if (victim.UnitKind == UnitKind.Minion ||
                             victim.UnitKind == UnitKind.Monster)
                    {
                        Increment(
                            result.KillerHeroUid,
                            StatisticKind.CreepKill);
                    }
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
                    if (heroVictim)
                    {
                        ValidateHero(unitWorld, assistant, "assistant");
                        Increment(assistant, StatisticKind.Assist);
                    }
                }

                // ---- Reward settlement ----
                // Hero victims: killer + assisters split gold and experience
                // (Combat v13.2 11.5).
                // Minion victims: experience is shared with enemy heroes in
                // MinionRewardShareRadius; gold goes to the killer only
                // (user rule).
                // Monster / structure victims: killer takes the full base
                // gold and experience (Combat v13.2 11.6).
                if (heroVictim)
                {
                    SettleHeroRewards(
                        unitWorld,
                        victim,
                        result);
                }
                else if (victim.UnitKind ==
                         UnitKind.Minion)
                {
                    SettleMinionRewards(
                        unitWorld,
                        victim,
                        result);
                }
                else
                {
                    if (result.KillerHeroUid.IsValid())
                    {
                        AddGoldAllocation(
                            result.KillerHeroUid,
                            victim.BaseGoldValue,
                            result.DeathSequenceInTick);
                        if (victim.BaseExperienceValue > 0)
                        {
                            GrantExperience(
                                unitWorld,
                                result.KillerHeroUid,
                                victim
                                    .BaseExperienceValue);
                        }
                    }
                }
            }
        }

        private void SettleHeroRewards(
            UnitWorld unitWorld,
            UnitType victim,
            in DeathResult result)
        {
            int baseGold = victim.BaseGoldValue;
            int baseExperience =
                victim.BaseExperienceValue;
            bool hasKiller =
                result.KillerHeroUid.IsValid();
            UnitUid[] assistants =
                result.AssistantHeroUids ??
                Array.Empty<UnitUid>();

            if (hasKiller && assistants.Length == 0)
            {
                AddGoldAllocation(
                    result.KillerHeroUid,
                    baseGold,
                    result.DeathSequenceInTick);
                if (baseExperience > 0)
                {
                    GrantExperience(
                        unitWorld,
                        result.KillerHeroUid,
                        baseExperience);
                }
                return;
            }

            if (hasKiller)
            {
                int killerGold =
                    ScaleIntegerFloor(
                        baseGold,
                        HeroKillerShareNumerator,
                        HeroKillerShareDenominator);
                int killerExperience =
                    ScaleIntegerFloor(
                        baseExperience,
                        HeroKillerShareNumerator,
                        HeroKillerShareDenominator);
                AddGoldAllocation(
                    result.KillerHeroUid,
                    killerGold,
                    result.DeathSequenceInTick);
                if (killerExperience > 0)
                {
                    GrantExperience(
                        unitWorld,
                        result.KillerHeroUid,
                        killerExperience);
                }
                SplitAmong(
                    unitWorld,
                    assistants,
                    baseGold - killerGold,
                    baseExperience - killerExperience,
                    result.DeathSequenceInTick);
                return;
            }

            SplitAmong(
                unitWorld,
                assistants,
                baseGold,
                baseExperience,
                result.DeathSequenceInTick);
        }

        private void SettleMinionRewards(
            UnitWorld unitWorld,
            UnitType victim,
            in DeathResult result)
        {
            if (result.KillerHeroUid.IsValid())
            {
                AddGoldAllocation(
                    result.KillerHeroUid,
                    victim.BaseGoldValue,
                    result.DeathSequenceInTick);
            }
            if (victim.BaseExperienceValue <= 0)
            {
                return;
            }

            // Experience recipients: the killer (forced, even if outside
            // range) plus every alive enemy hero within the share radius.
            var recipients =
                new List<UnitUid>();
            if (result.KillerHeroUid.IsValid() &&
                IsEnemyHero(
                    unitWorld,
                    result.KillerHeroUid,
                    victim))
            {
                recipients.Add(
                    result.KillerHeroUid);
            }
            fp logicRadius =
                (fp)MinionRewardShareRadius *
                unitWorld.StatDistanceToLogicDistanceScale;
            fp radiusSq =
                logicRadius * logicRadius;
            fp2 victimPosition =
                victim.PhysicsEntity != null
                    ? victim.PhysicsEntity
                        .Transform2D.Position
                    : fp2.zero;
            IReadOnlyList<UnitType> units =
                unitWorld.GetAllUnits();
            for (int i = 0;
                 i < units.Count;
                 i++)
            {
                UnitType candidate = units[i];
                if (candidate == null ||
                    candidate.UnitKind !=
                        UnitKind.Hero ||
                    candidate.LifeState !=
                        LifeState.Alive ||
                    candidate.TeamId ==
                        victim.TeamId ||
                    recipients.Contains(
                        candidate.UnitUid) ||
                    candidate.PhysicsEntity == null)
                {
                    continue;
                }
                fp2 delta =
                    candidate.PhysicsEntity
                        .Transform2D.Position -
                    victimPosition;
                if (fpmath.lengthsq(delta) >
                    radiusSq)
                {
                    continue;
                }
                recipients.Add(
                    candidate.UnitUid);
            }
            recipients.Sort(
                (left, right) =>
                    left.CompareTo(right));
            SplitExperience(
                unitWorld,
                recipients,
                victim.BaseExperienceValue);
        }

        private void SplitAmong(
            UnitWorld unitWorld,
            UnitUid[] assistants,
            int goldRemainder,
            int experienceRemainder,
            ushort deathSequence)
        {
            if (assistants == null ||
                assistants.Length == 0)
            {
                return;
            }
            int goldShare =
                goldRemainder / assistants.Length;
            int goldExtra =
                goldRemainder -
                goldShare * assistants.Length;
            int experienceShare =
                experienceRemainder /
                assistants.Length;
            int experienceExtra =
                experienceRemainder -
                experienceShare *
                assistants.Length;
            for (int i = 0;
                 i < assistants.Length;
                 i++)
            {
                int gold =
                    goldShare +
                    (i < goldExtra ? 1 : 0);
                if (gold > 0)
                {
                    AddGoldAllocation(
                        assistants[i],
                        gold,
                        deathSequence);
                }
                int experience =
                    experienceShare +
                    (i < experienceExtra
                        ? 1
                        : 0);
                if (experience > 0)
                {
                    GrantExperience(
                        unitWorld,
                        assistants[i],
                        experience);
                }
            }
        }

        private static void SplitExperience(
            UnitWorld unitWorld,
            List<UnitUid> recipients,
            int totalExperience)
        {
            if (recipients == null ||
                recipients.Count == 0 ||
                totalExperience <= 0)
            {
                return;
            }
            int share =
                totalExperience / recipients.Count;
            int extra =
                totalExperience -
                share * recipients.Count;
            for (int i = 0;
                 i < recipients.Count;
                 i++)
            {
                GrantExperience(
                    unitWorld,
                    recipients[i],
                    share + (i < extra ? 1 : 0));
            }
        }

        private static void GrantExperience(
            UnitWorld unitWorld,
            UnitUid heroUid,
            int amount)
        {
            if (amount <= 0 ||
                !unitWorld.TryGetUnit(
                    heroUid,
                    out UnitType hero))
            {
                return;
            }
            hero.StatHandler?.AddExperience(
                amount);
        }

        private static bool IsEnemyHero(
            UnitWorld unitWorld,
            UnitUid uid,
            UnitType victim)
        {
            if (!unitWorld.TryGetUnit(
                    uid,
                    out UnitType unit))
            {
                return false;
            }
            return unit.UnitKind ==
                    UnitKind.Hero &&
                unit.TeamId != victim.TeamId;
        }

        private static int ScaleIntegerFloor(
            int amount,
            int numerator,
            int denominator)
        {
            if (amount < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(amount));
            }
            if (numerator < 0 || denominator <= 0 ||
                numerator > denominator)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(numerator));
            }
            return checked(
                (int)((long)amount * numerator /
                    denominator));
        }

        public void Capture(ref MatchStatisticsRuntimeSnapshot state) =>
            state.Entries = new System.Collections.Generic.List<MatchStatisticsEntry>(entries);

        public void Restore(in MatchStatisticsRuntimeSnapshot state)
        {
            entries.Clear();
            var restored = state.Entries ?? new System.Collections.Generic.List<MatchStatisticsEntry>();
            UnitUid previous = default;
            for (int i = 0; i < restored.Count; i++)
            {
                MatchStatisticsEntry entry = restored[i];
                if (!entry.HeroUnitUid.IsValid() ||
                    (i > 0 && previous.CompareTo(entry.HeroUnitUid) >= 0) ||
                    entry.Kills < 0 || entry.Deaths < 0 ||
                    entry.Assists < 0 || entry.CreepKills < 0)
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
                case StatisticKind.CreepKill: entry.CreepKills++; break;
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
                GoldAmount = amount,
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

        private enum StatisticKind : byte { Kill, Death, Assist, CreepKill }
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
