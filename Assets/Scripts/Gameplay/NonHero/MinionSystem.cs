using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using FrameSyncMoba.RuntimeConfig;

namespace FrameSyncMoba.Unit
{
    public sealed class MinionSystem
    {
        private readonly UnitWorld unitWorld;
        private readonly BakedMinionWaveConfig schedule;
        private readonly LaneRuntimeData[] lanes;
        private readonly List<MinionTicket> pendingTickets =
            new List<MinionTicket>(64);
        private readonly List<UnitUid> managedMinionUids =
            new List<UnitUid>(128);
        private int waveIndex;
        private int nextWaveLogicTick;
        private int nextTicketCursor;

        public int WaveIndex => waveIndex;
        public int NextWaveLogicTick => nextWaveLogicTick;
        public IReadOnlyList<MinionTicket> PendingTickets =>
            pendingTickets;
        public IReadOnlyList<UnitUid> ManagedMinionUids =>
            managedMinionUids;

        public bool TryGetLane(
            int laneId,
            out LaneRuntimeData lane)
        {
            for (int i = 0; i < lanes.Length; i++)
            {
                if (lanes[i].LaneId != laneId)
                    continue;
                lane = lanes[i];
                return true;
            }
            lane = null;
            return false;
        }

        public MinionSystem(
            UnitWorld unitWorld,
            in BakedMinionWaveConfig schedule,
            LaneRuntimeData[] lanes)
        {
            this.unitWorld = unitWorld ??
                throw new ArgumentNullException(nameof(unitWorld));
            if (schedule.WaveIntervalTicks <= 0 ||
                schedule.FirstWaveTick < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(schedule));
            this.schedule = schedule;
            this.lanes = lanes ??
                Array.Empty<LaneRuntimeData>();
            ValidateStaticTopology();
            nextWaveLogicTick = schedule.FirstWaveTick;
        }

        public void TickLogic()
        {
            int currentTick =
                SimulationTickContext.Current.Tick;
            while (currentTick >= nextWaveLogicTick)
            {
                ExpandWave(nextWaveLogicTick);
                waveIndex++;
                nextWaveLogicTick = checked(
                    nextWaveLogicTick +
                    schedule.WaveIntervalTicks);
            }

            while (nextTicketCursor <
                   pendingTickets.Count &&
                   pendingTickets[nextTicketCursor]
                       .SpawnLogicTick <= currentTick)
            {
                SpawnSingleMinion(
                    pendingTickets[nextTicketCursor]);
                nextTicketCursor++;
            }
        }

        public bool UnregisterManagedUnit(
            UnitUid unitUid)
        {
            for (int i = 0;
                 i < managedMinionUids.Count;
                 i++)
            {
                if (managedMinionUids[i] != unitUid)
                    continue;
                managedMinionUids.RemoveAt(i);
                return true;
            }
            return false;
        }

        public void Capture(
            ref MinionSystemSnapshot state)
        {
            state.WaveIndex = waveIndex;
            state.NextWaveLogicTick =
                nextWaveLogicTick;
            state.PendingTickets =
                pendingTickets.ToArray();
            state.NextTicketCursor =
                nextTicketCursor;
            state.ManagedMinionUids =
                managedMinionUids.ToArray();
        }

        public void Restore(
            in MinionSystemSnapshot state)
        {
            if (state.WaveIndex < 0 ||
                state.NextWaveLogicTick < 0)
                throw new DeterministicSimulationException(
                    "Minion schedule snapshot is invalid.");

            MinionTicket[] tickets =
                state.PendingTickets ??
                Array.Empty<MinionTicket>();
            for (int i = 0; i < tickets.Length; i++)
            {
                ValidateTicket(tickets[i]);
                if (i > 0 &&
                    CompareTicket(
                        tickets[i - 1],
                        tickets[i]) >= 0)
                    throw new DeterministicSimulationException(
                        "Minion tickets are not in canonical order.");
            }
            if (state.NextTicketCursor < 0 ||
                state.NextTicketCursor >
                tickets.Length)
                throw new DeterministicSimulationException(
                    "Minion ticket cursor is invalid.");

            UnitUid[] managed =
                state.ManagedMinionUids ??
                Array.Empty<UnitUid>();
            UnitUid previous = default;
            bool hasPrevious = false;
            for (int i = 0; i < managed.Length; i++)
            {
                UnitUid uid = managed[i];
                if (!uid.IsValid())
                    continue;
                if (hasPrevious &&
                    previous.CompareTo(uid) >= 0)
                    throw new DeterministicSimulationException(
                        "Managed Minion UIDs are not canonical.");
                previous = uid;
                hasPrevious = true;
            }

            waveIndex = state.WaveIndex;
            nextWaveLogicTick =
                state.NextWaveLogicTick;
            nextTicketCursor =
                state.NextTicketCursor;
            pendingTickets.Clear();
            pendingTickets.AddRange(tickets);
            managedMinionUids.Clear();
            managedMinionUids.AddRange(managed);
        }

        public void Resolve(
            in RollbackContext context)
        {
            for (int i = 0;
                 i < managedMinionUids.Count;
                 i++)
            {
                UnitUid uid = managedMinionUids[i];
                if (uid.IsValid() &&
                    !unitWorld.TryGetUnit(uid, out _))
                    throw new DeterministicSimulationException(
                        $"Managed Minion {uid} is missing after restore.");
            }
        }

        public void Rebuild(
            in RollbackContext context)
        {
        }

        private void ExpandWave(int scheduledTick)
        {
            MinionWaveComposition composition =
                ResolveComposition(waveIndex);
            MinionWaveMember[] members =
                composition.Members ??
                Array.Empty<MinionWaveMember>();
            int stableEntryIndex = 0;
            for (int laneIndex = 0;
                 laneIndex < lanes.Length;
                 laneIndex++)
            {
                LaneRuntimeData lane = lanes[laneIndex];
                for (int teamIndex = 0;
                     teamIndex <
                     lane.TeamSpawns.Length;
                     teamIndex++)
                {
                    LaneTeamSpawnData spawn =
                        lane.TeamSpawns[teamIndex];
                    for (int memberIndex = 0;
                         memberIndex < members.Length;
                         memberIndex++)
                    {
                        MinionWaveMember member =
                            members[memberIndex];
                        for (int countIndex = 0;
                             countIndex < member.Count;
                             countIndex++)
                        {
                            var ticket = new MinionTicket
                            {
                                SpawnLogicTick = checked(
                                    scheduledTick +
                                    member.FirstSpawnOffsetTicks +
                                    member.SpawnStepTicks *
                                    countIndex),
                                TeamId = spawn.TeamId,
                                LaneId = lane.LaneId,
                                UnitPrototypeId =
                                    member.ResolveUnitPrototypeId(
                                        spawn.TeamId.Value),
                                StableEntryIndex =
                                    stableEntryIndex++,
                                SpawnPosition =
                                    spawn.Position,
                                SpawnForward =
                                    spawn.Forward,
                            };
                            InsertTicket(ticket);
                        }
                    }
                }
            }
        }

        private MinionWaveComposition ResolveComposition(
            int currentWaveIndex)
        {
            MinionWavePhase[] phases =
                schedule.Phases ??
                Array.Empty<MinionWavePhase>();
            if (phases.Length == 0)
                return default;

            int phaseIndex = 0;
            for (int i = 1;
                 i < phases.Length &&
                 phases[i].StartWaveIndex <=
                 currentWaveIndex;
                 i++)
            {
                phaseIndex = i;
            }
            MinionWavePhase phase =
                phases[phaseIndex];
            int offset =
                currentWaveIndex -
                phase.StartWaveIndex;
            int cycleIndex =
                offset %
                phase.CompositionCycle.Length;
            return phase.CompositionCycle[cycleIndex];
        }

        private void InsertTicket(
            in MinionTicket ticket)
        {
            ValidateTicket(ticket);
            int index = pendingTickets.Count;
            while (index > nextTicketCursor &&
                   CompareTicket(
                       pendingTickets[index - 1],
                       ticket) > 0)
            {
                index--;
            }
            if (index > 0 &&
                CompareTicket(
                    pendingTickets[index - 1],
                    ticket) == 0)
                throw new DeterministicSimulationException(
                    "Duplicate Minion ticket key.");
            pendingTickets.Insert(index, ticket);
        }

        private void SpawnSingleMinion(
            in MinionTicket ticket)
        {
            var request = new UnitSpawnRequest(
                ticket.UnitPrototypeId,
                ticket.TeamId,
                ticket.SpawnPosition,
                ticket.SpawnForward,
                default);
            UnitUid uid = unitWorld.SpawnUnit(request);
            if (!unitWorld.TryGetUnit(
                    uid,
                    out Unit minion) ||
                minion.UnitKind != UnitKind.Minion)
                throw new DeterministicSimulationException(
                    $"Minion ticket spawned invalid Unit {uid}.");

            InsertManagedUid(uid);
            var controller =
                new MinionAIController(
                    minion,
                    ticket.LaneId);
            if (!unitWorld.RegisterAIController(
                    uid,
                    controller))
                throw new DeterministicSimulationException(
                    $"Failed to register Minion AI for {uid}.");
        }

        private void InsertManagedUid(UnitUid uid)
        {
            int index = managedMinionUids.Count;
            while (index > 0)
            {
                UnitUid previous =
                    managedMinionUids[index - 1];
                if (previous.IsValid() &&
                    previous.CompareTo(uid) < 0)
                    break;
                index--;
            }
            managedMinionUids.Insert(index, uid);
        }

        private void ValidateStaticTopology()
        {
            for (int i = 0; i < lanes.Length; i++)
            {
                if (lanes[i] == null ||
                    lanes[i].LaneId == 0 ||
                    lanes[i].TeamSpawns == null ||
                    lanes[i].TeamSpawns.Length == 0 ||
                    (i > 0 &&
                     lanes[i - 1].LaneId >=
                     lanes[i].LaneId))
                    throw new ArgumentException(
                        "Lanes must be non-null and strictly ordered by nonzero LaneId.");
            }

            MinionWavePhase[] phases =
                schedule.Phases ??
                Array.Empty<MinionWavePhase>();
            for (int phaseIndex = 0;
                 phaseIndex < phases.Length;
                 phaseIndex++)
            {
                MinionWavePhase phase =
                    phases[phaseIndex];
                if (phase.StartWaveIndex < 0 ||
                    (phaseIndex > 0 &&
                     phases[phaseIndex - 1]
                         .StartWaveIndex >=
                     phase.StartWaveIndex) ||
                    phase.CompositionCycle == null ||
                    phase.CompositionCycle.Length == 0)
                    throw new ArgumentException(
                        "Minion phases are invalid.");
                for (int compositionIndex = 0;
                     compositionIndex <
                     phase.CompositionCycle.Length;
                     compositionIndex++)
                {
                    MinionWaveMember[] members =
                        phase.CompositionCycle[
                            compositionIndex]
                        .Members;
                    if (members == null ||
                        members.Length == 0)
                        throw new ArgumentException(
                            "Minion composition is empty.");
                    for (int memberIndex = 0;
                         memberIndex <
                         members.Length;
                         memberIndex++)
                    {
                        MinionWaveMember member =
                            members[memberIndex];
                        if (member.UnitPrototypeId <= 0 ||
                            member.Count <= 0 ||
                            member.FirstSpawnOffsetTicks < 0 ||
                            member.SpawnStepTicks < 0 ||
                            checked(
                                member.FirstSpawnOffsetTicks +
                                member.SpawnStepTicks *
                                (member.Count - 1)) >=
                            schedule.WaveIntervalTicks)
                            throw new ArgumentException(
                                "Minion wave member is invalid or overlaps the next wave.");
                    }
                }
            }
        }

        private static int CompareTicket(
            MinionTicket left,
            MinionTicket right)
        {
            int comparison =
                left.SpawnLogicTick.CompareTo(
                    right.SpawnLogicTick);
            if (comparison != 0) return comparison;
            comparison = left.TeamId.CompareTo(
                right.TeamId);
            if (comparison != 0) return comparison;
            comparison = left.LaneId.CompareTo(
                right.LaneId);
            if (comparison != 0) return comparison;
            return left.StableEntryIndex.CompareTo(
                right.StableEntryIndex);
        }

        private static void ValidateTicket(
            in MinionTicket ticket)
        {
            if (ticket.SpawnLogicTick < 0 ||
                ticket.TeamId == TeamId.Neutral ||
                ticket.LaneId == 0 ||
                ticket.UnitPrototypeId <= 0 ||
                ticket.StableEntryIndex < 0 ||
                !Physics.PhysicsGeometry2D.TryCreateFacing(
                    ticket.SpawnForward,
                    out _,
                    out _))
                throw new DeterministicSimulationException(
                    "Minion ticket is invalid.");
        }
    }
}
