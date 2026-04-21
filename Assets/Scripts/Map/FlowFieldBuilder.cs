using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sirenix.OdinInspector;

namespace FlowField
{
    public class FlowFieldBuilder : MonoBehaviour
    {
        #region 地图基础配置

        [FoldoutGroup("地图基础配置")]
        [LabelText("地图中心点")]
        [InfoBox("地图网格将以该Transform为中心生成", InfoMessageType.Info)]
        [Required("请设置地图中心点Transform")]
        public Transform mapCenter;

        [FoldoutGroup("地图基础配置")]
        [LabelText("网格大小")]
        public float gridSize = 1f;

        [FoldoutGroup("地图基础配置")]
        [LabelText("地图宽度(X方向格子数)")]
        [MinValue(1)]
        public int mapSizeX = 50;

        [FoldoutGroup("地图基础配置")]
        [LabelText("地图深度(Z方向格子数)")]
        [MinValue(1)]
        public int mapSizeZ = 50;

        [FoldoutGroup("地图基础配置")]
        [LabelText("障碍物父物体")]
        public Transform obstaclesParent;

        #endregion

        #region 构建配置

        [FoldoutGroup("流场生成配置")]
        [LabelText("障碍物影响半径")]
        [Range(0f, 2f)]
        public float obstacleInfluenceRadius = 0.3f;

        [FoldoutGroup("流场生成配置")]
        [LabelText("走廊惩罚系数")]
        [Range(1, 30)]
        public int corridorPenaltyPerCell = 8;

        [FoldoutGroup("流场生成配置")]
        [LabelText("方向平滑迭代次数")]
        [Range(0, 5)]
        public int directionSmoothingIterations = 2;

        [FoldoutGroup("流场生成配置")]
        [LabelText("障碍物边缘平滑强度")]
        [Range(0f, 1f)]
        public float obstacleEdgeSmoothStrength = 0.8f;

        [FoldoutGroup("流场生成配置")]
        [LabelText("启用障碍物边缘对齐")]
        public bool enableObstacleEdgeAlignment = true;

        [FoldoutGroup("流场生成配置")]
        [LabelText("路径融合权重")]
        [Range(0f, 1f)]
        public float pathMergeWeight = 0.35f;

        #endregion

        #region 兵线配置

        [FoldoutGroup("蓝队兵线配置")]
        [LabelText("上路")]
        public LanePathConfig blueLane1 = new LanePathConfig { pathName = "蓝队上路", pathColor = new Color(0.3f, 0.3f, 1f) };

        [FoldoutGroup("蓝队兵线配置")]
        [LabelText("中路")]
        public LanePathConfig blueLane2 = new LanePathConfig { pathName = "蓝队中路", pathColor = new Color(0.4f, 0.4f, 1f) };

        [FoldoutGroup("蓝队兵线配置")]
        [LabelText("下路")]
        public LanePathConfig blueLane3 = new LanePathConfig { pathName = "蓝队下路", pathColor = new Color(0.5f, 0.5f, 1f) };

        [FoldoutGroup("红队兵线配置")]
        [LabelText("上路")]
        public LanePathConfig redLane1 = new LanePathConfig { pathName = "红队上路", pathColor = new Color(1f, 0.3f, 0.3f) };

        [FoldoutGroup("红队兵线配置")]
        [LabelText("中路")]
        public LanePathConfig redLane2 = new LanePathConfig { pathName = "红队中路", pathColor = new Color(1f, 0.4f, 0.4f) };

        [FoldoutGroup("红队兵线配置")]
        [LabelText("下路")]
        public LanePathConfig redLane3 = new LanePathConfig { pathName = "红队下路", pathColor = new Color(1f, 0.5f, 0.5f) };

        #endregion

        #region 序列化数据

        [FoldoutGroup("构建结果数据")]
        [LabelText("可行走图")]
        [ReadOnly]
        [SerializeField] private WalkableFieldData walkableField = new WalkableFieldData();

        [FoldoutGroup("构建结果数据")]
        [LabelText("蓝队矢量场")]
        [ReadOnly]
        [SerializeField] private DirectionFieldData blueDirectionField = new DirectionFieldData();

        [FoldoutGroup("构建结果数据")]
        [LabelText("红队矢量场")]
        [ReadOnly]
        [SerializeField] private DirectionFieldData redDirectionField = new DirectionFieldData();

        #endregion

        #region 属性访问

        public WalkableFieldData WalkableField => walkableField;
        public DirectionFieldData BlueDirectionField => blueDirectionField;
        public DirectionFieldData RedDirectionField => redDirectionField;

        public Vector3 MapCenterPosition => mapCenter != null ? mapCenter.position : Vector3.zero;

        #endregion

        private static readonly Vector2Int[] Neighbors8 =
        {
            new Vector2Int(1,0), new Vector2Int(-1,0), new Vector2Int(0,1), new Vector2Int(0,-1),
            new Vector2Int(1,1), new Vector2Int(-1,1), new Vector2Int(1,-1), new Vector2Int(-1,-1)
        };

        #region 初始化

        public void Initialize(int sizeX, int sizeZ, float gridSz)
        {
            mapSizeX = sizeX;
            mapSizeZ = sizeZ;
            gridSize = gridSz;

            walkableField.Initialize(sizeX, sizeZ);
            blueDirectionField.Initialize(sizeX, sizeZ);
            redDirectionField.Initialize(sizeX, sizeZ);
        }

        public void EnsureInitialized()
        {
            if (walkableField.cells == null || walkableField.cells.Length == 0)
                Initialize(mapSizeX, mapSizeZ, gridSize);
        }

        #endregion

        #region 坐标转换

        public Vector2Int WorldToGridCoord(Vector3 worldPos)
        {
            Vector3 center = MapCenterPosition;
            float offsetX = mapSizeX * gridSize * 0.5f;
            float offsetZ = mapSizeZ * gridSize * 0.5f;

            return new Vector2Int(
                Mathf.FloorToInt((worldPos.x - center.x + offsetX) / gridSize),
                Mathf.FloorToInt((worldPos.z - center.z + offsetZ) / gridSize)
            );
        }

        public Vector3 GridToWorld(Vector2Int gridPos)
        {
            Vector3 center = MapCenterPosition;
            float offsetX = mapSizeX * gridSize * 0.5f;
            float offsetZ = mapSizeZ * gridSize * 0.5f;

            return new Vector3(
                gridPos.x * gridSize - offsetX + gridSize * 0.5f + center.x,
                center.y,
                gridPos.y * gridSize - offsetZ + gridSize * 0.5f + center.z
            );
        }

        public Vector3 GridToWorld(Vector3 gridPos) => GridToWorld(new Vector2Int((int)gridPos.x, (int)gridPos.z));

        #endregion

        #region 数据访问

        public bool IsValidCoord(int x, int z) => x >= 0 && x < mapSizeX && z >= 0 && z < mapSizeZ;
        public bool IsValidCoord(Vector2Int coord) => IsValidCoord(coord.x, coord.y);

        public bool IsWalkable(int x, int z) => walkableField.GetCell(x, z);

        #endregion

        #region 构建

        [Button("构建静态矢量场", ButtonSizes.Large)]
        [GUIColor(0.4f, 0.8f, 0.4f)]
        [PropertyOrder(-1)]
        public void BuildFlowField()
        {
            EnsureInitialized();

            var obstacleColliders = CollectObstacles();

            BuildWalkableField(obstacleColliders);
            BuildTeamDirectionField(blueDirectionField, new[] { blueLane1, blueLane2, blueLane3 }, "蓝队");
            BuildTeamDirectionField(redDirectionField, new[] { redLane1, redLane2, redLane3 }, "红队");

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssets();
            Debug.Log($"静态矢量场构建完成：{mapSizeX}x{mapSizeZ}，障碍物数量 {obstacleColliders.Count}");
#endif
        }

        private List<Collider> CollectObstacles()
        {
            var colliders = new List<Collider>();

            if (obstaclesParent != null)
            {
                var allColliders = obstaclesParent.GetComponentsInChildren<Collider>();
                foreach (var col in allColliders)
                {
                    if (col.CompareTag("Obstacle"))
                        colliders.Add(col);
                }
            }

            return colliders;
        }

        private void BuildWalkableField(List<Collider> obstacleColliders)
        {
            for (int x = 0; x < mapSizeX; x++)
                for (int z = 0; z < mapSizeZ; z++)
                    walkableField.SetCell(x, z, true);

            foreach (var col in obstacleColliders)
                MarkObstacle(col);
        }

        private void MarkObstacle(Collider obstacle)
        {
            Bounds bounds = obstacle.bounds;

            Vector2Int min = WorldToGridCoord(bounds.min);
            Vector2Int max = WorldToGridCoord(bounds.max);

            min.x = Mathf.Max(0, min.x - 1);
            min.y = Mathf.Max(0, min.y - 1);
            max.x = Mathf.Min(mapSizeX - 1, max.x + 1);
            max.y = Mathf.Min(mapSizeZ - 1, max.y + 1);

            for (int x = min.x; x <= max.x; x++)
            {
                for (int z = min.y; z <= max.y; z++)
                {
                    Vector3 cellCenter = GridToWorld(new Vector2Int(x, z));
                    Vector3 closest = obstacle.ClosestPoint(cellCenter);
                    float dist = Vector3.Distance(cellCenter, closest);

                    if (dist < 0.01f)
                    {
                        walkableField.SetCell(x, z, false);
                        continue;
                    }

                    if (obstacleInfluenceRadius > 0 && dist < obstacleInfluenceRadius * gridSize)
                        walkableField.SetCell(x, z, false);
                }
            }
        }

        private void BuildTeamDirectionField(DirectionFieldData resultField, LanePathConfig[] lanes, string teamName)
        {
            resultField.Initialize(mapSizeX, mapSizeZ);

            var validLanePaths = new List<List<Vector2Int>>();
            var laneCosts = new List<int[,]>();

            for (int i = 0; i < lanes.Length; i++)
            {
                if (!lanes[i].IsValid)
                    continue;

                var gridPath = BuildLaneGridPath(lanes[i]);
                if (gridPath.Count < 2)
                    continue;

                int[,] cost = BuildLaneIntegratedCost(gridPath);
                validLanePaths.Add(gridPath);
                laneCosts.Add(cost);
            }

            if (validLanePaths.Count == 0)
            {
                Debug.LogWarning($"{teamName}没有有效兵线路径，矢量场为空");
                return;
            }

            for (int x = 0; x < mapSizeX; x++)
            {
                for (int z = 0; z < mapSizeZ; z++)
                {
                    if (!walkableField.GetCell(x, z))
                    {
                        resultField.SetDirection(x, z, Vector3.zero);
                        continue;
                    }

                    Vector3 dir = ComputeMergedDirection(x, z, laneCosts, validLanePaths);
                    resultField.SetDirection(x, z, dir);
                }
            }

            if (enableObstacleEdgeAlignment)
                AlignDirectionNearObstacles(resultField, validLanePaths);

            SmoothDirectionField(resultField, directionSmoothingIterations);
        }

        private List<Vector2Int> BuildLaneGridPath(LanePathConfig lane)
        {
            var worldPath = lane.GetWorldPositions();
            var result = new List<Vector2Int>();

            if (worldPath.Count < 2)
                return result;

            for (int i = 0; i < worldPath.Count - 1; i++)
            {
                var a = WorldToGridCoord(worldPath[i]);
                var b = WorldToGridCoord(worldPath[i + 1]);

                var segment = RasterizeLine(a, b);

                for (int j = 0; j < segment.Count; j++)
                {
                    if (!IsValidCoord(segment[j]))
                        continue;

                    if (result.Count == 0 || result[result.Count - 1] != segment[j])
                        result.Add(segment[j]);
                }
            }

            return result;
        }

        private List<Vector2Int> RasterizeLine(Vector2Int start, Vector2Int end)
        {
            var result = new List<Vector2Int>();

            int x0 = start.x;
            int y0 = start.y;
            int x1 = end.x;
            int y1 = end.y;

            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            while (true)
            {
                result.Add(new Vector2Int(x0, y0));

                if (x0 == x1 && y0 == y1)
                    break;

                int e2 = 2 * err;

                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }

                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }

            return result;
        }

        private int[,] BuildLaneIntegratedCost(List<Vector2Int> lanePath)
        {
            int[,] cost = new int[mapSizeX, mapSizeZ];

            for (int x = 0; x < mapSizeX; x++)
                for (int z = 0; z < mapSizeZ; z++)
                    cost[x, z] = walkableField.GetCell(x, z) ? int.MaxValue : -1;

            Vector2Int goal = lanePath[lanePath.Count - 1];
            if (!walkableField.GetCell(goal.x, goal.y))
            {
                var newGoal = FindNearestWalkable(goal);
                if (newGoal.HasValue)
                    goal = newGoal.Value;
                else
                    return cost;
            }

            var skeletonBonus = BuildSkeletonBonus(lanePath);

            var open = new List<(int cost, int x, int z)>();
            void Push(int c, int x, int z)
            {
                int lo = 0, hi = open.Count;
                while (lo < hi)
                {
                    int mid = (lo + hi) >> 1;
                    if (c < open[mid].cost) hi = mid;
                    else lo = mid + 1;
                }
                open.Insert(lo, (c, x, z));
            }

            cost[goal.x, goal.y] = 0;
            Push(0, goal.x, goal.y);

            while (open.Count > 0)
            {
                var cur = open[0];
                open.RemoveAt(0);

                if (cur.cost > cost[cur.x, cur.z])
                    continue;

                for (int i = 0; i < Neighbors8.Length; i++)
                {
                    int nx = cur.x + Neighbors8[i].x;
                    int nz = cur.z + Neighbors8[i].y;

                    if (!IsValidCoord(nx, nz) || !walkableField.GetCell(nx, nz))
                        continue;

                    if (Neighbors8[i].x != 0 && Neighbors8[i].y != 0)
                    {
                        if (!walkableField.GetCell(cur.x + Neighbors8[i].x, cur.z) ||
                            !walkableField.GetCell(cur.x, cur.z + Neighbors8[i].y))
                            continue;
                    }

                    int step = (Neighbors8[i].x != 0 && Neighbors8[i].y != 0) ? 14 : 10;

                    float laneDist = DistanceToLane(lanePath, nx, nz);
                    int penalty = Mathf.RoundToInt(laneDist * corridorPenaltyPerCell);

                    if (skeletonBonus.TryGetValue(new Vector2Int(nx, nz), out int bonus))
                        penalty += bonus;

                    if (penalty < 0)
                        penalty = 0;

                    if (cur.cost > int.MaxValue - step - penalty)
                        continue;

                    int newCost = cur.cost + step + penalty;
                    if (newCost < cost[nx, nz])
                    {
                        cost[nx, nz] = newCost;
                        Push(newCost, nx, nz);
                    }
                }
            }

            return cost;
        }

        private Dictionary<Vector2Int, int> BuildSkeletonBonus(List<Vector2Int> lanePath)
        {
            var result = new Dictionary<Vector2Int, int>();

            for (int i = 0; i < lanePath.Count; i++)
            {
                result[lanePath[i]] = -20;

                for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (dx == 0 && dz == 0)
                            continue;

                        var p = lanePath[i] + new Vector2Int(dx, dz);
                        if (!result.ContainsKey(p))
                            result[p] = -10;
                    }
            }

            return result;
        }

        private float DistanceToLane(List<Vector2Int> lanePath, int x, int z)
        {
            if (lanePath == null || lanePath.Count < 2)
                return float.PositiveInfinity;

            var p = new Vector2(x, z);
            float minDist = float.PositiveInfinity;

            for (int i = 0; i < lanePath.Count - 1; i++)
            {
                var a = new Vector2(lanePath[i].x, lanePath[i].y);
                var b = new Vector2(lanePath[i + 1].x, lanePath[i + 1].y);
                var ab = b - a;
                float t = Vector2.Dot(p - a, ab) / (ab.sqrMagnitude + 1e-6f);
                t = Mathf.Clamp01(t);

                var proj = a + t * ab;
                float d = Vector2.Distance(p, proj);

                if (d < minDist)
                    minDist = d;
            }

            return minDist;
        }

        private Vector2Int? FindNearestWalkable(Vector2Int start, int maxRadius = 10)
        {
            for (int r = 1; r <= maxRadius; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dz = -r; dz <= r; dz++)
                    {
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dz) != r)
                            continue;

                        int nx = start.x + dx;
                        int nz = start.y + dz;

                        if (IsValidCoord(nx, nz) && walkableField.GetCell(nx, nz))
                            return new Vector2Int(nx, nz);
                    }
                }
            }

            return null;
        }

        private Vector3 ComputeMergedDirection(int x, int z, List<int[,]> laneCosts, List<List<Vector2Int>> lanePaths)
        {
            Vector3 bestDir = Vector3.zero;
            float bestWeight = -1f;

            for (int i = 0; i < laneCosts.Count; i++)
            {
                Vector3 laneDir = ComputeDirectionFromCost(laneCosts[i], x, z);
                if (laneDir.sqrMagnitude < 0.0001f)
                    continue;

                float dist = DistanceToLane(lanePaths[i], x, z);
                float weight = Mathf.Exp(-dist * Mathf.Max(0.001f, pathMergeWeight));

                if (weight > bestWeight)
                {
                    bestWeight = weight;
                    bestDir = laneDir;
                }
            }

            if (laneCosts.Count <= 1)
                return bestDir;

            Vector3 blended = Vector3.zero;
            float totalWeight = 0f;

            for (int i = 0; i < laneCosts.Count; i++)
            {
                Vector3 laneDir = ComputeDirectionFromCost(laneCosts[i], x, z);
                if (laneDir.sqrMagnitude < 0.0001f)
                    continue;

                float dist = DistanceToLane(lanePaths[i], x, z);
                float weight = Mathf.Exp(-dist * Mathf.Max(0.001f, pathMergeWeight));

                blended += laneDir * weight;
                totalWeight += weight;
            }

            if (totalWeight > 0.0001f && blended.sqrMagnitude > 0.0001f)
                return (blended / totalWeight).normalized;

            return bestDir;
        }

        private Vector3 ComputeDirectionFromCost(int[,] cost, int x, int z)
        {
            if (!walkableField.GetCell(x, z))
                return Vector3.zero;

            int current = cost[x, z];
            if (current < 0 || current >= int.MaxValue)
                return Vector3.zero;

            int best = current;
            Vector3 bestDir = Vector3.zero;

            for (int i = 0; i < Neighbors8.Length; i++)
            {
                int nx = x + Neighbors8[i].x;
                int nz = z + Neighbors8[i].y;

                if (!IsValidCoord(nx, nz) || !walkableField.GetCell(nx, nz))
                    continue;

                int nCost = cost[nx, nz];
                if (nCost < 0 || nCost >= int.MaxValue)
                    continue;

                if (nCost < best)
                {
                    best = nCost;
                    bestDir = new Vector3(Neighbors8[i].x, 0, Neighbors8[i].y).normalized;
                }
            }

            return bestDir;
        }

        private void AlignDirectionNearObstacles(DirectionFieldData directionField, List<List<Vector2Int>> lanePaths)
        {
            var adjustments = new List<(int x, int z, Vector3 dir)>();

            for (int x = 0; x < mapSizeX; x++)
            {
                for (int z = 0; z < mapSizeZ; z++)
                {
                    if (!walkableField.GetCell(x, z))
                        continue;

                    Vector3 obstacleNormal = Vector3.zero;
                    int obstacleCount = 0;

                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            if (dx == 0 && dz == 0)
                                continue;

                            int nx = x + dx;
                            int nz = z + dz;

                            if (!IsValidCoord(nx, nz))
                                continue;

                            if (!walkableField.GetCell(nx, nz))
                            {
                                obstacleNormal += new Vector3(dx, 0, dz);
                                obstacleCount++;
                            }
                        }
                    }

                    if (obstacleCount <= 0)
                        continue;

                    obstacleNormal.Normalize();

                    Vector3 parallelDir = Vector3.Cross(Vector3.up, obstacleNormal).normalized;
                    Vector3 laneDir = GetLaneDirectionAt(new Vector2Int(x, z), lanePaths);

                    if (Vector3.Dot(parallelDir, laneDir) < 0f)
                        parallelDir = -parallelDir;

                    Vector3 currentDir = directionField.GetDirection(x, z);
                    Vector3 blended = Vector3.Lerp(currentDir, parallelDir, obstacleEdgeSmoothStrength);

                    if (blended.sqrMagnitude > 0.0001f)
                        adjustments.Add((x, z, blended.normalized));
                }
            }

            for (int i = 0; i < adjustments.Count; i++)
                directionField.SetDirection(adjustments[i].x, adjustments[i].z, adjustments[i].dir);
        }

        private Vector3 GetLaneDirectionAt(Vector2Int pos, List<List<Vector2Int>> lanePaths)
        {
            Vector3 bestDir = Vector3.forward;
            float minDist = float.PositiveInfinity;

            for (int i = 0; i < lanePaths.Count; i++)
            {
                var path = lanePaths[i];
                if (path.Count < 2)
                    continue;

                for (int j = 0; j < path.Count - 1; j++)
                {
                    var a = new Vector2(path[j].x, path[j].y);
                    var b = new Vector2(path[j + 1].x, path[j + 1].y);
                    var p = new Vector2(pos.x, pos.y);

                    var ab = b - a;
                    float t = Vector2.Dot(p - a, ab) / (ab.sqrMagnitude + 1e-6f);
                    t = Mathf.Clamp01(t);
                    var proj = a + t * ab;
                    float d = Vector2.Distance(p, proj);

                    if (d < minDist)
                    {
                        minDist = d;
                        var dir = new Vector3(ab.x, 0, ab.y).normalized;
                        if (dir.sqrMagnitude > 0.0001f)
                            bestDir = dir;
                    }
                }
            }

            return bestDir;
        }

        private void SmoothDirectionField(DirectionFieldData field, int iterations)
        {
            for (int iter = 0; iter < iterations; iter++)
            {
                var temp = new Vector3[mapSizeX, mapSizeZ];

                for (int x = 0; x < mapSizeX; x++)
                    for (int z = 0; z < mapSizeZ; z++)
                        temp[x, z] = field.GetDirection(x, z);

                for (int x = 0; x < mapSizeX; x++)
                {
                    for (int z = 0; z < mapSizeZ; z++)
                    {
                        if (!walkableField.GetCell(x, z))
                            continue;

                        Vector3 avg = temp[x, z];
                        float totalWeight = 1f;

                        for (int dx = -1; dx <= 1; dx++)
                        {
                            for (int dz = -1; dz <= 1; dz++)
                            {
                                if (dx == 0 && dz == 0)
                                    continue;

                                int nx = x + dx;
                                int nz = z + dz;

                                if (!IsValidCoord(nx, nz) || !walkableField.GetCell(nx, nz))
                                    continue;

                                Vector3 neighbor = temp[nx, nz];
                                if (neighbor.sqrMagnitude < 0.0001f)
                                    continue;

                                float weight = (dx != 0 && dz != 0) ? 0.7f : 1f;
                                avg += neighbor * weight;
                                totalWeight += weight;
                            }
                        }

                        if (avg.sqrMagnitude > 0.0001f)
                            field.SetDirection(x, z, (avg / totalWeight).normalized);
                    }
                }
            }
        }

        #endregion
    }
}