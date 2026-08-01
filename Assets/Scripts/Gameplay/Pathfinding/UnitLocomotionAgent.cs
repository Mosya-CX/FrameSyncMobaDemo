using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;

namespace FrameSyncMoba.Unit
{
    public sealed class UnitLocomotionAgent : IRollback<LocomotionAgentSnapshot>
    {
        private readonly Unit _owner;
        private readonly PathGridMap2D _grid;
        private readonly AStarPathService _aStar;
        private readonly PathFollower2D _follower;
        private readonly TeamFlowFieldService _flowFieldService;

        private MovementTask _currentTask;
        private RouteRuntime _route;

        // Flow-field registry for runtime lookup
        private FlowFieldRegistry _flowFieldRegistry;

        private const int RepathCooldownTicks = 10;
        private static readonly fp RepathThresholdSq =
            (fp)0.25m;
        private static readonly fp DirectMaxDistanceSq =
            (fp)9m;

        public UnitLocomotionAgent(Unit owner, PathGridMap2D grid)
        {
            _owner = owner;
            _grid = grid;
            _aStar = new AStarPathService(grid);
            _follower = new PathFollower2D(grid);
            _flowFieldService = new TeamFlowFieldService(grid);
            _currentTask = MovementTask.None;
            _route = RouteRuntime.Empty;
        }

        public Unit Owner => _owner;
        public PathGridMap2D Grid => _grid;
        public ref readonly MovementTask CurrentTask => ref _currentTask;
        public ref readonly RouteRuntime Route => ref _route;

        /// <summary>
        /// Current logical position. Reads from PhysicsEntity2D per
        /// Pathfinding Design v13.1 section 1.1 contract.
        /// </summary>
        public fp2 Position => _owner.PhysicsEntity?.Transform2D.Position ?? fp2.zero;

        /// <summary>
        /// Set the flow-field registry for runtime flow-field lookups.
        /// </summary>
        public void SetFlowFieldRegistry(FlowFieldRegistry registry)
        {
            _flowFieldRegistry = registry;
        }

        public MoveAcceptResult AcceptRouteRequest(RouteMoveRequest request)
        {
            if (!request.Target.HasTarget)
                return MoveAcceptResult.Rejected_InvalidTarget;

            if (!_owner.CanRunActiveGameplayThisTick)
                return MoveAcceptResult.Rejected_NoAgent;

            if (MatchesActiveTask(request))
                return MoveAcceptResult.Rejected_AlreadyActive;

            _currentTask = new MovementTask
            {
                Purpose = request.Purpose,
                Target = request.Target,
                StopDistance = request.StopDistance,
                AllowRVO = request.AllowRVO,
                AllowRepath = request.AllowRepath,
                State = MovementTaskState.Active,
            };

            RouteKind routeKind =
                ResolveRouteKind(request);
            _route = new RouteRuntime
            {
                Kind = routeKind,
                NeedRepath =
                    routeKind == RouteKind.AStar,
                LastPathTargetPosition = request.Target.Position ?? fp2.zero,
                FlowFieldKey =
                    routeKind == RouteKind.FlowField
                        ? new FlowFieldKey(
                            _owner.TeamId.Value,
                            OwnerRadiusClass)
                            .Packed
                        : 0,
                FollowerState = PathFollowerState.Empty,
            };

            _follower.Reset();

            return MoveAcceptResult.Accepted;
        }

        private bool MatchesActiveTask(
            in RouteMoveRequest request)
        {
            if (_currentTask.State != MovementTaskState.Active ||
                _currentTask.Purpose != request.Purpose ||
                _currentTask.StopDistance != request.StopDistance ||
                _currentTask.AllowRVO != request.AllowRVO ||
                _currentTask.AllowRepath != request.AllowRepath)
                return false;

            if (_currentTask.Target.Position.HasValue !=
                    request.Target.Position.HasValue ||
                _currentTask.Target.TargetUid.HasValue !=
                    request.Target.TargetUid.HasValue)
                return false;

            if (_currentTask.Target.Position.HasValue &&
                (_currentTask.Target.Position.Value.x !=
                     request.Target.Position.Value.x ||
                 _currentTask.Target.Position.Value.y !=
                     request.Target.Position.Value.y))
                return false;

            return !_currentTask.Target.TargetUid.HasValue ||
                _currentTask.Target.TargetUid.Value ==
                    request.Target.TargetUid.Value;
        }

        public void CancelRoute(MoveCancelReason reason)
        {
            _currentTask = MovementTask.None;
            _route = RouteRuntime.Empty;
            _follower.Reset();
        }

        /// <summary>
        /// Clear all locomotion-owned runtime state on formal death.
        /// (Pathfinding Design v13.1 section 11.10)
        /// Ownership rules:
        ///   - UnitLocomotionAgent owns: MovementTask, A* path, PathCursor, NeedRepath, Route
        ///   - UnitLocomotionAgent does NOT own: LifeState, PhysicsEntity2D position, CrowdControl instances
        /// </summary>
        public void ClearForDeath()
        {
            CancelRoute(MoveCancelReason.Death);
            _route = RouteRuntime.Empty;
            _follower.Reset();
        }

        public LocomotionResult Evaluate()
        {
            // D-008 spawn-Tick gate
            if (!_owner.CanRunActiveGameplayThisTick)
                return LocomotionResult.Idle(_owner.UnitUid);

            if (_currentTask.State != MovementTaskState.Active)
                return LocomotionResult.Idle(_owner.UnitUid);

            fp2 currentPos = Position;
            fp statMoveSpeed =
                _owner.StatHandler?.GetStat(StatId.MoveSpeed) ?? fp.one;
            fp moveSpeed = statMoveSpeed *
                (_owner.World?.MoveSpeedToLogicVelocityScale ?? fp.one);

            // Flow-field route: sample direction from baked field
            if (_route.Kind == RouteKind.FlowField)
            {
                return EvaluateFlowField(currentPos, moveSpeed);
            }

            // Determine target position
            fp2 targetPos = ResolveTargetPosition();
            if (_currentTask.State ==
                MovementTaskState.Cancelled)
            {
                return new LocomotionResult
                {
                    UnitUid = _owner.UnitUid,
                    Status =
                        RouteEvaluationStatus.TargetLost,
                };
            }

            // Check arrival at destination
            if (CheckArrival(currentPos, targetPos))
            {
                _currentTask.State = MovementTaskState.Completed;
                _follower.Reset();
                _route.NeedRepath = false;
                return new LocomotionResult
                {
                    UnitUid = _owner.UnitUid,
                    HasMovement = false,
                    AllowRVO = _currentTask.AllowRVO,
                    Status = RouteEvaluationStatus.Reached,
                };
            }

            if (_route.Kind == RouteKind.Direct)
            {
                return BuildDirectResult(
                    currentPos,
                    targetPos,
                    moveSpeed);
            }

            UpdateChaseRepath(targetPos);

            // Repath if needed
            int currentTick = SimulationTickContext.Current.Tick;
            if (_route.NeedRepath && currentTick >= _route.NextRepathTick)
            {
                if (!RebuildPath(currentPos, targetPos))
                {
                    return new LocomotionResult
                    {
                        UnitUid = _owner.UnitUid,
                        HasMovement = false,
                        AllowRVO = _currentTask.AllowRVO,
                        Status = RouteEvaluationStatus.NoRoute,
                    };
                }
                _route.NeedRepath = false;
                _route.NextRepathTick =
                    currentTick +
                    RepathCooldownTicks;
            }

            // Advance path follower cursor
            _follower.AdvanceCursor(currentPos);

            // Check if route is finished
            if (_follower.RouteFinished)
            {
                _currentTask.State = MovementTaskState.Completed;
                return new LocomotionResult
                {
                    UnitUid = _owner.UnitUid,
                    HasMovement = false,
                    AllowRVO = _currentTask.AllowRVO,
                    Status = RouteEvaluationStatus.Reached,
                };
            }

            // Check corridor deviation -> trigger repath
            if (_follower.IsOutsideCorridor(currentPos))
            {
                _route.NeedRepath = true;
            }

            return _follower.BuildLocomotionResult(currentPos, moveSpeed, _owner.UnitUid, _currentTask.AllowRVO);
        }

        private LocomotionResult EvaluateFlowField(fp2 currentPos, fp moveSpeed)
        {
            if (_flowFieldRegistry == null)
                return new LocomotionResult
                {
                    UnitUid = _owner.UnitUid,
                    HasMovement = false,
                    AllowRVO = _currentTask.AllowRVO,
                    Status = RouteEvaluationStatus.NoRoute,
                };

            var key = UnpackFlowFieldKey(
                _route.FlowFieldKey);
            if (!_flowFieldRegistry.TryGet(key, out var field))
                return new LocomotionResult
                {
                    UnitUid = _owner.UnitUid,
                    HasMovement = false,
                    AllowRVO = _currentTask.AllowRVO,
                    Status = RouteEvaluationStatus.NoRoute,
                };

            return _follower.BuildFlowFieldLocomotionResult(
                currentPos, moveSpeed, _owner.UnitUid, field, _flowFieldService, _currentTask.AllowRVO);
        }

        private fp2 ResolveTargetPosition()
        {
            if (_currentTask.Target.Position.HasValue)
                return _currentTask.Target.Position.Value;

            if (_currentTask.Target.TargetUid.HasValue)
            {
                UnitUid targetUid = _currentTask.Target.TargetUid.Value;
                if (_owner.World != null && _owner.World.TryGetUnit(targetUid, out Unit targetUnit))
                {
                    // Read target position from PhysicsEntity2D per design v13.1 section 1.1
                    return targetUnit.PhysicsEntity?.Transform2D.Position ?? fp2.zero;
                }
                _currentTask.State = MovementTaskState.Cancelled;
                return fp2.zero;
            }

            return _route.LastPathTargetPosition;
        }

        private bool CheckArrival(fp2 currentPos, fp2 targetPos)
        {
            fp stopDist = _currentTask.StopDistance;
            if (stopDist <= fp.zero)
                stopDist = (fp)0.3m;
            fp distSq = fpmath.dot(currentPos - targetPos, currentPos - targetPos);
            return distSq <= stopDist * stopDist;
        }

        private bool RebuildPath(fp2 currentPos, fp2 targetPos)
        {
            if (_route.Kind == RouteKind.AStar)
            {
                PathResult result = _aStar.FindPath(
                    currentPos,
                    targetPos,
                    OwnerRadiusClass);
                if (!result.Success)
                {
                    _route.AStarPathCellIndices = null;
                    _follower.Reset();
                    return false;
                }

                _route.AStarPathCellIndices = result.PathCellIndices;
                _follower.SetPath(result.PathCellIndices);
                _route.LastPathTargetPosition = targetPos;
                return true;
            }

            // FlowField: no repath needed - per-tick direction query handles it
            if (_route.Kind == RouteKind.FlowField)
            {
                _route.LastPathTargetPosition = targetPos;
                return true;
            }

            return false;
        }

        private RouteKind ResolveRouteKind(
            in RouteMoveRequest request)
        {
            switch (request.Purpose)
            {
                case MovePurpose.LaneAdvance:
                    return RouteKind.FlowField;
                case MovePurpose.ChaseForAttack:
                case MovePurpose.ChaseForCast:
                    return RouteKind.AStar;
                case MovePurpose.PointMove:
                case MovePurpose.ReturnToCamp:
                case MovePurpose.ControlMove:
                    fp2 target = request.Target.Position ??
                        Position;
                    return CanUseDirect(
                            Position,
                            target)
                        ? RouteKind.Direct
                        : RouteKind.AStar;
                default:
                    throw new DeterministicSimulationException(
                        $"Unsupported movement purpose {request.Purpose}.");
            }
        }

        private bool CanUseDirect(
            fp2 start,
            fp2 target)
        {
            fp2 delta = target - start;
            return fpmath.lengthsq(delta) <=
                    DirectMaxDistanceSq &&
                _grid.HasLineOfSight(
                    start,
                    target,
                    OwnerRadiusClass);
        }

        private LocomotionResult BuildDirectResult(
            fp2 currentPos,
            fp2 targetPos,
            fp moveSpeed)
        {
            fp2 delta = targetPos - currentPos;
            fp lengthSq = fpmath.lengthsq(delta);
            if (lengthSq <= fp.zero)
            {
                return LocomotionResult.Idle(
                    _owner.UnitUid);
            }
            fp length = fpmath.sqrt(lengthSq);
            return new LocomotionResult
            {
                UnitUid = _owner.UnitUid,
                HasMovement = true,
                AllowRVO = _currentTask.AllowRVO,
                DesiredDirection = delta / length,
                DesiredSpeed = moveSpeed,
                Status =
                    RouteEvaluationStatus.Moving,
            };
        }

        private void UpdateChaseRepath(
            fp2 targetPosition)
        {
            if (!_currentTask.Target.TargetUid.HasValue ||
                ! _currentTask.AllowRepath)
            {
                return;
            }
            // A newly accepted A* chase already owns a pending rebuild.
            // Do not move its cooldown forward before that initial path exists.
            if (_route.NeedRepath)
            {
                return;
            }
            int currentTick =
                SimulationTickContext.Current.Tick;
            if (currentTick <
                _route.NextRepathTick)
            {
                return;
            }
            fp2 delta =
                targetPosition -
                _route.LastPathTargetPosition;
            if (fpmath.lengthsq(delta) >=
                RepathThresholdSq)
            {
                _route.NeedRepath = true;
            }
            _route.NextRepathTick =
                currentTick +
                RepathCooldownTicks;
        }

        private static FlowFieldKey
            UnpackFlowFieldKey(int packed)
        {
            return new FlowFieldKey(
                checked((byte)(packed >> 2)),
                (RadiusClass)(packed & 0x3));
        }

        private RadiusClass OwnerRadiusClass =>
            RadiusClassHelper.FromRadius(
                _owner.PhysicsEntity?.Shape.Radius ??
                RadiusClassHelper.MediumRadius);

        public void Capture(ref LocomotionAgentSnapshot state)
        {
            state.HasActiveTask = _currentTask.State == MovementTaskState.Active;
            state.Task = _currentTask;
            state.Route = _route;
            if (_route.AStarPathCellIndices != null)
            {
                state.Route.AStarPathCellIndices = new int[_route.AStarPathCellIndices.Length];
                System.Array.Copy(_route.AStarPathCellIndices,
                    state.Route.AStarPathCellIndices, _route.AStarPathCellIndices.Length);
            }
            state.FollowerState = _follower.CaptureState();
            state.Route.FollowerState = CloneFollowerState(state.FollowerState);
        }

        public void Restore(in LocomotionAgentSnapshot state)
        {
            ValidateSnapshot(state);
            _currentTask = state.Task;
            _route = state.Route;
            if (state.Route.AStarPathCellIndices != null)
            {
                _route.AStarPathCellIndices =
                    (int[])state.Route.AStarPathCellIndices.Clone();
            }
            _route.FollowerState = CloneFollowerState(state.FollowerState);
            PathFollowerState restoredFollower = CloneFollowerState(state.FollowerState);
            _follower.RestoreState(restoredFollower);
        }

        public void Resolve(in RollbackContext context)
        {
            if (_currentTask.Target.TargetUid.HasValue &&
                (_owner.World == null ||
                 !_owner.World.TryGetUnit(_currentTask.Target.TargetUid.Value, out _)))
            {
                throw new DeterministicSimulationException(
                    $"Unit {_owner.UnitUid} restored locomotion target {_currentTask.Target.TargetUid.Value} does not exist.");
            }
        }

        public void Rebuild(in RollbackContext context) { }

        private static void ValidateSnapshot(in LocomotionAgentSnapshot state)
        {
            if (state.Task.Purpose < MovePurpose.PointMove ||
                state.Task.Purpose > MovePurpose.ControlMove ||
                state.Task.State < MovementTaskState.Idle ||
                state.Task.State > MovementTaskState.Cancelled ||
                state.Route.Kind < RouteKind.None ||
                state.Route.Kind > RouteKind.FlowField)
            {
                throw new DeterministicSimulationException(
                    "Locomotion snapshot contains an invalid enum value.");
            }

            if (state.HasActiveTask !=
                (state.Task.State == MovementTaskState.Active))
            {
                throw new DeterministicSimulationException(
                    "Locomotion snapshot HasActiveTask does not match MovementTask state.");
            }

            if (state.HasActiveTask &&
                (!state.Task.Target.HasTarget ||
                 state.Task.StopDistance < fp.zero))
            {
                throw new DeterministicSimulationException(
                    "Active locomotion snapshot requires a target and non-negative stop distance.");
            }

            ValidateFollowerState(state.FollowerState);
            ValidateFollowerState(state.Route.FollowerState);
            if (!FollowerStatesEqual(
                    state.FollowerState,
                    state.Route.FollowerState))
            {
                throw new DeterministicSimulationException(
                    "Locomotion snapshot route and follower states disagree.");
            }
        }

        private static void ValidateFollowerState(in PathFollowerState state)
        {
            int length = state.PathCellIndices?.Length ?? 0;
            if (state.PathCursor < -1 ||
                (length == 0 && state.PathCursor != -1) ||
                (length > 0 && state.PathCursor >= length))
            {
                throw new DeterministicSimulationException(
                    "Locomotion snapshot contains an invalid path cursor.");
            }
        }

        private static bool FollowerStatesEqual(
            in PathFollowerState left,
            in PathFollowerState right)
        {
            if (left.PathCursor != right.PathCursor ||
                left.RouteFinished != right.RouteFinished)
            {
                return false;
            }

            int[] leftPath = left.PathCellIndices;
            int[] rightPath = right.PathCellIndices;
            int leftLength = leftPath?.Length ?? 0;
            int rightLength = rightPath?.Length ?? 0;
            if (leftLength != rightLength)
            {
                return false;
            }
            for (int i = 0; i < leftLength; i++)
            {
                if (leftPath[i] != rightPath[i])
                {
                    return false;
                }
            }
            return true;
        }

        private static PathFollowerState CloneFollowerState(
            in PathFollowerState source)
        {
            return new PathFollowerState
            {
                PathCursor = source.PathCursor,
                RouteFinished = source.RouteFinished,
                PathCellIndices = source.PathCellIndices == null
                    ? null
                    : (int[])source.PathCellIndices.Clone(),
            };
        }
    }
}
