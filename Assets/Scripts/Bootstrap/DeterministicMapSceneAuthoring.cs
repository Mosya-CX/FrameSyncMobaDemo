using FrameSyncMoba.Unit;
using UnityEngine;

namespace FrameSyncMoba.Bootstrap
{
    /// <summary>
    /// Scene or prefab-side reference for immutable deterministic map
    /// configuration. Drawing belongs to FlowFieldVisualizer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DeterministicMapSceneAuthoring :
        MonoBehaviour
    {
        [SerializeField] private DeterministicMapConfig mapConfig;

        public DeterministicMapConfig MapConfig =>
            mapConfig;

        public BakedDeterministicMapData BakeOrThrow()
        {
            if (mapConfig == null)
                throw new System.InvalidOperationException(
                    "DeterministicMapSceneAuthoring requires a map config.");
            return mapConfig.BakeOrThrow();
        }
    }
}
