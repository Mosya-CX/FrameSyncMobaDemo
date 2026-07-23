using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public sealed class MovementHandler : UnitHandler, IMovementAgent, IRollback<MovementSnapshot>
    {
        private MovementSnapshot _snapshot;
        private MoveIntent _currentIntent;
        private IMovementCollisionResolver _collisionResolver;
        private static readonly fp UnitRadius = (fp)0.5f;
        private LocomotionResult _pendingLocomotion;

        internal void InitializeRuntime(fp2 startPosition, fp moveSpeed)
        {
            _snapshot = MovementSnapshot.Default;
            _snapshot.Position = startPosition;
            _snapshot.MoveSpeed = moveSpeed;
            _currentIntent = MoveIntent.None;
            _pendingLocomotion = default;
        }

        public ref readonly MovementSnapshot Snapshot => ref _snapshot;

        public void SetCollisionResolver(IMovementCollisionResolver resolver)
        {
            _collisionResolver = resolver;
        }

        public void SetMoveSpeed(fp moveSpeed)
        {
            _snapshot.MoveSpeed = moveSpeed;
        }

        public void ApplyMoveInput(in MoveIntent intent)
        {
            if (Owner != null && Owner.HitReaction.InterruptsMovement) return;
            _currentIntent = intent;
        }

        /// <summary>
        /// Applies the per-tick locomotion result from UnitLocomotionAgent.
        /// Called before TickUpdate to provide route-based movement.
        /// </summary>
        public void ApplyRouteMovement(in LocomotionResult locomotion)
        {
            if (Owner != null && Owner.HitReaction.InterruptsMovement) return;
            _pendingLocomotion = locomotion;
        }

        public void TickUpdate(fp deltaTime)
        {
            if (Owner != null && Owner.HitReaction.IsActive && Owner.HitReaction.InterruptsMovement)
            {
                _snapshot.Velocity = fp2.zero;
                _snapshot.IsMoving = false;
                _currentIntent = MoveIntent.None;
                _pendingLocomotion = default;
                return;
            }
            if (_pendingLocomotion.HasMovement)
            {
                // Route-movement mode: use LocomotionResult direction and speed
                fp2 desiredVelocity = _pendingLocomotion.DesiredDirection * _pendingLocomotion.DesiredSpeed;
                _snapshot.Velocity = desiredVelocity;
                fp2 desiredPosition = _snapshot.Position + desiredVelocity * deltaTime;

                if (_collisionResolver != null)
                {
                    desiredPosition = _collisionResolver.ClampPosition(
                        desiredPosition, _snapshot.Position, UnitRadius);
                }

                _snapshot.Position = desiredPosition;
                _snapshot.IsMoving = true;
                _snapshot.TargetDirection = _pendingLocomotion.DesiredDirection;

                if (Physics.PhysicsGeometry2D.TryCreateFacing(
                    _pendingLocomotion.DesiredDirection, out fp2 facing, out _))
                {
                    _snapshot.Facing = facing;
                }
            }
            else if (_currentIntent.HasInput)
            {
                fp2 desiredVelocity = _currentIntent.Direction * _snapshot.MoveSpeed;
                _snapshot.Velocity = desiredVelocity;
                fp2 desiredPosition = _snapshot.Position + desiredVelocity * deltaTime;

                if (_collisionResolver != null)
                {
                    desiredPosition = _collisionResolver.ClampPosition(
                        desiredPosition, _snapshot.Position, UnitRadius);
                }

                _snapshot.Position = desiredPosition;
                _snapshot.IsMoving = true;
                _snapshot.TargetDirection = _currentIntent.Direction;

                if (Physics.PhysicsGeometry2D.TryCreateFacing(
                    _currentIntent.Direction, out fp2 facing, out _))
                {
                    _snapshot.Facing = facing;
                }
            }
            else
            {
                _snapshot.Velocity = fp2.zero;
                _snapshot.IsMoving = false;
                _snapshot.TargetDirection = fp2.zero;
            }

            _currentIntent = MoveIntent.None;
            _pendingLocomotion = default;
        }

        public void ForceSetPosition(fp2 position)
        {
            _snapshot.Position = position;
            _snapshot.Velocity = fp2.zero;
            _snapshot.IsMoving = false;
        }

        public void ApplyForcedMovement(fp2 delta)
        {
            _snapshot.Position += delta;
            _snapshot.Velocity = delta;
            _snapshot.IsMoving = delta.x != fp.zero || delta.y != fp.zero;
        }

        public void Capture(ref MovementSnapshot state)
        {
            state = _snapshot;
        }

        public void Restore(in MovementSnapshot state)
        {
            _snapshot = state;
            _currentIntent = MoveIntent.None;
            _pendingLocomotion = default;
        }

        public void Resolve(in RollbackContext context) { }
        public void Rebuild(in RollbackContext context) { }

        public override void ClearForDeath()
        {
            _currentIntent = MoveIntent.None;
            _pendingLocomotion = default;
            _snapshot.Velocity = fp2.zero;
            _snapshot.IsMoving = false;
        }

        public override void ResetForPool()
        {
            _snapshot = MovementSnapshot.Default;
            _currentIntent = MoveIntent.None;
            _pendingLocomotion = default;
            _collisionResolver = null;
        }
    }
}
