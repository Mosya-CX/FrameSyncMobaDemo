using System;
using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public sealed class MinionSystem
    {
        private readonly UnitWorld _unitWorld;
        private int _waveIndex;
        private int _nextWaveLogicTick;
        private readonly List<MinionTicket> _pendingTickets = new List<MinionTicket>();
        private readonly List<UnitUid> _managedMinionUids = new List<UnitUid>();
        private int _nextTicketCursor;

        private readonly int minionWaveIntervalTicks;
        private const int MinionsPerWave = 6;

        public int WaveIndex => _waveIndex;
        public int NextWaveLogicTick => _nextWaveLogicTick;
        public IReadOnlyList<UnitUid> ManagedMinionUids => _managedMinionUids;

        public MinionSystem(
            UnitWorld unitWorld,
            int startLogicTick,
            int waveIntervalTicks)
        {
            _unitWorld = unitWorld ?? throw new ArgumentNullException(nameof(unitWorld));
            if (waveIntervalTicks <= 0)
                throw new ArgumentOutOfRangeException(nameof(waveIntervalTicks));
            minionWaveIntervalTicks = waveIntervalTicks;
            _nextWaveLogicTick = GetNextWaveLogicTick(startLogicTick);
        }

        public void ProcessWave(int currentTick)
        {
            if (currentTick < _nextWaveLogicTick) return;

            _waveIndex++;
            _nextWaveLogicTick = currentTick + minionWaveIntervalTicks;
        }

        /// <summary>
        /// Spawn a full wave of minions for a given lane.
        /// Called after ProcessWave to materialize the scheduled wave.
        /// </summary>
        public void SpawnWave(int laneId, TeamId team, int minionPrototypeId, fp2 spawnPosition, fp2 laneDirection)
        {
            for (int i = 0; i < MinionsPerWave; i++)
            {
                fp2 offset = laneDirection * ((fp)i * (fp)0.8m);
                fp2 pos = spawnPosition + offset;

                var request = new UnitSpawnRequest(
                    minionPrototypeId, team, pos, laneDirection, default);

                UnitUid uid = _unitWorld.SpawnUnit(request);
                if (uid.IsValid() && _unitWorld.TryGetUnit(uid, out Unit minion))
                {
                    var controller = new MinionAIController(minion, laneId);
                    _unitWorld.RegisterAIController(controller);
                    RegisterMinion(uid, laneId);
                }
            }
        }

        public void RegisterMinion(UnitUid uid, int laneId)
        {
            if (!_managedMinionUids.Contains(uid))
            {
                int managedIndex = _managedMinionUids.Count;
                while (managedIndex > 0 &&
                    _managedMinionUids[managedIndex - 1].CompareTo(uid) > 0)
                    managedIndex--;
                _managedMinionUids.Insert(managedIndex, uid);
            }

            var ticket = new MinionTicket
            {
                UnitUid = uid,
                SpawnLogicTick = uid.SpawnLogicTick,
                LaneId = laneId,
                IsSpawned = true,
            };
            int ticketIndex = _pendingTickets.Count;
            while (ticketIndex > 0 &&
                _pendingTickets[ticketIndex - 1].UnitUid.CompareTo(uid) > 0)
                ticketIndex--;
            _pendingTickets.Insert(ticketIndex, ticket);
        }

        public void UnregisterMinion(UnitUid uid)
        {
            _managedMinionUids.Remove(uid);
            for (int i = 0; i < _pendingTickets.Count; i++)
            {
                var ticket = _pendingTickets[i];
                if (ticket.UnitUid == uid)
                {
                    ticket.IsSpawned = false;
                    _pendingTickets[i] = ticket;
                    break;
                }
            }
        }

        public MinionTicket GetNextPendingTicket()
        {
            if (_nextTicketCursor >= _pendingTickets.Count)
                return MinionTicket.Empty;
            return _pendingTickets[_nextTicketCursor];
        }

        public bool HasPendingTickets => _nextTicketCursor < _pendingTickets.Count;

        public void AdvanceTicketCursor()
        {
            if (_nextTicketCursor < _pendingTickets.Count)
                _nextTicketCursor++;
        }

        private int GetNextWaveLogicTick(int currentTick)
        {
            if (currentTick <= 0) return minionWaveIntervalTicks;
            int remainder = currentTick % minionWaveIntervalTicks;
            return remainder == 0
                ? currentTick + minionWaveIntervalTicks
                : currentTick + (minionWaveIntervalTicks - remainder);
        }

        public void Capture(ref MinionSystemSnapshot state)
        {
            state.WaveIndex = _waveIndex;
            state.NextWaveLogicTick = _nextWaveLogicTick;
            state.PendingTickets = new List<MinionTicket>(_pendingTickets);
            state.NextTicketCursor = _nextTicketCursor;
            state.ManagedMinionUids = new List<UnitUid>(_managedMinionUids);
        }

        public void Restore(in MinionSystemSnapshot state)
        {
            _waveIndex = state.WaveIndex;
            _nextWaveLogicTick = state.NextWaveLogicTick;
            _pendingTickets.Clear();
            UnitUid previousTicket = default;
            if (state.PendingTickets != null)
            {
                for (int i = 0; i < state.PendingTickets.Count; i++)
                {
                    MinionTicket ticket = state.PendingTickets[i];
                    if (!ticket.UnitUid.IsValid() ||
                        (i > 0 && previousTicket.CompareTo(ticket.UnitUid) >= 0))
                        throw new DeterministicSimulationException(
                            "Minion ticket snapshot is not in canonical UnitUid order.");
                    previousTicket = ticket.UnitUid;
                    _pendingTickets.Add(ticket);
                }
            }
            if (state.NextTicketCursor < 0 || state.NextTicketCursor > _pendingTickets.Count)
                throw new DeterministicSimulationException("Invalid Minion ticket cursor.");
            _nextTicketCursor = state.NextTicketCursor;
            _managedMinionUids.Clear();
            UnitUid previousManaged = default;
            if (state.ManagedMinionUids != null)
            {
                for (int i = 0; i < state.ManagedMinionUids.Count; i++)
                {
                    UnitUid uid = state.ManagedMinionUids[i];
                    if (!uid.IsValid() || (i > 0 && previousManaged.CompareTo(uid) >= 0))
                        throw new DeterministicSimulationException(
                            "Managed Minion snapshot is not in canonical UnitUid order.");
                    previousManaged = uid;
                    _managedMinionUids.Add(uid);
                }
            }
        }

        public void Resolve(in RollbackContext context)
        {
            for (int i = 0; i < _managedMinionUids.Count; i++)
                if (!_unitWorld.TryGetUnit(_managedMinionUids[i], out _))
                    throw new DeterministicSimulationException(
                        $"Managed Minion {_managedMinionUids[i]} is missing after restore.");
        }

        public void Rebuild(in RollbackContext context) { }
    }
}
