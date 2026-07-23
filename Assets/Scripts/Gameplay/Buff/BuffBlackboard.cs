using System.Collections.Generic;
using System;

namespace FrameSyncMoba.Unit
{
    public sealed class BuffBlackboard
    {
        private readonly Dictionary<string, StatModifierHandle> _statHandles =
            new Dictionary<string, StatModifierHandle>();
        private readonly Dictionary<string, CombatModifierHandle> _combatHandles =
            new Dictionary<string, CombatModifierHandle>();

        public void SetStatHandle(string key, StatModifierHandle handle)
        {
            _statHandles[key] = handle;
        }

        public bool TryGetStatHandle(string key, out StatModifierHandle handle)
        {
            return _statHandles.TryGetValue(key, out handle);
        }

        public StatModifierHandle GetStatHandleOrDefault(string key)
        {
            return _statHandles.TryGetValue(key, out var h) ? h : default;
        }

        public void SetCombatHandle(string key, CombatModifierHandle handle)
        {
            _combatHandles[key] = handle;
        }

        public bool TryGetCombatHandle(string key, out CombatModifierHandle handle)
        {
            return _combatHandles.TryGetValue(key, out handle);
        }

        public CombatModifierHandle GetCombatHandleOrDefault(string key)
        {
            return _combatHandles.TryGetValue(key, out var h) ? h : default;
        }

        public void InvalidateAll()
        {
            var statKeys = new List<string>(_statHandles.Keys);
            foreach (var key in statKeys)
            {
                _statHandles[key] = default;
            }

            var combatKeys = new List<string>(_combatHandles.Keys);
            foreach (var key in combatKeys)
            {
                _combatHandles[key] = default;
            }
        }

        public void Clear()
        {
            _statHandles.Clear();
            _combatHandles.Clear();
        }

        public BuffBlackboardSnapshot Capture()
        {
            var stats = new List<BuffStatHandleSnapshot>(_statHandles.Count);
            foreach (var pair in _statHandles)
                stats.Add(new BuffStatHandleSnapshot { Key = pair.Key, Handle = pair.Value });
            stats.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            var combats = new List<BuffCombatHandleSnapshot>(_combatHandles.Count);
            foreach (var pair in _combatHandles)
                combats.Add(new BuffCombatHandleSnapshot { Key = pair.Key, Handle = pair.Value });
            combats.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return new BuffBlackboardSnapshot
            {
                StatHandles = stats.ToArray(),
                CombatHandles = combats.ToArray(),
            };
        }

        public void Restore(in BuffBlackboardSnapshot snapshot)
        {
            Clear();
            BuffStatHandleSnapshot[] stats = snapshot.StatHandles ?? Array.Empty<BuffStatHandleSnapshot>();
            for (int i = 0; i < stats.Length; i++)
            {
                if (string.IsNullOrEmpty(stats[i].Key) ||
                    (i > 0 && string.CompareOrdinal(stats[i - 1].Key, stats[i].Key) >= 0))
                    throw new Deterministic.DeterministicSimulationException(
                        "Buff stat-handle snapshot is not in canonical key order.");
                _statHandles.Add(stats[i].Key, stats[i].Handle);
            }
            BuffCombatHandleSnapshot[] combats = snapshot.CombatHandles ?? Array.Empty<BuffCombatHandleSnapshot>();
            for (int i = 0; i < combats.Length; i++)
            {
                if (string.IsNullOrEmpty(combats[i].Key) ||
                    (i > 0 && string.CompareOrdinal(combats[i - 1].Key, combats[i].Key) >= 0))
                    throw new Deterministic.DeterministicSimulationException(
                        "Buff combat-handle snapshot is not in canonical key order.");
                _combatHandles.Add(combats[i].Key, combats[i].Handle);
            }
        }
    }

    public struct BuffStatHandleSnapshot { public string Key; public StatModifierHandle Handle; }
    public struct BuffCombatHandleSnapshot { public string Key; public CombatModifierHandle Handle; }
    public struct BuffBlackboardSnapshot
    {
        public BuffStatHandleSnapshot[] StatHandles;
        public BuffCombatHandleSnapshot[] CombatHandles;
    }
}
