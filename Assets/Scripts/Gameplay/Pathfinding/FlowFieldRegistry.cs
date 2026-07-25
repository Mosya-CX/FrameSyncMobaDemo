using System.Collections.Generic;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Runtime registry for baked flow-field data.
    /// Static configuration — NOT in Gameplay snapshot.
    /// (Pathfinding Design v13.1 section 8.9)
    /// </summary>
    public sealed class FlowFieldRegistry
    {
        private readonly Dictionary<int, TeamFlowFieldData> _fields
            = new Dictionary<int, TeamFlowFieldData>();

        /// <summary>
        /// Register a baked flow field.
        /// </summary>
        public void Register(TeamFlowFieldData field)
        {
            if (!field.IsValid) return;
            _fields[field.Key.Packed] = field;
        }

        /// <summary>
        /// Try to get a flow field by key.
        /// </summary>
        public bool TryGet(FlowFieldKey key, out TeamFlowFieldData field)
        {
            return _fields.TryGetValue(key.Packed, out field);
        }

        /// <summary>
        /// Get a flow field by key. Returns Empty if not found.
        /// </summary>
        public TeamFlowFieldData Get(FlowFieldKey key)
        {
            return _fields.TryGetValue(key.Packed, out var field) ? field : TeamFlowFieldData.Empty;
        }

        /// <summary>Number of registered fields.</summary>
        public int Count => _fields.Count;

        /// <summary>Clear all registered fields.</summary>
        public void Clear() => _fields.Clear();
    }
}
