using System.Collections.Generic;
using System;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public sealed class BuffBlackboard
    {
        private readonly Dictionary<string, StatModifierHandle> _statHandles =
            new Dictionary<string, StatModifierHandle>();
        private readonly Dictionary<string, CombatModifierHandle> _combatHandles =
            new Dictionary<string, CombatModifierHandle>();
        private readonly Dictionary<string, fp> _numbers =
            new Dictionary<string, fp>();

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

        public void SetNumber(string key, fp value)
        {
            _numbers[key] = value;
        }

        public bool TryGetNumber(string key, out fp value)
        {
            return _numbers.TryGetValue(key, out value);
        }

        public fp GetNumberOrDefault(string key)
        {
            return _numbers.TryGetValue(key, out var v) ? v : fp.zero;
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

            _numbers.Clear();
        }

        public void Clear()
        {
            _statHandles.Clear();
            _combatHandles.Clear();
            _numbers.Clear();
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
            var numbers = new List<BuffNumberSnapshot>(_numbers.Count);
            foreach (var pair in _numbers)
                numbers.Add(new BuffNumberSnapshot { Key = pair.Key, Value = pair.Value });
            numbers.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return new BuffBlackboardSnapshot
            {
                StatHandles = new System.Collections.Generic.List<BuffStatHandleSnapshot>(stats),
                CombatHandles = new System.Collections.Generic.List<BuffCombatHandleSnapshot>(combats),
                Numbers = new System.Collections.Generic.List<BuffNumberSnapshot>(numbers),
            };
        }

        public void Restore(in BuffBlackboardSnapshot snapshot)
        {
            Clear();
            var stats = snapshot.StatHandles ?? new System.Collections.Generic.List<BuffStatHandleSnapshot>();
            for (int i = 0; i < stats.Count; i++)
            {
                if (string.IsNullOrEmpty(stats[i].Key) ||
                    (i > 0 && string.CompareOrdinal(stats[i - 1].Key, stats[i].Key) >= 0))
                    throw new Deterministic.DeterministicSimulationException(
                        "Buff stat-handle snapshot is not in canonical key order.");
                _statHandles.Add(stats[i].Key, stats[i].Handle);
            }
            var combats = snapshot.CombatHandles ?? new System.Collections.Generic.List<BuffCombatHandleSnapshot>();
            for (int i = 0; i < combats.Count; i++)
            {
                if (string.IsNullOrEmpty(combats[i].Key) ||
                    (i > 0 && string.CompareOrdinal(combats[i - 1].Key, combats[i].Key) >= 0))
                    throw new Deterministic.DeterministicSimulationException(
                        "Buff combat-handle snapshot is not in canonical key order.");
                _combatHandles.Add(combats[i].Key, combats[i].Handle);
            }
            var numbers = snapshot.Numbers ?? new System.Collections.Generic.List<BuffNumberSnapshot>();
            for (int i = 0; i < numbers.Count; i++)
            {
                if (string.IsNullOrEmpty(numbers[i].Key) ||
                    (i > 0 && string.CompareOrdinal(numbers[i - 1].Key, numbers[i].Key) >= 0))
                    throw new Deterministic.DeterministicSimulationException(
                        "Buff number snapshot is not in canonical key order.");
                _numbers.Add(numbers[i].Key, numbers[i].Value);
            }
        }
    }

    public struct BuffStatHandleSnapshot { public string Key; public StatModifierHandle Handle; }
    public struct BuffCombatHandleSnapshot { public string Key; public CombatModifierHandle Handle; }
    public struct BuffNumberSnapshot { public string Key; public fp Value; }
    public struct BuffBlackboardSnapshot
    {
        public System.Collections.Generic.List<BuffStatHandleSnapshot> StatHandles;
        public System.Collections.Generic.List<BuffCombatHandleSnapshot> CombatHandles;
        public System.Collections.Generic.List<BuffNumberSnapshot> Numbers;
    }
}
