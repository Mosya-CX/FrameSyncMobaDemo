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

        private static readonly fp RepathCooldownTicks = (fp)10m;

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

            _currentTask = new MovementTask
            {
                Purpose = request.Purpose,
                Target = request.Target,
                StopDistance = request.StopDistance,
                AllowRVO = request.AllowRVO,
                AllowRepath = request.AllowRepath,
                State = MovementTaskState.Active,
            };

            _route = new RouteRuntime
            {
                Kind = request.Kind != RouteKind.None ? request.Kind : RouteKind.Direct,
                NeedRepath = true,
                LastPathTargetPosition = request.Target.Position ?? fp2.zero,
                FollowerState = PathFollowerState.Empty,
            };

            _follower.Reset();

            return MoveAcceptResult.Accepted;
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
            fp moveSpeed = _owner.StatHandler?.GetStat(StatId.MoveSpeed) ?? fp.one;

            // Flow-field route: sample direction from baked field
            if (_route.Kind == RouteKind.FlowField)
            {
                return EvaluateFlowField(currentPos, moveSpeed);
            }

            // Determine target position
            fp2 targetPos = ResolveTargetPosition();

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
                _route.NextRepathTick = currentTick + (int)RepathCooldownTicks;
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

            var key = new FlowFieldKey(_owner.TeamId.Value, RadiusClass.Medium);
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
            if (_route.Kind == RouteKind.Direct || _route.Kind == RouteKind.None)
                _route.Kind = RouteKind.AStar;

            if (_route.Kind == RouteKind.AStar)
            {
                PathResult result = _aStar.FindPath(currentPos, targetPos);
                if (!result.Success)
                {
                    _route.Kind = RouteKind.Direct;
                    _route.AStarPathCellIndices = null;
                    _follower.Reset();
                    return true;
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

            _route.Kind = RouteKind.Direct;
            _follower.Reset();
            return true;
        }

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
        }

        public void Restore(in LocomotionAgentSnapshot state)
        {
            _currentTask = state.Task;
            _route = state.Route;
            _follower.RestoreState(state.FollowerState);
        }

        public void Resolve(in RollbackContext context) { }
        public void Rebuild(in RollbackContext context) { }
    }
}
