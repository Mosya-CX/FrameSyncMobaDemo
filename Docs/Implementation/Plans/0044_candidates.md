# Candidate Plans — Batch 0044–0046 (Detailed)

> Created: 2026-07-22 (post 0043 On-Hit Effect Pipeline)
> Based on: MODULE_STATUS.md Known Gaps, DESIGN_INDEX.md, Roadmap Phase 13
> Recommendation: A → B → C

---

## Candidate A: A* Pathfinding — Deterministic Search + PathFollower (~650 lines)

**Gap**: 0041 built the grid map, data model, and agent skeleton. But `UnitLocomotionAgent.Evaluate()` returns Idle every tick — no actual pathfinding happens. Units still move only via direct MoveCommand. A* search, the indexed min-heap, path smoothing, and waypoint following are all absent.

**Design authority**: `Docs/Design/MOBA_FrameSync_Integrated_Pathfinding_Design_v13_1.md` §7, §9, §14, §15

### What this unlocks
- Right-click ground → compute path → follow waypoints → reach destination
- Minion lane movement (move-to-lane with repath)
- AI chase/retreat with pathfinding
- Validates the entire locomotion pipeline (0041) end-to-end

### New files — Unit/Pathfinding/ (~350 lines)

| # | File | Lines | Description |
|---|---|---|---|
| 1 | `Unit/Pathfinding/IndexedMinHeap.cs` | ~95 | Deterministic indexed binary min-heap for A* open-set. `Push(PathNode)`, `Pop()`, `DecreaseKey(int index, fp newFCost)`. Array-backed; internal indices stable. `Clear()` via SearchId pattern. No boxing, no LINQ. Stable tie-breaking: lower FCost, then lower CellY, then lower CellX. |
| 2 | `Unit/Pathfinding/AStarPathService.cs` | ~180 | Deterministic A*. `SearchId` pattern: increment per search, skip reallocating ClosedSet. `FindPath(fp2 start, fp2 target, int maxIterations = 1200) → PathResult`. Steps: (1) validate start/end cells via Grid.IsPassable, (2) if end blocked: 8-dir expanding neighbor search (max 3-cell radius), (3) indexed heap open-set, (4) octile heuristic: `h = max(dx,dy) × √2_cost + min(dx,dy) × straight_cost` using fp constants, (5) LOS-based path smoothing: walk result indices, remove intermediate nodes visible via Grid Bresenham line check. Internal reusable state: `int _searchId`, `int[] _closedSetSearchIds`, `int[] _parentIndices`, `fp[] _gCosts`. |
| 3 | `Unit/Pathfinding/PathFollower2D.cs` | ~110 | Consumes `int[] pathCellIndices` from AStarPathService. Fields: `int PathCursor`, `bool RouteFinished`, `fp ReachThreshold` (default 0.15 cells), `fp PathCorridorTolerance`. `AdvanceCursor(fp2 position)`: advances cursor when distance to current waypoint < ReachThreshold. `IsOutsideCorridor(fp2 position) → bool`: lateral distance to path segment centerline exceeds tolerance → triggers NeedRepath. `BuildLocomotionResult(fp2 position, fp moveSpeed) → LocomotionResult`: computes DesiredDirection from current waypoint, sets HasMovement/Status. Capture/Restore saves `PathCursor`, `RouteFinished`, `pathCellIndices`. |

### New files — Unit/Pathfinding/ data (~40 lines)

| # | File | Lines | Description |
|---|---|---|---|
| 4 | `Unit/Pathfinding/AStarPathServiceSettings.cs` | ~25 | Config: `fp StraightCost` (1.0), `fp DiagonalCost` (≈1.414), `int MaxIterations` (1200), `fp ReachThreshold` (0.15). |
| 5 | `Unit/Pathfinding/PathSmoother.cs` | ~25 | Static `SmoothPath(int[] rawIndices, PathGridMap2D grid) → int[]`: LOS-based node reduction. |

### Modified files (~300 lines)

| # | File | Lines | Change |
|---|---|---|---|
| 6 | `Unit/Pathfinding/UnitLocomotionAgent.cs` | +120 | Full `Evaluate()`: (1) check if NeedRepath → call AStarPathService.FindPath, (2) store result indices in Route.AStarPathCellIndices, (3) create/reuse PathFollower2D with indices, (4) Follower.AdvanceCursor, (5) check outside-corridor → set NeedRepath, (6) check arrival (PathCursor at end) → complete task → LocomotionResult.Reached, (7) Follower.BuildLocomotionResult. Gate: if `!Owner.CanRunActiveGameplayThisTick` → Idle. Add fields: `AStarPathService _pathService`, `PathFollower2D _follower`. |
| 7 | `Unit/Movement/MovementHandler.cs` | +55 | `ApplyRouteMovement(in LocomotionResult locomotion)`: if locomotion.HasMovement, compute velocity = locomotion.DesiredDirection × min(CurrentMoveSpeed, DesiredSpeed), call `PhysicsEntity2D.SetLogicPose(...)`. Integrate into TickUpdate: if locomotion provided, use it; else use direct input. |
| 8 | `Unit/Core/UnitWorld.cs` | +25 | Add `AStarPathService PathService` singleton. `SpawnUnit()`: if PathGrid set, create UnitLocomotionAgent with PathService reference. Provide `PathGrid` initialization from PhysicsWorld. |
| 9 | `FrameSync/SimulationTickPipeline.cs` | +45 | In ExecuteTick per-unit loop: (1) `locomotionResult = unit.Locomotion?.Evaluate()`, (2) skip RVO for first pass (use direct LocomotionResult), (3) `unit.MovementHandler.ApplyRouteMovement(locomotionResult)`. Ensure order: locomotion evaluate → movement apply → collision resolve. |
| 10 | `Unit/Movement/MovementSnapshot.cs` | +15 | Capture PathFollower2D state: `CurrentWaypointIndex`, `bool RouteFinished`. `SnapshotPathCellIndices` shallow copy on Capture. |
| 11 | `Unit/Pathfinding/RouteRuntime.cs` | +25 | Add `bool RouteFinished`, `int PathCursor` fields for restore. |
| 12 | `Unit/Pathfinding/LocomotionAgentSnapshot.cs` | +15 | Add `bool RouteFinished`, `int PathCursor`, `int[] PathCellIndices` for full PathFollower restore. |

### Design conformance
- §7 AStarPathService: SearchId reuse, indexed heap, octile heuristic, LOS smoothing
- §9 PathFollower2D: cursor advance, corridor check, arrival detection, ReachThreshold
- §14 LocomotionResult: tick-local struct, no cross-tick storage
- §15 Snapshot: PathFollower state captured (cursor, finished flag, path indices); A* open/closed sets rebuildable

### RVO — deferred
First pass uses direct LocomotionResult without RVO blending. `DeterministicRVOSystem` (§10) deferred to follow-up plan.

### Tests needed (~120 lines)
- `AStarPathServiceTests.cs`: straight line, obstacle detour, unreachable target, max iterations, stable ordering, LOS smoothing
- `IndexedMinHeapTests.cs`: push/pop ordering, decrease-key, search-id reuse
- `PathFollower2DTests.cs`: cursor advance, arrival, corridor deviation, capture/restore

---

## Candidate B: XP & Level-Up System (~450 lines)

**Gap**: `DeathEffectDispatcher` has XP distribution stubs but no XP tracking, level-up stat growth, or skill point granting. Units spawn at level 1 and stay there. Death rewards are not meaningful. The game loop (kill → earn XP → level up → grow stronger) is missing.

**Design authority**: `Docs/Design/unit_behavior_framework_design_v27_3.md` (level-up lifecycle), `Docs/Design/moba_combat_system_design_v13_2.md` (XP on kill), `Docs/Design/moba_ability_system_design_v15_2.md` (skill points)

### What this unlocks
- Kill rewards: XP + gold on enemy death
- Level progression with configurable XP thresholds
- Stat growth on level-up (attack damage, max health, etc.)
- Skill point granting → ability leveling
- Respawn timer scaling by level

### New files — Unit/XP/ (~180 lines)

| # | File | Lines | Description |
|---|---|---|---|
| 1 | `Unit/XP/ExperienceTable.cs` | ~50 | Static table: `int[] LevelThresholds` (cumulative XP to reach each level). `GetLevel(int totalXp) → int`, `GetXpToNextLevel(int currentLevel) → int`, `GetTotalXpForLevel(int level) → int`. Max level constant. |
| 2 | `Unit/XP/ExperienceTracker.cs` | ~90 | Per-unit XP state: `int CurrentLevel` (starts at 1), `int TotalExperience`, `int ExperienceToNextLevel`. `GrantExperience(int amount) → LevelUpResult`: adds XP, checks level-up thresholds, returns `LevelUpResult` with new level, skill points gained, stats to grow. `LevelUpResult`: `bool LeveledUp`, `int NewLevel`, `int SkillPointsGained`. IRollback snapshot. |
| 3 | `Unit/XP/LevelUpResult.cs` | ~15 | Struct: `bool LeveledUp`, `int NewLevel`, `int SkillPointsGained`, `StatPreset StatGrowth`. |
| 4 | `Unit/XP/XpRewardTable.cs` | ~25 | Static lookup: `GetXpReward(int victimLevel, int killerLevel) → int`. Diminishing returns for killing lower-level units. |

### Modified files (~270 lines)

| # | File | Lines | Change |
|---|---|---|---|
| 5 | `Unit/Core/Unit.cs` | +25 | Add `ExperienceTracker Xp` property. Add `int Level` derived from Xp.CurrentLevel. |
| 6 | `Unit/Core/UnitWorld.cs` | +35 | `SpawnUnit()`: initialize ExperienceTracker. `GrantExperience(UnitUid uid, int amount)`: delegate to tracker. On level-up: apply stat growth to StatHandler, grant skill points to AbilityHandler. |
| 7 | `Unit/Combat/DeathEffectDispatcher.cs` | +40 | `OnUnitKill(Unit killer, Unit victim)`: compute XP reward via XpRewardTable, call `UnitWorld.GrantExperience(killer.Uid, amount)`. Also compute gold bounty (GoldIncomeRuntime integration). |
| 8 | `Unit/Combat/CombatSystem.cs` | +30 | After formal death confirmation: call `DeathEffectDispatcher.OnUnitKill(killer, victim)` for each killer in DamageContributionTracker. |
| 9 | `Unit/Stats/StatHandler.cs` | +30 | `ApplyLevelUpStatGrowth(StatPreset growth, int newLevel)`: add permanent base stat modifiers for the new level. `RecalculateStats()`: force recompute all derived stats. |
| 10 | `Unit/Ability/AbilityHandler.cs` | +20 | `GrantSkillPoint()`: already exists. Wire to UnitWorld level-up path. |
| 11 | `Unit/Core/RespawnTimer.cs` | +20 | `GetRespawnTicks(int level) → int`: respawn time scales with level. Configurable base + per-level increment. |
| 12 | `Unit/XP/ExperienceTrackerSnapshot.cs` | ~30 | Snapshot struct: `CurrentLevel`, `TotalExperience`, `ExperienceToNextLevel`. |
| 13 | `FrameSync/SimulationTickPipeline.cs` | +15 | Capture ExperienceTracker in aggregate snapshot. Restore on rollback. |

### Design conformance
- Unit v27.3 §LevelUp: level progression, skill point granting
- Combat v13.2 §KillRewards: XP distribution on kill
- Ability v15.2 §SkillPoints: ability level up via skill points
- Stats: StatHandler level-up growth via permanent modifiers

### Tests needed (~100 lines)
- `ExperienceTrackerTests.cs`: XP grant, level-up, multi-level jump, max level cap
- `ExperienceTableTests.cs`: threshold lookup, level calculation
- `DeathEffectDispatcherXpTests.cs`: kill reward computation, level differential

---

## Candidate C: MatchRuleRuntime Foundation (~400 lines)

**Gap**: No match timer, no win/loss conditions, no game phase transitions. The match runs indefinitely with no structure. Gold income, respawn timers, and minion wave power should all scale with match phase, but there's no phase system to drive them.

**Design authority**: `Docs/Design/FrameSync_Flow_Integrated_System_Design_v10_2.md` (match flow), `Docs/Design/moba_equipment_shop_gold_system_design_v12.md` (gold income scaling), `Docs/Design/unit_behavior_framework_design_v27_3.md` (respawn scaling)

### What this unlocks
- Match timer (visible countdown)
- Game phases: Early (0–10min) / Mid (10–25min) / Late (25min+)
- Win/loss condition checking (nexus destruction, surrender)
- Gold income passive scaling by phase
- Respawn timer scaling by phase + level
- Minion wave power scaling by phase

### New files — FrameSync/ or Unit/ (~200 lines)

| # | File | Lines | Description |
|---|---|---|---|
| 1 | `FrameSync/MatchRuleRuntime.cs` | ~100 | Match lifecycle: `MatchPhase Phase` enum (Early/Mid/Late/Finished), `int MatchElapsedTicks`, `int MatchDurationTicks` (configurable), `TeamId? WinningTeam`. `TickUpdate()`: increments timer, checks phase transitions, checks win conditions. `CheckWinCondition()`: nexus destroyed → winner; surrender vote → winner. `GetPhaseMultiplier(PhaseScalingTarget target) → fp`: returns 1.0-based scaling for gold income, respawn time, minion power by phase. |
| 2 | `FrameSync/MatchPhase.cs` | ~10 | Enum: `Early`, `Mid`, `Late`, `Finished`. |
| 3 | `FrameSync/PhaseScalingTarget.cs` | ~15 | Enum: `GoldIncome`, `RespawnTime`, `MinionPower`, `TowerDefense`. Configurable per-phase multipliers. |
| 4 | `FrameSync/PhaseScalingTable.cs` | ~40 | Static config: `fp GetMultiplier(MatchPhase phase, PhaseScalingTarget target)`. Early: all 1.0. Mid: gold 1.3, respawn 1.5, minion 1.5. Late: gold 1.0, respawn 2.0, minion 2.5. |
| 5 | `FrameSync/MatchRuleSnapshot.cs` | ~35 | Snapshot: `MatchPhase`, `int ElapsedTicks`, `TeamId? WinningTeam`. |

### Modified files (~200 lines)

| # | File | Lines | Change |
|---|---|---|---|
| 6 | `FrameSync/SimulationTickPipeline.cs` | +30 | `ExecuteTick()`: call `MatchRuleRuntime.TickUpdate()` first. After phase transition: broadcast MatchPhaseChanged event. On Finished: pause simulation, trigger match-end flow. |
| 7 | `FrameSync/FrameSyncGameRuntime.cs` | +40 | Add `MatchRuleRuntime MatchRule` property. Initialize with configurable match duration. `IsMatchActive → bool`: Phase != Finished. |
| 8 | `Unit/Core/RespawnTimer.cs` | +20 | `GetRespawnTicks(int level, MatchPhase phase)`: apply phase multiplier from PhaseScalingTable. |
| 9 | `FrameSync/GoldIncomeRuntime.cs` | +25 | `GetPassiveGoldPerTick(MatchPhase phase)`: apply phase multiplier. |
| 10 | `Unit/NonHero/MinionSystem.cs` | +25 | `GetMinionStatMultiplier(MatchPhase phase)`: scale minion damage/health by phase. |
| 11 | `Unit/Core/UnitWorld.cs` | +15 | Pass MatchRuleRuntime reference for phase queries. |
| 12 | `FrameSync/GameplaySnapshot.cs` | +20 | Add MatchRuleSnapshot to aggregate snapshot. |
| 13 | `FrameSync/PredictionRollbackCoordinator.cs` | +15 | Restore MatchRule state on rollback. |

### Design conformance
- FrameSync v10.2: match lifecycle, phase transitions, win conditions
- Equipment/Gold v12: gold income scaling by phase
- Unit v27.3: respawn scaling by phase

### Deferred
- Surrender vote system (requires networking)
- Detailed end-of-match statistics screen
- Match replay save/load

### Tests needed (~80 lines)
- `MatchRuleRuntimeTests.cs`: phase transitions, timer accuracy, win condition detection
- `PhaseScalingTableTests.cs`: multiplier lookup per phase
- `RespawnTimerPhaseTests.cs`: respawn time with phase multiplier

---

## Comparison Matrix

| Dimension | A: A* Pathfinding | B: XP & Level-Up | C: MatchRuleRuntime |
|---|---|---|---|
| **Lines (new + modified)** | ~650 | ~450 | ~400 |
| **New files** | 5 | 4 | 5 |
| **Modified files** | 7 | 9 | 8 |
| **Assemblies touched** | Unit, FrameSync | Unit, FrameSync | Unit, FrameSync |
| **Design doc** | Pathfinding v13.1 | Unit v27.3 + Combat v13.2 | FrameSync v10.2 |
| **Risk** | Medium (algorithm) | Low (data plumbing) | Low (state machine) |
| **Unlocks** | Real MOBA movement | Kill-reward loop | Match structure |
| **Blocked by** | 0041 Pathfinding grid (✅) | DeathEffectDispatcher (✅) | FrameSync pipeline (✅) |
| **Vertical slice** | Click-to-move end-to-end | Kill → XP → level → stronger | Match start → phase → end |

## Recommendation

**A → B → C**

1. **A* Pathfinding first** — it's the largest remaining framework gap and directly validates all the locomotion infrastructure from 0041. A working click-to-move loop is the single most impactful gameplay milestone. At ~650 lines it's substantial but cleanly scoped by the design doc's §7 + §9.

2. **XP & Level-Up second** — builds on Combat and DeathEffectDispatcher to create the core progression loop. Makes kills meaningful. At ~450 lines it's well-contained and the design doc has clear level-up lifecycle specifications.

3. **MatchRuleRuntime third** — provides match structure (timer, phases, win conditions). Without it, the match is an infinite sandbox. At ~400 lines it's the lightest of the three but completes the match-flow skeleton that Phase 14 (Dedicated Server) will later build on.

All three plans are in the 400–650 line range, meeting the 300–1000 line target, and each produces a visible gameplay outcome.
