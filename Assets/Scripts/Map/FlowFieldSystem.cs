using System.Collections.Generic;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sirenix.OdinInspector;
using Unity.Mathematics.FixedPoint;
using System;

namespace FlowField
{
    public class FlowFieldSystem : MonoSingleton<FlowFieldSystem>
    {
        [FoldoutGroup("流场配置")]
        [LabelText("流场构建器")]
        [Required("请设置流场构建器")]
        public FlowFieldBuilder flowFieldBuilder;

        [FoldoutGroup("流场配置")]
        [LabelText("自动初始化")]
        public bool autoInitialize = true;

        [FoldoutGroup("运行时设置")]
        [LabelText("使用插值获取方向")]
        public bool useInterpolation = true;

#if UNITY_EDITOR
        [FoldoutGroup("可视化配置")]
        [LabelText("显示队伍")]
        public ViewTeam viewTeam = ViewTeam.Blue;

        [FoldoutGroup("可视化配置")]
        [LabelText("显示模式")]
        public DisplayMode displayMode = DisplayMode.VectorField;

        [FoldoutGroup("可视化配置")]
        [LabelText("显示网格")]
        public bool showGrid = true;

        [FoldoutGroup("可视化配置")]
        [LabelText("显示障碍物")]
        public bool showObstacles = true;

        [FoldoutGroup("可视化配置")]
        [LabelText("向量缩放")]
        [Range(0.1f, 2f)]
        public float vectorScale = 0.5f;

        [FoldoutGroup("可视化配置")]
        [LabelText("流场采样间隔")]
        [Range(1, 5)]
        public int flowSampleInterval = 2;

        [FoldoutGroup("可视化配置")]
        [LabelText("关键点大小")]
        [Range(0.1f, 1f)]
        public float waypointSize = 0.3f;

        public enum ViewTeam
        {
            Blue,
            Red
        }

        public enum DisplayMode
        {
            None,
            VectorField
        }
#endif

        private WalkableFieldData walkableField;
        private DirectionFieldData blueDirectionField;
        private DirectionFieldData redDirectionField;
        private bool isInitialized;

        public int MapSizeX => flowFieldBuilder != null ? flowFieldBuilder.mapSizeX : 0;
        public int MapSizeZ => flowFieldBuilder != null ? flowFieldBuilder.mapSizeZ : 0;
        public float GridSize => flowFieldBuilder != null ? flowFieldBuilder.gridSize : 1f;

        private void Start()
        {
            if (autoInitialize)
                Initialize();
        }

        [Button("初始化系统", ButtonSizes.Medium)]
        [GUIColor(0.4f, 0.8f, 0.4f)]
        [PropertyOrder(-1)]
        public void Initialize()
        {
            if (flowFieldBuilder == null)
            {
                Debug.LogError("FlowFieldBuilder 未设置！");
                return;
            }

            flowFieldBuilder.EnsureInitialized();

            walkableField = flowFieldBuilder.WalkableField.Clone();
            blueDirectionField = flowFieldBuilder.BlueDirectionField.Clone();
            redDirectionField = flowFieldBuilder.RedDirectionField.Clone();

            isInitialized = true;
            Debug.Log($"FlowFieldSystem 初始化完成：{MapSizeX}x{MapSizeZ}");
        }

        public bool IsCellMovable(Vector2Int gridPos)
        {
            if (!isInitialized || walkableField == null || !walkableField.IsValidCoord(gridPos.x, gridPos.y))
                return false;

            return walkableField.GetCell(gridPos.x, gridPos.y);
        }

        public Vector3 GetMoveDirection(Vector2Int gridPos, bool isBlueTeam)
        {
            if (!isInitialized)
                return Vector3.zero;

            var field = isBlueTeam ? blueDirectionField : redDirectionField;
            if (field == null || !field.IsValidCoord(gridPos.x, gridPos.y))
                return Vector3.zero;

            return field.GetDirection(gridPos.x, gridPos.y);
        }

        public Vector3 GetMoveDirectionWorld(Vector3 worldPos, bool isBlueTeam)
        {
            if (!isInitialized || flowFieldBuilder == null)
                return Vector3.zero;

            var gridPos = flowFieldBuilder.WorldToGridCoord(worldPos);
            if (!flowFieldBuilder.IsValidCoord(gridPos))
                return Vector3.zero;

            if (!useInterpolation)
                return GetMoveDirection(gridPos, isBlueTeam);

            var field = isBlueTeam ? blueDirectionField : redDirectionField;
            return GetInterpolatedDirection(worldPos, field);
        }

        public fp3 GetMoveDirectionFP(fp3 logicWorldPos, bool isBlueTeam)
        {
            Vector3 worldPos = new Vector3((float)logicWorldPos.x, (float)logicWorldPos.y, (float)logicWorldPos.z);
            Vector3 dir = GetMoveDirectionWorld(worldPos, isBlueTeam);
            return new fp3((fp)dir.x, (fp)dir.y, (fp)dir.z);
        }

        private Vector3 GetInterpolatedDirection(Vector3 worldPos, DirectionFieldData field)
        {
            var center = flowFieldBuilder.MapCenterPosition;
            float offsetX = MapSizeX * GridSize * 0.5f;
            float offsetZ = MapSizeZ * GridSize * 0.5f;

            float gx = (worldPos.x - center.x + offsetX) / GridSize;
            float gz = (worldPos.z - center.z + offsetZ) / GridSize;

            int x0 = Mathf.FloorToInt(gx);
            int z0 = Mathf.FloorToInt(gz);
            float localX = gx - x0;
            float localZ = gz - z0;

            var d00 = GetDirectionSafe(field, x0, z0);
            var d10 = GetDirectionSafe(field, x0 + 1, z0);
            var d01 = GetDirectionSafe(field, x0, z0 + 1);
            var d11 = GetDirectionSafe(field, x0 + 1, z0 + 1);

            var dx0 = Vector3.Lerp(d00, d10, localX);
            var dx1 = Vector3.Lerp(d01, d11, localX);

            Vector3 result = Vector3.Lerp(dx0, dx1, localZ);
            return result.sqrMagnitude > 0.0001f ? result.normalized : Vector3.zero;
        }

        private Vector3 GetDirectionSafe(DirectionFieldData field, int x, int z)
        {
            if (field != null && field.IsValidCoord(x, z))
                return field.GetDirection(x, z);

            return Vector3.zero;
        }

        public List<Vector3> BuildPath(Vector3 startWorld, bool isBlueTeam, int maxSteps = 1024)
        {
            var path = new List<Vector3>();
            if (!isInitialized || flowFieldBuilder == null)
                return path;

            var current = flowFieldBuilder.WorldToGridCoord(startWorld);
            if (!flowFieldBuilder.IsValidCoord(current))
                return path;

            var visited = new HashSet<Vector2Int>();

            for (int step = 0; step < maxSteps; step++)
            {
                if (!flowFieldBuilder.IsValidCoord(current))
                    break;

                if (!walkableField.GetCell(current.x, current.y))
                    break;

                path.Add(flowFieldBuilder.GridToWorld(current));

                if (!visited.Add(current))
                    break;

                Vector3 dir = GetMoveDirection(current, isBlueTeam);
                if (dir.sqrMagnitude < 0.0001f)
                    break;

                int nextX = current.x + Mathf.RoundToInt(dir.x);
                int nextZ = current.y + Mathf.RoundToInt(dir.z);
                var next = new Vector2Int(nextX, nextZ);

                if (!flowFieldBuilder.IsValidCoord(next))
                    break;

                current = next;
            }

            return path;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!isInitialized || flowFieldBuilder == null)
                return;

            int sizeX = MapSizeX;
            int sizeZ = MapSizeZ;
            float gridSz = GridSize;
            var center = flowFieldBuilder.MapCenterPosition;

            if (showGrid) DrawGrid(center, sizeX, sizeZ, gridSz);
            if (showObstacles) DrawObstacles(sizeX, sizeZ, gridSz);

            DrawLanePathsForTeam(viewTeam);

            if (displayMode == DisplayMode.None)
                return;

            if (displayMode == DisplayMode.VectorField)
            {
                var dirField = (viewTeam == ViewTeam.Blue) ? blueDirectionField : redDirectionField;
                if (dirField != null)
                {
                    Color teamColor = (viewTeam == ViewTeam.Blue)
                        ? new Color(0.2f, 0.5f, 1f, 0.8f)
                        : new Color(1f, 0.4f, 0.4f, 0.8f);

                    DrawVectorField(dirField, sizeX, sizeZ, gridSz, teamColor);
                }
            }
        }

        private void DrawVectorField(DirectionFieldData dirField, int sizeX, int sizeZ, float gridSz, Color color)
        {
            Gizmos.color = color;

            for (int x = 0; x < sizeX; x += flowSampleInterval)
            {
                for (int z = 0; z < sizeZ; z += flowSampleInterval)
                {
                    if (!walkableField.GetCell(x, z))
                        continue;

                    var dir = dirField.GetDirection(x, z);
                    if (dir.sqrMagnitude < 0.01f)
                        continue;

                    var pos = flowFieldBuilder.GridToWorld(new Vector2Int(x, z));
                    var vec = dir * gridSz * vectorScale;

                    Gizmos.DrawRay(pos, vec);

                    var right = Vector3.Cross(Vector3.up, vec).normalized * gridSz * 0.1f;
                    Gizmos.DrawRay(pos + vec, -vec * 0.25f + right);
                    Gizmos.DrawRay(pos + vec, -vec * 0.25f - right);
                }
            }
        }

        private void DrawGrid(Vector3 center, int sizeX, int sizeZ, float gridSz)
        {
            Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.3f);
            float halfX = sizeX * gridSz * 0.5f;
            float halfZ = sizeZ * gridSz * 0.5f;

            for (int z = 0; z <= sizeZ; z++)
            {
                var start = center + new Vector3(-halfX, 0, z * gridSz - halfZ);
                var end = center + new Vector3(halfX, 0, z * gridSz - halfZ);
                Gizmos.DrawLine(start, end);
            }

            for (int x = 0; x <= sizeX; x++)
            {
                var start = center + new Vector3(x * gridSz - halfX, 0, -halfZ);
                var end = center + new Vector3(x * gridSz - halfX, 0, halfZ);
                Gizmos.DrawLine(start, end);
            }
        }

        private void DrawObstacles(int sizeX, int sizeZ, float gridSz)
        {
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.6f);

            for (int x = 0; x < sizeX; x++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    if (!walkableField.GetCell(x, z))
                    {
                        var pos = flowFieldBuilder.GridToWorld(new Vector2Int(x, z));
                        Gizmos.DrawCube(pos, new Vector3(gridSz * 0.9f, 0.1f, gridSz * 0.9f));
                    }
                }
            }
        }

        private void DrawLanePathsForTeam(ViewTeam team)
        {
            LanePathConfig lane1, lane2, lane3;

            if (team == ViewTeam.Blue)
            {
                lane1 = flowFieldBuilder.blueLane1;
                lane2 = flowFieldBuilder.blueLane2;
                lane3 = flowFieldBuilder.blueLane3;
            }
            else
            {
                lane1 = flowFieldBuilder.redLane1;
                lane2 = flowFieldBuilder.redLane2;
                lane3 = flowFieldBuilder.redLane3;
            }

            DrawSingleLanePath(lane1);
            DrawSingleLanePath(lane2);
            DrawSingleLanePath(lane3);
        }

        private void DrawSingleLanePath(LanePathConfig lane)
        {
            if (!lane.IsValid)
                return;

            Gizmos.color = lane.pathColor;
            var points = lane.wayPoints.Where(t => t != null).ToList();

            for (int i = 0; i < points.Count - 1; i++)
                Gizmos.DrawLine(points[i].position, points[i + 1].position);

            foreach (var point in points)
                Gizmos.DrawSphere(point.position, waypointSize);
        }
#endif
    }
}
