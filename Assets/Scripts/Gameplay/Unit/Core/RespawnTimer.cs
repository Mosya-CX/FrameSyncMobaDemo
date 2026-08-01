using System.Collections.Generic;
using FrameSyncMoba.Deterministic;

namespace FrameSyncMoba.Unit
{
    public sealed class RespawnTimer
    {
        private readonly UnitWorld _unitWorld;
        private readonly List<RespawnEntry> _entries = new List<RespawnEntry>();
        private readonly List<DeathDisposalEntry> _disposalEntries =
            new List<DeathDisposalEntry>();

        public RespawnTimer(UnitWorld unitWorld)
        {
            _unitWorld = unitWorld;
        }

        public void RegisterDeath(UnitUid unitUid, int deathTick, int respawnDelayTicks)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].UnitUid == unitUid)
                {
                    _entries.RemoveAt(i);
                    break;
                }
            }

            var newEntry = new RespawnEntry
            {
                UnitUid = unitUid,
                DeathLogicTick = deathTick,
                RespawnLogicTick = deathTick + respawnDelayTicks,
            };
            int insertIndex = _entries.Count;
            while (insertIndex > 0 &&
                _entries[insertIndex - 1].UnitUid.CompareTo(unitUid) > 0)
                insertIndex--;
            _entries.Insert(insertIndex, newEntry);
        }

        public void RegisterDisposal(
            UnitUid unitUid,
            int deathTick,
            int deathPresentationTicks)
        {
            if (!unitUid.IsValid())
                throw new DeterministicSimulationException(
                    "Death disposal requires a valid UnitUid.");
            if (deathPresentationTicks < 0)
                throw new DeterministicSimulationException(
                    "DeathPresentationTicks must not be negative.");

            for (int i = 0; i < _disposalEntries.Count; i++)
            {
                if (_disposalEntries[i].UnitUid == unitUid)
                {
                    _disposalEntries.RemoveAt(i);
                    break;
                }
            }

            var entry = new DeathDisposalEntry
            {
                UnitUid = unitUid,
                DeathLogicTick = deathTick,
                DisposeLogicTick = checked(
                    deathTick + deathPresentationTicks),
            };
            int insertIndex = 0;
            while (insertIndex < _disposalEntries.Count &&
                _disposalEntries[insertIndex].UnitUid
                    .CompareTo(unitUid) < 0)
            {
                insertIndex++;
            }
            _disposalEntries.Insert(insertIndex, entry);
        }

        public void Tick(int currentTick)
        {
            int disposalIndex = 0;
            while (disposalIndex < _disposalEntries.Count)
            {
                DeathDisposalEntry entry =
                    _disposalEntries[disposalIndex];
                if (currentTick < entry.DisposeLogicTick)
                {
                    disposalIndex++;
                    continue;
                }

                if (!_unitWorld.TryGetUnit(
                        entry.UnitUid,
                        out Unit unit))
                    throw new DeterministicSimulationException(
                        $"Death disposal references missing Unit {entry.UnitUid}.");
                if (unit.LifeState != LifeState.Dead)
                    throw new DeterministicSimulationException(
                        $"Death disposal Unit {entry.UnitUid} is not Dead.");

                _unitWorld.ResolveDeathDispose(unit);
                _disposalEntries.RemoveAt(disposalIndex);
            }

            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                var entry = _entries[i];
                if (currentTick >= entry.RespawnLogicTick)
                {
                    if (_unitWorld.TryGetUnit(entry.UnitUid, out Unit unit)
                        && unit.LifeState == LifeState.Dead)
                    {
                        _unitWorld.BeginRespawn(unit);
                        _unitWorld.CompleteRespawn(unit);
                        unit.ClearForRespawn();
                    }
                    _entries.RemoveAt(i);
                }
            }
        }

        public int GetRemainingTicks(UnitUid unitUid, int currentTick)
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i].UnitUid == unitUid)
                {
                    int remaining = _entries[i].RespawnLogicTick - currentTick;
                    return remaining > 0 ? remaining : 0;
                }
            }
            return 0;
        }

        public void Capture(ref RespawnTimerSnapshot state)
        {
            state.Entries = new System.Collections.Generic.List<RespawnEntry>(_entries);
            state.DisposalEntries =
                new List<DeathDisposalEntry>(_disposalEntries);
        }

        public void Restore(in RespawnTimerSnapshot state)
        {
            _entries.Clear();
            _disposalEntries.Clear();
            if (state.Entries != null)
            {
                UnitUid previous = default;
                for (int i = 0; i < state.Entries.Count; i++)
                {
                    RespawnEntry entry = state.Entries[i];
                    if (!entry.UnitUid.IsValid() ||
                        (i > 0 && previous.CompareTo(entry.UnitUid) >= 0) ||
                        entry.RespawnLogicTick < entry.DeathLogicTick)
                        throw new DeterministicSimulationException(
                            "RespawnTimer snapshot is invalid or non-canonical.");
                    previous = entry.UnitUid;
                    _entries.Add(entry);
                }
            }

            if (state.DisposalEntries == null)
            {
                return;
            }
            UnitUid previousDisposal = default;
            for (int i = 0; i < state.DisposalEntries.Count; i++)
            {
                DeathDisposalEntry entry =
                    state.DisposalEntries[i];
                if (!entry.UnitUid.IsValid() ||
                    (i > 0 && previousDisposal
                        .CompareTo(entry.UnitUid) >= 0) ||
                    entry.DisposeLogicTick < entry.DeathLogicTick)
                    throw new DeterministicSimulationException(
                        "Death disposal snapshot is invalid or non-canonical.");
                previousDisposal = entry.UnitUid;
                _disposalEntries.Add(entry);
            }
        }

        public void Resolve(in RollbackContext context)
        {
            for (int i = 0; i < _entries.Count; i++)
                if (!_unitWorld.TryGetUnit(_entries[i].UnitUid, out _))
                    throw new DeterministicSimulationException(
                        $"Respawn entry references missing Unit {_entries[i].UnitUid}.");
            for (int i = 0; i < _disposalEntries.Count; i++)
                if (!_unitWorld.TryGetUnit(
                        _disposalEntries[i].UnitUid,
                        out Unit unit) ||
                    unit.LifeState != LifeState.Dead)
                    throw new DeterministicSimulationException(
                        $"Death disposal entry references invalid Unit {_disposalEntries[i].UnitUid}.");
        }
        public void Rebuild(in RollbackContext context) { }
    }

    public struct RespawnEntry
    {
        public UnitUid UnitUid;
        public int DeathLogicTick;
        public int RespawnLogicTick;
    }

    public struct RespawnTimerSnapshot
    {
        public System.Collections.Generic.List<RespawnEntry> Entries;
        public System.Collections.Generic.List<DeathDisposalEntry>
            DisposalEntries;
    }

    public struct DeathDisposalEntry
    {
        public UnitUid UnitUid;
        public int DeathLogicTick;
        public int DisposeLogicTick;
    }
}
