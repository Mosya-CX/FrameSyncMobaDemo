using System.Collections.Generic;

namespace FrameSyncMoba.Unit
{
    public sealed class ProjectileDefRegistry
    {
        private readonly Dictionary<int, ProjectileDef> _byId = new Dictionary<int, ProjectileDef>();

        public int Count => _byId.Count;

        public void Register(ProjectileDef def)
        {
            if (def == null || !def.IsValid) return;
            _byId[def.DefId] = def;
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
            return list;
        }
    }
}
