using System;
using System.Collections.Generic;

namespace FrameSyncMoba.Unit
{
    public sealed class ProjectileDefRegistry
    {
        private readonly Dictionary<int, ProjectileDef> _byId = new Dictionary<int, ProjectileDef>();

        public int Count => _byId.Count;

        public void Register(ProjectileDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            def.ValidateOrThrow();
            if (_byId.ContainsKey(def.DefId))
                throw new InvalidOperationException(
                    $"Duplicate ProjectileDef id {def.DefId}.");
            _byId.Add(def.DefId, def);
        }

        public void RegisterAll(IEnumerable<ProjectileDef> defs)
        {
            foreach (var def in defs)
                Register(def);
        }

        public ProjectileDef FindById(int defId)
        {
            _byId.TryGetValue(defId, out var def);
            return def;
        }

        public void Clear()
        {
            _byId.Clear();
        }

        public IReadOnlyList<ProjectileDef> GetAllDefs()
        {
            var list = new List<ProjectileDef>(_byId.Values);
            list.Sort((a, b) => a.DefId.CompareTo(b.DefId));
            return list;
        }
    }
}
