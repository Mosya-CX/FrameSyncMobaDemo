using Unity.Mathematics.FixedPoint;
using UnityEngine;

namespace FrameSyncMoba.Physics
{
    /// <summary>
    /// MonoBehaviour component owning the authoritative 2D logical transform.
    /// Pathfinding Design v13.1 v13.1 patch note:
    /// "冻结所有帧同步 GameObject 的 Unity Transform 唯一写入点为 PhysicsEntity2D.LateUpdate"
    /// </summary>
    public sealed class PhysicsEntity2D : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("When enabled, LateUpdate syncs the logical Transform2D to the Unity Transform.")]
        private bool syncTransform = true;

        private Vector3 presentationStartPosition;
        private Vector3 presentationTargetPosition;
        private Quaternion presentationStartRotation = Quaternion.identity;
        private Quaternion presentationTargetRotation = Quaternion.identity;
        private float presentationPositionElapsed;
        private float presentationRotationElapsed;
        private bool presentationInitialized;
        private bool presentationSnapRequested = true;

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
            presentationSnapRequested = true;
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
            presentationSnapRequested = true;
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
            presentationInitialized = false;
            presentationSnapRequested = true;
        }

        /// <summary>
        /// Sole write point for Unity Transform from logical position.
        /// (Pathfinding Design v13.1 v13.1 patch note)
        /// Converts fp Transform2D to Vector3 and assigns to transform.position/forward.
        /// </summary>
        private void LateUpdate()
        {
            if (!syncTransform) return;

            var pos2D = Transform2D.Position;
            Vector3 desiredPosition = new Vector3(
                (float)pos2D.x,
                0f,
                (float)pos2D.y);

            var fwd2D = Transform2D.Forward;
            Quaternion desiredRotation = transform.rotation;
            if (fwd2D.x != fp.zero || fwd2D.y != fp.zero)
            {
                var fwd = new Vector3((float)fwd2D.x, 0f, (float)fwd2D.y);
                if (fwd.sqrMagnitude > 0.0001f)
                    desiredRotation = Quaternion.LookRotation(
                        fwd.normalized,
                        Vector3.up);
            }

            ProjectPresentationPose(desiredPosition, desiredRotation);
        }

        private void ProjectPresentationPose(
            Vector3 desiredPosition,
            Quaternion desiredRotation)
        {
            bool smoothingEnabled =
                PhysicsPresentationSettings.Enabled &&
                Application.isPlaying;
            float snapDistance =
                PhysicsPresentationSettings.SnapDistance;
            bool exceedsSnapDistance =
                presentationInitialized &&
                (desiredPosition - presentationTargetPosition)
                    .sqrMagnitude > snapDistance * snapDistance;
            if (!smoothingEnabled ||
                !presentationInitialized ||
                presentationSnapRequested ||
                exceedsSnapDistance)
            {
                transform.SetPositionAndRotation(
                    desiredPosition,
                    desiredRotation);
                presentationStartPosition = desiredPosition;
                presentationTargetPosition = desiredPosition;
                presentationStartRotation = desiredRotation;
                presentationTargetRotation = desiredRotation;
                presentationPositionElapsed =
                    PhysicsPresentationSettings.DurationSeconds;
                presentationRotationElapsed =
                    PhysicsPresentationSettings.DurationSeconds;
                presentationInitialized = true;
                presentationSnapRequested = false;
                return;
            }

            if ((desiredPosition - presentationTargetPosition)
                    .sqrMagnitude > 0.0000001f)
            {
                presentationStartPosition = transform.position;
                presentationTargetPosition = desiredPosition;
                presentationPositionElapsed = 0f;
            }
            if (Quaternion.Angle(
                    desiredRotation,
                    presentationTargetRotation) > 0.001f)
            {
                presentationStartRotation = transform.rotation;
                presentationTargetRotation = desiredRotation;
                presentationRotationElapsed = 0f;
            }

            presentationPositionElapsed += Time.unscaledDeltaTime;
            presentationRotationElapsed += Time.unscaledDeltaTime;
            float positionT = Mathf.Clamp01(
                presentationPositionElapsed /
                PhysicsPresentationSettings.DurationSeconds);
            float rotationT = Mathf.Clamp01(
                presentationRotationElapsed /
                PhysicsPresentationSettings.DurationSeconds);
            transform.SetPositionAndRotation(
                Vector3.LerpUnclamped(
                    presentationStartPosition,
                    presentationTargetPosition,
                    positionT),
                Quaternion.SlerpUnclamped(
                    presentationStartRotation,
                    presentationTargetRotation,
                    rotationT));
        }

        private void CommitTransform(in PhysicsTransform2D transform)
        {
            PhysicsBounds2D bounds = PhysicsGeometry2D.CalculateBounds(transform, Shape);

            Transform2D = transform;
            Bounds = bounds;
        }
    }
}
