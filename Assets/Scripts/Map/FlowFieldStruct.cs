using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

namespace FlowField
{
    [System.Serializable]
    public class WalkableFieldData
    {
        public bool[] cells;
        public int sizeX;
        public int sizeZ;

        public WalkableFieldData()
        {
            cells = new bool[0];
            sizeX = 0;
            sizeZ = 0;
        }

        public WalkableFieldData(int x, int z)
        {
            Initialize(x, z);
        }

        public void Initialize(int x, int z)
        {
            sizeX = x;
            sizeZ = z;
            cells = new bool[x * z];

            for (int i = 0; i < cells.Length; i++)
                cells[i] = true;
        }

        public bool IsValidCoord(int x, int z)
        {
            return x >= 0 && x < sizeX && z >= 0 && z < sizeZ;
        }

        public int GetIndex(int x, int z)
        {
            return x * sizeZ + z;
        }

        public bool GetCell(int x, int z)
        {
            if (!IsValidCoord(x, z) || cells == null || cells.Length == 0)
                return false;

            return cells[GetIndex(x, z)];
        }

        public void SetCell(int x, int z, bool movable)
        {
            if (!IsValidCoord(x, z))
                return;

            cells[GetIndex(x, z)] = movable;
        }

        public WalkableFieldData Clone()
        {
            var clone = new WalkableFieldData
            {
                sizeX = sizeX,
                sizeZ = sizeZ,
                cells = new bool[cells.Length]
            };

            for (int i = 0; i < cells.Length; i++)
                clone.cells[i] = cells[i];

            return clone;
        }
    }

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

        public DirectionFieldData(int x, int z)
        {
            Initialize(x, z);
        }

        public void Initialize(int x, int z)
        {
            sizeX = x;
            sizeZ = z;
            directions = new Vector3[x * z];

            for (int i = 0; i < directions.Length; i++)
                directions[i] = Vector3.zero;
        }

        public bool IsValidCoord(int x, int z)
        {
            return x >= 0 && x < sizeX && z >= 0 && z < sizeZ;
        }

        public int GetIndex(int x, int z)
        {
            return x * sizeZ + z;
        }

        public Vector3 GetDirection(int x, int z)
        {
            if (!IsValidCoord(x, z) || directions == null || directions.Length == 0)
                return Vector3.zero;

            return directions[GetIndex(x, z)];
        }

        public void SetDirection(int x, int z, Vector3 dir)
        {
            if (!IsValidCoord(x, z))
                return;

            directions[GetIndex(x, z)] = dir;
        }

        public DirectionFieldData Clone()
        {
            var clone = new DirectionFieldData
            {
                sizeX = sizeX,
                sizeZ = sizeZ,
                directions = new Vector3[directions.Length]
            };

            for (int i = 0; i < directions.Length; i++)
                clone.directions[i] = directions[i];

            return clone;
        }
    }

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

        public string PathName => string.IsNullOrEmpty(pathName) ? "兵线路径" : pathName;

        public List<Vector3> GetWorldPositions()
        {
            var result = new List<Vector3>();
            foreach (var t in wayPoints)
            {
                if (t != null)
                    result.Add(t.position);
            }
            return result;
        }

        public bool IsValid => wayPoints != null && wayPoints.Count >= 2 && wayPoints.Exists(t => t != null);
    }
}
