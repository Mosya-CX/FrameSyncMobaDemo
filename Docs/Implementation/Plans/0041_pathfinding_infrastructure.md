# Plan 0041: Pathfinding Infrastructure 鈥?Grid Map + Data Model + Agent Skeleton

> Status: Completed
> Created: 2026-07-22
> Based on: `Docs/Design/MOBA_FrameSync_Integrated_Pathfinding_Design_v13_1.md` 搂4, 搂5, 搂6, 搂14, 搂15
> Predecessor: 0040 Hit Reaction Integration
> Lines target: ~550 (new + modified)

## Scope

Build the scaffolding that A*, flow-field, and RVO will later plug into. **No A* search, no flow field, no RVO in this plan.** Those are deferred to dedicated large-module plans.

### In scope

1. `PathGridMap2D` 鈥?grid map with WorldToCell/CellToWorld/IsPassable/GetNeighbors/BuildFromPhysics
2. All pathfinding data structures 鈥?PathNode, PathResult, MovementTask, RouteRuntime, RouteMoveRequest, LocomotionResult
3. `UnitLocomotionAgent` 鈥?accept/cancel route requests, Evaluate() skeleton returning Idle
4. `MovementHandler` 鈥?`ApplyRouteMovement()` method for route-based movement
5. `MovementSnapshot` 鈥?path-following state fields
6. `Unit` 鈥?`LocomotionAgent` property
7. `UnitWorld` 鈥?`PathGrid` singleton, LocomotionAgent binding on spawn
8. `SimulationTickPipeline` 鈥?locomotion evaluation step

### Out of scope (deferred)

- A* search (`AStarPathService`) 鈥?deferred to dedicated A* plan
- `IndexedMinHeap` 鈥?deferred with A*
- `PathFollower2D` 鈥?deferred (needs A* results to follow)
- `TeamFlowFieldService` 鈥?deferred to flow-field plan
- `DeterministicRVOSystem` / `RvoGrid` 鈥?deferred to RVO plan
- `WallPenetrationResolver` 鈥?deferred

---

## Files

### New: `Unit/Pathfinding/PathNode.cs` (~55 lines)

```csharp
namespace FrameSyncMoba.Unit
{
    public struct PathNode : IComparable<PathNode>
    {
        public int CellX;
        public int CellY;
        public fp GCost;
        public fp HCost;
        public int ParentIndex;
        public bool Closed;
        public readonly fp FCost => GCost + HCost;
        // Stable ordering: FCost descending; tie-break by (CellX, CellY)
        public int CompareTo(PathNode other) { ... }
    }
}
```

### New: `Unit/Pathfinding/PathGridMap2D.cs` (~140 lines)

- Fields: `fp2 WorldCenter`, `fp CellSize`, `int Width`, `int Height`, `bool[] Walkable`, `fp2 WorldMin`
- `WorldToCell(fp2 pos) 鈫?(int cx, int cy)`: clamp to grid bounds
- `CellToWorld(int cx, int cy) 鈫?fp2`: cell center in world space
- `IsPassable(int cx, int cy) 鈫?bool`: bounds-check + Walkable lookup
- `GetNeighbors(int cx, int cy) 鈫?ReadOnlySpan<(int,int)>`: 8-direction clockwise stable order; filters out-of-bounds
- `BuildFromPhysics(PhysicsWorld physicsWorld, fp cellSize)`: scans UnitEntities' Bounds, marks obstructed cells
- `Clear()`: resets all cells to walkable
- `SetObstruction(fp2 worldMin, fp2 worldMax, bool blocked)`: marks a rectangular region

### New: `Unit/Pathfinding/PathResult.cs` (~30 lines)

```csharp
public enum PathStatus { Success, InvalidStart, InvalidEnd, EndBlocked, NoPath, MaxIterationReached, SystemNotReady }
public struct PathResult
{
    public bool Success;
    public PathStatus Status;
    public int[] PathCellIndices;
    public static PathResult Failed(PathStatus status) => new() { Status = status };
    public static PathResult Ok(int[] indices) => new() { Success = true, PathCellIndices = indices };
}
```

### New: `Unit/Pathfinding/MovementTask.cs` (~35 lines)

```csharp
public enum MovePurpose { MoveToPosition, FollowTarget, Flee, MoveToLane }
public enum MovementTaskState { Idle, Active, Completed, Cancelled }
public struct MoveTarget { public fp2? Position; public UnitUid? TargetUid; }
public struct MovementTask
{
    public MovePurpose Purpose;
    public MoveTarget Target;
    public fp StopDistance;
    public bool AllowRVO;
    public bool AllowRepath;
    public MovementTaskState State;
    public static readonly MovementTask None = default;
}
```

### New: `Unit/Pathfinding/RouteRuntime.cs` (~40 lines)

```csharp
public enum RouteKind { None, Direct, AStar, FlowField }
public struct RouteRuntime
{
    public RouteKind Kind;
    public bool NeedRepath;
    public int NextRepathTick;
    public fp2 LastPathTargetPosition;
    public int[] AStarPathCellIndices;   // placeholder for A* plan
    public int FlowFieldKey;             // placeholder for flow-field plan
    // Capture/Restore: all fields captured
}
```

### New: `Unit/Pathfinding/RouteMoveRequest.cs` (~20 lines)

```csharp
public struct RouteMoveRequest
{
    public MoveTarget Target;
    public MovePurpose Purpose;
    public fp StopDistance;
    public bool AllowRepath;
    public bool AllowRVO;
}
```

### New: `Unit/Pathfinding/LocomotionResult.cs` (~30 lines)

```csharp
public enum RouteEvaluationStatus { Idle, Moving, Reached, Blocked, NoRoute, TargetLost, Cancelled }
public struct LocomotionResult
{
    public UnitUid UnitUid;
    public bool HasMovement;
    public fp2 DesiredDirection;
    public fp DesiredSpeed;
    public RouteEvaluationStatus Status;
    public static LocomotionResult Idle(UnitUid uid) => new() { UnitUid = uid, Status = RouteEvaluationStatus.Idle };
}
```

### New: `Unit/Pathfinding/UnitLocomotionAgent.cs` (~90 lines)

```csharp
public sealed class UnitLocomotionAgent : IRollback<LocomotionAgentSnapshot>
{
    private readonly Unit _owner;
    private readonly PhysicsEntity2D _entity;
    private readonly PathGridMap2D _grid;
    private MovementTask _currentTask;
    private RouteRuntime _route;

    public MoveAcceptResult AcceptRouteRequest(RouteMoveRequest req);  // validates, sets task
    public void CancelRoute(MoveCancelReason reason);                   // clears task
    public LocomotionResult Evaluate();                                 // skeleton: returns Idle
    // Capture/Restore: _currentTask, _route
}
```

### Modified: `Unit/Movement/MovementHandler.cs` (+50 lines)

- Add method: `void ApplyRouteMovement(in LocomotionResult locomotion)` 鈥?sets velocity from LocomotionResult if HasMovement
- In `TickUpdate()`: add route-movement branch (when LocomotionResult has movement, use it instead of direct input)
- Add field: `LocomotionResult _pendingLocomotion` (tick-transient)

### Modified: `Unit/Movement/MovementSnapshot.cs` (+20 lines)

- Add: `public int CurrentWaypointIndex;`
- Add: `public int[] SnapshotPathCellIndices;` (shallow copy on Capture)

### Modified: `Unit/Core/Unit.cs` (+5 lines)

- Add: `public UnitLocomotionAgent Locomotion { get; internal set; }`

### Modified: `Unit/Core/UnitWorld.cs` (+30 lines)

- Add: `public PathGridMap2D PathGrid { get; set; }`
- `SpawnUnit()`: if `PathGrid != null`, create `new UnitLocomotionAgent(unit, entity, PathGrid)` and assign to `unit.Locomotion`
- Note: `PhysicsEntity2D entity` is not directly available in `SpawnUnit` currently. Will add `PhysicsWorld` reference to UnitWorld or pass via parameter.

### Modified: `FrameSync/SimulationTickPipeline.cs` (+35 lines)

- In `ExecuteTick`: before the unit loop, evaluate locomotion for all units:
  ```
  foreach unit: locomotionResult = unit.Locomotion?.Evaluate()
  ```
- Pass `locomotionResult` to `unit.MovementHandler.ApplyRouteMovement(locomotionResult)` before `TickUpdate`
- In `CaptureAggregateSnapshot`: capture Locomotion state
- In `RestoreFromSnapshot`: restore Locomotion state

---

## Design conformance

| Design requirement | Implementation |
|---|---|
| 搂4 PathGridMap2D: binary grid, WorldToCell/CellToWorld | 鉁?PathGridMap2D.cs |
| 搂5 MovePurpose + movement request | 鉁?MovementTask.cs, RouteMoveRequest.cs |
| 搂6 UnitLocomotionAgent: accept/cancel/evaluate | 鉁?UnitLocomotionAgent.cs (skeleton) |
| 搂14 LocomotionResult: tick-local, not cross-tick | 鉁?LocomotionResult.cs (struct, per-tick) |
| 搂14 MovementTask: Purpose/Target/StopDistance | 鉁?MovementTask.cs |
| 搂14 RouteRuntime: Kind/NeedRepath | 鉁?RouteRuntime.cs |
| 搂14.7 LocomotionResult: tick-local, no snapshot | 鉁?struct, created per Evaluate() |
| 搂15 Snapshot: UnitLocomotionAgent state captured | 鉁?IRollback<LocomotionAgentSnapshot> |
| 搂2.5 CanRunActiveGameplayThisTick gate | 鉁?Already exists on Unit.cs |
| noEngineReferences (Unit asmdef) | 鉁?All new types are pure C#, no UnityEngine |
