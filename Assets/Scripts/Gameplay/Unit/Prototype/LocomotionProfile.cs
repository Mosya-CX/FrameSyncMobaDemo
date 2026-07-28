using System;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    /// <summary>
    /// Unit Framework v27.3 §1.6 — movement execution parameters for a Unit.
    /// Read from UnitPrototype during spawn and used by MovementHandler,
    /// UnitLocomotionAgent, and RVO.
    /// </summary>
    [Serializable]
    public struct LocomotionProfile
    {
        /// <summary>Base move speed in logic units per tick (before stat modifiers).</summary>
        public fp BaseMoveSpeed;

        /// <summary>Collision radius for RVO and physics.</summary>
        public fp CollisionRadius;

        /// <summary>Radius class for FlowField lane selection.</summary>
        public RadiusClass RadiusClass;

        /// <summary>Maximum turn rate (radians per tick). 0 = instant turn.</summary>
        public fp MaxTurnRate;

        /// <summary>Stop distance for point-move commands.</summary>
        public fp ArriveDistance;

        public static readonly LocomotionProfile DefaultHero = new LocomotionProfile
        {
            BaseMoveSpeed = (fp)3.5m,
            CollisionRadius = (fp)0.5m,
            RadiusClass = RadiusClass.Medium,
            MaxTurnRate = fp.zero,
            ArriveDistance = (fp)0.05m,
        };

        public static readonly LocomotionProfile DefaultMinion = new LocomotionProfile
        {
            BaseMoveSpeed = (fp)3.0m,
            CollisionRadius = (fp)0.35m,
            RadiusClass = RadiusClass.Small,
            MaxTurnRate = fp.zero,
            ArriveDistance = (fp)0.05m,
        };

        public static readonly LocomotionProfile DefaultMonster = new LocomotionProfile
        {
            BaseMoveSpeed = (fp)2.5m,
            CollisionRadius = (fp)0.6m,
            RadiusClass = RadiusClass.Medium,
            MaxTurnRate = fp.zero,
            ArriveDistance = (fp)0.05m,
        };

        public static readonly LocomotionProfile DefaultTower = new LocomotionProfile
        {
            BaseMoveSpeed = fp.zero,
            CollisionRadius = (fp)1.0m,
            RadiusClass = RadiusClass.Large,
            MaxTurnRate = fp.zero,
            ArriveDistance = (fp)0.05m,
        };
    }
}
