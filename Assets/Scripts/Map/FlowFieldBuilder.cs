using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Sirenix.OdinInspector;

namespace FlowField
{
    /// <summary>
    /// 流场构建器（ScriptableObject）
    /// 负责离线构建和存储价值场数据
    /// </summary>
    [CreateAssetMenu(fileName = "FlowFieldBuilder", menuName = "FlowField/流场构建器")]
    public class FlowFieldBuilder : ScriptableObject
    {
        #region 地图基础配置

#if ODIN_INSPECTOR
        [FoldoutGroup("地图基础配置")]
        [LabelText("地图中心点")]
        [InfoBox("地图网格将以该Transform为中心生成", InfoMessageType.Info)]
        [Required("请设置地图中心点Transform")]
#endif
        public Transform mapCenter;

#if ODIN_INSPECTOR
        [FoldoutGroup("地图基础配置")]
        [LabelText("网格大小")]
#endif
        public float gridSize = 1f;

#if ODIN_INSPECTOR
        [FoldoutGroup("地图基础配置")]
        [LabelText("地图宽度(X方向格子数)")]
        [MinValue(1)]
#endif
        public int mapSizeX = 50;

#if ODIN_INSPECTOR
        [FoldoutGroup("地图基础配置")]
        [LabelText("地图深度(Z方向格子数)")]
        [MinValue(1)]
#endif
        public int mapSizeZ = 50;

#if ODIN_INSPECTOR
        [FoldoutGroup("地图基础配置")]
        [LabelText("障碍物父物体")]
        [InfoBox("将自动检测该物体下所有标记为Obstacle的子物体", InfoMessageType.Info)]
#endif
        public Transform obstaclesParent;

        #endregion

        #region 流场生成配置

#if ODIN_INSPECTOR
        [FoldoutGroup("流场生成配置")]
        [LabelText("障碍物影响半径")]
        [Range(0f, 2f)]
#endif
        public float obstacleInfluenceRadius = 0.3f;

#if ODIN_INSPECTOR
        [FoldoutGroup("流场生成配置")]
        [LabelText("走廊惩罚系数")]
        [InfoBox("距离兵线每增加1格的额外代价，值越大越贴近兵线")]
        [Range(1, 30)]
#endif
        public int corridorPenaltyPerCell = 8;

#if ODIN_INSPECTOR
        [FoldoutGroup("流场生成配置")]
        [LabelText("方向平滑迭代次数")]
        [Range(0, 5)]
#endif
        public int directionSmoothingIterations = 2;

#if ODIN_INSPECTOR
        [FoldoutGroup("流场生成配置")]
        [LabelText("障碍物边缘平滑强度")]
        [Range(0f, 1f)]
#endif
        public float obstacleEdgeSmoothStrength = 0.8f;

#if ODIN_INSPECTOR
        [FoldoutGroup("流场生成配置")]
        [LabelText("启用障碍物边缘对齐")]
#endif
        public bool enableObstacleEdgeAlignment = true;

#if ODIN_INSPECTOR
        [FoldoutGroup("流场生成配置")]
        [LabelText("路径融合权重")]
        [Range(0f, 1f)]
#endif
        public float pathMergeWeight = 0.5f;

        #endregion

        #region 兵线关键点配置

#if ODIN_INSPECTOR
        [FoldoutGroup("蓝队兵线配置")]
        [LabelText("上路")]
#endif
        public LanePathConfig blueLane1 = new LanePathConfig { pathName = "蓝队上路", pathColor = new Color(0.3f, 0.3f, 1f) };

#if ODIN_INSPECTOR
        [FoldoutGroup("蓝队兵线配置")]
        [LabelText("中路")]
#endif
        public LanePathConfig blueLane2 = new LanePathConfig { pathName = "蓝队中路", pathColor = new Color(0.4f, 0.4f, 1f) };

#if ODIN_INSPECTOR
        [FoldoutGroup("蓝队兵线配置")]
        [LabelText("下路")]
#endif
        public LanePathConfig blueLane3 = new LanePathConfig { pathName = "蓝队下路", pathColor = new Color(0.5f, 0.5f, 1f) };

#if ODIN_INSPECTOR
        [FoldoutGroup("红队兵线配置")]
        [LabelText("上路")]
#endif
        public LanePathConfig redLane1 = new LanePathConfig { pathName = "红队上路", pathColor = new Color(1f, 0.3f, 0.3f) };

#if ODIN_INSPECTOR
        [FoldoutGroup("红队兵线配置")]
        [LabelText("中路")]
#endif
        public LanePathConfig redLane2 = new LanePathConfig { pathName = "红队中路", pathColor = new Color(1f, 0.4f, 0.4f) };

#if ODIN_INSPECTOR
        [FoldoutGroup("红队兵线配置")]
        [LabelText("下路")]
#endif
        public LanePathConfig redLane3 = new LanePathConfig { pathName = "红队下路", pathColor = new Color(1f, 0.5f, 0.5f) };

        #endregion

        #region 序列化数据

#if ODIN_INSPECTOR
        [FoldoutGroup("构建结果数据")]
        [LabelText("基础价值场")]
        [ReadOnly]
#endif
        [SerializeField] private CostFieldData baseCostField = new CostFieldData();

#if ODIN_INSPECTOR
        [FoldoutGroup("构建结果数据")]
        [LabelText("蓝队价值场")]
        [ReadOnly]
#endif
        [SerializeField] private CostFieldData blueTeamCostField = new CostFieldData();

#if ODIN_INSPECTOR
        [FoldoutGroup("构建结果数据")]
        [LabelText("红队价值场")]
        [ReadOnly]
#endif
        [SerializeField] private CostFieldData redTeamCostField = new CostFieldData();

#if ODIN_INSPECTOR
        [FoldoutGroup("构建结果数据")]
        [LabelText("蓝队方向场")]
        [ReadOnly]
#endif
        [SerializeField] private DirectionFieldData blueTeamDirectionField = new DirectionFieldData();

#if ODIN_INSPECTOR
        [FoldoutGroup("构建结果数据")]
        [LabelText("红队方向场")]
        [ReadOnly]
#endif
        [SerializeField] private DirectionFieldData redTeamDirectionField = new DirectionFieldData();

        #endregion

        #region 属性访问

        public CostFieldData BaseCostField => baseCostField;
        public CostFieldData BlueTeamCostField => blueTeamCostField;
        public CostFieldData RedTeamCostField => redTeamCostField;
        public DirectionFieldData BlueTeamDirectionField => blueTeamDirectionField;
        public DirectionFieldData RedTeamDirectionField => redTeamDirectionField;

        /// <summary>地图中心世界坐标</summary>
        public Vector3 MapCenterPosition => mapCenter != null ? mapCenter.position : Vector3.zero;

        #endregion

        #region 初始化方法

        public void Initialize(int sizeX, int sizeZ, float gridSz)
        {
            mapSizeX = sizeX;
            mapSizeZ = sizeZ;
            gridSize = gridSz;
            baseCostField.Initialize(sizeX, sizeZ);
            blueTeamCostField.Initialize(sizeX, sizeZ);
            redTeamCostField.Initialize(sizeX, sizeZ);
            blueTeamDirectionField.Initialize(sizeX, sizeZ);
            redTeamDirectionField.Initialize(sizeX, sizeZ);
        }

        public void EnsureInitialized()
        {
            if (baseCostField.cells == null || baseCostField.cells.Length == 0)
                Initialize(mapSizeX, mapSizeZ, gridSize);
        }

        #endregion

        #region 坐标转换

        /// <summary>世界坐标转网格坐标</summary>
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

        /// <summary>网格坐标转世界坐标</summary>
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

        /// <summary>网格坐标转世界坐标（Vector3版本）</summary>
        public Vector3 GridToWorld(Vector3 gridPos) => GridToWorld(new Vector2Int((int)gridPos.x, (int)gridPos.z));

        #endregion

        #region 数据访问方法

        public bool IsValidCoord(int x, int z) => x >= 0 && x < mapSizeX && z >= 0 && z < mapSizeZ;
        public bool IsValidCoord(Vector2Int coord) => IsValidCoord(coord.x, coord.y);

        public CostCell GetBaseCell(int x, int z) => baseCostField.GetCell(x, z);
        public CostCell GetBlueTeamCell(int x, int z) => blueTeamCostField.GetCell(x, z);
        public CostCell GetRedTeamCell(int x, int z) => redTeamCostField.GetCell(x, z);

        public void SetBaseCell(int x, int z, CostCell cell) => baseCostField.SetCell(x, z, cell);
        public void SetBlueTeamCell(int x, int z, CostCell cell) => blueTeamCostField.SetCell(x, z, cell);
        public void SetRedTeamCell(int x, int z, CostCell cell) => redTeamCostField.SetCell(x, z, cell);

        public Vector3 GetBlueTeamDirection(int x, int z) => blueTeamDirectionField.GetDirection(x, z);
        public Vector3 GetRedTeamDirection(int x, int z) => redTeamDirectionField.GetDirection(x, z);

        #endregion

        #region 构建方法

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.Button("构建流场数据", Sirenix.OdinInspector.ButtonSizes.Large)]
        [Sirenix.OdinInspector.GUIColor(0.4f, 0.8f, 0.4f)]
        [Sirenix.OdinInspector.PropertyOrder(-1)]
#endif
        public void BuildFlowField()
        {
            EnsureInitialized();

            // 收集障碍物
            var obstacleBounds = CollectObstacles();

            // 构建基础价值场
            BuildBaseCostField(obstacleBounds);

            // 构建队伍价值场和方向场
            BuildTeamFlowField(blueTeamCostField, blueTeamDirectionField, blueLane1, blueLane2, blueLane3);
            BuildTeamFlowField(redTeamCostField, redTeamDirectionField, redLane1, redLane2, redLane3);

            // 输出统计信息以便调试
#if UNITY_EDITOR
            Debug.Log(GetCostFieldStats(blueTeamCostField, "蓝队"));
            Debug.Log(GetCostFieldStats(redTeamCostField, "红队"));

            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssets();
            Debug.Log($"流场构建完成！地图尺寸: {mapSizeX}x{mapSizeZ}, 障碍物: {obstacleBounds.Count}");
#endif
        }

        private string GetCostFieldStats(CostFieldData field, string teamName)
        {
            int min = int.MaxValue, max = 0, reachableCount = 0;
            for (int x = 0; x < mapSizeX; x++)
            {
                for (int z = 0; z < mapSizeZ; z++)
                {
                    var cell = field.GetCell(x, z);
                    if (cell.canMove && cell.baseCost > 0 && cell.baseCost < int.MaxValue)
                    {
                        reachableCount++;
                        if (cell.baseCost < min) min = cell.baseCost;
                        if (cell.baseCost > max) max = cell.baseCost;
                    }
                }
            }
            return $"{teamName}价值场: 可达格子数 {reachableCount}/{mapSizeX * mapSizeZ}, 代价范围 {min} ~ {max}";
        }

        /// <summary>收集场景中的障碍物（返回Collider列表）</summary>
        private List<Collider> CollectObstacles()
        {
            var colliders = new List<Collider>();
            if (obstaclesParent != null)
            {
                var allColliders = obstaclesParent.GetComponentsInChildren<Collider>();
                foreach (var col in allColliders)
                    if (col.CompareTag("Obstacle"))
                        colliders.Add(col);
            }
            return colliders;
        }

        /// <summary>构建基础价值场（障碍物信息）</summary>
        private void BuildBaseCostField(List<Collider> obstacleColliders)
        {
            // 先全部初始化为可通行
            for (int x = 0; x < mapSizeX; x++)
                for (int z = 0; z < mapSizeZ; z++)
                    baseCostField.SetCell(x, z, new CostCell(0, true));

            // 逐个障碍物精确标记
            foreach (var col in obstacleColliders)
                MarkObstacle(col);
        }

        /// <summary>精确标记单个障碍物影响的格子</summary>
        private void MarkObstacle(Collider obstacle)
        {
            Bounds bounds = obstacle.bounds;
            // 计算可能受影响的网格范围（扩展 1 格以避免遗漏边缘格子）
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
                    // 精确判断：格子中心是否在障碍物内部（使用 ClosestPoint）
                    Vector3 closest = obstacle.ClosestPoint(cellCenter);
                    float dist = Vector3.Distance(cellCenter, closest);

                    // 如果距离小于一个很小的阈值，说明中心点在障碍物内部
                    if (dist < 0.01f)
                    {
                        baseCostField.SetMovable(x, z, false);
                        continue;
                    }

                    // 影响半径处理：如果格子中心到障碍物的最近距离小于影响半径，也设为不可通行
                    if (obstacleInfluenceRadius > 0 && dist < obstacleInfluenceRadius * gridSize)
                    {
                        baseCostField.SetMovable(x, z, false);
                    }
                }
            }
        }

        /// <summary>构建队伍流场（价值场 + 方向场）</summary>
        private void BuildTeamFlowField(CostFieldData costField, DirectionFieldData directionField,
            LanePathConfig lane1, LanePathConfig lane2, LanePathConfig lane3)
        {
            // 初始化价值场
            for (int x = 0; x < mapSizeX; x++)
                for (int z = 0; z < mapSizeZ; z++)
                {
                    var baseCell = baseCostField.GetCell(x, z);
                    costField.SetCell(x, z, new CostCell(baseCell.canMove ? int.MaxValue : -1, baseCell.canMove));
                }

            // 收集有效兵线
            var lanes = new List<LanePathConfig>();
            if (lane1.IsValid) lanes.Add(lane1);
            if (lane2.IsValid) lanes.Add(lane2);
            if (lane3.IsValid) lanes.Add(lane3);

            if (lanes.Count == 0)
            {
                Debug.LogWarning("未找到有效兵线，队伍流场将保持初始值（全部不可达）");
                return;
            }

            // 为每条兵线构建独立的价值场
            var laneCostFields = new List<CostFieldData>();
            var laneGridPaths = new List<List<Vector2Int>>();

            foreach (var lane in lanes)
            {
                var laneCost = new CostFieldData(mapSizeX, mapSizeZ);
                var gridPath = BuildLaneCostField(laneCost, lane.GetWorldPositions());
                if (gridPath.Count < 2)
                {
                    Debug.LogWarning($"兵线 {lane.pathName} 的有效网格路径点不足2个，已跳过");
                    continue;
                }
                laneCostFields.Add(laneCost);
                laneGridPaths.Add(gridPath);
            }

            if (laneCostFields.Count == 0)
            {
                Debug.LogWarning("所有兵线均无法生成有效网格路径，队伍流场将保持初始值");
                return;
            }

            // 融合多条兵线的价值场
            MergeLaneCostFields(costField, laneCostFields, laneGridPaths);

            // 计算方向场
            CalculateDirectionField(costField, directionField);

            // 障碍物边缘对齐
            if (enableObstacleEdgeAlignment)
                AlignDirectionNearObstacles(directionField, costField, laneGridPaths);

            // 平滑方向场
            SmoothDirectionField(directionField, costField, directionSmoothingIterations);
        }

        /// <summary>构建单条兵线的价值场</summary>
        private List<Vector2Int> BuildLaneCostField(CostFieldData costField, List<Vector3> worldPath)
        {
            // 初始化所有格子
            for (int x = 0; x < mapSizeX; x++)
                for (int z = 0; z < mapSizeZ; z++)
                {
                    var baseCell = baseCostField.GetCell(x, z);
                    costField.SetCell(x, z, new CostCell(baseCell.canMove ? int.MaxValue : -1, baseCell.canMove));
                }

            var gridPath = worldPath.Select(p => WorldToGridCoord(p)).Where(c => IsValidCoord(c)).ToList();
            if (gridPath.Count < 2) return gridPath;

            // 确保终点可移动，若不可移动则寻找最近的可移动格子作为新终点
            var goal = gridPath[gridPath.Count - 1];
            if (!costField.GetCell(goal.x, goal.y).canMove)
            {
                var newGoal = FindNearestMovable(goal, costField);
                if (newGoal.HasValue)
                {
                    Debug.LogWarning($"兵线终点 {goal} 不可移动，已自动修正为最近可移动点 {newGoal.Value}");
                    gridPath[gridPath.Count - 1] = newGoal.Value;
                }
                else
                {
                    Debug.LogError($"兵线终点 {goal} 周围无可移动格子，无法构建价值场");
                    return new List<Vector2Int>();
                }
            }

            BuildCostFieldWithImprovedDijkstra(costField, gridPath);
            return gridPath;
        }

        /// <summary>寻找最近的可移动格子</summary>
        private Vector2Int? FindNearestMovable(Vector2Int start, CostFieldData costField, int maxRadius = 10)
        {
            for (int r = 1; r <= maxRadius; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dz = -r; dz <= r; dz++)
                    {
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dz) != r) continue; // 只检查曼哈顿距离为r的边界
                        int nx = start.x + dx, nz = start.y + dz;
                        if (IsValidCoord(nx, nz) && costField.GetCell(nx, nz).canMove)
                            return new Vector2Int(nx, nz);
                    }
                }
            }
            return null;
        }

        /// <summary>改进的Dijkstra算法构建价值场</summary>
        private void BuildCostFieldWithImprovedDijkstra(CostFieldData costField, List<Vector2Int> lanePath)
        {
            var goal = lanePath[lanePath.Count - 1];
            var skeletonCost = BuildSkeletonCost(lanePath);

            var open = new List<(int cost, int x, int z)>();
            void Push(int c, int x, int z)
            {
                int lo = 0, hi = open.Count;
                while (lo < hi) { int mid = (lo + hi) >> 1; if (c < open[mid].cost) hi = mid; else lo = mid + 1; }
                open.Insert(lo, (c, x, z));
            }

            costField.SetBaseCost(goal.x, goal.y, 0);
            Push(0, goal.x, goal.y);

            var directions = new Vector2Int[] {
                new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
                new Vector2Int(1, 1), new Vector2Int(-1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1)
            };

            while (open.Count > 0)
            {
                var cur = open[0];
                open.RemoveAt(0);

                int cx = cur.x, cz = cur.z;
                int curCost = costField.GetCell(cx, cz).baseCost;
                if (curCost < cur.cost) continue;

                foreach (var d in directions)
                {
                    int nx = cx + d.x, nz = cz + d.y;
                    if (!IsValidCoord(nx, nz)) continue;

                    var neighborCell = costField.GetCell(nx, nz);
                    if (!neighborCell.canMove) continue;

                    int step = (d.x != 0 && d.y != 0) ? 14 : 10;
                    float laneDist = ImprovedDistanceToLane(lanePath, nx, nz);

                    int skeletonBonus = skeletonCost.TryGetValue(new Vector2Int(nx, nz), out int skCost) ? skCost : 0;
                    int penalty = Mathf.RoundToInt(laneDist * corridorPenaltyPerCell) + skeletonBonus;
                    // 确保惩罚非负，避免因骨架奖励导致负数
                    penalty = Mathf.Max(0, penalty);

                    // 防止整数溢出：如果 curCost 或 step 太大，跳过
                    if (curCost > int.MaxValue - step - penalty) continue;

                    int newCost = curCost + step + penalty;

                    if (newCost < neighborCell.baseCost)
                    {
                        costField.SetBaseCost(nx, nz, newCost);
                        Push(newCost, nx, nz);
                    }
                }
            }
        }

        /// <summary>构建兵线骨架代价</summary>
        private Dictionary<Vector2Int, int> BuildSkeletonCost(List<Vector2Int> lanePath)
        {
            var skeleton = new Dictionary<Vector2Int, int>();
            for (int i = 0; i < lanePath.Count; i++)
            {
                skeleton[lanePath[i]] = -20;
                for (int dx = -1; dx <= 1; dx++)
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        if (dx == 0 && dz == 0) continue;
                        var neighbor = lanePath[i] + new Vector2Int(dx, dz);
                        if (!skeleton.ContainsKey(neighbor))
                            skeleton[neighbor] = -10;
                    }
            }
            return skeleton;
        }

        /// <summary>改进的兵线距离计算</summary>
        private float ImprovedDistanceToLane(List<Vector2Int> lanePath, int x, int z)
        {
            if (lanePath == null || lanePath.Count < 2) return float.PositiveInfinity;

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
                if (d < minDist) minDist = d;
            }
            return minDist;
        }

        /// <summary>融合多条兵线的价值场</summary>
        private void MergeLaneCostFields(CostFieldData result, List<CostFieldData> laneCosts, List<List<Vector2Int>> lanePaths)
        {
            for (int x = 0; x < mapSizeX; x++)
            {
                for (int z = 0; z < mapSizeZ; z++)
                {
                    var baseCell = baseCostField.GetCell(x, z);
                    if (!baseCell.canMove) { result.SetCell(x, z, new CostCell(-1, false)); continue; }

                    float minDist = float.PositiveInfinity;
                    int bestLane = 0;

                    for (int i = 0; i < lanePaths.Count; i++)
                    {
                        float dist = ImprovedDistanceToLane(lanePaths[i], x, z);
                        if (dist < minDist) { minDist = dist; bestLane = i; }
                    }

                    int cost = laneCosts[bestLane].GetCell(x, z).baseCost;

                    if (pathMergeWeight > 0 && laneCosts.Count > 1)
                    {
                        float totalWeight = 1f;
                        float weightedCost = cost;

                        for (int i = 0; i < laneCosts.Count; i++)
                        {
                            if (i == bestLane) continue;
                            float dist = ImprovedDistanceToLane(lanePaths[i], x, z);
                            float weight = Mathf.Exp(-dist * pathMergeWeight);
                            int otherCost = laneCosts[i].GetCell(x, z).baseCost;
                            if (otherCost > 0 && otherCost < int.MaxValue)
                            {
                                weightedCost += otherCost * weight;
                                totalWeight += weight;
                            }
                        }
                        cost = Mathf.RoundToInt(weightedCost / totalWeight);
                    }

                    result.SetCell(x, z, new CostCell(cost, true));
                }
            }
        }

        /// <summary>计算方向场</summary>
        private void CalculateDirectionField(CostFieldData costField, DirectionFieldData directionField)
        {
            for (int x = 0; x < mapSizeX; x++)
            {
                for (int z = 0; z < mapSizeZ; z++)
                {
                    var cell = costField.GetCell(x, z);
                    if (!cell.canMove || cell.baseCost <= 0 || cell.baseCost >= int.MaxValue)
                    {
                        directionField.SetDirection(x, z, Vector3.zero);
                        continue;
                    }

                    int lowest = cell.baseCost;
                    Vector3 bestDir = Vector3.zero;

                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            if (dx == 0 && dz == 0) continue;
                            int nx = x + dx, nz = z + dz;
                            if (!IsValidCoord(nx, nz)) continue;

                            var neighbor = costField.GetCell(nx, nz);
                            if (!neighbor.canMove || neighbor.baseCost < 0 || neighbor.baseCost >= int.MaxValue) continue;

                            if (neighbor.baseCost < lowest)
                            {
                                lowest = neighbor.baseCost;
                                bestDir = new Vector3(dx, 0, dz).normalized;
                            }
                        }
                    }
                    directionField.SetDirection(x, z, bestDir);
                }
            }
        }

        /// <summary>障碍物边缘方向对齐</summary>
        private void AlignDirectionNearObstacles(DirectionFieldData directionField, CostFieldData costField, List<List<Vector2Int>> lanePaths)
        {
            var adjustments = new List<(int x, int z, Vector3 newDir)>();

            for (int x = 0; x < mapSizeX; x++)
            {
                for (int z = 0; z < mapSizeZ; z++)
                {
                    var cell = costField.GetCell(x, z);
                    if (!cell.canMove) continue;

                    Vector3 obstacleNormal = Vector3.zero;
                    int obstacleCount = 0;

                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            if (dx == 0 && dz == 0) continue;
                            int nx = x + dx, nz = z + dz;
                            if (!IsValidCoord(nx, nz)) continue;

                            var neighbor = costField.GetCell(nx, nz);
                            if (!neighbor.canMove)
                            {
                                obstacleNormal += new Vector3(dx, 0, dz);
                                obstacleCount++;
                            }
                        }
                    }

                    if (obstacleCount > 0)
                    {
                        obstacleNormal.Normalize();
                        var parallelDir = Vector3.Cross(Vector3.up, obstacleNormal).normalized;
                        var laneDir = GetLaneDirectionAt(new Vector2Int(x, z), lanePaths);

                        if (Vector3.Dot(parallelDir, laneDir) < 0)
                            parallelDir = -parallelDir;

                        var currentDir = directionField.GetDirection(x, z);
                        var blendedDir = Vector3.Lerp(currentDir, parallelDir, obstacleEdgeSmoothStrength);

                        if (blendedDir.sqrMagnitude > 0.01f)
                            adjustments.Add((x, z, blendedDir.normalized));
                    }
                }
            }

            foreach (var adj in adjustments)
                directionField.SetDirection(adj.x, adj.z, adj.newDir);
        }

        /// <summary>获取指定位置的兵线方向</summary>
        private Vector3 GetLaneDirectionAt(Vector2Int pos, List<List<Vector2Int>> lanePaths)
        {
            Vector3 bestDir = Vector3.forward;
            float minDist = float.PositiveInfinity;

            foreach (var path in lanePaths)
            {
                if (path.Count < 2) continue;

                for (int i = 0; i < path.Count - 1; i++)
                {
                    var a = new Vector2(path[i].x, path[i].y);
                    var b = new Vector2(path[i + 1].x, path[i + 1].y);
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
                        if (dir.sqrMagnitude > 0.01f) bestDir = dir;
                    }
                }
            }
            return bestDir;
        }

        /// <summary>平滑方向场</summary>
        private void SmoothDirectionField(DirectionFieldData directionField, CostFieldData costField, int iterations)
        {
            for (int iter = 0; iter < iterations; iter++)
            {
                var tempDirs = new Vector3[mapSizeX, mapSizeZ];

                for (int x = 0; x < mapSizeX; x++)
                    for (int z = 0; z < mapSizeZ; z++)
                        tempDirs[x, z] = directionField.GetDirection(x, z);

                for (int x = 0; x < mapSizeX; x++)
                {
                    for (int z = 0; z < mapSizeZ; z++)
                    {
                        var cell = costField.GetCell(x, z);
                        if (!cell.canMove) continue;

                        var avgDir = tempDirs[x, z];
                        float totalWeight = 1f;

                        for (int dx = -1; dx <= 1; dx++)
                        {
                            for (int dz = -1; dz <= 1; dz++)
                            {
                                if (dx == 0 && dz == 0) continue;
                                int nx = x + dx, nz = z + dz;
                                if (!IsValidCoord(nx, nz)) continue;

                                var neighbor = costField.GetCell(nx, nz);
                                if (!neighbor.canMove) continue;

                                var neighborDir = tempDirs[nx, nz];
                                if (neighborDir.sqrMagnitude > 0.01f)
                                {
                                    float weight = (dx != 0 && dz != 0) ? 0.7f : 1f;
                                    avgDir += neighborDir * weight;
                                    totalWeight += weight;
                                }
                            }
                        }

                        if (totalWeight > 1f && avgDir.sqrMagnitude > 0.01f)
                            directionField.SetDirection(x, z, (avgDir / totalWeight).normalized);
                    }
                }
            }
        }

        #endregion

        #region 数据拷贝

        public CostFieldData CreateRuntimeCostField(bool isBlueTeam)
            => (isBlueTeam ? blueTeamCostField : redTeamCostField).Clone();

        public CostFieldData CreateRuntimeBaseCostField() => baseCostField.Clone();

        #endregion
    }
}
