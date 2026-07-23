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

        private MovementTask _currentTask;
        private RouteRuntime _route;

        private static readonly fp RepathCooldownTicks = (fp)10m;

        public UnitLocomotionAgent(Unit owner, PathGridMap2D grid)
        {
            _owner = owner;
            _grid = grid;
            _aStar = new AStarPathService(grid);
            _follower = new PathFollower2D(grid);
            _currentTask = MovementTask.None;
            _route = RouteRuntime.Empty;
        }

        public Unit Owner => _owner;
        public PathGridMap2D Grid => _grid;
        public ref readonly MovementTask CurrentTask => ref _currentTask;
        public ref readonly RouteRuntime Route => ref _route;

        public fp2 Position => _owner.MovementHandler?.Snapshot.Position ?? fp2.zero;

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
                Kind = RouteKind.Direct,
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

        public LocomotionResult Evaluate()
        {
            // D-008 spawn-Tick gate: only run active gameplay after spawn tick
            if (!_owner.CanRunActiveGameplayThisTick)
                return LocomotionResult.Idle(_owner.UnitUid);

            if (_currentTask.State != MovementTaskState.Active)
                return LocomotionResult.Idle(_owner.UnitUid);

            fp2 currentPos = Position;

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
                    Status = RouteEvaluationStatus.Reached,
                };
            }

            // Check corridor deviation -> trigger repath
            if (_follower.IsOutsideCorridor(currentPos))
            {
                _route.NeedRepath = true;
            }

            // Build locomotion result from follower
            fp moveSpeed = _owner.MovementHandler?.Snapshot.MoveSpeed ?? fp.one;
            return _follower.BuildLocomotionResult(currentPos, moveSpeed, _owner.UnitUid);
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
                    return targetUnit.MovementHandler?.Snapshot.Position ?? fp2.zero;
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

            // FlowField not yet implemented; fall back to Direct
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
