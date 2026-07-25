using UnityEngine;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// ScriptableObject asset holding a single baked flow field.
    /// Loaded at bootstrap and populated into FlowFieldRegistry.
    /// NOT in Gameplay snapshot (static configuration).
    /// (Pathfinding Design v13.1 section 8.9)
    /// </summary>
    [CreateAssetMenu(fileName = "FlowFieldBake", menuName = "FrameSyncMoba/FlowField Bake Asset")]
    public class FlowFieldBakeAsset : ScriptableObject
    {
        public FlowFieldKey Key;
        public TeamFlowFieldData Field;

        public bool IsValid => Field.IsValid;
    }
}
