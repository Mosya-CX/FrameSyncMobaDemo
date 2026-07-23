using System;
using System.Collections.Generic;

namespace FrameSyncMoba.Unit
{
    internal sealed class BuffStore
    {
        private readonly Dictionary<BuffConfigId, BuffRuntime> _lookup =
            new Dictionary<BuffConfigId, BuffRuntime>();
        private readonly List<BuffRuntime> _ordered =
            new List<BuffRuntime>();

        public int Count => _ordered.Count;

        public bool TryGet(BuffConfigId configId, out BuffRuntime runtime)
        {
            return _lookup.TryGetValue(configId, out runtime);
        }

        public void Add(BuffRuntime runtime)
        {
            if (runtime == null) throw new ArgumentNullException(nameof(runtime));

            _lookup[runtime.ConfigId] = runtime;
            _ordered.Add(runtime);
            _ordered.Sort(CompareByConfigId);
        }

        public bool Remove(BuffConfigId configId)
        {
            if (!_lookup.TryGetValue(configId, out var runtime)) return false;

            _lookup.Remove(configId);
            _ordered.Remove(runtime);
            return true;
        }

        public void Clear()
        {
            _lookup.Clear();
            _ordered.Clear();
        }

        public IReadOnlyList<BuffRuntime> GetAllOrdered()
        {
            return _ordered;
        }

        private static int CompareByConfigId(BuffRuntime a, BuffRuntime b)
        {
            return a.ConfigId.CompareTo(b.ConfigId);
        }
    }
}
