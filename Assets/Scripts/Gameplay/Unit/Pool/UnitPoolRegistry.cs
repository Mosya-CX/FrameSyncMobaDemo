using System.Collections.Generic;
using FrameSyncMoba.Deterministic;

namespace FrameSyncMoba.Unit
{
    public sealed class UnitPoolRegistry
    {
        private readonly Dictionary<ushort, Queue<Unit>> _pools = new Dictionary<ushort, Queue<Unit>>();
        private readonly Dictionary<ushort, UnitPoolConfig> _configs = new Dictionary<ushort, UnitPoolConfig>();
        public void RegisterConfig(ushort subKindId, in UnitPoolConfig config)
        {
            _configs[subKindId] = config;
            if (!_pools.ContainsKey(subKindId)) _pools[subKindId] = new Queue<Unit>();
        }
        public bool TryRent(ushort subKindId, out Unit unit)
        {
            if (_pools.TryGetValue(subKindId, out var pool) && pool.Count > 0)
            {
                unit = pool.Dequeue();
                unit.gameObject.SetActive(true);
                return true;
            }
            unit = null;
            return false;
        }
        public void Return(ushort subKindId, Unit unit)
        {
            if (unit == null) return;
            if (!_pools.TryGetValue(subKindId, out var pool))
            {
                _pools[subKindId] = pool = new Queue<Unit>();
            }
            if (!_configs.TryGetValue(subKindId, out var cfg)) cfg = UnitPoolConfig.Default;
            if (pool.Count >= cfg.MaxCapacity && cfg.ResizePolicy == UnitPoolResizePolicy.Fixed)
            {
                UnityEngine.Object.Destroy(unit.gameObject);
                return;
            }
            unit.ResetForPool();
            unit.gameObject.SetActive(false);
            pool.Enqueue(unit);
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
