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
        private fp2 _pendingRvoVelocity;
        private bool _hasPendingRvo;

        internal void InitializeRuntime(fp2 startPosition, fp moveSpeed)
        {
            _snapshot = MovementSnapshot.Default;
            _snapshot.Position = startPosition;
            _snapshot.MoveSpeed = moveSpeed;
            _currentIntent = MoveIntent.None;
            _pendingLocomotion = default;
            _pendingRvoVelocity = fp2.zero;
            _hasPendingRvo = false;
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

        /// <summary>
        /// Applies the per-tick RVO avoidance result.
        /// (Pathfinding Design v13.1 section 10.6, section 11.2)
        /// </summary>
        public void ApplyRvoResult(in RvoResult rvoResult)
        {
            if (!rvoResult.HasResult) return;
            _pendingRvoVelocity = rvoResult.FinalVelocity;
            _hasPendingRvo = true;
        }

        /// <summary>
        /// Per-tick movement update. Reads SimulationTickContext.Current.DeltaTick
        /// internally per Pathfinding Design v13.1 section 1.4.
        /// </summary>
        public void TickUpdate()
        {
            fp deltaTime = SimulationTickContext.Current.DeltaTick;

            // Dash gets highest priority -- overrides normal movement
            if (_isDashing)
            {
                AdvanceDash(deltaTime);
                _currentIntent = MoveIntent.None;
                _pendingLocomotion = default;
                _pendingRvoVelocity = fp2.zero;
                _hasPendingRvo = false;
                return;
            }

            if (Owner != null && Owner.HitReaction.IsActive && Owner.HitReaction.InterruptsMovement)
            {
                _snapshot.Velocity = fp2.zero;
                _snapshot.IsMoving = false;
                _currentIntent = MoveIntent.None;
                _pendingLocomotion = default;
                _pendingRvoVelocity = fp2.zero;
                _hasPendingRvo = false;
                return;
            }
            if (_pendingLocomotion.HasMovement)
            {
                // Route-movement mode: use LocomotionResult direction and speed
                fp2 desiredVelocity = _pendingLocomotion.DesiredDirection * _pendingLocomotion.DesiredSpeed;

                // RVO override: use RVO-computed velocity when AllowRVO is set
                if (_pendingLocomotion.AllowRVO && _hasPendingRvo)
                {
                    desiredVelocity = _pendingRvoVelocity;
                }

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
            _pendingRvoVelocity = fp2.zero;
            _hasPendingRvo = false;
        }

        public void ForceSetPosition(fp2 position)
        {
            _snapshot.Position = position;
            _snapshot.Velocity = fp2.zero;
            _snapshot.IsMoving = false;
        }

        /// <summary>
        /// Apply a forced movement delta (knockback, dash, pull).
        /// Optionally consumes RVO velocity for avoidance during forced moves.
        /// Resolves static wall collision.
        /// (Pathfinding Design v13.1 sections 11.4, 11.6)
        /// </summary>
        public void ApplyForcedMovement(fp2 delta, bool allowRVO = false, fp2 rvoVelocity = default)
        {
            fp2 effectiveDelta = delta;

            // RVO override: use avoidance velocity when enabled
            if (allowRVO && (_hasPendingRvo || (rvoVelocity.x != fp.zero || rvoVelocity.y != fp.zero)))
            {
                fp2 rvo = _hasPendingRvo ? _pendingRvoVelocity : rvoVelocity;
                if (rvo.x != fp.zero || rvo.y != fp.zero)
                    effectiveDelta = rvo;
            }

            // Resolve static wall collision
            if (_collisionResolver != null)
            {
                fp2 clamped = _collisionResolver.ClampPosition(
                    _snapshot.Position + effectiveDelta,
                    _snapshot.Position,
                    UnitRadius);
                effectiveDelta = clamped - _snapshot.Position;
            }

            _snapshot.Position += effectiveDelta;
            _snapshot.Velocity = effectiveDelta;
            _snapshot.IsMoving = effectiveDelta.x != fp.zero || effectiveDelta.y != fp.zero;
        }


        // Dash runtime state (Pathfinding Design v13.1 sections 11.5-11.6)
        private bool _isDashing;
        private fp _dashRemainingDistance;
        private fp2 _dashDirection;
        private fp _dashSpeed;
        private int _dashEndTick;

        /// <summary>
        /// Initiate a dash movement. Dash overrides normal route/input movement 
        /// and RVO during execution, ending when distance or duration is exhausted.
        /// (Pathfinding Design v13.1 section 11.5)
        /// </summary>
        public void ApplyDash(fp2 direction, fp distance, fp durationTicks)
        {
            if (distance <= fp.zero) return;
            _isDashing = true;
            _dashRemainingDistance = distance;
            _dashSpeed = distance / durationTicks;
            if (Physics.PhysicsGeometry2D.TryCreateFacing(direction, out fp2 facing, out _))
                _dashDirection = facing;
            else
                _dashDirection = direction;
            _dashEndTick = SimulationTickContext.Current.Tick + (int)durationTicks;
        }

        /// <summary>
        /// Per-tick dash advancement. Called from TickUpdate before normal movement.
        /// </summary>
        private void AdvanceDash(fp deltaTime)
        {
            if (!_isDashing) return;
            fp step = _dashSpeed * deltaTime;
            if (step >= _dashRemainingDistance)
            {
                step = _dashRemainingDistance;
                _isDashing = false;
            }
            _dashRemainingDistance -= step;
            fp2 delta = _dashDirection * step;

            // Wall collision for dash
            if (_collisionResolver != null)
            {
                fp2 clamped = _collisionResolver.ClampPosition(
                    _snapshot.Position + delta, _snapshot.Position, UnitRadius);
                delta = clamped - _snapshot.Position;
            }

            _snapshot.Position += delta;
            _snapshot.Velocity = delta;
            _snapshot.IsMoving = delta.x != fp.zero || delta.y != fp.zero;
            if (_isDashing && Physics.PhysicsGeometry2D.TryCreateFacing(_dashDirection, out fp2 facing, out _))
                _snapshot.Facing = facing;
        }

        /// <summary>
        /// Instantly teleport to a target position.
        /// Calls PhysicsEntity2D.TeleportLogicPosition for spatial state sync.
        /// (Pathfinding Design v13.1 section 11.7)
        /// </summary>
        public void ApplyTeleport(fp2 position)
        {
            ForceSetPosition(position);
            Owner?.PhysicsEntity?.TeleportLogicPosition(position);
        }

        /// <summary>
        /// Apply a wall-penetration push-out correction.
        /// (Pathfinding Design v13.1 section 12.3, Candidate 0100)
        /// </summary>
        public void ApplyCorrection(in MovementCorrectionRequest correction)
        {
            _snapshot.Position += correction.Delta;
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
            _pendingRvoVelocity = fp2.zero;
            _hasPendingRvo = false;
        }

        public void Resolve(in RollbackContext context) { }
        public void Rebuild(in RollbackContext context) { }

        public override void ClearForDeath()
        {
            _currentIntent = MoveIntent.None;
            _pendingLocomotion = default;
            _pendingRvoVelocity = fp2.zero;
            _hasPendingRvo = false;
            _isDashing = false;
            _dashRemainingDistance = fp.zero;
            _snapshot.Velocity = fp2.zero;
            _snapshot.IsMoving = false;
        }

        public override void ResetForPool()
        {
            _snapshot = MovementSnapshot.Default;
            _currentIntent = MoveIntent.None;
            _pendingLocomotion = default;
            _pendingRvoVelocity = fp2.zero;
            _hasPendingRvo = false;
            _isDashing = false;
            _dashRemainingDistance = fp.zero;
            _collisionResolver = null;
        }
    }
}
