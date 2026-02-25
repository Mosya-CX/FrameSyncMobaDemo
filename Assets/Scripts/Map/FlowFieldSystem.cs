using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Sirenix.OdinInspector;

namespace FlowField
{
    /// <summary>
    /// 流场运行时系统
    /// 存储运行时数据，支持动态计算路径和修改格子价值
    /// 包含完整的可视化功能
    /// </summary>
    public class FlowFieldSystem : MonoSingleton<FlowFieldSystem>
    {
        #region 配置

#if ODIN_INSPECTOR
        [FoldoutGroup("流场配置")]
        [LabelText("流场构建器")]
        [Required("请设置流场构建器")]
        [InfoBox("流场构建器包含预计算的价值场和方向场数据", Sirenix.OdinInspector.InfoMessageType.Info)]
#endif
        public FlowFieldBuilder flowFieldBuilder;

#if ODIN_INSPECTOR
        [FoldoutGroup("流场配置")]
        [LabelText("自动初始化")]
#endif
        public bool autoInitialize = true;

#if ODIN_INSPECTOR
        [FoldoutGroup("运行时设置")]
        [LabelText("使用插值获取方向")]
#endif
        public bool useInterpolation = true;

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.FoldoutGroup("运行时设置")]
        [Sirenix.OdinInspector.LabelText("方向平滑迭代次数")]
        [Range(0, 5)]
#endif
        public int directionSmoothingIterations = 1;

        #endregion

        #region 可视化配置

#if ODIN_INSPECTOR
        [FoldoutGroup("可视化配置")]
        [LabelText("显示网格")]
#endif
        public bool showGrid = true;

#if ODIN_INSPECTOR
        [FoldoutGroup("可视化配置")]
        [LabelText("显示障碍物")]
#endif
        public bool showObstacles = true;

#if ODIN_INSPECTOR
        [FoldoutGroup("可视化配置")]
        [LabelText("显示兵线路径")]
#endif
        public bool showLanePaths = true;

#if ODIN_INSPECTOR
        [FoldoutGroup("可视化配置")]
        [LabelText("显示蓝队流场")]
#endif
        public bool showBlueTeamFlow = true;

#if ODIN_INSPECTOR
        [FoldoutGroup("可视化配置")]
        [LabelText("显示红队流场")]
#endif
        public bool showRedTeamFlow = false;

#if ODIN_INSPECTOR
        [FoldoutGroup("可视化配置")]
        [LabelText("显示价值场热力图")]
#endif
        public bool showCostHeatmap = false;

#if ODIN_INSPECTOR
        [FoldoutGroup("可视化配置")]
        [LabelText("向量缩放")]
        [Range(0.1f, 2f)]
#endif
        public float vectorScale = 0.5f;

#if ODIN_INSPECTOR
        [FoldoutGroup("可视化配置")]
        [LabelText("流场采样间隔")]
        [Range(1, 5)]
#endif
        public int flowSampleInterval = 2;

#if ODIN_INSPECTOR
        [FoldoutGroup("可视化配置")]
        [LabelText("关键点大小")]
        [Range(0.1f, 1f)]
#endif
        public float waypointSize = 0.3f;

        #endregion

        #region 运行时数据

        private FlowFieldCell[,] blueTeamRuntimeField;
        private FlowFieldCell[,] redTeamRuntimeField;
        private Vector2Int? blueTeamTarget;
        private Vector2Int? redTeamTarget;
        private ModifierManager modifierManager = new ModifierManager();
        private bool isInitialized = false;

        public int MapSizeX => flowFieldBuilder?.mapSizeX ?? 0;
        public int MapSizeZ => flowFieldBuilder?.mapSizeZ ?? 0;
        public float GridSize => flowFieldBuilder?.gridSize ?? 1f;

        #endregion

        #region 初始化

        private void Start()
        {
            if (autoInitialize) Initialize();
        }

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.Button("初始化系统", Sirenix.OdinInspector.ButtonSizes.Medium)]
        [Sirenix.OdinInspector.GUIColor(0.4f, 0.8f, 0.4f)]
        [Sirenix.OdinInspector.PropertyOrder(-1)]
#endif
        public void Initialize()
        {
            if (flowFieldBuilder == null)
            {
                Debug.LogError("FlowFieldBuilder 未设置！");
                return;
            }

            flowFieldBuilder.EnsureInitialized();

            int sizeX = flowFieldBuilder.mapSizeX;
            int sizeZ = flowFieldBuilder.mapSizeZ;

            blueTeamRuntimeField = new FlowFieldCell[sizeX, sizeZ];
            redTeamRuntimeField = new FlowFieldCell[sizeX, sizeZ];

            CopyFromBuilder(blueTeamRuntimeField, true);
            CopyFromBuilder(redTeamRuntimeField, false);

            isInitialized = true;
            Debug.Log($"FlowFieldSystem 初始化完成，地图尺寸: {sizeX}x{sizeZ}");
        }

        private void CopyFromBuilder(FlowFieldCell[,] runtimeField, bool isBlueTeam)
        {
            int sizeX = flowFieldBuilder.mapSizeX;
            int sizeZ = flowFieldBuilder.mapSizeZ;

            var costField = isBlueTeam ? flowFieldBuilder.BlueTeamCostField : flowFieldBuilder.RedTeamCostField;
            var dirField = isBlueTeam ? flowFieldBuilder.BlueTeamDirectionField : flowFieldBuilder.RedTeamDirectionField;

            for (int x = 0; x < sizeX; x++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    var costCell = costField.GetCell(x, z);
                    var direction = dirField.GetDirection(x, z);
                    runtimeField[x, z] = new FlowFieldCell(costCell.baseCost, costCell.canMove) { direction = direction };
                }
            }
        }

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.Button("重置运行时数据", Sirenix.OdinInspector.ButtonSizes.Small)]
#endif
        public void ResetRuntimeData()
        {
            if (!isInitialized) return;
            CopyFromBuilder(blueTeamRuntimeField, true);
            CopyFromBuilder(redTeamRuntimeField, false);
            blueTeamTarget = null;
            redTeamTarget = null;
            modifierManager.ClearNonPersistent();
        }

        #endregion

        #region 修改器管理

#if ODIN_INSPECTOR
        [Sirenix.OdinInspector.Button("清除所有修改器", Sirenix.OdinInspector.ButtonSizes.Small)]
#endif
        public void ClearAllModifiers()
        {
            modifierManager.ClearAll();
            ResetRuntimeData();
        }

        public int AddModifier(FlowFieldModifier modifier)
        {
            int id = modifierManager.AddModifier(modifier);
            ApplyModifier(modifier);
            RecalculateDirectionField(modifier, true);
            RecalculateDirectionField(modifier, false);
            return id;
        }

        public bool RemoveModifier(int id)
        {
            var modifier = modifierManager.GetModifier(id);
            if (modifier != null)
            {
                RevertModifier(modifier);
                bool removed = modifierManager.RemoveModifier(id);
                if (removed)
                {
                    RecalculateDirectionField(modifier, true);
                    RecalculateDirectionField(modifier, false);
                }
                return removed;
            }
            return false;
        }

        public FlowFieldModifier GetModifier(int id) => modifierManager.GetModifier(id);
        public List<FlowFieldModifier> GetAllModifiers() => modifierManager.GetAllModifiers();

        private void ApplyModifier(FlowFieldModifier modifier)
        {
            if (!isInitialized || !modifier.enabled) return;
            var affectedCells = modifier.GetAffectedCells(MapSizeX, MapSizeZ);

            foreach (var cell in affectedCells)
            {
                var blueCell = blueTeamRuntimeField[cell.x, cell.y];
                modifier.ApplyToCell(ref blueCell);
                blueTeamRuntimeField[cell.x, cell.y] = blueCell;

                var redCell = redTeamRuntimeField[cell.x, cell.y];
                modifier.ApplyToCell(ref redCell);
                redTeamRuntimeField[cell.x, cell.y] = redCell;
            }
        }

        private void RevertModifier(FlowFieldModifier modifier)
        {
            if (!isInitialized) return;
            var affectedCells = modifier.GetAffectedCells(MapSizeX, MapSizeZ);

            foreach (var cell in affectedCells)
            {
                var baseCell = flowFieldBuilder.GetBaseCell(cell.x, cell.y);

                var blueCell = blueTeamRuntimeField[cell.x, cell.y];
                blueCell.baseCost = baseCell.baseCost;
                blueCell.canMove = baseCell.canMove;
                blueCell.dynamicCost = 0;
                blueTeamRuntimeField[cell.x, cell.y] = blueCell;

                var redCell = redTeamRuntimeField[cell.x, cell.y];
                redCell.baseCost = baseCell.baseCost;
                redCell.canMove = baseCell.canMove;
                redCell.dynamicCost = 0;
                redTeamRuntimeField[cell.x, cell.y] = redCell;
            }

            foreach (var otherModifier in modifierManager.GetAllModifiers())
                if (otherModifier.id != modifier.id)
                    ApplyModifier(otherModifier);
        }

        private void RecalculateDirectionField(FlowFieldModifier modifier, bool isBlueTeam)
        {
            var runtimeField = isBlueTeam ? blueTeamRuntimeField : redTeamRuntimeField;
            var dirField = isBlueTeam ? flowFieldBuilder.BlueTeamDirectionField : flowFieldBuilder.RedTeamDirectionField;
            var affectedCells = modifier.GetAffectedCells(MapSizeX, MapSizeZ);

            foreach (var cell in affectedCells)
            {
                if (runtimeField[cell.x, cell.y].canMove)
                    runtimeField[cell.x, cell.y].direction = dirField.GetDirection(cell.x, cell.y);
                else
                    runtimeField[cell.x, cell.y].direction = Vector3.zero;
            }
        }

        #endregion

        #region 目标点设置与路径计算

        public void SetTarget(Vector2Int target, bool isBlueTeam)
        {
            if (!isInitialized) return;
            if (!flowFieldBuilder.IsValidCoord(target))
            {
                Debug.LogWarning($"目标点 {target} 超出地图范围");
                return;
            }

            // 确保目标点可移动，否则寻找最近可移动点
            var runtimeField = isBlueTeam ? blueTeamRuntimeField : redTeamRuntimeField;
            if (!runtimeField[target.x, target.y].canMove)
            {
                var newTarget = FindNearestMovable(target, runtimeField);
                if (newTarget.HasValue)
                {
                    Debug.LogWarning($"目标点 {target} 不可移动，已自动修正为最近可移动点 {newTarget.Value}");
                    target = newTarget.Value;
                }
                else
                {
                    Debug.LogError($"目标点 {target} 周围无可移动格子，无法设置目标");
                    return;
                }
            }

            if (isBlueTeam)
            {
                blueTeamTarget = target;
                CalculateDirectionField(blueTeamRuntimeField, target);
            }
            else
            {
                redTeamTarget = target;
                CalculateDirectionField(redTeamRuntimeField, target);
            }
        }

        private Vector2Int? FindNearestMovable(Vector2Int start, FlowFieldCell[,] runtimeField, int maxRadius = 10)
        {
            for (int r = 1; r <= maxRadius; r++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    for (int dz = -r; dz <= r; dz++)
                    {
                        if (Mathf.Abs(dx) != r && Mathf.Abs(dz) != r) continue;
                        int nx = start.x + dx, nz = start.y + dz;
                        if (flowFieldBuilder.IsValidCoord(nx, nz) && runtimeField[nx, nz].canMove)
                            return new Vector2Int(nx, nz);
                    }
                }
            }
            return null;
        }

        public void SetTargetWorld(Vector3 worldPos, bool isBlueTeam)
        {
            var gridPos = flowFieldBuilder.WorldToGridCoord(worldPos);
            SetTarget(gridPos, isBlueTeam);
        }

        private void CalculateDirectionField(FlowFieldCell[,] runtimeField, Vector2Int target)
        {
            int sizeX = MapSizeX, sizeZ = MapSizeZ;

            // 重置整合代价
            for (int x = 0; x < sizeX; x++)
                for (int z = 0; z < sizeZ; z++)
                {
                    var cell = runtimeField[x, z];
                    cell.integratedCost = cell.canMove ? int.MaxValue : -1;
                    runtimeField[x, z] = cell;
                }

            // Dijkstra扩散
            var open = new List<(int cost, int x, int z)>();
            void Push(int c, int x, int z)
            {
                int lo = 0, hi = open.Count;
                while (lo < hi) { int mid = (lo + hi) >> 1; if (c < open[mid].cost) hi = mid; else lo = mid + 1; }
                open.Insert(lo, (c, x, z));
            }

            var targetCell = runtimeField[target.x, target.y];
            targetCell.integratedCost = 0;
            runtimeField[target.x, target.y] = targetCell;
            Push(0, target.x, target.y);

            var directions = new Vector2Int[] {
                new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1),
                new Vector2Int(1, 1), new Vector2Int(-1, 1), new Vector2Int(1, -1), new Vector2Int(-1, -1)
            };

            while (open.Count > 0)
            {
                var cur = open[0];
                open.RemoveAt(0);

                int cx = cur.x, cz = cur.z;
                int curCost = runtimeField[cx, cz].integratedCost;
                if (curCost < cur.cost) continue;

                foreach (var d in directions)
                {
                    int nx = cx + d.x, nz = cz + d.y;
                    if (!flowFieldBuilder.IsValidCoord(nx, nz)) continue;

                    var neighborCell = runtimeField[nx, nz];
                    if (!neighborCell.canMove) continue;

                    int step = (d.x != 0 && d.y != 0) ? 14 : 10;
                    // 防止溢出：如果邻居总代价过大，则跳过
                    if (neighborCell.totalCost >= int.MaxValue - step - curCost) continue;

                    int newCost = curCost + step + neighborCell.totalCost;

                    if (newCost < neighborCell.integratedCost && newCost >= 0)
                    {
                        var updatedCell = runtimeField[nx, nz];
                        updatedCell.integratedCost = newCost;
                        runtimeField[nx, nz] = updatedCell;
                        Push(newCost, nx, nz);
                    }
                }
            }

            // 计算方向
            for (int x = 0; x < sizeX; x++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    var cell = runtimeField[x, z];
                    if (!cell.canMove) continue;

                    int lowest = cell.integratedCost;
                    Vector3 bestDir = Vector3.zero;

                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            if (dx == 0 && dz == 0) continue;
                            int nx = x + dx, nz = z + dz;
                            if (!flowFieldBuilder.IsValidCoord(nx, nz)) continue;

                            var neighbor = runtimeField[nx, nz];
                            if (neighbor.integratedCost < 0 || neighbor.integratedCost >= int.MaxValue) continue;

                            if (neighbor.integratedCost < lowest)
                            {
                                lowest = neighbor.integratedCost;
                                bestDir = new Vector3(dx, 0, dz).normalized;
                            }
                        }
                    }

                    cell.direction = bestDir;
                    runtimeField[x, z] = cell;
                }
            }

            SmoothDirectionField(runtimeField, directionSmoothingIterations);
        }

        private void SmoothDirectionField(FlowFieldCell[,] runtimeField, int iterations)
        {
            int sizeX = MapSizeX, sizeZ = MapSizeZ;

            for (int iter = 0; iter < iterations; iter++)
            {
                var tempDirs = new Vector3[sizeX, sizeZ];
                for (int x = 0; x < sizeX; x++)
                    for (int z = 0; z < sizeZ; z++)
                        tempDirs[x, z] = runtimeField[x, z].direction;

                for (int x = 0; x < sizeX; x++)
                {
                    for (int z = 0; z < sizeZ; z++)
                    {
                        var cell = runtimeField[x, z];
                        if (!cell.canMove) continue;

                        var avgDir = tempDirs[x, z];
                        float totalWeight = 1f;

                        for (int dx = -1; dx <= 1; dx++)
                        {
                            for (int dz = -1; dz <= 1; dz++)
                            {
                                if (dx == 0 && dz == 0) continue;
                                int nx = x + dx, nz = z + dz;
                                if (!flowFieldBuilder.IsValidCoord(nx, nz)) continue;

                                var neighbor = runtimeField[nx, nz];
                                if (!neighbor.canMove || neighbor.direction.sqrMagnitude < 0.01f) continue;

                                float weight = (dx != 0 && dz != 0) ? 0.7f : 1f;
                                avgDir += neighbor.direction * weight;
                                totalWeight += weight;
                            }
                        }

                        if (totalWeight > 1f && avgDir.sqrMagnitude > 0.01f)
                        {
                            cell.direction = (avgDir / totalWeight).normalized;
                            runtimeField[x, z] = cell;
                        }
                    }
                }
            }
        }

        #endregion

        #region 路径查询

        public Vector3 GetMoveDirection(Vector2Int gridPos, bool isBlueTeam)
        {
            if (!isInitialized || !flowFieldBuilder.IsValidCoord(gridPos)) return Vector3.zero;
            var runtimeField = isBlueTeam ? blueTeamRuntimeField : redTeamRuntimeField;
            return runtimeField[gridPos.x, gridPos.y].direction;
        }

        public Vector3 GetMoveDirectionWorld(Vector3 worldPos, bool isBlueTeam)
        {
            if (!isInitialized || flowFieldBuilder == null) return Vector3.zero;
            var gridPos = flowFieldBuilder.WorldToGridCoord(worldPos);
            if (!flowFieldBuilder.IsValidCoord(gridPos)) return Vector3.zero;

            var runtimeField = isBlueTeam ? blueTeamRuntimeField : redTeamRuntimeField;
            if (!useInterpolation) return runtimeField[gridPos.x, gridPos.y].direction;
            return GetInterpolatedDirection(worldPos, runtimeField);
        }

        private Vector3 GetInterpolatedDirection(Vector3 worldPos, FlowFieldCell[,] runtimeField)
        {
            var center = flowFieldBuilder.MapCenterPosition;
            float offsetX = MapSizeX * GridSize * 0.5f;
            float offsetZ = MapSizeZ * GridSize * 0.5f;

            float gx = (worldPos.x - center.x + offsetX) / GridSize;
            float gz = (worldPos.z - center.z + offsetZ) / GridSize;

            int x0 = Mathf.FloorToInt(gx), z0 = Mathf.FloorToInt(gz);
            float localX = gx - x0, localZ = gz - z0;

            var d00 = GetDirectionSafe(runtimeField, x0, z0);
            var d10 = GetDirectionSafe(runtimeField, x0 + 1, z0);
            var d01 = GetDirectionSafe(runtimeField, x0, z0 + 1);
            var d11 = GetDirectionSafe(runtimeField, x0 + 1, z0 + 1);

            var dx0 = Vector3.Lerp(d00, d10, localX);
            var dx1 = Vector3.Lerp(d01, d11, localX);
            return Vector3.Lerp(dx0, dx1, localZ).normalized;
        }

        private Vector3 GetDirectionSafe(FlowFieldCell[,] runtimeField, int x, int z)
        {
            if (x >= 0 && x < MapSizeX && z >= 0 && z < MapSizeZ)
                return runtimeField[x, z].direction;
            return Vector3.zero;
        }

        public List<Vector3> BuildPath(Vector3 startWorld, Vector3 targetWorld, bool isBlueTeam)
        {
            var path = new List<Vector3>();
            if (!isInitialized) return path;

            var startGrid = flowFieldBuilder.WorldToGridCoord(startWorld);
            var targetGrid = flowFieldBuilder.WorldToGridCoord(targetWorld);

            SetTarget(targetGrid, isBlueTeam);
            var runtimeField = isBlueTeam ? blueTeamRuntimeField : redTeamRuntimeField;

            var current = startGrid;
            int maxSteps = MapSizeX * MapSizeZ, steps = 0;

            while (steps < maxSteps)
            {
                path.Add(flowFieldBuilder.GridToWorld(current));
                if (current == targetGrid) break;

                var cell = runtimeField[current.x, current.y];
                if (cell.direction.sqrMagnitude < 0.01f) break;

                int nextX = current.x + Mathf.RoundToInt(cell.direction.x);
                int nextZ = current.y + Mathf.RoundToInt(cell.direction.z);
                if (!flowFieldBuilder.IsValidCoord(nextX, nextZ)) break;

                current = new Vector2Int(nextX, nextZ);
                steps++;
            }

            return path;
        }

        public List<Vector2Int> BuildGridPath(Vector2Int start, Vector2Int target, bool isBlueTeam)
        {
            var path = new List<Vector2Int>();
            if (!isInitialized) return path;

            SetTarget(target, isBlueTeam);
            var runtimeField = isBlueTeam ? blueTeamRuntimeField : redTeamRuntimeField;

            var current = start;
            int maxSteps = MapSizeX * MapSizeZ, steps = 0;

            while (steps < maxSteps)
            {
                path.Add(current);
                if (current == target) break;

                var cell = runtimeField[current.x, current.y];
                if (cell.direction.sqrMagnitude < 0.01f) break;

                int nextX = current.x + Mathf.RoundToInt(cell.direction.x);
                int nextZ = current.y + Mathf.RoundToInt(cell.direction.z);
                if (!flowFieldBuilder.IsValidCoord(nextX, nextZ)) break;

                current = new Vector2Int(nextX, nextZ);
                steps++;
            }

            return path;
        }

        #endregion

        #region 格子信息查询

        public bool IsCellMovable(Vector2Int gridPos, bool isBlueTeam)
        {
            if (!isInitialized || !flowFieldBuilder.IsValidCoord(gridPos)) return false;
            return (isBlueTeam ? blueTeamRuntimeField : redTeamRuntimeField)[gridPos.x, gridPos.y].canMove;
        }

        public int GetCellTotalCost(Vector2Int gridPos, bool isBlueTeam)
        {
            if (!isInitialized || !flowFieldBuilder.IsValidCoord(gridPos)) return int.MaxValue;
            return (isBlueTeam ? blueTeamRuntimeField : redTeamRuntimeField)[gridPos.x, gridPos.y].totalCost;
        }

        public int GetCellIntegratedCost(Vector2Int gridPos, bool isBlueTeam)
        {
            if (!isInitialized || !flowFieldBuilder.IsValidCoord(gridPos)) return int.MaxValue;
            return (isBlueTeam ? blueTeamRuntimeField : redTeamRuntimeField)[gridPos.x, gridPos.y].integratedCost;
        }

        #endregion

        #region 可视化

        private void OnDrawGizmos()
        {
            if (!isInitialized || flowFieldBuilder == null) return;

            int sizeX = MapSizeX, sizeZ = MapSizeZ;
            float gridSz = GridSize;
            var center = flowFieldBuilder.MapCenterPosition;

            // 绘制网格
            if (showGrid) DrawGrid(center, sizeX, sizeZ, gridSz);

            // 绘制障碍物
            if (showObstacles) DrawObstacles(sizeX, sizeZ, gridSz);

            // 绘制兵线路径
            if (showLanePaths) DrawLanePaths();

            // 绘制流场方向
            if (showBlueTeamFlow && blueTeamRuntimeField != null)
                DrawFlowField(blueTeamRuntimeField, sizeX, sizeZ, gridSz, new Color(0.2f, 0.5f, 1f, 0.8f));

            if (showRedTeamFlow && redTeamRuntimeField != null)
                DrawFlowField(redTeamRuntimeField, sizeX, sizeZ, gridSz, new Color(1f, 0.4f, 0.4f, 0.8f));

            // 绘制价值场热力图
            if (showCostHeatmap) DrawCostHeatmap(sizeX, sizeZ, gridSz);
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
                    if (!flowFieldBuilder.GetBaseCell(x, z).canMove)
                    {
                        var pos = flowFieldBuilder.GridToWorld(new Vector2Int(x, z));
                        Gizmos.DrawCube(pos, new Vector3(gridSz * 0.9f, 0.1f, gridSz * 0.9f));
                    }
                }
            }
        }

        private void DrawLanePaths()
        {
            DrawSingleLanePath(flowFieldBuilder.blueLane1);
            DrawSingleLanePath(flowFieldBuilder.blueLane2);
            DrawSingleLanePath(flowFieldBuilder.blueLane3);
            DrawSingleLanePath(flowFieldBuilder.redLane1);
            DrawSingleLanePath(flowFieldBuilder.redLane2);
            DrawSingleLanePath(flowFieldBuilder.redLane3);
        }

        private void DrawSingleLanePath(LanePathConfig lane)
        {
            if (!lane.IsValid) return;

            Gizmos.color = lane.pathColor;
            var points = lane.wayPoints.Where(t => t != null).ToList();

            // 绘制连接线
            for (int i = 0; i < points.Count - 1; i++)
                Gizmos.DrawLine(points[i].position, points[i + 1].position);

            // 绘制关键点
            foreach (var point in points)
                Gizmos.DrawSphere(point.position, waypointSize);
        }

        private void DrawFlowField(FlowFieldCell[,] runtimeField, int sizeX, int sizeZ, float gridSz, Color color)
        {
            Gizmos.color = color;

            for (int x = 0; x < sizeX; x += flowSampleInterval)
            {
                for (int z = 0; z < sizeZ; z += flowSampleInterval)
                {
                    var cell = runtimeField[x, z];
                    if (cell.canMove && cell.direction.sqrMagnitude > 0.01f)
                    {
                        var pos = flowFieldBuilder.GridToWorld(new Vector2Int(x, z));
                        var dir = cell.direction * gridSz * vectorScale;

                        Gizmos.DrawRay(pos, dir);

                        // 绘制箭头
                        var right = Vector3.Cross(Vector3.up, dir).normalized * gridSz * 0.1f;
                        Gizmos.DrawRay(pos + dir, -dir * 0.25f + right);
                        Gizmos.DrawRay(pos + dir, -dir * 0.25f - right);
                    }
                }
            }
        }

        private void DrawCostHeatmap(int sizeX, int sizeZ, float gridSz)
        {
            var costField = flowFieldBuilder.BlueTeamCostField;
            if (costField.cells == null || costField.cells.Length == 0) return;

            // 找最大代价用于归一化
            int maxCost = 0;
            for (int x = 0; x < sizeX; x++)
                for (int z = 0; z < sizeZ; z++)
                {
                    int cost = costField.GetCell(x, z).baseCost;
                    if (cost > 0 && cost < int.MaxValue && cost > maxCost)
                        maxCost = cost;
                }

            if (maxCost == 0) return;

            for (int x = 0; x < sizeX; x += 2)
            {
                for (int z = 0; z < sizeZ; z += 2)
                {
                    var cell = costField.GetCell(x, z);
                    if (!cell.canMove || cell.baseCost <= 0 || cell.baseCost >= int.MaxValue) continue;

                    float t = (float)cell.baseCost / maxCost;
                    Gizmos.color = new Color(t, 1f - t, 0f, 0.5f);

                    var pos = flowFieldBuilder.GridToWorld(new Vector2Int(x, z));
                    Gizmos.DrawCube(pos, new Vector3(gridSz * 1.5f, 0.02f, gridSz * 1.5f));
                }
            }
        }

        #endregion
    }
}
