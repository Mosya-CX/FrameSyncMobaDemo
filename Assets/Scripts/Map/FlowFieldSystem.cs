using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Sirenix.OdinInspector;
using System;

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

        [FoldoutGroup("流场配置")]
        [LabelText("流场构建器")]
        [Required("请设置流场构建器")]
        [InfoBox("流场构建器包含预计算的价值场数据", InfoMessageType.Info)]
        public FlowFieldBuilder flowFieldBuilder;

        [FoldoutGroup("流场配置")]
        [LabelText("自动初始化")]
        public bool autoInitialize = true;

        [FoldoutGroup("运行时设置")]
        [LabelText("使用插值获取方向")]
        public bool useInterpolation = true;

        [FoldoutGroup("运行时设置")]
        [LabelText("方向平滑迭代次数")]
        [Range(0, 5)]
        public int directionSmoothingIterations = 1;

        #endregion

        #region 可视化配置
#if UNITY_EDITOR

        /// <summary>当前要显示的队伍</summary>
        [FoldoutGroup("可视化配置")]
        [LabelText("显示队伍")]
        [Tooltip("选择要查看蓝队还是红队的数据")]
        public ViewTeam viewTeam = ViewTeam.Blue;

        /// <summary>当前显示模式</summary>
        [FoldoutGroup("可视化配置")]
        [LabelText("显示模式")]
        [Tooltip("选择流场的可视化模式")]
        public DisplayMode displayMode = DisplayMode.None;

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

        /// <summary>当前要显示的队伍</summary>
        public enum ViewTeam
        {
            Blue,
            Red
        }

        /// <summary>当前显示模式</summary>
        public enum DisplayMode
        {
            None,           // 不显示流场
            VectorField,    // 显示矢量场
            Heatmap         // 显示热力图
        }

        [NonSerialized] private DirectionFieldData previewBlueDirectionField;
        [NonSerialized] private DirectionFieldData previewRedDirectionField;
#endif
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

            for (int x = 0; x < sizeX; x++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    var costCell = costField.GetCell(x, z);
                    // 初始方向为零，需要时通过 CalculateDirectionField 计算
                    runtimeField[x, z] = new FlowFieldCell(costCell.baseCost, costCell.canMove);
                }
            }
        }

        [Button("重置运行时数据", ButtonSizes.Small)]
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

        [Button("清除所有修改器", ButtonSizes.Small)]
        public void ClearAllModifiers()
        {
            modifierManager.ClearAll();
            ResetRuntimeData();
        }

        public int AddModifier(FlowFieldModifier modifier)
        {
            int id = modifierManager.AddModifier(modifier);
            ApplyModifier(modifier);
            // 修改器影响后，重新计算受影响区域的方向（可选）
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
            // 修改器影响后，可重新计算局部方向，这里简化为使用预计算的方向（但预计算已移除）
            // 实际可根据需要重新计算整个场，但为了性能，此处仅清零受影响区域的方向
            var runtimeField = isBlueTeam ? blueTeamRuntimeField : redTeamRuntimeField;
            var affectedCells = modifier.GetAffectedCells(MapSizeX, MapSizeZ);

            foreach (var cell in affectedCells)
            {
                // 方向将在下一次可视化或路径查询时重新计算
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
#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!isInitialized || flowFieldBuilder == null) return;

            int sizeX = MapSizeX, sizeZ = MapSizeZ;
            float gridSz = GridSize;
            var center = flowFieldBuilder.MapCenterPosition;

            if (showGrid) DrawGrid(center, sizeX, sizeZ, gridSz);
            if (showObstacles) DrawObstacles(sizeX, sizeZ, gridSz);

            // 绘制当前队伍的兵线路径
            DrawLanePathsForTeam(viewTeam);

            // 根据显示模式绘制流场数据
            if (displayMode == DisplayMode.None) return;

            switch (displayMode)
            {
                case DisplayMode.VectorField:
                    EnsurePreviewDirectionFields();
                    var dirField = (viewTeam == ViewTeam.Blue) ? previewBlueDirectionField : previewRedDirectionField;
                    if (dirField != null)
                    {
                        Color teamColor = (viewTeam == ViewTeam.Blue)
                            ? new Color(0.2f, 0.5f, 1f, 0.8f)
                            : new Color(1f, 0.4f, 0.4f, 0.8f);
                        DrawPreviewVectorField(dirField, sizeX, sizeZ, gridSz, teamColor);
                    }
                    break;
                case DisplayMode.Heatmap:
                    var costField = (viewTeam == ViewTeam.Blue)
                        ? flowFieldBuilder.BlueTeamCostField
                        : flowFieldBuilder.RedTeamCostField;
                    DrawCostHeatmap(costField, sizeX, sizeZ, gridSz);
                    break;
            }
        }

        private void DrawPreviewVectorField(DirectionFieldData dirField, int sizeX, int sizeZ, float gridSz, Color color)
        {
            Gizmos.color = color;
            for (int x = 0; x < sizeX; x += flowSampleInterval)
            {
                for (int z = 0; z < sizeZ; z += flowSampleInterval)
                {
                    var dir = dirField.GetDirection(x, z);
                    if (dir.sqrMagnitude < 0.01f) continue;

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
                    if (!flowFieldBuilder.GetBaseCell(x, z).canMove)
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
            if (!lane.IsValid) return;

            Gizmos.color = lane.pathColor;
            var points = lane.wayPoints.Where(t => t != null).ToList();

            for (int i = 0; i < points.Count - 1; i++)
                Gizmos.DrawLine(points[i].position, points[i + 1].position);

            foreach (var point in points)
                Gizmos.DrawSphere(point.position, waypointSize);
        }

        private void DrawCostHeatmap(CostFieldData costField, int sizeX, int sizeZ, float gridSz)
        {
            if (costField.cells == null || costField.cells.Length == 0) return;

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
#endif
        #endregion

        #region 预览方向场构建
#if UNITY_EDITOR
        /// <summary>确保预览方向场已构建</summary>
        private void EnsurePreviewDirectionFields()
        {
            if (previewBlueDirectionField == null || previewBlueDirectionField.directions == null || previewBlueDirectionField.directions.Length == 0)
            {
                previewBlueDirectionField = new DirectionFieldData();
                previewBlueDirectionField.Initialize(MapSizeX, MapSizeZ);
                BuildPreviewDirectionField(true, previewBlueDirectionField);
            }
            if (previewRedDirectionField == null || previewRedDirectionField.directions == null || previewRedDirectionField.directions.Length == 0)
            {
                previewRedDirectionField = new DirectionFieldData();
                previewRedDirectionField.Initialize(MapSizeX, MapSizeZ);
                BuildPreviewDirectionField(false, previewRedDirectionField);
            }
        }

        /// <summary>为指定队伍构建预览方向场</summary>
        private void BuildPreviewDirectionField(bool isBlue, DirectionFieldData dirField)
        {
            if (flowFieldBuilder == null) return;
            var costField = isBlue ? flowFieldBuilder.BlueTeamCostField : flowFieldBuilder.RedTeamCostField;
            var lanes = isBlue
                ? new[] { flowFieldBuilder.blueLane1, flowFieldBuilder.blueLane2, flowFieldBuilder.blueLane3 }
                : new[] { flowFieldBuilder.redLane1, flowFieldBuilder.redLane2, flowFieldBuilder.redLane3 };

            CalculateDirectionFieldFromCost(costField, dirField);

            var laneGridPaths = new List<List<Vector2Int>>();
            foreach (var lane in lanes)
            {
                if (lane.IsValid)
                {
                    var worldPath = lane.GetWorldPositions();
                    var gridPath = worldPath.Select(p => flowFieldBuilder.WorldToGridCoord(p))
                                             .Where(flowFieldBuilder.IsValidCoord).ToList();
                    if (gridPath.Count >= 2) laneGridPaths.Add(gridPath);
                }
            }

            if (flowFieldBuilder.enableObstacleEdgeAlignment && laneGridPaths.Count > 0)
                AlignDirectionNearObstacles(dirField, costField, laneGridPaths);

            SmoothDirectionField(dirField, costField, flowFieldBuilder.directionSmoothingIterations);
        }

        /// <summary>从价值场计算方向场</summary>
        private void CalculateDirectionFieldFromCost(CostFieldData costField, DirectionFieldData dirField)
        {
            int sizeX = MapSizeX, sizeZ = MapSizeZ;
            for (int x = 0; x < sizeX; x++)
            {
                for (int z = 0; z < sizeZ; z++)
                {
                    var cell = costField.GetCell(x, z);
                    if (!cell.canMove || cell.baseCost <= 0 || cell.baseCost >= int.MaxValue)
                    {
                        dirField.SetDirection(x, z, Vector3.zero);
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
                            if (!flowFieldBuilder.IsValidCoord(nx, nz)) continue;

                            var neighbor = costField.GetCell(nx, nz);
                            if (!neighbor.canMove || neighbor.baseCost < 0 || neighbor.baseCost >= int.MaxValue) continue;

                            if (neighbor.baseCost < lowest)
                            {
                                lowest = neighbor.baseCost;
                                bestDir = new Vector3(dx, 0, dz).normalized;
                            }
                        }
                    }
                    dirField.SetDirection(x, z, bestDir);
                }
            }
        }

        /// <summary>障碍物边缘方向对齐</summary>
        private void AlignDirectionNearObstacles(DirectionFieldData directionField, CostFieldData costField, List<List<Vector2Int>> lanePaths)
        {
            int sizeX = MapSizeX, sizeZ = MapSizeZ;
            var adjustments = new List<(int x, int z, Vector3 newDir)>();

            for (int x = 0; x < sizeX; x++)
            {
                for (int z = 0; z < sizeZ; z++)
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
                            if (!flowFieldBuilder.IsValidCoord(nx, nz)) continue;

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
                        var blendedDir = Vector3.Lerp(currentDir, parallelDir, flowFieldBuilder.obstacleEdgeSmoothStrength);

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
            int sizeX = MapSizeX, sizeZ = MapSizeZ;
            for (int iter = 0; iter < iterations; iter++)
            {
                var tempDirs = new Vector3[sizeX, sizeZ];

                for (int x = 0; x < sizeX; x++)
                    for (int z = 0; z < sizeZ; z++)
                        tempDirs[x, z] = directionField.GetDirection(x, z);

                for (int x = 0; x < sizeX; x++)
                {
                    for (int z = 0; z < sizeZ; z++)
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
                                if (!flowFieldBuilder.IsValidCoord(nx, nz)) continue;

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

        /// <summary>手动重建预览方向场</summary>
        [Button("重建预览方向场")]
        [GUIColor(0.8f, 0.6f, 0.2f)]
        private void RebuildPreviewDirectionFields()
        {
            previewBlueDirectionField = null;
            previewRedDirectionField = null;
            EnsurePreviewDirectionFields();
            Debug.Log("预览方向场重建完成");
        }
#endif
        #endregion
    }
}
