using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public sealed class MovementHandler :
        UnitHandler,
        IMovementAgent,
        IRollback<MovementSnapshot>
    {
        private enum MovementMode : byte
        {
            Idle,
            RouteMove,
            Dash,
            ForcedMove,
        }

        private MovementSnapshot _snapshot;
        private MoveIntent _currentIntent;
        private IMovementCollisionResolver _collisionResolver;
        private LocomotionResult _pendingLocomotion;
        private fp2 _pendingRvoVelocity;
        private bool _hasPendingRvo;
        private DashRuntime _dash;
        private ForcedMoveRuntime _forcedMove;
        private fp _moveSpeed;
        private fp2 _velocity;
        private bool _isMoving;
        private fp2 _targetDirection;

        internal void InitializeRuntime(
            fp2 startPosition,
            fp moveSpeed)
        {
            if (moveSpeed < fp.zero)
            {
                throw new DeterministicSimulationException(
                    "Movement speed must not be negative.");
            }

            _snapshot = MovementSnapshot.Default;
            _moveSpeed = moveSpeed;
            _velocity = fp2.zero;
            _isMoving = false;
            _targetDirection = fp2.zero;
            _currentIntent = MoveIntent.None;
            ClearTickInputs();
            _dash = default;
            _forcedMove = default;
        }

        public ref readonly MovementSnapshot Snapshot
        {
            get
            {
                _snapshot.Dash = _dash;
                _snapshot.ForcedMove = _forcedMove;
                return ref _snapshot;
            }
        }
        public fp2 Position => CurrentPosition;
        public fp2 Facing => CurrentForward;
        public fp2 Velocity => _velocity;
        public fp MoveSpeed => _moveSpeed;
        public bool IsMoving => _isMoving;
        public fp2 TargetDirection => _targetDirection;
        public bool IsDashing => _dash.IsActive;
        public bool HasForcedMove => _forcedMove.IsActive;

        public void SetCollisionResolver(
            IMovementCollisionResolver resolver)
        {
            _collisionResolver = resolver;
        }

        public void SetMoveSpeed(fp moveSpeed)
        {
            if (moveSpeed < fp.zero)
            {
                throw new DeterministicSimulationException(
                    "Movement speed must not be negative.");
            }
            _moveSpeed = moveSpeed;
        }

        public void ApplyMoveInput(in MoveIntent intent)
        {
            if (Owner != null &&
                Owner.HitReaction.InterruptsMovement)
            {
                return;
            }
            _currentIntent = intent;
        }

        public void ApplyRouteMovement(
            in LocomotionResult locomotion)
        {
            _pendingLocomotion = locomotion;
        }

        public void ApplyRvoResult(in RvoResult result)
        {
            if (!result.HasResult)
            {
                return;
            }
            if (Owner != null &&
                result.UnitUid != Owner.UnitUid)
            {
                throw new DeterministicSimulationException(
                    "RVO result UnitUid does not match its MovementHandler owner.");
            }
            _pendingRvoVelocity = result.FinalVelocity;
            _hasPendingRvo = true;
        }

        public void TickUpdate()
        {
            MovementMode mode = ResolveMovementMode();
            switch (mode)
            {
                case MovementMode.ForcedMove:
                    AdvanceForcedMove();
                    break;
                case MovementMode.Dash:
                    AdvanceDash();
                    break;
                case MovementMode.RouteMove:
                    AdvanceRouteMove();
                    break;
                default:
                    ApplyStationaryPose();
                    break;
            }

            _currentIntent = MoveIntent.None;
            ClearTickInputs();
        }

        public bool StartDash(in DashRequest request)
        {
            ValidateDashRequest(request);
            if (_dash.IsActive)
            {
                return false;
            }

            fp2 start = CurrentPosition;
            Physics.PhysicsGeometry2D.TryCreateFacing(
                request.Direction,
                out fp2 direction,
                out _);
            _dash = new DashRuntime
            {
                IsActive = true,
                StartTick =
                    SimulationTickContext.Current.Tick,
                ConfigId = request.ConfigId,
                DurationTicks =
                    request.DurationTicks,
                StartPosition = start,
                Direction = direction,
                TargetPosition =
                    start + direction * request.Distance,
                WallPolicy = request.WallPolicy,
            };
            return true;
        }

        public void ApplyDash(
            fp2 direction,
            fp distance,
            fp durationTicks)
        {
            int ticks = (int)durationTicks;
            if (durationTicks != (fp)ticks)
            {
                throw new DeterministicSimulationException(
                    "Dash duration must be a whole positive Tick count.");
            }
            StartDash(new DashRequest(
                1,
                direction,
                distance,
                ticks));
        }

        public bool StopDash(int configId = 0)
        {
            if (!_dash.IsActive ||
                (configId > 0 &&
                 _dash.ConfigId != configId))
            {
                return false;
            }
            _dash = default;
            return true;
        }

        public bool IsDashActive(int configId) =>
            _dash.IsActive &&
            _dash.ConfigId == configId;

        public void StartForcedMove(
            in ResolvedForcedMove request)
        {
            if (_forcedMove.IsActive)
            {
                throw new DeterministicSimulationException(
                    "StartForcedMove requires no active forced-move runtime.");
            }
            BeginForcedMove(request);
        }

        public void ReplaceForcedMove(
            in ResolvedForcedMove request)
        {
            BeginForcedMove(request);
        }

        public void StopForcedMove(
            CrowdControlHandle sourceHandle)
        {
            if (!_forcedMove.IsActive ||
                _forcedMove.SourceControlHandle !=
                    sourceHandle)
            {
                return;
            }
            _forcedMove = default;
        }

        public void ForceSetPosition(fp2 position) =>
            ApplyTeleport(position);

        public void ApplyTeleport(fp2 position)
        {
            Owner?.PhysicsEntity?.TeleportLogicPosition(
                position);
            _velocity = fp2.zero;
            _isMoving = false;
            _targetDirection = fp2.zero;
        }

        public void ApplyCorrection(
            in MovementCorrectionRequest correction)
        {
            if (Owner != null &&
                correction.UnitUid.IsValid() &&
                correction.UnitUid != Owner.UnitUid)
            {
                throw new DeterministicSimulationException(
                    "Movement correction UnitUid does not match its owner.");
            }

            Owner?.PhysicsEntity?.ApplyLogicPositionDelta(
                correction.Delta);
        }

        public void Capture(ref MovementSnapshot state)
        {
            state = _snapshot;
            state.Dash = _dash;
            state.ForcedMove = _forcedMove;
        }

        public void Restore(in MovementSnapshot state)
        {
            ValidateSnapshot(state);
            _snapshot = state;
            _dash = state.Dash;
            _forcedMove = state.ForcedMove;
            ClearDerivedMovementState();
            _currentIntent = MoveIntent.None;
            ClearTickInputs();
        }

        public void Resolve(in RollbackContext context)
        {
            if (Owner?.CrowdControl == null)
            {
                return;
            }
            CrowdControlHandle controlHandle =
                Owner.CrowdControl.ActiveForcedMoveHandle;
            if (_forcedMove.IsActive !=
                controlHandle.IsValid ||
                (_forcedMove.IsActive &&
                 _forcedMove.SourceControlHandle !=
                    controlHandle))
            {
                throw new DeterministicSimulationException(
                    "Movement forced-move runtime and CrowdControl ownership disagree after restore.");
            }
        }

        public void Rebuild(in RollbackContext context)
        {
            ClearDerivedMovementState();
        }

        public override void ClearForDeath() =>
            ClearRuntimeMovement();

        public override void ClearForRespawn() =>
            ClearRuntimeMovement();

        public override void ResetForPool()
        {
            _snapshot = MovementSnapshot.Default;
            _currentIntent = MoveIntent.None;
            ClearTickInputs();
            _dash = default;
            _forcedMove = default;
            _collisionResolver = null;
            _moveSpeed = fp.zero;
            ClearDerivedMovementState();
        }

        private MovementMode ResolveMovementMode()
        {
            if (_forcedMove.IsActive)
            {
                return MovementMode.ForcedMove;
            }
            if (Owner != null &&
                !Owner.CanRunActiveGameplayThisTick)
            {
                return MovementMode.Idle;
            }
            if (_dash.IsActive)
            {
                return MovementMode.Dash;
            }
            if (Owner != null &&
                (Owner.HitReaction.InterruptsMovement ||
                 Owner.CrowdControl?.IsMovementRestricted ==
                    true))
            {
                return MovementMode.Idle;
            }
            if (_pendingLocomotion.HasMovement ||
                _currentIntent.HasInput)
            {
                return MovementMode.RouteMove;
            }
            return MovementMode.Idle;
        }

        private void AdvanceRouteMove()
        {
            fp2 direction;
            fp desiredSpeed;
            fp2 desiredVelocity;
            if (_pendingLocomotion.HasMovement)
            {
                direction =
                    _pendingLocomotion.DesiredDirection;
                desiredSpeed =
                    _pendingLocomotion.DesiredSpeed;
                desiredVelocity =
                    _pendingLocomotion.AllowRVO &&
                    _hasPendingRvo
                        ? _pendingRvoVelocity
                        : direction * desiredSpeed;
            }
            else
            {
                direction = _currentIntent.Direction;
                desiredSpeed = _moveSpeed;
                desiredVelocity =
                    direction * desiredSpeed;
            }

            fp deltaTicks =
                SimulationTickContext.Current.DeltaTick;
            CommitContinuousMovement(
                desiredVelocity * deltaTicks,
                direction,
                desiredVelocity);
        }

        private void AdvanceDash()
        {
            AdvanceTrajectory(
                _dash.StartTick,
                GetDashDurationTicks(),
                _dash.StartPosition,
                _dash.TargetPosition,
                _dash.Direction,
                _dash.WallPolicy,
                out bool finished);
            if (finished)
            {
                _dash = default;
            }
        }

        private void AdvanceForcedMove()
        {
            CrowdControlHandle source =
                _forcedMove.SourceControlHandle;
            AdvanceTrajectory(
                _forcedMove.StartTick,
                _forcedMove.DurationTicks,
                _forcedMove.StartPosition,
                _forcedMove.TargetPosition,
                _forcedMove.Direction,
                _forcedMove.WallPolicy,
                out bool finished);
            if (!finished)
            {
                return;
            }

            _forcedMove = default;
            Owner?.CrowdControl?.OnForcedMoveFinished(
                source);
        }

        private void AdvanceTrajectory(
            int startTick,
            int durationTicks,
            fp2 startPosition,
            fp2 targetPosition,
            fp2 direction,
            ForceMoveWallPolicy wallPolicy,
            out bool finished)
        {
            int elapsed =
                SimulationTickContext.Current.Tick -
                startTick;
            if (elapsed < 0)
            {
                throw new DeterministicSimulationException(
                    "Special movement started in a future Tick.");
            }

            int completedTicks =
                System.Math.Min(
                    elapsed + 1,
                    durationTicks);
            fp progress =
                (fp)completedTicks /
                (fp)durationTicks;
            fp2 desiredPosition =
                startPosition +
                (targetPosition - startPosition) *
                progress;
            fp2 delta =
                desiredPosition - CurrentPosition;
            if (wallPolicy ==
                ForceMoveWallPolicy.StopAtWall)
            {
                delta = ResolveStaticWall(delta);
            }
            CommitContinuousMovement(
                delta,
                direction,
                delta);
            finished =
                completedTicks >= durationTicks;
        }

        private void CommitContinuousMovement(
            fp2 desiredDelta,
            fp2 desiredFacing,
            fp2 velocity)
        {
            fp2 start = CurrentPosition;
            fp2 desiredPosition =
                start + desiredDelta;
            if (_collisionResolver != null)
            {
                desiredPosition =
                    _collisionResolver.ClampPosition(
                        desiredPosition,
                        start,
                        CurrentRadius,
                        CurrentRadiusClass);
            }

            fp2 actualDelta =
                desiredPosition - start;
            fp2 facing = CurrentForward;
            fp2 facingSource =
                actualDelta.x != fp.zero ||
                actualDelta.y != fp.zero
                    ? actualDelta
                    : desiredFacing;
            if (Physics.PhysicsGeometry2D.TryCreateFacing(
                    facingSource,
                    out fp2 normalized,
                    out _))
            {
                facing = normalized;
            }

            Owner?.PhysicsEntity?.SetLogicPose(
                desiredPosition,
                facing);
            _velocity = velocity;
            _isMoving =
                actualDelta.x != fp.zero ||
                actualDelta.y != fp.zero;
            _targetDirection =
                _isMoving
                    ? facing
                    : fp2.zero;
        }

        private fp2 ResolveStaticWall(fp2 delta)
        {
            if (Owner?.World?.PathGrid == null ||
                (delta.x == fp.zero &&
                 delta.y == fp.zero))
            {
                return delta;
            }
            return ForcedMoveExecutor.ResolveWall(
                CurrentPosition,
                delta,
                Owner.World.PathGrid,
                CurrentRadiusClass);
        }

        private void BeginForcedMove(
            in ResolvedForcedMove request)
        {
            ValidateForcedMove(request);
            fp2 start = CurrentPosition;
            Physics.PhysicsGeometry2D.TryCreateFacing(
                request.Direction,
                out fp2 direction,
                out _);
            _forcedMove = new ForcedMoveRuntime
            {
                IsActive = true,
                SourceControlHandle =
                    request.SourceControlHandle,
                StartTick =
                    SimulationTickContext.Current.Tick,
                DurationTicks =
                    request.DurationTicks,
                StartPosition = start,
                Direction = direction,
                TargetPosition =
                    request.TargetPosition,
                ConfigId = request.ConfigId,
                WallPolicy = request.WallPolicy,
            };
        }

        private int GetDashDurationTicks()
        {
            return _dash.DurationTicks;
        }

        private void ValidateDashRequest(
            in DashRequest request)
        {
            if (request.ConfigId <= 0 ||
                request.Distance <= fp.zero ||
                request.DurationTicks <= 0 ||
                fpmath.lengthsq(request.Direction) <=
                    fp.zero ||
                request.WallPolicy <
                    ForceMoveWallPolicy.StopAtWall ||
                request.WallPolicy >
                    ForceMoveWallPolicy.PassThrough)
            {
                throw new DeterministicSimulationException(
                    "Dash requires a positive config ID, distance and duration, a non-zero direction, and a valid wall policy.");
            }
        }

        private static void ValidateForcedMove(
            in ResolvedForcedMove request)
        {
            if (!request.SourceControlHandle.IsValid ||
                request.ConfigId <= 0 ||
                request.DurationTicks <= 0 ||
                fpmath.lengthsq(request.Direction) <=
                    fp.zero ||
                request.WallPolicy <
                    ForceMoveWallPolicy.StopAtWall ||
                request.WallPolicy >
                    ForceMoveWallPolicy.PassThrough)
            {
                throw new DeterministicSimulationException(
                    "Resolved forced move contains invalid source, config, duration, direction or wall policy.");
            }
        }

        private static void ValidateSnapshot(
            in MovementSnapshot state)
        {
            if (state.Dash.IsActive &&
                (state.Dash.ConfigId <= 0 ||
                 state.Dash.DurationTicks <= 0 ||
                 fpmath.lengthsq(
                     state.Dash.Direction) <= fp.zero))
            {
                throw new DeterministicSimulationException(
                    "Active Dash snapshot is invalid.");
            }
            if (state.ForcedMove.IsActive &&
                (!state.ForcedMove
                    .SourceControlHandle.IsValid ||
                 state.ForcedMove.ConfigId <= 0 ||
                 state.ForcedMove.DurationTicks <= 0 ||
                 fpmath.lengthsq(
                     state.ForcedMove.Direction) <=
                     fp.zero))
            {
                throw new DeterministicSimulationException(
                    "Active forced-move snapshot is invalid.");
            }
        }

        private fp2 CurrentPosition =>
            Owner?.PhysicsEntity != null
                ? Owner.PhysicsEntity.Transform2D.Position
                : fp2.zero;

        private fp2 CurrentForward =>
            Owner?.PhysicsEntity != null
                ? Owner.PhysicsEntity.Transform2D.Forward
                : new fp2(fp.one, fp.zero);

        private fp CurrentRadius =>
            Owner?.PhysicsEntity != null
                ? Owner.PhysicsEntity.Shape.Radius
                : RadiusClassHelper.MediumRadius;

        private RadiusClass CurrentRadiusClass =>
            RadiusClassHelper.FromRadius(CurrentRadius);

        private void ApplyStationaryPose()
        {
            if (Owner?.PhysicsEntity != null)
            {
                Owner.PhysicsEntity.SetLogicPose(
                    CurrentPosition,
                    CurrentForward);
            }
            ClearDerivedMovementState();
        }

        private void ClearTickInputs()
        {
            _pendingLocomotion = default;
            _pendingRvoVelocity = fp2.zero;
            _hasPendingRvo = false;
        }

        private void ClearRuntimeMovement()
        {
            _currentIntent = MoveIntent.None;
            ClearTickInputs();
            _dash = default;
            _forcedMove = default;
            ApplyStationaryPose();
        }

        private void ClearDerivedMovementState()
        {
            _velocity = fp2.zero;
            _isMoving = false;
            _targetDirection = fp2.zero;
        }
    }
}
