# Candidate Plans — Batch 0047–0049

> Created: 2026-07-23 (post 0046 conformance recovery)
> Based on: MODULE_STATUS.md Known Gaps, DESIGN_INDEX.md, CURRENT_HANDOFF.md
> Recommendation: A → B → C

---

## Candidate A: A* Pathfinding — Deterministic Search + PathFollower (~650 lines)

**Gap**: 0041 built the grid map, data model, and agent skeleton. But `UnitLocomotionAgent.Evaluate()` returns `Idle` every tick — no actual pathfinding happens. A* search, the indexed min-heap, path smoothing, and waypoint following are all absent. This is the single largest remaining framework gap and has been the top candidate in every round since 0041.

**Design authority**: `Docs/Design/MOBA_FrameSync_Integrated_Pathfinding_Design_v13_1.md` §4, §7, §9, §14, §15

### What this unlocks

- Right-click ground → compute A* path → follow waypoints → reach destination
- AI chase/retreat with obstacle-aware navigation
- Minion lane movement with repath on deviation
- Validates the entire locomotion pipeline (0041) end-to-end

### Scope

**In scope:**
1. `IndexedMinHeap` — deterministic indexed binary min-heap for A* open-set. SearchId reuse pattern. No boxing, no LINQ.
2. `AStarPathService` — deterministic A* with: octile heuristic (`h = max(dx,dy)×√2_cost + min(dx,dy)×straight_cost`), 8-direction neighbor expansion in stable clockwise order, LOS-based path smoothing via Bresenham grid check, max-iteration guard, neighbor-expand target if blocked (3-cell radius).
3. `PathFollower2D` — waypoint consumption: cursor advance on ReachThreshold, corridor-lateral check triggering NeedRepath, arrival detection, `BuildLocomotionResult` output.
4. Wire into `UnitLocomotionAgent.Evaluate()`: NeedRepath → AStarPathService → PathFollower2D.AdvanceCursor → BuildLocomotionResult.
5. Tests: A* on open grid, walled corridor, blocked-target neighbor expansion, LOS smoothing reduces node count, empty open-set returns NoPath, PathFollower cursor advance, corridor detection, arrival at final waypoint, rollback round-trip for follower state.

**Out of scope:**
- Team FlowField (`TeamFlowFieldService`) — separate candidate
- RVO local avoidance (`DeterministicRVOSystem`) — separate candidate
- `WallPenetrationResolver` — deferred
- Dynamic obstacle updates (static map only for this slice)
- Grid Bake from scene — use programmatic test grid

### New files — `Unit/Pathfinding/` (~350 lines, pure C#, no UnityEngine)

| # | File | Lines | Description |
|---|---|---|---|
| 1 | `IndexedMinHeap.cs` | ~100 | Deterministic indexed binary min-heap. `Push(PathNode)`, `Pop()`, `DecreaseKey(int, fp)`. Array-backed; SearchId reuse clears without realloc. Stable tie-break: lower FCost, then lower CellY, then lower CellX. |
| 2 | `AStarPathService.cs` | ~180 | Deterministic A*. `FindPath(fp2 start, fp2 target, RadiusClass rc, int maxIterations=1200) → PathResult`. Internal reusable state: `_searchId`, `_closedSetSearchIds[]`, `_parentIndices[]`, `_gCosts[]`. |
| 3 | `PathFollower2D.cs` | ~110 | Waypoint consumer: `AdvanceCursor(fp2 position)`, `IsOutsideCorridor(fp2 position) → bool`, `BuildLocomotionResult(fp2 position, fp moveSpeed) → LocomotionResult`. Capture/Restore: PathCursor, RouteFinished, pathCellIndices. |

### Modified files (~300 lines)

| File | Lines | Change |
|---|---|---|
| `Unit/Pathfinding/UnitLocomotionAgent.cs` | +120 | `Evaluate()`: if HasTask & NeedRepath → call AStarPathService; advance PathFollower; check corridor → set NeedRepath; check arrival → complete task; build LocomotionResult. Gate: `!Owner.CanRunActiveGameplayThisTick` → still returns Idle. |
| `Unit/Pathfinding/RouteRuntime.cs` | +30 | Add `PathFollower2D` field; Capture/Restore includes follower state. |
| `Unit/Movement/MovementHandler.cs` | +30 | `ApplyRouteMovement()`: use `LocomotionResult.DesiredDirection` and `DesiredSpeed` to compute velocity; integrate with existing movement constraint checks. |
| `FrameSync/SimulationTickPipeline.cs` | +20 | Ensure `PathGrid` is built/available before locomotion phase. |
| Tests | ~100 | New EditMode test file: `AStarPathfindingTests.cs`. |

### Design conformance

| Requirement | How met |
|---|---|
| §7 AStarPathService: deterministic, stable ordering | Octile heuristic, 8-dir clockwise stable neighbor expansion, SearchId reuse |
| §9 PathFollower2D: waypoint advance, corridor tolerance, arrival | Cursor advance on ReachThreshold, lateral corridor check, arrival detection |
| §6 UnitLocomotionAgent: Accept/Cancel/Evaluate | Evaluate now calls A* + Follower; Accept/Cancel unchanged |
| §2.5 CanRunActiveGameplayThisTick gate | Spawn Tick returns Idle; existing Unit.cs property used |
| §14 LocomotionResult: tick-local, not cross-tick | Struct created per Evaluate(), not stored |
| §15 Snapshot: follower state captured | PathCursor, RouteFinished, pathCellIndices in snapshot |
| Deterministic: no float, no LINQ, no Dictionary enumeration | fp throughout, array indices, stable comparison keys |

### Assembly

All new types in `FrameSyncMoba.Unit` (noEngineReferences: true). No asmdef change. No new dependency.

---

## Candidate B: Ability CastModelDef → Player Input Profile Bake (~500 lines)

**Gap**: The player-input system defines `IPlayerAbilityInputProfileProvider` with three modes (`PressCommit`, `LocalAimPrimaryCommit`, `PressFocusReleaseOrPrimaryCommit`) but has no implementation that reads from actual ability data. `AbilityDef` and `CastModelDef` exist as runtime types, but there is no Bake step that converts authoring `CastModelDef` into `BakedPlayerAbilityInputProfile`. Every ability slot currently requires a hardcoded profile. This blocks hold-release (Varus Q-style), local-aim (skillshot), and press-commit abilities from working through the normal input pipeline.

**Design authority**:
- `Docs/Design/moba_ability_system_design_v15_2.md` §CastModelDef, §AbilityDef authoring
- `Docs/Design/MOBA_Player_Input_Command_Module_Design_v1_1.md` §3 (hold-release FSM), §5 (profile derivation)

### What this unlocks

- Hold-release abilities: press → Focus Command, release/primary-click → Commit Command
- Local-aim skillshots: press → enter local-aim mode, primary-click → Commit with AimSnapshot
- Press-commit abilities: press → immediate Commit (no aim)
- All three modes derived automatically from `CastModelDef` authoring, not hardcoded per-hero

### Scope

**In scope:**
1. `AbilityInputProfileBaker` — static utility that reads `CastModelDef` and outputs `BakedPlayerAbilityInputProfile`:
   - `CastModelDef.IsHoldRelease == true` → `PressFocusReleaseOrPrimaryCommit`
   - `CastModelDef.RequiresAim == true` → `LocalAimPrimaryCommit`
   - Otherwise → `PressCommit`
2. `AbilityInputProfileProvider` — runtime implementation of `IPlayerAbilityInputProfileProvider` that queries the baked profile table.
3. `PlayerInputController` — wire `AbilityInputProfileProvider` into the existing input pipeline so ability key presses use the correct mode per slot.
4. Tests: hold-release profile derivation, local-aim profile derivation, press-commit default, slot-indexed lookup, missing ability returns false.

**Out of scope:**
- Full AbilityDef ScriptableObject authoring inspector — deferred to authoring-tool candidate
- Targeting indicator rendering (skillshot arrows, range circles) — deferred to presentation candidate
- `AimKind` auto-detection from CastModelDef — keep explicit for now
- Abilities with multiple cast models per slot (ultimate evolution) — first version is one profile per slot

### New files (~250 lines)

| # | File | Lines | Description |
|---|---|---|---|
| 1 | `PlayerInput/AbilityInputProfileProvider.cs` | ~100 | `AbilityInputProfileProvider : IPlayerAbilityInputProfileProvider`. Holds `BakedPlayerAbilityInputProfile[]` array indexed by slot. `TryGetProfile(byte slot, out profile)`, `TryGetAimKind(byte slot, out aimKind)`. |
| 2 | `PlayerInput/AbilityInputProfileBaker.cs` | ~80 | Static `Bake(CastModelDef def) → BakedPlayerAbilityInputProfile`. Reads `IsHoldRelease`, `RequiresAim` to select mode. |
| 3 | Tests: `PlayerInput/AbilityInputProfileTests.cs` | ~70 | EditMode tests for all three mode derivations. |

### Modified files (~250 lines)

| File | Lines | Change |
|---|---|---|
| `PlayerInput/PlayerInputController.cs` | +80 | Inject `IPlayerAbilityInputProfileProvider`; on ability key press, query profile to determine mode; route to correct input path (Focus/Commit/Aim). |
| `PlayerInput/PlayerCommandRequester.cs` | +60 | Integrate profile-aware mode routing: Focus on press for hold-release, enter LocalAiming for skillshots, Commit on press for instant. |
| `Bootstrap/GameBootstrap.cs` | +30 | Create `AbilityInputProfileProvider` from `AbilityDefinitionRegistry` + Bake; inject into `PlayerInputController`. |
| `Gameplay/Ability/CastModelDef.cs` | +30 | Add `IsHoldRelease` and `RequiresAim` boolean fields (already implied by design; formalize). |
| `Gameplay/Ability/AbilityDef.cs` | +20 | Add `CastModelDef` reference field for Bake to read. |
| Tests | ~30 | Existing `PlayerCommandRequesterTests` — add profile-aware command routing assertions. |

### Design conformance

| Requirement | How met |
|---|---|
| Player Input v1.1 §3: Hold-release FSM | `PressFocusReleaseOrPrimaryCommit` mode routes press→Focus, release→Commit |
| Player Input v1.1 §5: Profile derivation from CastModelDef | Baker reads CastModelDef fields, produces deterministic profile |
| Ability v15.2: CastModelDef owns timing/input semantics | Baker reads existing CastModelDef, no duplicate config |
| No Gameplay state in input | Profiles are Bake-time static; runtime lookup is read-only |
| Framework, not content | No hero-specific profiles; all derived from generic CastModelDef |

### Assembly

New types in `FrameSyncMoba.PlayerInput`. Modified `CastModelDef`/`AbilityDef` in `FrameSyncMoba.Unit`. Bootstrap wiring in `FrameSyncMoba.Bootstrap`. No new asmdef, no cycle.

---

## Candidate C: Minion Wave Spawning + Lane Push AI (~550 lines)

**Gap**: `MinionSystem.ProcessWave()` increments the wave counter but never calls `UnitWorld.SpawnUnit`. `MinionAIController` has decision scaffolding but lacks the actual lane-advance, target-acquire, and return-to-lane logic that makes minions push lanes. No minion ever appears in the game world. This is the core PvE element of a MOBA and the natural first integration test for the pathfinding system (Candidate A).

**Design authority**:
- `Docs/Design/moba_non_hero_unit_modules_design_v5.md` §4 (MinionSystem wave spawning), §5 (MinionAIController lane push)
- `Docs/Design/MOBA_FrameSync_Integrated_Pathfinding_Design_v13_1.md` §5 (MovePurpose.LaneAdvance)

### What this unlocks

- Minions spawn on schedule and push down their lane
- Minions acquire enemy targets (enemy minions → heroes → structures)
- Minions return to lane after chasing too far
- First end-to-end validation of: Spawn → AI → Pathfinding → Movement → Combat chain

### Scope

**In scope:**
1. `MinionSystem.SpawnWave()` — spawn a full wave of minions for each lane. Creates `UnitSpawnRequest` with correct prototype, team, and lane spawn position. Registers `MinionAIController` for each spawned unit.
2. `MinionAIController` decision logic — implement the three-state FSM (AdvanceLane → EngageTarget → ReturnToLane) with:
   - Lane advancement via `MovePurpose.LaneAdvance` (uses FlowField or A* once available)
   - Target selection: priority-ordered scan (hero-assist → current target → enemy minion → enemy hero → structure), stable sorting by distance then UnitUid
   - Chase boundary enforcement (max distance from engage origin, max distance from home lane centerline)
   - Return-to-lane on target loss or boundary exceeded
3. `UnitLocomotionAgent` — add `MovePurpose.LaneAdvance` routing (uses FlowField when available, falls back to A* for this slice)
4. Tests: wave spawn produces correct count, minion AI state transitions, target priority ordering, boundary enforcement triggers ReturnToLane, rollback round-trip for AI state.

**Out of scope:**
- `TeamFlowFieldService` — minions use A* pathfinding for lane movement in this slice
- `JungleCampSystem` spawn/respawn logic — separate candidate
- Tower AI targeting — separate candidate (TowerAIController skeleton exists)
- Super minion wave composition changes — deferred to content configuration
- Multi-lane wave coordination — first version handles one lane

### New files (~150 lines)

| # | File | Lines | Description |
|---|---|---|---|
| 1 | Tests: `MinionSystemSpawnTests.cs` | ~80 | Wave spawn count, UID assignment, controller registration, stable spawn ordering. |
| 2 | Tests: `MinionAIControllerTests.cs` | ~70 | State transitions, target priority, boundary enforcement. |

### Modified files (~400 lines)

| File | Lines | Change |
|---|---|---|
| `Gameplay/NonHero/MinionSystem.cs` | +150 | `SpawnWave(int currentTick, int laneId, TeamId team)`: create `UnitSpawnRequest` per minion in wave, call `UnitWorld.SpawnUnit`, create+register `MinionAIController`, track in `ManagedMinionUids`. Use prototype table for unit stats. |
| `Gameplay/NonHero/UnitAIController.cs` | +80 | `MinionAIController.TickLogic()`: implement three-state FSM. Target selection with priority bands and stable comparison. Chase boundary check against `EngageOrigin` and `HomeLaneId` centerline. |
| `Gameplay/Pathfinding/UnitLocomotionAgent.cs` | +40 | `MovePurpose.LaneAdvance` routing: use A* to next waypoint along lane centerline; fall back to direct if close enough. |
| `FrameSync/SimulationTickPipeline.cs` | +40 | Call `MinionSystem.ProcessWave()` + `SpawnWave()` before unit Tick loop. Call `TickAIControllers()` after command dispatch. |
| `Gameplay/NonHero/NonHeroSnapshot.cs` | +40 | Add `MinionAIController` state fields to snapshot: `HomeLaneId`, `AIState`, `EngageOrigin`, `TargetLockUntilLogicTick`. |
| `Gameplay/Unit/Core/UnitWorld.cs` | +30 | Add `MinionSystem` reference and `TickAIControllers()` call. |
| `Bootstrap/GameBootstrap.cs` | +20 | Create and configure `MinionSystem` with lane data; register with `UnitWorld` and pipeline. |

### Design conformance

| Requirement | How met |
|---|---|
| Non-Hero v5 §4: wave schedule + stable spawn | `ProcessWave()` triggers at interval; `SpawnWave()` creates via `UnitWorld.SpawnUnit` |
| Non-Hero v5 §5: three-state AI FSM | AdvanceLane → EngageTarget → ReturnToLane with boundary enforcement |
| Non-Hero v5 §5.3: target priority order | Hero assist > current target > enemy minion > hero > structure; stable tie-break |
| Non-Hero v5 §1.5: AI reuses unit behavior chain | AI Order → Unit Intent → BehaviorPlanner → Handler (not direct Handler calls) |
| Pathfinding v13.1 §5: MovePurpose.LaneAdvance | `UnitLocomotionAgent` routes LaneAdvance through A* along lane centerline |
| Deterministic: stable ordering | UnitUid-based sorting for targets, fixed priority bands, fp distance comparisons |

### Assembly

Modified types in `FrameSyncMoba.Unit` and `FrameSyncMoba.FrameSync`. No new asmdef, no cycle. Depends on Candidate A for pathfinding execution.

---

## Comparison

| | A: A* Pathfinding | B: Ability Input Bake | C: Minion Wave + Lane AI |
|---|---|---|---|
| **Lines** | ~650 | ~500 | ~550 |
| **Depends on** | 0041 (grid/data model) | CastModelDef fields | Candidate A (pathfinding) |
| **Unlocks** | Right-click move, AI navigation | Hold-release + skillshot input | PvE lane push gameplay |
| **Test surface** | Search correctness, follower state | Mode derivation, command routing | Spawn count, AI FSM, targeting |
| **Risk** | Medium: search perf, LOS edge cases | Low: deterministic bake from static data | Medium: AI tuning requires pathfinding |
| **Design conformance** | Pathfinding v13.1 §7,§9 | Input v1.1 §3,§5, Ability v15.2 | Non-Hero v5 §4,§5 |

## Recommended order

**A → B → C** (or A → C → B if PvE demo is higher priority than input feel).

A is gating: both B and C are more valuable with working pathfinding. B and C are independent of each other and can be swapped based on priority.

A* pathfinding is the foundation that makes the framework feel like a real game — units navigating around obstacles rather than walking through walls. It has been the #1 candidate since 0041 and remains the single most impactful remaining gap.
