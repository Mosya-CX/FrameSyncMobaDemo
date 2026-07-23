using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Physics
{
    public sealed class PhysicsEntity2D : MonoBehaviour
    {
        public PhysicsTransform2D Transform2D { get; private set; }

        public PhysicsShape2D Shape { get; private set; }

        public PhysicsBounds2D Bounds { get; private set; }

        /// <summary>
        /// Query identity metadata (Physics v13.1 section 2.3).
        /// Set once at registration via <see cref="SetQueryInfo"/>; read-only after.
        /// </summary>
        public PhysicsEntityQueryInfo QueryInfo { get; private set; }

        public void SetQueryInfo(in PhysicsEntityQueryInfo queryInfo)
        {
            QueryInfo = queryInfo;
        }

        public void SetLogicPosition(fp2 position)
        {
            var transform = new PhysicsTransform2D(
                position,
                Transform2D.Position,
                Transform2D.Forward,
                Transform2D.Right);
            CommitTransform(transform);
        }

        public void SetLogicPose(fp2 position, fp2 forward)
        {
            fp2 nextForward = Transform2D.Forward;
            fp2 nextRight = Transform2D.Right;
            if (PhysicsGeometry2D.TryCreateFacing(forward, out fp2 normalized, out fp2 right))
            {
                nextForward = normalized;
                nextRight = right;
            }

            var transform = new PhysicsTransform2D(
                position,
                Transform2D.Position,
                nextForward,
                nextRight);
            CommitTransform(transform);
        }

        public void ApplyLogicPositionDelta(fp2 delta)
        {
            SetLogicPosition(Transform2D.Position + delta);
        }

        public void TeleportLogicPosition(fp2 position)
        {
            var transform = new PhysicsTransform2D(
                position,
                position,
                Transform2D.Forward,
                Transform2D.Right);
            CommitTransform(transform);
        }

        public void SetLogicForward(fp2 forward)
        {
            if (!PhysicsGeometry2D.TryCreateFacing(forward, out fp2 normalized, out fp2 right))
            {
                return;
            }

            var transform = new PhysicsTransform2D(
                Transform2D.Position,
                Transform2D.PrevPosition,
                normalized,
                right);
            CommitTransform(transform);
        }

        public void SetLogicShape(in PhysicsShape2D shape)
        {
            shape.ValidateSupported();
            PhysicsBounds2D bounds = PhysicsGeometry2D.CalculateBounds(Transform2D, shape);

            Shape = shape;
            Bounds = bounds;
        }

        internal void RestoreLogicSpatialState(
            in PhysicsTransform2D transform,
            in PhysicsShape2D shape)
        {
            PhysicsGeometry2D.ValidateTransform(transform);
            shape.ValidateSupported();
            PhysicsBounds2D bounds = PhysicsGeometry2D.CalculateBounds(transform, shape);

            Transform2D = transform;
            Shape = shape;
            Bounds = bounds;
        }

        /// <summary>
        /// Clears physics-component runtime state only (Physics v13.1 section 3.6).
        /// Resets Transform2D, Shape, Bounds and QueryInfo. Does NOT clear
        /// Unit Handler/EventBus/Stats or Projectile HitMemory/ModuleState/Def.
        /// </summary>
        internal void ClearRuntime()
        {
            Transform2D = default;
            Shape = default;
            Bounds = default;
            QueryInfo = default;
        }
        private void CommitTransform(in PhysicsTransform2D transform)
        {
            PhysicsBounds2D bounds = PhysicsGeometry2D.CalculateBounds(transform, Shape);

            Transform2D = transform;
            Bounds = bounds;
        }
    }
}
