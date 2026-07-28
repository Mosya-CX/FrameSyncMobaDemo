using System;
using Unity.Mathematics.FixedPoint;
using PhysicsShapeKind = FrameSyncMoba.Physics.PhysicsShapeKind;

namespace FrameSyncMoba.Unit
{
    [Serializable]
    public struct PhysicsProfile2D
    {
        public PhysicsShapeKind DefaultShape;
        public fp ShapeParam;
        public fp2 InitialForward;
        public bool RegisterForSpatialQuery;

        public static readonly PhysicsProfile2D DefaultCircle = new PhysicsProfile2D
        {
            DefaultShape = PhysicsShapeKind.Circle,
            ShapeParam = (fp)0.5m,
            InitialForward = new fp2(fp.zero, fp.one),
            RegisterForSpatialQuery = true,
        };

        public static readonly PhysicsProfile2D DefaultTower = new PhysicsProfile2D
        {
            DefaultShape = PhysicsShapeKind.Circle,
            ShapeParam = (fp)1.0m,
            InitialForward = new fp2(fp.zero, fp.one),
            RegisterForSpatialQuery = true,
        };
    }
}
