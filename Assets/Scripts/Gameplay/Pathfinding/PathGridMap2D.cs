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

        // Original flat walkable flag (backward-compatible, defaults to Medium layer)
        private bool[] _walkable;

        // Per-radius-class walkability layers (RadiusClassHelper.Count layers)
        // Layer 0 = Small, Layer 1 = Medium, Layer 2 = Large
        // Each layer: true = walkable for units of that radius class or smaller
        // Blocking Small also blocks Medium and Large layers.
        private bool[][] _walkableByRadiusClass;

        public fp2 WorldCenter { get; private set; }
        public fp2 WorldMin => _worldMin;
        public fp CellSize => _cellSize;
        public int Width => _width;
        public int Height => _height;
        public fp2 WorldMax => new fp2(
            _worldMin.x +
                (fp)(_width - 1) * _cellSize,
            _worldMin.y +
                (fp)(_height - 1) * _cellSize);

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

            // Legacy flat walkable
            _walkable = new bool[totalCells];
            for (int i = 0; i < totalCells; i++)
                _walkable[i] = true;

            // Initialize radius-clearance layers (all walkable by default)
            _walkableByRadiusClass = new bool[RadiusClassHelper.Count][];
            for (int layer = 0; layer < RadiusClassHelper.Count; layer++)
            {
                _walkableByRadiusClass[layer] = new bool[totalCells];
                for (int i = 0; i < totalCells; i++)
                    _walkableByRadiusClass[layer][i] = true;
            }

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

        public bool HasLineOfSight(
            fp2 from,
            fp2 to,
            RadiusClass radiusClass)
        {
            (int fromX, int fromY) =
                WorldToCell(from);
            (int toX, int toY) =
                WorldToCell(to);
            int dx = Math.Abs(toX - fromX);
            int dy = -Math.Abs(toY - fromY);
            int stepX =
                fromX < toX ? 1 : -1;
            int stepY =
                fromY < toY ? 1 : -1;
            int error = dx + dy;
            int x = fromX;
            int y = fromY;
            while (true)
            {
                if (!IsPassable(
                        x,
                        y,
                        radiusClass))
                {
                    return false;
                }
                if (x == toX && y == toY)
                {
                    return true;
                }
                int doubled = error * 2;
                if (doubled >= dy)
                {
                    error += dy;
                    x += stepX;
                }
                if (doubled <= dx)
                {
                    error += dx;
                    y += stepY;
                }
            }
        }

        /// <summary>
        /// Legacy passability check. Defaults to Medium RadiusClass for backward compatibility.
        /// </summary>
        public bool IsPassable(int cx, int cy)
        {
            return IsPassable(cx, cy, RadiusClass.Medium);
        }

        /// <summary>
        /// Check cell passability for a specific unit radius class.
        /// Larger radius classes are more restrictive (fewer walkable cells).
        /// </summary>
        public bool IsPassable(int cx, int cy, RadiusClass rc)
        {
            if (cx < 0 || cx >= _width || cy < 0 || cy >= _height)
                return false;

            if (_walkableByRadiusClass == null)
                return _walkable != null && _walkable[CellIndex(cx, cy)];

            int layer = (int)rc;
            if (layer < 0 || layer >= _walkableByRadiusClass.Length)
                return false;

            return _walkableByRadiusClass[layer][CellIndex(cx, cy)];
        }

        /// <summary>
        /// Check if a circle of given radius can be placed at the world position
        /// without overlapping blocked cells. Used by wall constraint and movement.
        /// </summary>
        public bool IsCircleWalkable(fp2 worldPos, fp radius)
        {
            (int cx, int cy) = WorldToCell(worldPos);
            if (!IsPassable(cx, cy)) return false;

            // For radius larger than half cell, check neighbor cells too
            fp cellRadius = radius / _cellSize;
            int checkRadius = (int)(cellRadius + (fp)0.5m);
            if (checkRadius <= 0) return IsPassable(cx, cy);

            for (int dy = -checkRadius; dy <= checkRadius; dy++)
            {
                for (int dx = -checkRadius; dx <= checkRadius; dx++)
                {
                    int nx = cx + dx;
                    int ny = cy + dy;
                    if (!IsPassable(nx, ny)) return false;
                }
            }
            return true;
        }

        /// <summary>
        /// Get the walkability layer array for a specific RadiusClass.
        /// </summary>
        public bool[] GetWalkableLayer(RadiusClass rc)
        {
            if (_walkableByRadiusClass == null) return null;
            int layer = (int)rc;
            if (layer < 0 || layer >= _walkableByRadiusClass.Length) return null;
            return _walkableByRadiusClass[layer];
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

            // Mark obstructed cells by entity shape's RadiusClass
            for (int i = 0; i < entities.Count; i++)
            {
                var entity = entities[i];
                if (entity.QueryInfo.Owner == null) continue;
                var bounds = entity.Bounds;
                var shape = entity.Shape;
                RadiusClass rc = RadiusClassHelper.FromRadius(shape.Radius);
                SetObstruction(bounds.Min, bounds.Max, blocked: true, rc);
            }
        }

        public void SetObstruction(fp2 worldMin, fp2 worldMax, bool blocked)
        {
            SetObstruction(worldMin, worldMax, blocked, RadiusClass.Medium);
        }

        /// <summary>
        /// Set obstruction for a specific radius class and all larger classes.
        /// Blocking a cell for Small also blocks it for Medium and Large.
        /// Unblocking for Large also unblocks for Medium and Small.
        /// </summary>
        public void SetObstruction(fp2 worldMin, fp2 worldMax, bool blocked, RadiusClass minAffectedClass)
        {
            (int cxMin, int cyMin) = WorldToCell(worldMin);
            (int cxMax, int cyMax) = WorldToCell(worldMax);

            int startLayer = (int)minAffectedClass;
            if (blocked)
            {
                // Block affects this layer and all larger layers
                for (int layer = startLayer; layer < RadiusClassHelper.Count; layer++)
                {
                    for (int cy = cyMin; cy <= cyMax; cy++)
                    {
                        for (int cx = cxMin; cx <= cxMax; cx++)
                        {
                            if (cx >= 0 && cx < _width && cy >= 0 && cy < _height)
                                SetLayerWalkable(layer, cx, cy, false);
                        }
                    }
                }
            }
            else
            {
                // Unblock affects this layer and all smaller layers (down to Small)
                for (int layer = startLayer; layer >= 0; layer--)
                {
                    for (int cy = cyMin; cy <= cyMax; cy++)
                    {
                        for (int cx = cxMin; cx <= cxMax; cx++)
                        {
                            if (cx >= 0 && cx < _width && cy >= 0 && cy < _height)
                                SetLayerWalkable(layer, cx, cy, true);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Rasterizes an oriented rectangle into every affected radius layer.
        /// The cell square and agent radius are conservatively included, so a
        /// rotated thin wall keeps its real orientation instead of becoming its
        /// world-axis-aligned bounding square.
        /// </summary>
        public void SetOrientedRectObstruction(
            fp2 center,
            fp2 axisX,
            fp2 axisY,
            fp2 halfExtents,
            bool blocked,
            RadiusClass minAffectedClass)
        {
            if (halfExtents.x <= fp.zero ||
                halfExtents.y <= fp.zero)
                throw new ArgumentOutOfRangeException(
                    nameof(halfExtents));

            fp2 normalizedX =
                fpmath.normalize(axisX);
            fp2 normalizedY =
                fpmath.normalize(axisY);
            fp2 aabbHalf =
                new fp2(
                    fpmath.abs(normalizedX.x) *
                        halfExtents.x +
                    fpmath.abs(normalizedY.x) *
                        halfExtents.y,
                    fpmath.abs(normalizedX.y) *
                        halfExtents.x +
                    fpmath.abs(normalizedY.y) *
                        halfExtents.y);
            (int minX, int minY) =
                WorldToCell(
                    center -
                    aabbHalf -
                    new fp2(
                        RadiusClassHelper.LargeRadius +
                            _cellSize,
                        RadiusClassHelper.LargeRadius +
                            _cellSize));
            (int maxX, int maxY) =
                WorldToCell(
                    center +
                    aabbHalf +
                    new fp2(
                        RadiusClassHelper.LargeRadius +
                            _cellSize,
                        RadiusClassHelper.LargeRadius +
                            _cellSize));

            int firstLayer =
                (int)minAffectedClass;
            int lastLayer = blocked
                ? RadiusClassHelper.Count - 1
                : firstLayer;
            int layerStep = blocked ? 1 : -1;
            if (!blocked)
            {
                lastLayer = 0;
            }

            for (int layer = firstLayer;
                 blocked
                     ? layer <= lastLayer
                     : layer >= lastLayer;
                 layer += layerStep)
            {
                fp cellHalf =
                    _cellSize *
                    Half +
                    RadiusClassHelper.GetRadius(
                        (RadiusClass)layer);
                for (int cy = minY;
                     cy <= maxY;
                     cy++)
                {
                    for (int cx = minX;
                         cx <= maxX;
                         cx++)
                    {
                        fp2 cellCenter =
                            CellToWorld(
                                cx,
                                cy);
                        if (!OrientedRectOverlapsAxisAlignedSquare(
                                center,
                                normalizedX,
                                normalizedY,
                                halfExtents,
                                cellCenter,
                                cellHalf))
                            continue;
                        SetLayerWalkable(
                            layer,
                            cx,
                            cy,
                            !blocked);
                    }
                }
            }
        }

        private static bool
            OrientedRectOverlapsAxisAlignedSquare(
                fp2 rectangleCenter,
                fp2 axisX,
                fp2 axisY,
                fp2 rectangleHalfExtents,
                fp2 squareCenter,
                fp squareHalfExtent)
        {
            fp2 delta =
                squareCenter -
                rectangleCenter;
            fp worldXProjection =
                rectangleHalfExtents.x *
                    fpmath.abs(axisX.x) +
                rectangleHalfExtents.y *
                    fpmath.abs(axisY.x);
            if (fpmath.abs(delta.x) >
                squareHalfExtent +
                worldXProjection)
                return false;

            fp worldYProjection =
                rectangleHalfExtents.x *
                    fpmath.abs(axisX.y) +
                rectangleHalfExtents.y *
                    fpmath.abs(axisY.y);
            if (fpmath.abs(delta.y) >
                squareHalfExtent +
                worldYProjection)
                return false;

            fp squareOnAxisX =
                squareHalfExtent *
                (fpmath.abs(axisX.x) +
                 fpmath.abs(axisX.y));
            if (fpmath.abs(
                    fpmath.dot(
                        delta,
                        axisX)) >
                rectangleHalfExtents.x +
                squareOnAxisX)
                return false;

            fp squareOnAxisY =
                squareHalfExtent *
                (fpmath.abs(axisY.x) +
                 fpmath.abs(axisY.y));
            return fpmath.abs(
                       fpmath.dot(
                           delta,
                           axisY)) <=
                   rectangleHalfExtents.y +
                   squareOnAxisY;
        }

        public void Clear()
        {
            int totalCells = _width * _height;
            if (_walkable != null)
            {
                for (int i = 0; i < _walkable.Length; i++)
                    _walkable[i] = true;
            }
            if (_walkableByRadiusClass != null)
            {
                for (int layer = 0; layer < _walkableByRadiusClass.Length; layer++)
                {
                    if (_walkableByRadiusClass[layer] != null)
                    {
                        for (int i = 0; i < _walkableByRadiusClass[layer].Length; i++)
                            _walkableByRadiusClass[layer][i] = true;
                    }
                }
            }
        }

        private int CellIndex(int cx, int cy) => cy * _width + cx;

        private void SetLayerWalkable(int layer, int cx, int cy, bool walkable)
        {
            if (_walkableByRadiusClass == null || layer < 0 || layer >= _walkableByRadiusClass.Length)
                return;
            _walkableByRadiusClass[layer][CellIndex(cx, cy)] = walkable;
            // Also update the legacy _walkable for backward compatibility (Medium layer)
            if (layer == (int)RadiusClass.Medium && _walkable != null)
                _walkable[CellIndex(cx, cy)] = walkable;
        }

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
