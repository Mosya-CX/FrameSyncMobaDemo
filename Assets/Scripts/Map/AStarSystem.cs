using System;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using Unity.Mathematics.FixedPoint;

public class AStarSystem : MonoSingleton<AStarSystem>
{
    [SerializeField, LabelText("宽"), HorizontalGroup] 
    private int width = 128;
    [SerializeField, LabelText("高"), HorizontalGroup] 
    private int height = 128;
    [SerializeField, LabelText("格子大小")] 
    private float cellSize = 1f;
    [SerializeField, LabelText("静态障碍物层级")] 
    private LayerMask obstacleLayer;
    [SerializeField, LabelText("偏移")] 
    private Vector3 offset;

    [SerializeField, ReadOnly, LabelText("烘焙数据")]
    private bool[] walkable;

    private GridGraph graph;

    public Vector3 Center => transform.position + offset;

    private Vector3 Origin
    {
        get
        {
            return Center - new Vector3(
                width * cellSize * 0.5f,
                0,
                height * cellSize * 0.5f
            );
        }
    }

    private static readonly (int x, int y, fp cost)[] Directions =
    {
        (0,1,10),(1,0,10),(0,-1,10),(-1,0,10),
        (1,1,14),(-1,1,14),(1,-1,14),(-1,-1,14)
    };

    protected override void Awake()
    {
        base.Awake();
        InitGraph();
    }

    private void InitGraph()
    {
        if (walkable == null || walkable.Length != width * height)
            return;

        graph = new GridGraph(width, height);

        for (int i = 0; i < walkable.Length; i++)
        {
            ref var node = ref graph.GetNode(i);
            node.Walkable = walkable[i];
        }
    }

    // ===================== Pathfinding =======================

    public List<Vector3> FindPathWorld(Vector3 startWorld, Vector3 endWorld)
    {
        WorldToGrid(startWorld, out int sx, out int sy);
        WorldToGrid(endWorld, out int ex, out int ey);

        var gridPath = FindPath(sx, sy, ex, ey);
        if (gridPath == null) return null;

        List<Vector3> result = new();

        foreach (var p in gridPath)
            result.Add(GridToWorld(p.x, p.y));

        return result;
    }

    public List<(int x, int y)> FindPath(int startX, int startY, int endX, int endY)
    {
        if (graph == null) return null;

        var openSet = new BinaryHeap(graph);
        var closedSet = new HashSet<int>();

        int startIndex = graph.GetIndex(startX, startY);
        int endIndex = graph.GetIndex(endX, endY);

        ref var startNode = ref graph.GetNode(startIndex);
        startNode.G = 0;
        startNode.H = Heuristic(startX, startY, endX, endY);
        startNode.F = startNode.H;
        startNode.ParentIndex = -1;

        openSet.Push(startIndex);

        while (openSet.Count > 0)
        {
            int currentIndex = openSet.Pop();

            if (currentIndex == endIndex)
                return RetracePath(startIndex, endIndex);

            closedSet.Add(currentIndex);

            ref var currentNode = ref graph.GetNode(currentIndex);

            foreach (var dir in Directions)
            {
                int nx = currentNode.X + dir.x;
                int ny = currentNode.Y + dir.y;

                if (!graph.IsValid(nx, ny))
                    continue;

                int neighborIndex = graph.GetIndex(nx, ny);

                if (closedSet.Contains(neighborIndex))
                    continue;

                ref var neighbor = ref graph.GetNode(neighborIndex);

                if (!neighbor.Walkable)
                    continue;

                fp newCost = currentNode.G + dir.cost;

                if (neighbor.ParentIndex == -1 || newCost < neighbor.G)
                {
                    neighbor.G = newCost;
                    neighbor.H = Heuristic(nx, ny, endX, endY);
                    neighbor.F = neighbor.G + neighbor.H;
                    neighbor.ParentIndex = currentIndex;

                    openSet.Push(neighborIndex);
                }
            }
        }

        return null;
    }

    private List<(int x, int y)> RetracePath(int startIndex, int endIndex)
    {
        var path = new List<(int x, int y)>();
        int current = endIndex;

        while (current != startIndex)
        {
            ref var node = ref graph.GetNode(current);
            path.Add((node.X, node.Y));
            current = node.ParentIndex;
        }

        path.Reverse();
        return path;
    }

    private fp Heuristic(int x1, int y1, int x2, int y2)
    {
        int dx = Math.Abs(x1 - x2);
        int dy = Math.Abs(y1 - y2);

        int min = Math.Min(dx, dy);
        int max = Math.Max(dx, dy);

        return (fp)14 * min + (fp)10 * (max - min);
    }

    private void WorldToGrid(Vector3 world, out int x, out int y)
    {
        Vector3 bottomLeft = Origin;

        x = Mathf.FloorToInt((world.x - bottomLeft.x) / cellSize);
        y = Mathf.FloorToInt((world.z - bottomLeft.z) / cellSize);
    }

    private Vector3 GridToWorld(int x, int y)
    {
        Vector3 bottomLeft = Origin;

        return bottomLeft + new Vector3(
            x * cellSize + cellSize * 0.5f,
            0,
            y * cellSize + cellSize * 0.5f
        );
    }

#if UNITY_EDITOR
    [SerializeField, LabelText("是否开启可视化")]
    private bool isShowAStarGrids = true;
    [SerializeField, LabelText("可通行格子颜色")]
    private Color walkableColor = Color.blue;
    [SerializeField, LabelText("不可通行格子颜色")]
    private Color obstacleColor = Color.red;

    // ===================== Editor Bake =======================
    [Button("烘焙网格", ButtonSizes.Large)]
    private void BakeGrid()
    {
        walkable = new bool[width * height];

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Vector3 worldPos = Origin + new Vector3(
                    x * cellSize + cellSize * 0.5f,
                    0,
                    y * cellSize + cellSize * 0.5f
                );

                bool blocked = Physics.CheckBox(
                    worldPos,
                    Vector3.one * cellSize * 0.45f,
                    Quaternion.identity,
                    obstacleLayer
                );

                walkable[y * width + x] = !blocked;
            }

        UnityEditor.EditorUtility.SetDirty(this);
        InitGraph();
        Debug.Log("AStar Grid Bake Complete");
    }

    // ===================== Gizmo Draw ========================
    private void OnDrawGizmos()
    {
        if (!isShowAStarGrids) return;

        if (walkable == null) return;

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                bool canWalk = walkable[y * width + x];

                Gizmos.color = canWalk ? walkableColor : obstacleColor;

                Vector3 pos = Origin + new Vector3(
                    x * cellSize + cellSize * 0.5f,
                    0,
                    y * cellSize + cellSize * 0.5f
                );

                Gizmos.DrawCube(pos, Vector3.one * cellSize * 0.9f);
            }
    }

#endif
}
