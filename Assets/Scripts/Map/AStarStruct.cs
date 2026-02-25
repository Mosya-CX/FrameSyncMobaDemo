using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

public struct GridNode
{
    public int X;
    public int Y;

    public bool Walkable;

    public fp G;   // 起点到当前代价
    public fp H;   // 启发式
    public fp F;   // G + H

    public int ParentIndex;
}

public class GridGraph
{
    private int width;
    private int height;

    private GridNode[] nodes;

    public int Width => width;
    public int Height => height;

    public GridGraph(int width, int height)
    {
        this.width = width;
        this.height = height;

        nodes = new GridNode[width * height];

        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int index = GetIndex(x, y);
                nodes[index] = new GridNode
                {
                    X = x,
                    Y = y,
                    Walkable = true,
                    ParentIndex = -1
                };
            }
    }

    public int GetIndex(int x, int y)
    {
        return y * width + x;
    }

    public bool IsValid(int x, int y)
    {
        return x >= 0 && y >= 0 && x < width && y < height;
    }

    public ref GridNode GetNode(int index)
    {
        return ref nodes[index];
    }

    public ref GridNode GetNode(int x, int y)
    {
        return ref nodes[GetIndex(x, y)];
    }

    public void SetWalkable(int x, int y, bool walkable)
    {
        nodes[GetIndex(x, y)].Walkable = walkable;
    }
}

public class BinaryHeap
{
    private List<int> heap;
    private GridGraph graph;

    public BinaryHeap(GridGraph graph)
    {
        this.graph = graph;
        heap = new List<int>();
    }

    public int Count => heap.Count;

    public void Push(int nodeIndex)
    {
        heap.Add(nodeIndex);
        SortUp(heap.Count - 1);
    }

    public int Pop()
    {
        int first = heap[0];
        int last = heap[heap.Count - 1];

        heap[0] = last;
        heap.RemoveAt(heap.Count - 1);

        SortDown(0);

        return first;
    }

    private void SortUp(int index)
    {
        while (index > 0)
        {
            int parent = (index - 1) / 2;

            if (Compare(heap[index], heap[parent]) < 0)
            {
                Swap(index, parent);
                index = parent;
            }
            else break;
        }
    }

    private void SortDown(int index)
    {
        while (true)
        {
            int left = index * 2 + 1;
            int right = left + 1;
            int smallest = index;

            if (left < heap.Count && Compare(heap[left], heap[smallest]) < 0)
                smallest = left;

            if (right < heap.Count && Compare(heap[right], heap[smallest]) < 0)
                smallest = right;

            if (smallest == index) break;

            Swap(index, smallest);
            index = smallest;
        }
    }

    private int Compare(int a, int b)
    {
        ref var na = ref graph.GetNode(a);
        ref var nb = ref graph.GetNode(b);

        int fCompare = na.F.CompareTo(nb.F);
        if (fCompare == 0)
            return na.H.CompareTo(nb.H);

        return fCompare;
    }

    private void Swap(int a, int b)
    {
        (heap[a], heap[b]) = (heap[b], heap[a]);
    }
}