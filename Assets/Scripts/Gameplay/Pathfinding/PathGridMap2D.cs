using System;
using FrameSyncMoba.Physics;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public sealed class PathGridMap2D
    {
        private static readonly fp Half = (fp)0.5m;

        private fp2 _worldMin;
        private fp _cellSize;
        private int _width;
        private int _height;
        private bool[] _walkable;

        public fp2 WorldCenter { get; private set; }
        public fp2 WorldMin => _worldMin;
        public fp CellSize => _cellSize;
        public int Width => _width;
        public int Height => _height;

        private static readonly (int dx, int dy)[] NeighborOffsets = new (int, int)[]
        {
            ( 0, -1),
            ( 1, -1),
            ( 1,  0),
            ( 1,  1),
            ( 0,  1),
            (-1,  1),
            (-1,  0),
            (-1, -1),
        };

        private static readonly int MaxNeighbors = 8;

        private readonly (int, int)[] _neighborBuffer = new (int, int)[MaxNeighbors];
        private int _neighborCount;

        public PathGridMap2D() { }

        public void Initialise(fp2 worldMin, fp2 worldMax, fp cellSize)
        {
            if (cellSize <= fp.zero)
                throw new ArgumentOutOfRangeException(nameof(cellSize));

            _cellSize = cellSize;
            fp2 size = worldMax - worldMin;
            _width = (int)(size.x / cellSize) + 1;
            _height = (int)(size.y / cellSize) + 1;

            if (_width <= 0) _width = 1;
            if (_height <= 0) _height = 1;

            int totalCells = _width * _height;
            _walkable = new bool[totalCells];
            for (int i = 0; i < totalCells; i++)
                _walkable[i] = true;

            _worldMin = worldMin;
            WorldCenter = worldMin + (size * Half);
        }

        public (int cx, int cy) WorldToCell(fp2 worldPos)
        {
            int cx = (int)((worldPos.x - _worldMin.x) / _cellSize);
            int cy = (int)((worldPos.y - _worldMin.y) / _cellSize);
            if (cx < 0) cx = 0;
            if (cy < 0) cy = 0;
            if (cx >= _width) cx = _width - 1;
            if (cy >= _height) cy = _height - 1;
            return (cx, cy);
        }

        public fp2 CellToWorld(int cx, int cy)
        {
            return new fp2(
                _worldMin.x + ((fp)cx + Half) * _cellSize,
                _worldMin.y + ((fp)cy + Half) * _cellSize);
        }

        public bool IsPassable(int cx, int cy)
        {
            if (cx < 0 || cx >= _width || cy < 0 || cy >= _height)
                return false;
            return _walkable[CellIndex(cx, cy)];
        }

        public ReadOnlySpan<(int cx, int cy)> GetNeighbors(int cx, int cy)
        {
            _neighborCount = 0;
            bool n = IsPassable(cx, cy - 1);
            bool s = IsPassable(cx, cy + 1);
            bool e = IsPassable(cx + 1, cy);
            bool w = IsPassable(cx - 1, cy);

            if (n) AddNeighbor(cx, cy - 1);
            if (n && e) AddNeighbor(cx + 1, cy - 1);
            if (e) AddNeighbor(cx + 1, cy);
            if (s && e) AddNeighbor(cx + 1, cy + 1);
            if (s) AddNeighbor(cx, cy + 1);
            if (s && w) AddNeighbor(cx - 1, cy + 1);
            if (w) AddNeighbor(cx - 1, cy);
            if (n && w) AddNeighbor(cx - 1, cy - 1);

            return new ReadOnlySpan<(int, int)>(_neighborBuffer, 0, _neighborCount);
        }

        public void BuildFromPhysics(PhysicsWorld physicsWorld, fp cellSize)
        {
            if (physicsWorld == null)
                throw new ArgumentNullException(nameof(physicsWorld));

            var entities = physicsWorld.UnitEntities;
            if (entities.Count == 0)
                return;

            fp largePositive = new fp(int.MaxValue);
            fp2 min = new fp2(largePositive, largePositive);
            fp2 max = new fp2(-largePositive, -largePositive);
            bool hasAny = false;

            for (int i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (entity.QueryInfo.Owner == null) continue;
                var bounds = entity.Bounds;
                min = fpmath.min(min, bounds.Min);
                max = fpmath.max(max, bounds.Max);
                hasAny = true;
            }

            if (!hasAny)
                return;

            fp margin = cellSize * (fp)2;
            min -= new fp2(margin, margin);
            max += new fp2(margin, margin);

            Initialise(min, max, cellSize);

            for (int i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (entity.QueryInfo.Owner == null) continue;
                var bounds = entity.Bounds;
                SetObstruction(bounds.Min, bounds.Max, blocked: true);
            }
        }

        public void SetObstruction(fp2 worldMin, fp2 worldMax, bool blocked)
        {
            (int cxMin, int cyMin) = WorldToCell(worldMin);
            (int cxMax, int cyMax) = WorldToCell(worldMax);
            for (int cy = cyMin; cy <= cyMax; cy++)
            {
                for (int cx = cxMin; cx <= cxMax; cx++)
                {
                    if (cx >= 0 && cx < _width && cy >= 0 && cy < _height)
                        _walkable[CellIndex(cx, cy)] = !blocked;
                }
            }
        }

        public void Clear()
        {
            if (_walkable != null)
            {
                for (int i = 0; i < _walkable.Length; i++)
                    _walkable[i] = true;
            }
        }

        private int CellIndex(int cx, int cy) => cy * _width + cx;

        private void AddNeighbor(int cx, int cy)
        {
            if (_neighborCount < MaxNeighbors)
            {
                _neighborBuffer[_neighborCount] = (cx, cy);
                _neighborCount++;
            }
        }
    }
}
