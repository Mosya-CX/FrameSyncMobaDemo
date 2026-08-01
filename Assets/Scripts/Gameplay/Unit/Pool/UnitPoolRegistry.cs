using System.Collections.Generic;
using FrameSyncMoba.Deterministic;

namespace FrameSyncMoba.Unit
{
    public sealed class UnitPoolRegistry
    {
        private readonly Dictionary<int, Queue<Unit>> _pools =
            new Dictionary<int, Queue<Unit>>();
        private readonly Dictionary<int, UnitPoolConfig> _configs =
            new Dictionary<int, UnitPoolConfig>();

        public void RegisterConfig(
            int runtimeEntityPrefabId,
            in UnitPoolConfig config)
        {
            if (runtimeEntityPrefabId <= 0)
                throw new DeterministicSimulationException(
                    "Unit pool requires a positive RuntimeEntityPrefabId.");
            if (config.PrewarmCount < 0 || config.MaxCapacity <= 0)
                throw new DeterministicSimulationException(
                    $"Unit pool config for prefab {runtimeEntityPrefabId} is invalid.");

            _configs[runtimeEntityPrefabId] = config;
            if (!_pools.ContainsKey(runtimeEntityPrefabId))
                _pools[runtimeEntityPrefabId] = new Queue<Unit>();
        }

        public bool TryRent(int runtimeEntityPrefabId, out Unit unit)
        {
            if (_pools.TryGetValue(runtimeEntityPrefabId, out var pool) &&
                pool.Count > 0)
            {
                unit = pool.Dequeue();
                unit.gameObject.SetActive(true);
                return true;
            }
            unit = null;
            return false;
        }

        public void Return(int runtimeEntityPrefabId, Unit unit)
        {
            if (unit == null) return;
            if (!_configs.TryGetValue(runtimeEntityPrefabId, out var config))
                throw new DeterministicSimulationException(
                    $"Unit pool prefab {runtimeEntityPrefabId} was not configured.");
            if (!_pools.TryGetValue(runtimeEntityPrefabId, out var pool))
                throw new DeterministicSimulationException(
                    $"Unit pool prefab {runtimeEntityPrefabId} has no pool.");

            if (pool.Count >= config.MaxCapacity &&
                config.ResizePolicy == UnitPoolResizePolicy.Fixed)
            {
                UnityEngine.Object.Destroy(unit.gameObject);
                return;
            }
            unit.ResetForPool();
            unit.gameObject.SetActive(false);
            pool.Enqueue(unit);
        }

        public int GetAvailableCount(int runtimeEntityPrefabId)
        {
            return _pools.TryGetValue(runtimeEntityPrefabId, out var pool)
                ? pool.Count
                : 0;
        }

        public void Clear()
        {
            foreach (var pool in _pools.Values)
                while (pool.Count > 0)
                    UnityEngine.Object.Destroy(pool.Dequeue().gameObject);
            _pools.Clear();
            _configs.Clear();
        }
    }
}
