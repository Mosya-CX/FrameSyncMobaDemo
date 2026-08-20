using System.Collections.Generic;
using FrameSyncMoba.Deterministic;
using Unity.Mathematics.FixedPoint;
using FrameSyncMoba.RuntimeConfig;

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

        private int RepathCooldownTicks =>
            DeterministicTimeConversion.Legacy30HzTicksToTicks(
                10,
                _owner.World?.TickRate ?? 30);
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
            // Pressed-against waypoints (e.g. their cell center sits inside
            // another unit's collision body) can never be reached exactly;
            // skipping them prevents route dead-lock. Tolerance is the owner
            // collision radius: a waypoint centre inside the owner's own
            // collision body is physically unreachable. After skipping,
            // re-path from the current position so the new route keeps its
            // wall-avoiding waypoints.
            bool skippedWaypoint = _follower.SkipWaypointsWithin(
                currentPos,
                _owner.PhysicsEntity?.Shape.Radius ??
                    fp.zero);
            if (skippedWaypoint)
            {
                _route.NeedRepath = true;
            }

            // Check if route is finished
            if (_follower.RouteFinished)
            {
                // Chase tasks define arrival by the real target distance,
                // not by path consumption. The A* destination is the cell
                // centre containing the stable chase spot; that centre can
                // sit up to half a cell outside the attack radius, and
                // re-pathing to the same cell would complete again on the
                // next tick without the unit moving. Walk the remaining
                // gap directly toward the fresh stable spot; CheckArrival
                // at the top of the next evaluation completes the task
                // once the unit is truly inside range.
                if (_currentTask.Target.TargetUid.HasValue &&
                    _owner.World != null &&
                    _owner.World.TryGetUnit(
                        _currentTask.Target.TargetUid.Value,
                        out _))
                {
                    _route.NeedRepath = true;
                    return BuildDirectResult(
                        currentPos,
                        ResolveTargetPosition(),
                        moveSpeed);
                }

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
                    if (_currentTask.Purpose ==
                            MovePurpose.ChaseForAttack ||
                        _currentTask.Purpose ==
                            MovePurpose.ChaseForCast)
                    {
                        return ResolveStableChasePosition(
                            targetUnit);
                    }
                    // Read target position from PhysicsEntity2D per design v13.1 section 1.1
                    return targetUnit.PhysicsEntity?.Transform2D.Position ?? fp2.zero;
                }
                _currentTask.State = MovementTaskState.Cancelled;
                return fp2.zero;
            }

            return _route.LastPathTargetPosition;
        }

        /// <summary>
        /// Pick a chase target point that stays inside attack range even in
        /// a crowded lane. Formula:
        ///   ideal = targetPos + (selfPos - targetPos).normalized *
        ///           (range - own collision radius)
        ///         + stabilityCorrection
        ///         + attackDistanceCorrection
        /// The stability correction is non-zero only when the base spot is
        /// actually pressed by another living unit or is not walkable; in
        /// an open area with nothing in the way it is exactly zero, so the
        /// unit heads for the base spot. The attack-distance correction
        /// clamps the final spot so its distance to the target never
        /// exceeds the attack range.
        /// </summary>
        private fp2 ResolveStableChasePosition(
            Unit target)
        {
            fp2 targetPos =
                target.PhysicsEntity
                    ?.Transform2D.Position ??
                Position;
            fp2 myPos = Position;
            fp2 delta = targetPos - myPos;
            fp length = fpmath.length(delta);
            if (length <= fp.zero)
            {
                return targetPos;
            }

            fp range = _currentTask.StopDistance;
            if (range <= fp.zero)
            {
                range = (fp)1m;
            }
            fp2 direction = delta / length;
            fp ownRadius =
                _owner.PhysicsEntity
                    ?.Shape.Radius ??
                fp.zero;
            fp stopOffset =
                range - ownRadius;
            if (stopOffset < fp.zero)
            {
                stopOffset = fp.zero;
            }
            // Base spot: targetPos + (selfPos - targetPos).normalized *
            // (range - own collision radius). Keeps the unit just inside
            // attack range along the approach ray.
            fp2 ideal =
                targetPos -
                direction *
                stopOffset;

            // Stability correction: only when the base spot is pressed by
            // another living unit or sits on an impassable cell. Open areas
            // with nothing in the way are already fully stable, so the
            // correction is zero and the unit goes exactly to the base spot.
            fp2 stabilityCorrection =
                fp2.zero;
            if (_grid != null &&
                IsSpotPressed(
                    ideal,
                    target))
            {
                stabilityCorrection =
                    SearchStabilityCorrection(
                        ideal,
                        targetPos,
                        target,
                        range);
            }
            fp2 result =
                ideal +
                stabilityCorrection;

            // Attack-distance correction: keep the final spot inside the
            // attack radius so the unit can always start attacking once it
            // arrives.
            fp2 resultDelta =
                result - targetPos;
            fp resultDistance =
                fpmath.length(resultDelta);
            if (resultDistance > range)
            {
                result =
                    targetPos +
                    resultDelta /
                    resultDistance *
                    range;
            }
            return result;
        }

        /// <summary>
        /// True when the chase spot would be squeezed: either it is not
        /// walkable, or another living unit's collision body (with a small
        /// clearance) occupies it. In an open area with no overlapping
        /// units the spot is perfectly stable.
        /// </summary>
        private bool IsSpotPressed(
            fp2 spot,
            Unit target)
        {
            if (_grid != null)
            {
                (int cx, int cy) =
                    _grid.WorldToCell(spot);
                if (!_grid.IsPassable(
                        cx,
                        cy,
                        OwnerRadiusClass))
                {
                    return true;
                }
            }

            IReadOnlyList<Unit> units =
                _owner.World?.GetAllUnits();
            if (units == null)
            {
                return false;
            }
            fp ownRadius =
                _owner.PhysicsEntity
                    ?.Shape.Radius ??
                fp.zero;
            fp clearance =
                (fp)0.1m;
            for (int i = 0;
                 i < units.Count;
                 i++)
            {
                Unit unit = units[i];
                if (unit == null ||
                    unit == _owner ||
                    unit == target)
                {
                    continue;
                }
                if (unit.LifeState !=
                    LifeState.Alive)
                {
                    continue;
                }
                fp2 unitPosition =
                    unit.PhysicsEntity
                        ?.Transform2D.Position ??
                    fp2.zero;
                fp otherRadius =
                    unit.PhysicsEntity
                        ?.Shape.Radius ??
                    fp.zero;
                fp required =
                    ownRadius +
                    otherRadius +
                    clearance;
                fp2 delta =
                    spot - unitPosition;
                if (fpmath.dot(
                        delta,
                        delta) <
                    required *
                    required)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Search the eight neighbouring cells of the base spot for the most
        /// stable walkable point that still lies inside the attack range.
        /// Returns the displacement from the base spot (zero when the base
        /// spot is already the best choice).
        /// </summary>
        private fp2 SearchStabilityCorrection(
            fp2 baseIdeal,
            fp2 targetPos,
            Unit target,
            fp range)
        {
            fp step = _grid.CellSize;
            (int idealCx, int idealCy) =
                _grid.WorldToCell(baseIdeal);
            bool basePassable =
                _grid.IsPassable(
                    idealCx,
                    idealCy,
                    OwnerRadiusClass);

            fp2 best = baseIdeal;
            fp bestStability = fp.zero;
            bool found = basePassable;
            if (basePassable)
            {
                bestStability =
                    StabilityAt(
                        baseIdeal,
                        target);
            }

            for (int dy = -1;
                 dy <= 1;
                 dy++)
            {
                for (int dx = -1;
                     dx <= 1;
                     dx++)
                {
                    if (dx == 0 && dy == 0)
                    {
                        continue;
                    }
                    fp2 candidate =
                        baseIdeal +
                        new fp2(
                            (fp)dx * step,
                            (fp)dy * step);
                    (int cx, int cy) =
                        _grid.WorldToCell(
                            candidate);
                    if (!_grid.IsPassable(
                            cx,
                            cy,
                            OwnerRadiusClass))
                    {
                        continue;
                    }
                    fp distanceToTarget =
                        fpmath.length(
                            candidate -
                            targetPos);
                    if (distanceToTarget >
                        range)
                    {
                        continue;
                    }
                    fp stability =
                        StabilityAt(
                            candidate,
                            target);
                    if (!found ||
                        stability >
                            bestStability)
                    {
                        found = true;
                        bestStability =
                            stability;
                        best = candidate;
                    }
                }
            }
            return found
                ? best - baseIdeal
                : fp2.zero;
        }

        /// <summary>
        /// Stability of a candidate chase spot: distance to the nearest
        /// other living unit (excluding owner and target). Larger is more
        /// stable (less likely to be squeezed out of attack range).
        /// </summary>
        private fp StabilityAt(
            fp2 point,
            Unit target)
        {
            IReadOnlyList<Unit> units =
                _owner.World?.GetAllUnits();
            if (units == null)
            {
                return (fp)int.MaxValue;
            }
            fp nearest = (fp)int.MaxValue;
            for (int i = 0;
                 i < units.Count;
                 i++)
            {
                Unit unit = units[i];
                if (unit == null ||
                    unit == _owner ||
                    unit == target)
                {
                    continue;
                }
                if (unit.LifeState !=
                    LifeState.Alive)
                {
                    continue;
                }
                fp2 unitPosition =
                    unit.PhysicsEntity
                        ?.Transform2D.Position ??
                    fp2.zero;
                fp distance =
                    fpmath.length(
                        unitPosition -
                        point);
                if (distance < nearest)
                {
                    nearest = distance;
                }
            }
            return nearest;
        }

        private bool CheckArrival(fp2 currentPos, fp2 targetPos)
        {
            fp stopDist = _currentTask.StopDistance;
            if (stopDist <= fp.zero)
                stopDist = (fp)0.3m;

            // Chase tasks target the unit, and their stop point is defined
            // by the real target center (stopDistance = attack range), not
            // by the stability-adjusted ideal waypoint. Otherwise the unit
            // can be flagged Completed while its true gap to the target is
            // still out of attack range.
            if (_currentTask.Target.TargetUid.HasValue &&
                _owner.World != null &&
                _owner.World.TryGetUnit(
                    _currentTask.Target.TargetUid.Value,
                    out Unit chaseTarget))
            {
                fp2 realTargetPos =
                    chaseTarget.PhysicsEntity
                        ?.Transform2D.Position ??
                    targetPos;
                // Chase stops inside attack range (at the stability ideal
                // point: center distance = range - own radius), so the unit
                // does not oscillate at the range edge between chase and
                // attack decisions.
                fp chaseStop =
                    _currentTask.StopDistance;
                if (chaseStop > fp.zero)
                {
                    chaseStop -=
                        _owner.PhysicsEntity
                            ?.Shape.Radius ??
                        fp.zero;
                    if (chaseStop < fp.zero)
                    {
                        chaseStop = fp.zero;
                    }
                }
                fp chaseDistSq =
                    fpmath.dot(
                        currentPos - realTargetPos,
                        currentPos - realTargetPos);
                return chaseDistSq <=
                    chaseStop * chaseStop;
            }

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
                    (int startCx, int startCy) =
                        _grid.WorldToCell(currentPos);
                    (int targetCx, int targetCy) =
                        _grid.WorldToCell(targetPos);
                    bool startPassable =
                        _grid.IsPassable(
                            startCx,
                            startCy,
                            OwnerRadiusClass);
                    bool targetPassable =
                        _grid.IsPassable(
                            targetCx,
                            targetCy,
                            OwnerRadiusClass);
                    UnityEngine.Debug.Log(
                        $"[Loco][RepathFail] unit={_owner.UnitUid} " +
                        $"tick={SimulationTickContext.Current.Tick} " +
                        $"status={result.Status} " +
                        $"from={currentPos}({startCx},{startCy}) " +
                        $"to={targetPos}({targetCx},{targetCy}) " +
                        $"startPass={startPassable} " +
                        $"targetPass={targetPassable} " +
                        $"route={_route.Kind}");
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
