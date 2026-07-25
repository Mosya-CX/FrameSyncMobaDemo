using System.Collections.Generic;
using FrameSyncMoba.Deterministic;

namespace FrameSyncMoba.Unit
{
    public sealed class RespawnTimer
    {
        private readonly UnitWorld _unitWorld;
        private readonly List<RespawnEntry> _entries = new List<RespawnEntry>();

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

        public void Tick(int currentTick)
        {
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
        }

        public void Restore(in RespawnTimerSnapshot state)
        {
            _entries.Clear();
            if (state.Entries == null) return;
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

        public void Resolve(in RollbackContext context)
        {
            for (int i = 0; i < _entries.Count; i++)
                if (!_unitWorld.TryGetUnit(_entries[i].UnitUid, out _))
                    throw new DeterministicSimulationException(
                        $"Respawn entry references missing Unit {_entries[i].UnitUid}.");
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
    }
}
