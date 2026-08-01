using System;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    [DisallowMultipleComponent]
    public sealed class FlowFieldSceneAuthoring :
        MonoBehaviour
    {
        [SerializeField] private DeterministicMapConfig
            mapConfig;
        [SerializeField] private LaneAuthoring[] lanes =
            Array.Empty<LaneAuthoring>();
        [SerializeField] private FlowFieldBakeAsset[]
            bakedFields =
                Array.Empty<FlowFieldBakeAsset>();
        [Header("Offline lane field bake")]
        [SerializeField, Min(0)] private int
            guideCostPerCell = 1;
        [SerializeField, Min(0)] private int
            offGuideCostPerCell = 2;

        public DeterministicMapConfig MapConfig =>
            mapConfig;
        public LaneAuthoring[] Lanes =>
            lanes ?? Array.Empty<LaneAuthoring>();
        public FlowFieldBakeAsset[] BakedFields =>
            bakedFields ??
            Array.Empty<FlowFieldBakeAsset>();
        public int GuideCostPerCell =>
            Math.Max(0, guideCostPerCell);
        public int OffGuideCostPerCell =>
            Math.Max(0, offGuideCostPerCell);

        public bool TryGetField(
            byte teamId,
            RadiusClass radiusClass,
            out FlowFieldBakeAsset result)
        {
            int packed = new FlowFieldKey(
                    teamId,
                    radiusClass)
                .Packed;
            FlowFieldBakeAsset[] fields =
                BakedFields;
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i] != null &&
                    fields[i].Key.Packed == packed)
                {
                    result = fields[i];
                    return true;
                }
            }
            result = null;
            return false;
        }
    }
}
