using System;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Deterministic indexed binary min-heap for A* open-set operations.
    /// Stores PathNode values keyed by a flat cell index (cy * width + cx).
    /// Uses a SearchId pattern to reset without reallocating arrays.
    /// Stable tie-break matches PathNode.CompareTo (FCost → CellX → CellY).
    /// </summary>
    public sealed class IndexedMinHeap
    {
        private const int DefaultCapacity = 256;

        private PathNode[] _heap;
        private int[] _cellToHeapIndex;
        private int[] _searchIds;
        private int _currentSearchId;
        private int _count;
        private int _gridWidth;
        private int _gridHeight;

        public int Count => _count;

        public IndexedMinHeap(int gridWidth, int gridHeight, int initialCapacity = DefaultCapacity)
        {
            _gridWidth = gridWidth;
            _gridHeight = gridHeight;
            int totalCells = gridWidth * gridHeight;
            _heap = new PathNode[Math.Max(initialCapacity, 64)];
            _cellToHeapIndex = new int[totalCells];
            _searchIds = new int[totalCells];
            for (int i = 0; i < totalCells; i++)
            {
                _cellToHeapIndex[i] = -1;
            }
            _currentSearchId = 0;
            _count = 0;
        }

        /// <summary>
        /// Advance the search ID so that all previous entries are logically cleared
        /// without zeroing arrays. Must be paired with ClearStateForNewSearch.
        /// </summary>
        public void BeginNewSearch()
        {
            _currentSearchId++;
            if (_currentSearchId == int.MaxValue)
            {
                // Wrap around: force-clear all search IDs.
                _currentSearchId = 1;
                Array.Clear(_searchIds, 0, _searchIds.Length);
            }
            _count = 0;
        }

        /// <summary>
        /// Returns true if the cell has been visited in the current search.
        /// </summary>
        public bool IsInCurrentSearch(int cellIndex)
        {
            return _searchIds[cellIndex] == _currentSearchId;
        }

        /// <summary>
        /// Returns the heap index of a cell, or -1 if not in the heap for this search.
        /// </summary>
        public int GetHeapIndex(int cellIndex)
        {
            if (_searchIds[cellIndex] != _currentSearchId) return -1;
            return _cellToHeapIndex[cellIndex];
        }

        public void Push(PathNode node)
        {
            int cellIndex = node.CellY * _gridWidth + node.CellX;
            if (_searchIds[cellIndex] == _currentSearchId)
            {
                // Already in heap. Update cost if better.
                DecreaseKey(cellIndex, node.GCost);
                return;
            }

            EnsureCapacity(_count + 1);
            _searchIds[cellIndex] = _currentSearchId;
            _heap[_count] = node;
            _cellToHeapIndex[cellIndex] = _count;
            _count++;
            SiftUp(_count - 1);
        }

        public PathNode Pop()
        {
            if (_count == 0)
                throw new InvalidOperationException("Heap is empty.");

            PathNode min = _heap[0];
            int minCell = min.CellY * _gridWidth + min.CellX;
            _cellToHeapIndex[minCell] = -1;

            _count--;
            if (_count > 0)
            {
                _heap[0] = _heap[_count];
                int movedCell = _heap[0].CellY * _gridWidth + _heap[0].CellX;
                _cellToHeapIndex[movedCell] = 0;
                SiftDown(0);
            }

            return min;
        }

        /// <summary>
        /// Update the GCost of an existing heap entry when a better path is found.
        /// Only updates when the new GCost is lower than the stored GCost.
        /// </summary>
        public void DecreaseKey(int cellIndex, fp newGCost)
        {
            int heapIndex = _cellToHeapIndex[cellIndex];
            if (heapIndex < 0 || heapIndex >= _count)
                return;

            if (newGCost >= _heap[heapIndex].GCost)
                return;

            _heap[heapIndex].GCost = newGCost;
            SiftUp(heapIndex);
        }

        public PathNode Peek()
        {
            if (_count == 0)
                throw new InvalidOperationException("Heap is empty.");
            return _heap[0];
        }

        private void SiftUp(int index)
        {
            while (index > 0)
            {
                int parent = (index - 1) / 2;
                if (_heap[index].CompareTo(_heap[parent]) >= 0)
                    break;
                Swap(index, parent);
                index = parent;
            }
        }

        private void SiftDown(int index)
        {
            while (true)
            {
                int left = 2 * index + 1;
                int right = 2 * index + 2;
                int smallest = index;

                if (left < _count && _heap[left].CompareTo(_heap[smallest]) < 0)
                    smallest = left;
                if (right < _count && _heap[right].CompareTo(_heap[smallest]) < 0)
                    smallest = right;

                if (smallest == index)
                    break;

                Swap(index, smallest);
                index = smallest;
            }
        }

        private void Swap(int a, int b)
        {
            PathNode temp = _heap[a];
            _heap[a] = _heap[b];
            _heap[b] = temp;

            int cellA = _heap[a].CellY * _gridWidth + _heap[a].CellX;
            int cellB = _heap[b].CellY * _gridWidth + _heap[b].CellX;
            _cellToHeapIndex[cellA] = a;
            _cellToHeapIndex[cellB] = b;
        }

        private void EnsureCapacity(int required)
        {
            if (required <= _heap.Length) return;
            int newSize = _heap.Length * 2;
            while (newSize < required) newSize *= 2;
            Array.Resize(ref _heap, newSize);
        }
    }
}
