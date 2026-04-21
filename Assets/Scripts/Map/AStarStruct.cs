using System.Collections.Generic;
using Unity.Mathematics.FixedPoint;

public struct GridNode
{
    public int X;
    public int Y;

    public bool Walkable;

    public fp G;
    public fp H;
    public fp F;

    public int ParentIndex;

    public bool Opened;
    public bool Closed;
}

public class GridGraph
{
    private readonly int width;
    private readonly int height;
    private readonly GridNode[] nodes;

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
                    ParentIndex = -1,
                    Opened = false,
                    Closed = false,
                    G = fp.zero,
                    H = fp.zero,
                    F = fp.zero,
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

    public void ResetSearchState()
    {
        for (int i = 0; i < nodes.Length; i++)
        {
            nodes[i].G = fp.zero;
            nodes[i].H = fp.zero;
            nodes[i].F = fp.zero;
            nodes[i].ParentIndex = -1;
            nodes[i].Opened = false;
            nodes[i].Closed = false;
        }
    }
}

public class BinaryHeap
{
    private readonly List<int> heap;
    private readonly GridGraph graph;

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
        int lastIndex = heap.Count - 1;
        int first = heap[0];

        if (lastIndex == 0)
        {
            heap.RemoveAt(0);
            return first;
        }

        int last = heap[lastIndex];
        heap[0] = last;
        heap.RemoveAt(lastIndex);

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
            else
            {
                break;
            }
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

            if (smallest == index)
                break;

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