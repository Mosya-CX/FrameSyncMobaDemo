using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

namespace FlowField
{
    #region 运行时格子数据

    /// <summary>
    /// 运行时流场格子数据
    /// 包含基础代价、动态代价和计算出的方向
    /// </summary>
    [System.Serializable]
    public struct FlowFieldCell
    {
        /// <summary>基础代价（来自离线构建的价值场）</summary>
        public int baseCost;
        
        /// <summary>动态代价（运行时通过Modifier修改）</summary>
        public int dynamicCost;
        
        /// <summary>总代价 = baseCost + dynamicCost</summary>
        public int totalCost => baseCost + dynamicCost;
        
        /// <summary>是否可通行</summary>
        public bool canMove;
        
        /// <summary>到目标的最优移动方向（运行时计算）</summary>
        public Vector3 direction;
        
        /// <summary>到目标的整合代价（用于路径计算）</summary>
        public int integratedCost;

        public FlowFieldCell(int baseCost, bool movable)
        {
            this.baseCost = baseCost;
            this.dynamicCost = 0;
            this.canMove = movable;
            this.direction = Vector3.zero;
            this.integratedCost = int.MaxValue;
        }

        /// <summary>重置运行时数据（保留基础代价和可通行性）</summary>
        public void ResetRuntimeData()
        {
            dynamicCost = 0;
            direction = Vector3.zero;
            integratedCost = int.MaxValue;
        }
    }

    #endregion

    #region 价值场数据结构

    /// <summary>
    /// 价值场单个格子的数据
    /// </summary>
    [System.Serializable]
    public struct CostCell
    {
        /// <summary>基础代价（离线构建时计算）</summary>
        public int baseCost;
        
        /// <summary>是否可通行</summary>
        public bool canMove;

        public CostCell(int cost, bool movable)
        {
            baseCost = cost;
            canMove = movable;
        }
    }

    /// <summary>
    /// 价值场数据（可序列化）
    /// 存储每个格子的基础代价和可通行性
    /// </summary>
    [System.Serializable]
    public class CostFieldData
    {
        public CostCell[] cells;
        public int sizeX;
        public int sizeZ;

        public CostFieldData()
        {
            cells = new CostCell[0];
            sizeX = 0;
            sizeZ = 0;
        }

        public CostFieldData(int x, int z)
        {
            Initialize(x, z);
        }

        public void Initialize(int x, int z)
        {
            sizeX = x;
            sizeZ = z;
            cells = new CostCell[x * z];
            for (int i = 0; i < cells.Length; i++)
            {
                cells[i] = new CostCell(0, true);
            }
        }

        public CostCell GetCell(int x, int z)
        {
            if (cells == null || cells.Length == 0 || !IsValidCoord(x, z))
                return new CostCell(0, false);
            return cells[GetIndex(x, z)];
        }

        public void SetCell(int x, int z, CostCell cell)
        {
            if (!IsValidCoord(x, z)) return;
            cells[GetIndex(x, z)] = cell;
        }

        public void SetBaseCost(int x, int z, int cost)
        {
            if (!IsValidCoord(x, z)) return;
            cells[GetIndex(x, z)].baseCost = cost;
        }

        public void SetMovable(int x, int z, bool movable)
        {
            if (!IsValidCoord(x, z)) return;
            cells[GetIndex(x, z)].canMove = movable;
        }

        public bool IsValidCoord(int x, int z) => x >= 0 && x < sizeX && z >= 0 && z < sizeZ;

        public int GetIndex(int x, int z) => x * sizeZ + z;

        public Vector2Int GetCoord(int index) => new Vector2Int(index / sizeZ, index % sizeZ);

        public CostFieldData Clone()
        {
            var clone = new CostFieldData { sizeX = sizeX, sizeZ = sizeZ, cells = new CostCell[cells.Length] };
            for (int i = 0; i < cells.Length; i++)
                clone.cells[i] = new CostCell(cells[i].baseCost, cells[i].canMove);
            return clone;
        }
    }

    #endregion

    #region 方向场数据结构

    /// <summary>
    /// 方向场数据（可序列化）
    /// 存储每个格子的移动方向
    /// </summary>
    [System.Serializable]
    public class DirectionFieldData
    {
        public Vector3[] directions;
        public int sizeX;
        public int sizeZ;

        public DirectionFieldData()
        {
            directions = new Vector3[0];
            sizeX = 0;
            sizeZ = 0;
        }

        public void Initialize(int x, int z)
        {
            sizeX = x;
            sizeZ = z;
            directions = new Vector3[x * z];
            for (int i = 0; i < directions.Length; i++)
                directions[i] = Vector3.zero;
        }

        public Vector3 GetDirection(int x, int z)
        {
            if (directions == null || !IsValidCoord(x, z)) return Vector3.zero;
            return directions[x * sizeZ + z];
        }

        public void SetDirection(int x, int z, Vector3 dir)
        {
            if (directions == null || !IsValidCoord(x, z)) return;
            directions[x * sizeZ + z] = dir;
        }

        public bool IsValidCoord(int x, int z) => x >= 0 && x < sizeX && z >= 0 && z < sizeZ;
    }

    #endregion

    #region 兵线路径配置

    /// <summary>
    /// 兵线路径配置
    /// </summary>
    [System.Serializable]
    public class LanePathConfig
    {
#if ODIN_INSPECTOR
        [LabelText("路径名称")]
        [GUIColor(0.9f, 0.9f, 1f)]
#endif
        public string pathName = "兵线路径";

#if ODIN_INSPECTOR
        [LabelText("路径颜色")]
#endif
        public Color pathColor = Color.white;

#if ODIN_INSPECTOR
        [LabelText("关键点列表")]
        [InfoBox("按顺序添加兵线关键点Transform", InfoMessageType.Info)]
#endif
        public List<Transform> wayPoints = new List<Transform>();

        /// <summary>获取路径名称</summary>
        public string PathName => string.IsNullOrEmpty(pathName) ? "兵线路径" : pathName;

        /// <summary>获取世界坐标路径点</summary>
        public List<Vector3> GetWorldPositions()
        {
            var result = new List<Vector3>();
            foreach (var t in wayPoints)
                if (t != null) result.Add(t.position);
            return result;
        }

        /// <summary>检查路径是否有效</summary>
        public bool IsValid => wayPoints != null && wayPoints.Count >= 2 && wayPoints.Exists(t => t != null);
    }

    #endregion

    #region 修改器系统

    /// <summary>
    /// 流场修改器类型
    /// </summary>
    public enum ModifierType
    {
        /// <summary>增加代价</summary>
        AddCost,
        /// <summary>设置代价</summary>
        SetCost,
        /// <summary>设置不可通行</summary>
        SetImpassable,
        /// <summary>设置可通行</summary>
        SetPassable,
        /// <summary>乘以代价系数</summary>
        MultiplyCost
    }

    /// <summary>
    /// 流场修改器
    /// 用于运行时动态修改指定格子的价值
    /// </summary>
    [System.Serializable]
    public class FlowFieldModifier
    {
        public int id;
        public string name;
        public ModifierType type;
        public int value;
        public float floatValue = 1f;
        public int radius;
        public Vector2Int center;
        public bool enabled = true;
        public bool persistent;
        public float createTime;
        public float expireTime;
        public List<Vector2Int> affectedCells = new List<Vector2Int>();
        public bool useCustomCells;

        private static int nextId = 1;

        public FlowFieldModifier()
        {
            id = nextId++;
            name = $"Modifier_{id}";
            createTime = Time.time;
        }

        public static FlowFieldModifier CreateSingleCell(Vector2Int cell, ModifierType type, int value)
        {
            return new FlowFieldModifier { center = cell, type = type, value = value, radius = 0, useCustomCells = false };
        }

        public static FlowFieldModifier CreateRange(Vector2Int center, int radius, ModifierType type, int value)
        {
            return new FlowFieldModifier { center = center, radius = radius, type = type, value = value, useCustomCells = false };
        }

        public static FlowFieldModifier CreateCustomCells(List<Vector2Int> cells, ModifierType type, int value)
        {
            return new FlowFieldModifier { type = type, value = value, affectedCells = new List<Vector2Int>(cells), useCustomCells = true };
        }

        public List<Vector2Int> GetAffectedCells(int mapSizeX, int mapSizeZ)
        {
            if (useCustomCells) return new List<Vector2Int>(affectedCells);

            var result = new List<Vector2Int>();
            if (radius <= 0)
            {
                result.Add(center);
            }
            else
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dz = -radius; dz <= radius; dz++)
                    {
                        if (dx * dx + dz * dz <= radius * radius)
                        {
                            int nx = center.x + dx, nz = center.y + dz;
                            if (nx >= 0 && nx < mapSizeX && nz >= 0 && nz < mapSizeZ)
                                result.Add(new Vector2Int(nx, nz));
                        }
                    }
                }
            }
            return result;
        }

        public bool IsExpired() => expireTime > 0 && Time.time > createTime + expireTime;

        public void ApplyToCell(ref FlowFieldCell cell)
        {
            if (!enabled) return;
            switch (type)
            {
                case ModifierType.AddCost: cell.dynamicCost += value; break;
                case ModifierType.SetCost: cell.dynamicCost = value; break;
                case ModifierType.SetImpassable: cell.canMove = false; break;
                case ModifierType.SetPassable: cell.canMove = true; break;
                case ModifierType.MultiplyCost: cell.dynamicCost = Mathf.RoundToInt(cell.baseCost * floatValue) - cell.baseCost; break;
            }
        }
    }

    /// <summary>
    /// 修改器管理器
    /// </summary>
    public class ModifierManager : Singleton<ModifierManager>
    {
        private Dictionary<int, FlowFieldModifier> modifiers = new Dictionary<int, FlowFieldModifier>();
        private List<FlowFieldModifier> modifierList = new List<FlowFieldModifier>();

        public int AddModifier(FlowFieldModifier modifier)
        {
            if (modifier.id == 0) modifier.id = GetNextId();
            modifiers[modifier.id] = modifier;
            modifierList.Add(modifier);
            return modifier.id;
        }

        public bool RemoveModifier(int id)
        {
            if (modifiers.TryGetValue(id, out var modifier))
            {
                modifiers.Remove(id);
                modifierList.Remove(modifier);
                return true;
            }
            return false;
        }

        public FlowFieldModifier GetModifier(int id) => modifiers.TryGetValue(id, out var m) ? m : null;

        public List<FlowFieldModifier> GetAllModifiers()
        {
            modifierList.RemoveAll(m => m.IsExpired());
            foreach (var expired in modifierList.FindAll(m => m.IsExpired()))
                modifiers.Remove(expired.id);
            return new List<FlowFieldModifier>(modifierList);
        }

        public void ClearAll() { modifiers.Clear(); modifierList.Clear(); }

        public void ClearNonPersistent()
        {
            var toRemove = modifierList.FindAll(m => !m.persistent);
            foreach (var m in toRemove) { modifiers.Remove(m.id); modifierList.Remove(m); }
        }

        private static int nextId = 1;
        private static int GetNextId() => nextId++;
    }

    #endregion
}
