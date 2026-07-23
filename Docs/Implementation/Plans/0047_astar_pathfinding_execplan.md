# Plan 0047: A* Pathfinding — Deterministic Search + PathFollower

> Status: Completed
> Created: 2026-07-23
> Based on: `Docs/Design/MOBA_FrameSync_Integrated_Pathfinding_Design_v13_1.md` §7, §9, §14, §15
> Predecessor: 0046 (conformance recovery)
> Parent candidate: NEXT_CANDIDATES.md Candidate A

## Purpose

Implement deterministic A* pathfinding and waypoint following so `UnitLocomotionAgent.Evaluate()` produces real `LocomotionResult` outputs instead of always returning `Idle`. This replaces the scaffold from 0041 with a working search-and-follow pipeline.

## Observable behavior

- Right-click ground → A* path computed → waypoints followed → unit reaches destination
- AI chase/retreat with obstacle-aware navigation
- Path smoothing reduces waypoint count via LOS Bresenham grid check
- Blocked targets get neighbor-expand fallback (3-cell radius)
- Rollback: follower state captured/restored; A* search state is rebuildable

## Formal design documents

| Reference | Content |
|---|---|
| Pathfinding v13.1 §7 | AStarPathService: deterministic, stable ordering, octile heuristic, 8-dir expansion, LOS smoothing |
| Pathfinding v13.1 §9 | PathFollower2D: waypoint advance, corridor tolerance, arrival detection |
| Pathfinding v13.1 §14 | Public data structures: PathResult, RouteRuntime, LocomotionResult |
| Pathfinding v13.1 §15 | FrameSync marks: follower state captured; search state rebuildable |
| Decision D-008 | Spawn-Tick gate via `CanRunActiveGameplayThisTick` |
| Decision D-022 | Authoritative fn type is `fp`; no float/double in gameplay |
| Decision D-023 | Proportional focused tests |

## Current real code paths

| File | Current state |
|---|---|
| `Assets/Scripts/Gameplay/Pathfinding/PathGridMap2D.cs` | Complete: WorldToCell, CellToWorld, IsPassable, GetNeighbors, BuildFromPhysics |
| `Assets/Scripts/Gameplay/Pathfinding/PathNode.cs` | Complete: FCost, CompareTo with stable tie-break |
| `Assets/Scripts/Gameplay/Pathfinding/PathResult.cs` | Complete: status enum + result struct |
| `Assets/Scripts/Gameplay/Pathfinding/LocomotionResult.cs` | Complete: tick-local struct |
| `Assets/Scripts/Gameplay/Pathfinding/RouteRuntime.cs` | Has AStarPathCellIndices, missing PathFollower2D |
| `Assets/Scripts/Gameplay/Pathfinding/MovementTask.cs` | Complete |
| `Assets/Scripts/Gameplay/Pathfinding/RouteMoveRequest.cs` | Complete |
| `Assets/Scripts/Gameplay/Pathfinding/LocomotionAgentSnapshot.cs` | Complete |
| `Assets/Scripts/Gameplay/Pathfinding/UnitLocomotionAgent.cs` | Skeleton: Evaluate() always returns Idle |
| `Assets/Scripts/Gameplay/Movement/MovementHandler.cs` | Has ApplyRouteMovement, uses LocomotionResult |
| `Assets/Scripts/FrameSync/SimulationTickPipeline.cs` | Has locomotion evaluation phase, no PathGrid wiring |

## In scope

1. `IndexedMinHeap` — deterministic indexed binary min-heap for A* open-set. SearchId reuse pattern. No boxing, no LINQ.
2. `AStarPathService` — deterministic A* with octile heuristic, 8-direction stable neighbor expansion, LOS Bresenham path smoothing, max-iteration guard (1200), blocked-target neighbor expansion (3-cell radius).
3. `PathFollower2D` — waypoint consumption: cursor advance, corridor-lateral check, arrival detection, BuildLocomotionResult output. Capture/Restore via snapshot.
4. Wire into `UnitLocomotionAgent.Evaluate()`: NeedRepath → AStarPathService → PathFollower2D.AdvanceCursor → BuildLocomotionResult.
5. Tests: open grid, walled corridor, blocked-target neighbor expansion, LOS smoothing, empty open-set, follower cursor advance, corridor detection, arrival, rollback round-trip.

## Out of scope

- Team FlowField (`TeamFlowFieldService`) — separate candidate
- RVO local avoidance (`DeterministicRVOSystem`) — separate candidate
- `WallPenetrationResolver` — deferred
- Dynamic obstacle updates (static map only)
- Grid Bake from scene — use programmatic test grid

## New files — `Unit/Pathfinding/` (pure C#, no UnityEngine references)

| # | File | Lines | Description |
|---|---|---|---|
| 1 | `IndexedMinHeap.cs` | ~100 | Deterministic indexed binary min-heap. `Push(PathNode)`, `Pop()`, `DecreaseKey(int, fp)`. Array-backed; SearchId reuse clears without realloc. |
| 2 | `AStarPathService.cs` | ~180 | Deterministic A*. `FindPath(fp2 start, fp2 target, int maxIterations=1200) → PathResult`. Internal reusable state arrays. |
| 3 | `PathFollower2D.cs` | ~110 | Waypoint consumer: `AdvanceCursor`, `IsOutsideCorridor`, `BuildLocomotionResult`. IRollback-capable state. |

## Modified files

| File | Lines | Change |
|---|---|---|
| `Pathfinding/UnitLocomotionAgent.cs` | +120 | `Evaluate()`: if HasTask & NeedRepath → AStarPathService; advance PathFollower; check corridor; check arrival; build LocomotionResult. Gate: `!CanRunActiveGameplayThisTick` → Idle. |
| `Pathfinding/RouteRuntime.cs` | +30 | Add `PathFollower2D` field; Capture/Restore. |
| `Movement/MovementHandler.cs` | +10 | `ApplyRouteMovement()` already functional; minor integration fix. |
| `FrameSync/SimulationTickPipeline.cs` | +20 | Ensure PathGrid built before locomotion phase. |
| `Pathfinding/LocomotionAgentSnapshot.cs` | +15 | Add PathFollower2D state fields. |
| Tests (new) | ~100 | `AStarPathfindingTests.cs` in Unit.Tests asmdef. |

## Public contracts and ownership

| Type | Assembly | Ownership |
|---|---|---|
| `IndexedMinHeap` | FrameSyncMoba.Unit | New; generic min-heap for PathNode; internal to pathfinding |
| `AStarPathService` | FrameSyncMoba.Unit | New; depends on PathGridMap2D |
| `PathFollower2D` | FrameSyncMoba.Unit | New; owned by RouteRuntime via UnitLocomotionAgent |
| `RouteRuntime` (modified) | FrameSyncMoba.Unit | Existing; adds PathFollower2D member |

## Snapshot / serialization / checksum

- `PathFollower2D` state (PathCursor, RouteFinished, pathCellIndices) enters `LocomotionAgentSnapshot` → `UnitSnapshot` → `GameplaySnapshot` → `SharedGameplayChecksum`
- A* search state (`_searchId`, `_closedSetSearchIds`, `_parentIndices`, `_gCosts`) is tick-local rebuildable: NOT in snapshot
- `LocomotionResult` remains tick-local: NOT in snapshot

## Implementation steps

1. Create `IndexedMinHeap.cs`
2. Create `AStarPathService.cs`
3. Create `PathFollower2D.cs`
4. Modify `RouteRuntime.cs` — add PathFollower2D
5. Modify `LocomotionAgentSnapshot.cs` — add follower fields
6. Modify `UnitLocomotionAgent.cs` — implement Evaluate()
7. Modify `SimulationTickPipeline.cs` — PathGrid initialization
8. Create `AStarPathfindingTests.cs`
9. Unity MCP: compile, read Console, run Unit.Tests
10. Update MODULE_STATUS.md and CURRENT_HANDOFF.md

## Automated tests

- A* on open grid returns correct path
- A* on walled corridor finds path around wall
- A* blocked-target neighbor expansion finds nearby reachable cell
- LOS smoothing reduces node count on straight line
- A* empty open-set returns NoPath
- PathFollower cursor advance on reaching waypoint
- PathFollower corridor detection triggers NeedRepath
- PathFollower arrival at final waypoint
- Rollback round-trip for follower state
- MaxIterationReached when search exceeds limit

## Unity MCP validation

- Trigger compilation → read Console for errors/warnings
- Run `FrameSyncMoba.Unit.Tests` EditMode
- Run `FrameSyncMoba.FrameSync.Tests` EditMode if snapshot tests affected

## Risks and stop conditions

- **Risk**: A* search performance on large grids. Mitigation: max-iteration guard (1200), SearchId reuse, indexed min-heap for O(log n) operations.
- **Stop if**: `fp` math produces non-deterministic results across platforms. Verdict: `fp` is deterministic by design.
- **Stop if**: Current design conflicts with implementation. Not expected.

## Completion criteria

1. `UnitLocomotionAgent.Evaluate()` returns non-Idle LocomotionResult when task is active
2. Unity compilation: no new errors
3. All new A* pathfinding tests pass
4. Existing Unit.Tests pass (no regressions)
5. RouteRuntime snapshot round-trip verified
6. Design §7, §9 requirements verified

## Estimated code change

- New: ~390 lines (IndexedMinHeap + AStarPathService + PathFollower2D)
- Modified: ~195 lines (UnitLocomotionAgent + RouteRuntime + LocomotionAgentSnapshot + SimulationTickPipeline + MovementHandler)
- Tests: ~100 lines
- **Total: ~685 lines**
