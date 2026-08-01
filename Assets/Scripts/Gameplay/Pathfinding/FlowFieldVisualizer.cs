using System;
using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Unit
{
    public enum FlowFieldVisualizationMode : byte
    {
        Directions = 0,
        OwnerLane = 1,
        Reachability = 2,
    }

    public enum FlowFieldLaneView : byte
    {
        All = 0,
        Lane1 = 1,
        Lane2 = 2,
        Lane3 = 3,
    }

    public enum FlowFieldTeamView : byte
    {
        Blue = 1,
        Red = 2,
    }

    /// <summary>
    /// Editor-only visual projection of immutable baked map and flow data.
    /// It never builds fields and never writes Gameplay state.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(FlowFieldSceneAuthoring))]
    public sealed class FlowFieldVisualizer :
        MonoBehaviour
    {
        [SerializeField] private FlowFieldSceneAuthoring source;
        [SerializeField] private bool drawWhenNotSelected = true;
        [SerializeField] private bool drawGrid = true;
        [SerializeField] private bool drawObstacles = true;
        [SerializeField] private bool drawBlockedCells = true;
        [SerializeField] private bool drawLaneCenterlines = true;
        [SerializeField] private bool drawFlowTargets = true;
        [SerializeField] private bool drawFlowField = true;
        [SerializeField, Range(1, 16)] private int drawStride = 4;
        [SerializeField] private FlowFieldTeamView previewTeam =
            FlowFieldTeamView.Blue;
        [SerializeField] private RadiusClass previewRadiusClass =
            RadiusClass.Small;
        [SerializeField] private FlowFieldVisualizationMode mode =
            FlowFieldVisualizationMode.Directions;
        [SerializeField] private FlowFieldLaneView laneView =
            FlowFieldLaneView.All;
        [SerializeField, Min(0.01f)] private float arrowLength = 0.35f;
        [SerializeField, Min(0f)] private float drawHeight = 0.15f;

        public FlowFieldSceneAuthoring Source => source;
        public byte PreviewTeamId =>
            (byte)previewTeam;
        public RadiusClass PreviewRadiusClass => previewRadiusClass;
        public FlowFieldVisualizationMode Mode => mode;
        public FlowFieldLaneView LaneView => laneView;

        private void Reset()
        {
            source =
                GetComponent<FlowFieldSceneAuthoring>();
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (drawWhenNotSelected)
                DrawVisualization();
        }

        private void OnDrawGizmosSelected()
        {
            if (!drawWhenNotSelected)
                DrawVisualization();
        }

        private void DrawVisualization()
        {
            if (source == null)
                source =
                    GetComponent<FlowFieldSceneAuthoring>();
            if (source == null ||
                source.MapConfig == null)
                return;

            BakedDeterministicMapData map;
            try
            {
                map =
                    source.MapConfig.BakeOrThrow();
            }
            catch
            {
                return;
            }

            if (drawGrid)
                DrawGrid(map);
            if (drawObstacles)
                DrawObstacles(map);
            if (drawLaneCenterlines)
                DrawLanes();
            if (!drawFlowField ||
                !source.TryGetField(
                    (byte)previewTeam,
                    previewRadiusClass,
                    out FlowFieldBakeAsset asset) ||
                asset == null ||
                !asset.IsValid)
                return;

            DrawField(
                map,
                asset.Field);
            if (drawFlowTargets)
                DrawTargets();
        }

        private void DrawTargets()
        {
            LaneAuthoring[] lanes =
                source.Lanes;
            Gizmos.color =
                new Color(
                    1f,
                    0.95f,
                    0.15f,
                    0.95f);
            for (int i = 0;
                 i < lanes.Length;
                 i++)
            {
                if (lanes[i] == null ||
                    !ShouldDrawLane(i))
                    continue;
                LaneRuntimeData lane;
                try
                {
                    lane =
                        lanes[i].BakeOrThrow();
                }
                catch
                {
                    continue;
                }
                if (!lane.TryGetAdvanceTarget(
                        new TeamId(
                            (byte)previewTeam),
                        out fp2 target))
                    continue;
                Vector3 world = ToWorld(target);
                Gizmos.DrawWireSphere(
                    world,
                    0.45f);
                Gizmos.DrawLine(
                    world + Vector3.left * 0.5f,
                    world + Vector3.right * 0.5f);
                Gizmos.DrawLine(
                    world + Vector3.back * 0.5f,
                    world + Vector3.forward * 0.5f);
            }
        }

        private void DrawGrid(
            BakedDeterministicMapData map)
        {
            float cellSize =
                (float)map.CellSize;
            float minX =
                (float)map.WorldMinimum.x;
            float minY =
                (float)map.WorldMinimum.y;
            float maxX =
                (float)map.WorldMaximum.x;
            float maxY =
                (float)map.WorldMaximum.y;
            int width =
                Mathf.RoundToInt(
                    (maxX - minX) /
                    cellSize);
            int height =
                Mathf.RoundToInt(
                    (maxY - minY) /
                    cellSize);
            int stride =
                Math.Max(1, drawStride);
            Gizmos.color =
                new Color(
                    0.45f,
                    0.45f,
                    0.45f,
                    0.16f);
            for (int x = 0;
                 x <= width;
                 x += stride)
            {
                float worldX =
                    minX +
                    x * cellSize;
                Gizmos.DrawLine(
                    new Vector3(
                        worldX,
                        drawHeight,
                        minY),
                    new Vector3(
                        worldX,
                        drawHeight,
                        maxY));
            }
            for (int y = 0;
                 y <= height;
                 y += stride)
            {
                float worldY =
                    minY +
                    y * cellSize;
                Gizmos.DrawLine(
                    new Vector3(
                        minX,
                        drawHeight,
                        worldY),
                    new Vector3(
                        maxX,
                        drawHeight,
                        worldY));
            }
        }

        private void DrawObstacles(
            BakedDeterministicMapData map)
        {
            Gizmos.color =
                new Color(
                    1f,
                    0.2f,
                    0.15f,
                    0.6f);
            for (int i = 0;
                 i < map.Obstacles.Count;
                 i++)
            {
                BakedMapObstacle obstacle =
                    map.Obstacles[i];
                Vector3 center =
                    ToWorld(
                        obstacle.Center);
                Vector3 size =
                    new Vector3(
                        (float)obstacle.HalfExtents.x *
                            2f,
                        0.08f,
                        (float)obstacle.HalfExtents.y *
                            2f);
                float angle =
                    Mathf.Atan2(
                        (float)obstacle.AxisX.y,
                        (float)obstacle.AxisX.x) *
                    Mathf.Rad2Deg;
                Matrix4x4 previous =
                    Gizmos.matrix;
                Gizmos.matrix =
                    Matrix4x4.TRS(
                        center,
                        Quaternion.Euler(
                            0f,
                            -angle,
                            0f),
                        Vector3.one);
                Gizmos.DrawCube(
                    Vector3.zero,
                    size);
                Gizmos.matrix = previous;
            }
        }

        private void DrawLanes()
        {
            LaneAuthoring[] lanes =
                source.Lanes;
            for (int i = 0;
                 i < lanes.Length;
                 i++)
            {
                if (lanes[i] == null ||
                    !ShouldDrawLane(i))
                    continue;
                LaneRuntimeData lane;
                try
                {
                    lane =
                        lanes[i].BakeOrThrow();
                }
                catch
                {
                    continue;
                }
                Gizmos.color =
                    LaneColor(i);
                for (int point = 0;
                     point <
                     lane.CenterlinePoints.Length;
                     point++)
                {
                    Vector3 world =
                        ToWorld(
                            lane.CenterlinePoints[
                                point]);
                    Gizmos.DrawSphere(
                        world,
                        0.25f);
                    if (point > 0)
                        Gizmos.DrawLine(
                            ToWorld(
                                lane.CenterlinePoints[
                                    point - 1]),
                            world);
                }
            }
        }

        private void DrawField(
            BakedDeterministicMapData map,
            TeamFlowFieldData field)
        {
            if (drawBlockedCells)
                DrawBlockedCells(
                    map,
                    field);
            int stride =
                Math.Max(1, drawStride);
            float cellSize =
                (float)map.CellSize;
            for (int y = 0;
                 y < field.Height;
                 y++)
            {
                for (int x = 0;
                     x < field.Width;
                     x++)
                {
                    int index =
                        y * field.Width + x;
                    bool regularSample =
                        x % stride == 0 &&
                        y % stride == 0;
                    if (!ShouldDrawFieldCell(
                            field,
                            x,
                            y,
                            stride,
                            mode))
                        continue;
                    int visualStride =
                        regularSample
                            ? stride
                            : 1;
                    Vector3 origin =
                        new Vector3(
                            (float)map.WorldMinimum.x +
                                (x + 0.5f) *
                                cellSize,
                            drawHeight + 0.03f,
                            (float)map.WorldMinimum.y +
                                (y + 0.5f) *
                                cellSize);
                    if (field.Cost[index] ==
                        int.MaxValue)
                        continue;
                    int ownerLane =
                        field.OwnerLane[index];
                    if (ownerLane < 0 ||
                        ownerLane == byte.MaxValue ||
                        !ShouldDrawLane(ownerLane))
                        continue;

                    if (mode ==
                        FlowFieldVisualizationMode
                            .OwnerLane)
                    {
                        Gizmos.color =
                            LaneColor(ownerLane);
                        Gizmos.DrawCube(
                            origin,
                            new Vector3(
                                cellSize *
                                    visualStride *
                                    0.7f,
                                0.025f,
                                cellSize *
                                    visualStride *
                                    0.7f));
                        continue;
                    }
                    if (mode ==
                        FlowFieldVisualizationMode
                            .Reachability)
                    {
                        Gizmos.color =
                            new Color(
                                0.2f,
                                0.9f,
                                0.35f,
                                0.35f);
                        Gizmos.DrawCube(
                            origin,
                            new Vector3(
                                cellSize *
                                    visualStride *
                                    0.65f,
                                0.02f,
                                cellSize *
                                    visualStride *
                                    0.65f));
                        continue;
                    }

                    byte directionCode =
                        field.DirectionCode[index];
                    if (directionCode ==
                        (byte)Dir8.None)
                        continue;
                    fp2 direction =
                        Dir8Helper.ToFP2(
                            (Dir8)directionCode);
                    Vector3 delta =
                        new Vector3(
                            (float)direction.x,
                            0f,
                            (float)direction.y) *
                        arrowLength *
                        visualStride;
                    Gizmos.color =
                        previewTeam ==
                            FlowFieldTeamView.Blue
                            ? new Color(
                                0.2f,
                                0.65f,
                                1f,
                                0.9f)
                            : new Color(
                                1f,
                                0.25f,
                                0.2f,
                                0.9f);
                    DrawArrow(
                        origin,
                        delta);
                }
            }
        }

        internal static bool ShouldDrawFieldCell(
            TeamFlowFieldData field,
            int x,
            int y,
            int stride,
            FlowFieldVisualizationMode
                visualizationMode)
        {
            int safeStride =
                Math.Max(1, stride);
            return x % safeStride == 0 &&
                    y % safeStride == 0 ||
                visualizationMode ==
                    FlowFieldVisualizationMode
                        .Directions &&
                IsLaneBoundaryCell(
                    field,
                    x,
                    y);
        }

        internal static bool IsLaneBoundaryCell(
            TeamFlowFieldData field,
            int x,
            int y)
        {
            int index =
                y * field.Width + x;
            byte owner =
                field.OwnerLane[index];
            if (owner == byte.MaxValue)
                return false;

            return HasDifferentLaneOwner(
                    field,
                    owner,
                    x - 1,
                    y) ||
                HasDifferentLaneOwner(
                    field,
                    owner,
                    x + 1,
                    y) ||
                HasDifferentLaneOwner(
                    field,
                    owner,
                    x,
                    y - 1) ||
                HasDifferentLaneOwner(
                    field,
                    owner,
                    x,
                    y + 1);
        }

        private static bool HasDifferentLaneOwner(
            TeamFlowFieldData field,
            byte owner,
            int x,
            int y)
        {
            if (x < 0 ||
                x >= field.Width ||
                y < 0 ||
                y >= field.Height)
                return false;
            int index =
                y * field.Width + x;
            return field.Cost[index] !=
                    int.MaxValue &&
                field.OwnerLane[index] !=
                    byte.MaxValue &&
                field.OwnerLane[index] !=
                    owner;
        }

        private void DrawBlockedCells(
            BakedDeterministicMapData map,
            TeamFlowFieldData field)
        {
            float cellSize =
                (float)map.CellSize;
            Gizmos.color =
                new Color(
                    1f,
                    0.1f,
                    0.1f,
                    0.5f);
            Vector3 size =
                new Vector3(
                    cellSize * 0.88f,
                    0.035f,
                    cellSize * 0.88f);
            for (int y = 0;
                 y < field.Height;
                 y++)
            {
                for (int x = 0;
                     x < field.Width;
                     x++)
                {
                    int index =
                        y * field.Width + x;
                    if (field.Cost[index] !=
                        int.MaxValue)
                        continue;
                    Gizmos.DrawCube(
                        new Vector3(
                            (float)map.WorldMinimum.x +
                                (x + 0.5f) *
                                cellSize,
                            drawHeight + 0.03f,
                            (float)map.WorldMinimum.y +
                                (y + 0.5f) *
                                cellSize),
                        size);
                }
            }
        }

        private bool ShouldDrawLane(
            int zeroBasedLaneIndex)
        {
            return laneView ==
                    FlowFieldLaneView.All ||
                (int)laneView - 1 ==
                    zeroBasedLaneIndex;
        }

        private static Color LaneColor(
            int zeroBasedLaneIndex)
        {
            switch (zeroBasedLaneIndex)
            {
                case 0:
                    return new Color(
                        0.25f,
                        0.8f,
                        1f,
                        0.65f);
                case 1:
                    return new Color(
                        1f,
                        0.8f,
                        0.2f,
                        0.65f);
                default:
                    return new Color(
                        0.8f,
                        0.35f,
                        1f,
                        0.65f);
            }
        }

        private static Vector3 ToWorld(
            fp2 point)
        {
            return new Vector3(
                (float)point.x,
                0.15f,
                (float)point.y);
        }

        private static void DrawArrow(
            Vector3 origin,
            Vector3 delta)
        {
            Gizmos.DrawLine(
                origin,
                origin + delta);
            if (delta.sqrMagnitude <=
                0.0001f)
                return;
            Vector3 direction =
                delta.normalized;
            Vector3 side =
                Vector3.Cross(
                    Vector3.up,
                    direction) *
                delta.magnitude *
                0.22f;
            Vector3 back =
                direction *
                delta.magnitude *
                0.28f;
            Gizmos.DrawLine(
                origin + delta,
                origin + delta -
                    back +
                    side);
            Gizmos.DrawLine(
                origin + delta,
                origin + delta -
                    back -
                    side);
        }
#endif
    }
}
